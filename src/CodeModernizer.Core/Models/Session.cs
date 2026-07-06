namespace CodeModernizer.Core.Models;

public enum SessionStatus { Scanning, Running, Reviewing, Completed, Failed }

public enum FileChangeStatus { Pending, Modernizing, Ready, Unchanged, Failed }

public enum HunkDecision { Pending, Accepted, Rejected }

/// <summary>One contiguous change between the original and modernized file.</summary>
public sealed class DiffHunk
{
    public required int Id { get; init; }
    /// <summary>0-based line index into the original file where the hunk starts.</summary>
    public required int OriginalStart { get; init; }
    public required IReadOnlyList<string> OriginalLines { get; init; }
    public required IReadOnlyList<string> ModernizedLines { get; init; }
    public HunkDecision Decision { get; set; } = HunkDecision.Pending;
}

public sealed class FileChange
{
    public required string Id { get; init; }
    public required string RelativePath { get; init; }
    public required string OriginalContent { get; init; }
    public string? ModernizedContent { get; set; }
    public FileChangeStatus Status { get; set; } = FileChangeStatus.Pending;
    public string? Error { get; set; }
    public List<DiffHunk> Hunks { get; set; } = [];
    public bool Applied { get; set; }
    /// <summary>Live output of the agent model while (re)modernizing this file.</summary>
    public StreamLog AgentLog { get; } = new();
    /// <summary>Guards Hunks/ModernizedContent mutation across concurrent requests.</summary>
    public object SyncRoot { get; } = new();
}

public sealed record ReviewResult(string Verdict, string Summary);

public sealed class ModernizationSession
{
    public required string Id { get; init; }
    public required string ProjectPath { get; init; }
    public required string SkillId { get; init; }
    public required string ProviderId { get; init; }
    public required string AgentModelId { get; init; }
    public required string ReviewModelId { get; init; }
    public SessionStatus Status { get; set; } = SessionStatus.Scanning;
    public List<FileChange> Files { get; set; } = [];
    public ReviewResult? Review { get; set; }
    /// <summary>Live output of the overview model during the equivalence review.</summary>
    public StreamLog ReviewLog { get; } = new();
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}
