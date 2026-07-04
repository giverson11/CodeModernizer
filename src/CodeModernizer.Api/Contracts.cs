using CodeModernizer.Core.Models;

namespace CodeModernizer.Api;

public sealed record StartSessionRequest(
    string ProjectPath,
    string SkillId,
    string ProviderId,
    string AgentModelId,
    string ReviewModelId);

public sealed record HunkDecisionRequest(HunkDecision Decision);

public sealed record AdjustRequest(string Instructions);

public sealed record SkillDto(string Id, string DisplayName, string Language, string TargetVersion, IReadOnlyList<string> FileExtensions);

public sealed record ConfigDto(IReadOnlyList<SkillDto> Skills, IReadOnlyList<AiProviderInfo> Providers);

public sealed record FileSummaryDto(
    string Id, string RelativePath, FileChangeStatus Status,
    int HunkCount, int AcceptedCount, int RejectedCount,
    bool Applied, string? Error);

public sealed record SessionDto(
    string Id, SessionStatus Status, string ProjectPath, string SkillId,
    string ProviderId, string AgentModelId, string ReviewModelId,
    ReviewResult? Review, string? Error, IReadOnlyList<FileSummaryDto> Files);

public sealed record HunkDto(
    int Id, HunkDecision Decision, int OriginalStart,
    IReadOnlyList<string> OriginalLines, IReadOnlyList<string> ModernizedLines);

public sealed record FileDetailDto(
    string Id, string RelativePath, FileChangeStatus Status, string? Error,
    string OriginalContent, IReadOnlyList<HunkDto> Hunks);

public static class Mapping
{
    public static SessionDto ToDto(this ModernizationSession s) => new(
        s.Id, s.Status, s.ProjectPath, s.SkillId, s.ProviderId,
        s.AgentModelId, s.ReviewModelId, s.Review, s.Error,
        s.Files.Select(f =>
        {
            lock (f.SyncRoot)
            {
                return new FileSummaryDto(
                    f.Id, f.RelativePath, f.Status, f.Hunks.Count,
                    f.Hunks.Count(h => h.Decision == HunkDecision.Accepted),
                    f.Hunks.Count(h => h.Decision == HunkDecision.Rejected),
                    f.Applied, f.Error);
            }
        }).ToList());

    public static FileDetailDto ToDetailDto(this FileChange f)
    {
        lock (f.SyncRoot)
        {
            return new FileDetailDto(
                f.Id, f.RelativePath, f.Status, f.Error, f.OriginalContent,
                f.Hunks.Select(h => new HunkDto(
                    h.Id, h.Decision, h.OriginalStart, h.OriginalLines, h.ModernizedLines)).ToList());
        }
    }
}
