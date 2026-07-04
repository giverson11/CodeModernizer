namespace CodeModernizer.Infrastructure.Services;

/// <summary>
/// Extracts the code body from a model response. The prompts ask for raw code
/// only, but models occasionally wrap output in a markdown fence anyway.
/// </summary>
public static class CodeResponseParser
{
    public static string ExtractCode(string response)
    {
        var text = response.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
            return text;

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0) return text;

        var closingFence = text.LastIndexOf("\n```", StringComparison.Ordinal);
        if (closingFence <= firstNewline) return text;

        return text[(firstNewline + 1)..closingFence];
    }
}
