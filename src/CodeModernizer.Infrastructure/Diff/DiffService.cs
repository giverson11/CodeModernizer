using System.Text;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;
using DiffPlex;

namespace CodeModernizer.Infrastructure.Diff;

public sealed class DiffService : IDiffService
{
    private readonly IDiffer _differ = new Differ();

    public List<DiffHunk> ComputeHunks(string original, string modernized)
    {
        var result = _differ.CreateLineDiffs(original, modernized, ignoreWhitespace: false);
        var hunks = new List<DiffHunk>();
        var id = 0;

        foreach (var block in result.DiffBlocks)
        {
            hunks.Add(new DiffHunk
            {
                Id = id++,
                OriginalStart = block.DeleteStartA,
                OriginalLines = result.PiecesOld
                    .Skip(block.DeleteStartA).Take(block.DeleteCountA).ToList(),
                ModernizedLines = result.PiecesNew
                    .Skip(block.InsertStartB).Take(block.InsertCountB).ToList(),
            });
        }

        return hunks;
    }

    public string BuildContent(string original, IReadOnlyList<DiffHunk> hunks)
    {
        var eol = original.Contains("\r\n") ? "\r\n" : "\n";
        var originalLines = SplitLines(original);
        var sb = new StringBuilder();
        var cursor = 0;

        foreach (var hunk in hunks.OrderBy(h => h.OriginalStart))
        {
            // Unchanged lines before this hunk.
            for (; cursor < hunk.OriginalStart && cursor < originalLines.Count; cursor++)
                sb.Append(originalLines[cursor]).Append(eol);

            var lines = hunk.Decision == HunkDecision.Accepted ? hunk.ModernizedLines : hunk.OriginalLines;
            foreach (var line in lines)
                sb.Append(line).Append(eol);

            cursor += hunk.OriginalLines.Count;
        }

        for (; cursor < originalLines.Count; cursor++)
            sb.Append(originalLines[cursor]).Append(eol);

        // Preserve the original file's trailing-newline convention.
        var text = sb.ToString();
        if (!original.EndsWith('\n') && text.EndsWith(eol))
            text = text[..^eol.Length];
        return text;
    }

    private static List<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
}
