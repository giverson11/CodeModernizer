using CodeModernizer.Core.Models;

namespace CodeModernizer.Core.Abstractions;

public interface IDiffService
{
    /// <summary>Computes change hunks between the original and modernized text.</summary>
    List<DiffHunk> ComputeHunks(string original, string modernized);

    /// <summary>
    /// Rebuilds file content from the original, applying only hunks marked Accepted.
    /// Pending and Rejected hunks keep the original lines.
    /// </summary>
    string BuildContent(string original, IReadOnlyList<DiffHunk> hunks);
}
