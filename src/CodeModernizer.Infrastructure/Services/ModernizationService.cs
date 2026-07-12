using System.Text;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeModernizer.Infrastructure.Services;

public sealed class ModernizationService(
    ISkillRegistry skills,
    IAiProviderRegistry providers,
    IDiffService diff,
    ISessionStore sessions,
    ILogger<ModernizationService> logger)
{
    private const int MaxParallelFiles = 3;
    private const long MaxFileSizeBytes = 256 * 1024;
    private const int MaxFilesPerSession = 500;
    private const int ReviewDiffCharBudget = 300_000;

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", ".idea", ".vscode", "node_modules",
        "bin", "obj", "build", "target", "dist", "out", ".gradle",
    };

    public ModernizationSession Start(string projectPath, string skillId, string providerId, string agentModelId, string reviewModelId)
    {
        var fullPath = Path.GetFullPath(projectPath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Project folder not found: {fullPath}");

        var skill = skills.Get(skillId);
        providers.Get(providerId); // validate early

        var session = new ModernizationSession
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            ProjectPath = fullPath,
            SkillId = skillId,
            ProviderId = providerId,
            AgentModelId = agentModelId,
            ReviewModelId = reviewModelId,
        };

        session.Files = ScanFiles(fullPath, skill);
        session.Status = SessionStatus.Running;
        sessions.Add(session);

        _ = Task.Run(() => RunAsync(session, skill));
        return session;
    }

    private static List<FileChange> ScanFiles(string root, ModernizationSkill skill)
    {
        var files = new List<FileChange>();
        var extensions = new HashSet<string>(skill.FileExtensions, StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0 && files.Count < MaxFilesPerSession)
        {
            var dir = pending.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (!ExcludedDirectories.Contains(Path.GetFileName(sub)))
                    pending.Push(sub);
            }

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (!extensions.Contains(Path.GetExtension(file))) continue;
                if (new FileInfo(file).Length > MaxFileSizeBytes) continue;
                if (files.Count >= MaxFilesPerSession) break;

                files.Add(new FileChange
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    RelativePath = Path.GetRelativePath(root, file),
                    OriginalContent = File.ReadAllText(file),
                });
            }
        }

        return files.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task RunAsync(ModernizationSession session, ModernizationSkill skill)
    {
        try
        {
            var provider = providers.Get(session.ProviderId);

            await Parallel.ForEachAsync(
                session.Files,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelFiles },
                async (file, _) => await ModernizeFileAsync(provider, session.AgentModelId, skill, file));

            session.Status = SessionStatus.Completed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Modernization session {SessionId} failed", session.Id);
            session.Status = SessionStatus.Failed;
            session.Error = ex.Message;
        }
    }

    private async Task ModernizeFileAsync(IAiProvider provider, string modelId, ModernizationSkill skill, FileChange file)
    {
        file.AgentLog.Reset($"── modernizing {file.RelativePath} with {modelId} ──\n");
        file.Status = FileChangeStatus.Modernizing;
        try
        {
            var userPrompt =
                $"""
                File: {file.RelativePath}

                ```
                {file.OriginalContent}
                ```
                """;

            var response = await provider.CompleteAsync(modelId, skill.ModernizePrompt, userPrompt, file.AgentLog.Append);
            var code = CodeResponseParser.ExtractCode(response);

            if (NormalizeForComparison(code) == NormalizeForComparison(file.OriginalContent))
            {
                file.Status = FileChangeStatus.Unchanged;
                return;
            }

            lock (file.SyncRoot)
            {
                file.ModernizedContent = code;
                file.Hunks = diff.ComputeHunks(file.OriginalContent, code);
                file.Status = file.Hunks.Count == 0 ? FileChangeStatus.Unchanged : FileChangeStatus.Ready;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to modernize {Path}", file.RelativePath);
            file.Status = FileChangeStatus.Failed;
            file.Error = ex.Message;
        }
    }

    /// <summary>Re-runs the modernizer for one file with extra user instructions.</summary>
    public async Task AdjustFileAsync(ModernizationSession session, FileChange file, string instructions)
    {
        var skill = skills.Get(session.SkillId);
        var provider = providers.Get(session.ProviderId);

        string original, current;
        lock (file.SyncRoot)
        {
            original = file.OriginalContent;
            current = file.ModernizedContent ?? file.OriginalContent;
        }

        file.AgentLog.Reset($"── adjusting {file.RelativePath} with {session.AgentModelId} ──\n");
        file.Status = FileChangeStatus.Modernizing;
        try
        {
            var userPrompt =
                $"""
                File: {file.RelativePath}

                Original code:
                ```
                {original}
                ```

                Current modernized version:
                ```
                {current}
                ```

                The reviewer requests the following adjustments to the modernized version:
                {instructions}

                Return the full adjusted file.
                """;

            var response = await provider.CompleteAsync(
                session.AgentModelId, skill.ModernizePrompt, userPrompt, file.AgentLog.Append);
            var code = CodeResponseParser.ExtractCode(response);

            lock (file.SyncRoot)
            {
                file.ModernizedContent = code;
                file.Hunks = diff.ComputeHunks(file.OriginalContent, code);
                file.Status = file.Hunks.Count == 0 ? FileChangeStatus.Unchanged : FileChangeStatus.Ready;
                file.Error = null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to adjust {Path}", file.RelativePath);
            file.Status = FileChangeStatus.Failed;
            file.Error = ex.Message;
        }
    }

    /// <summary>
    /// Asks the overview model whether the modernized program should still behave
    /// the same as the original, based on the accepted/pending changes.
    /// </summary>
    public async Task<ReviewResult> ReviewAsync(ModernizationSession session)
    {
        var skill = skills.Get(session.SkillId);
        var provider = providers.Get(session.ProviderId);

        session.ReviewLog.Reset($"── overview review with {session.ReviewModelId} ──\n");
        session.Review = null; // the previous verdict is stale once a new review starts
        session.Status = SessionStatus.Reviewing;
        try
        {
            var digest = BuildReviewDigest(session);
            var response = await provider.CompleteAsync(
                session.ReviewModelId, skill.ReviewPrompt, digest, session.ReviewLog.Append);

            var lines = response.Trim().Split('\n', 2);
            var verdict = lines[0].Trim().ToUpperInvariant() switch
            {
                "EQUIVALENT" => "EQUIVALENT",
                "POTENTIALLY_DIFFERENT" => "POTENTIALLY_DIFFERENT",
                _ => "INSUFFICIENT_INFO",
            };
            var summary = lines.Length > 1 ? lines[1].Trim() : response.Trim();

            var result = new ReviewResult(verdict, summary);
            session.Review = result;
            return result;
        }
        finally
        {
            session.Status = SessionStatus.Completed;
        }
    }

    /// <summary>
    /// Feeds the review's concerns back to the agent model: re-adjusts the files
    /// the review mentions (or every ready file if none are named). Runs in the
    /// background; the client polls the session until it leaves Running.
    /// </summary>
    public int ImplementReview(ModernizationSession session)
    {
        var review = session.Review
            ?? throw new InvalidOperationException("No review to implement.");

        var ready = session.Files.Where(f => f.Status == FileChangeStatus.Ready).ToList();
        var targets = ready
            .Where(f => review.Summary.Contains(Path.GetFileName(f.RelativePath), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (targets.Count == 0) targets = ready;
        if (targets.Count == 0) throw new InvalidOperationException("No modernized files to adjust.");

        var instructions =
            $"""
            A behavioral-equivalence review of the whole modernized project raised these concerns:

            {review.Summary}

            Apply the fixes the reviewer requests where they involve this file. If none of the
            concerns involve this file, return the current modernized version unchanged.
            """;

        session.Review = null; // it describes the pre-fix state
        session.Status = SessionStatus.Running;
        foreach (var file in targets) file.Status = FileChangeStatus.Modernizing;

        _ = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(
                    targets,
                    new ParallelOptions { MaxDegreeOfParallelism = MaxParallelFiles },
                    async (file, _) => await AdjustFileAsync(session, file, instructions));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Implementing review feedback failed for session {SessionId}", session.Id);
                session.Error = ex.Message;
            }
            finally
            {
                session.Status = SessionStatus.Completed;
            }
        });

        return targets.Count;
    }

    private string BuildReviewDigest(ModernizationSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project: {Path.GetFileName(session.ProjectPath)}");
        sb.AppendLine($"Files scanned: {session.Files.Count}");
        sb.AppendLine();

        foreach (var file in session.Files.Where(f => f.Status == FileChangeStatus.Ready))
        {
            if (sb.Length > ReviewDiffCharBudget)
            {
                sb.AppendLine("[... remaining diffs truncated due to size ...]");
                break;
            }

            List<DiffHunk> hunks;
            lock (file.SyncRoot) hunks = file.Hunks.ToList();

            sb.AppendLine($"=== {file.RelativePath} ===");
            foreach (var hunk in hunks)
            {
                var state = hunk.Decision switch
                {
                    HunkDecision.Accepted => "accepted",
                    HunkDecision.Rejected => "rejected (original kept)",
                    _ => "pending",
                };
                sb.AppendLine($"--- change @ line {hunk.OriginalStart + 1} [{state}] ---");
                foreach (var line in hunk.OriginalLines) sb.AppendLine($"- {line}");
                foreach (var line in hunk.ModernizedLines) sb.AppendLine($"+ {line}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes each file's effective content (original + accepted hunks) back to disk.
    /// Files with no accepted hunks are left untouched.
    /// </summary>
    public List<string> Apply(ModernizationSession session)
    {
        var written = new List<string>();
        foreach (var file in session.Files.Where(f => f.Status == FileChangeStatus.Ready))
        {
            string content;
            lock (file.SyncRoot)
            {
                if (!file.Hunks.Any(h => h.Decision == HunkDecision.Accepted)) continue;
                content = diff.BuildContent(file.OriginalContent, file.Hunks);
            }

            var target = Path.Combine(session.ProjectPath, file.RelativePath);
            File.WriteAllText(target, content);
            file.Applied = true;
            written.Add(file.RelativePath);
        }

        return written;
    }

    private static string NormalizeForComparison(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n');
}
