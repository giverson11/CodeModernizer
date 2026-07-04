using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;

namespace CodeModernizer.Infrastructure.Providers;

/// <summary>
/// Claude provider backed by the official Anthropic SDK. Reads the API key from
/// the ANTHROPIC_API_KEY environment variable (or an `ant auth login` profile).
/// </summary>
public sealed class ClaudeProvider : IAiProvider
{
    // Models that use adaptive thinking. Fable 5 must omit the thinking parameter
    // entirely (always on) and Haiku 4.5 runs without adaptive thinking support.
    private static readonly HashSet<string> AdaptiveThinkingModels =
    [
        "claude-opus-4-8", "claude-opus-4-7", "claude-opus-4-6",
        "claude-sonnet-5", "claude-sonnet-4-6",
    ];

    private readonly AnthropicClient _client = new();

    public string Id => "claude";
    public string DisplayName => "Anthropic Claude";

    public IReadOnlyList<AiModelInfo> Models { get; } =
    [
        new("claude-opus-4-8", "Claude Opus 4.8"),
        new("claude-fable-5", "Claude Fable 5"),
        new("claude-sonnet-5", "Claude Sonnet 5"),
        new("claude-sonnet-4-6", "Claude Sonnet 4.6"),
        new("claude-opus-4-7", "Claude Opus 4.7"),
        new("claude-opus-4-6", "Claude Opus 4.6"),
        new("claude-haiku-4-5", "Claude Haiku 4.5"),
    ];

    public async Task<string> CompleteAsync(string modelId, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        // Fable 5 (thinking always on) and Haiku must omit the thinking parameter.
        ThinkingConfigParam? thinking = AdaptiveThinkingModels.Contains(modelId)
            ? new ThinkingConfigAdaptive()
            : null;

        var parameters = new MessageCreateParams
        {
            Model = modelId,
            MaxTokens = 64000,
            System = new List<TextBlockParam> { new() { Text = systemPrompt } },
            Messages = [new() { Role = Role.User, Content = userPrompt }],
            Thinking = thinking,
        };

        // Stream to avoid HTTP timeouts on long generations, accumulate text blocks.
        var sb = new StringBuilder();
        await foreach (var streamEvent in _client.Messages.CreateStreaming(parameters).WithCancellation(ct))
        {
            if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                delta.Delta.TryPickText(out var text))
            {
                sb.Append(text.Text);
            }
        }

        return sb.ToString();
    }
}
