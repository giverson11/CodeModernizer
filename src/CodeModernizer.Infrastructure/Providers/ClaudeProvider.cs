using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;

namespace CodeModernizer.Infrastructure.Providers;

/// <summary>
/// Claude provider backed by the official Anthropic SDK. Uses the API key passed
/// in (from configuration) or falls back to the ANTHROPIC_API_KEY environment
/// variable via the SDK's default resolution.
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

    private readonly AnthropicClient _client;

    public ClaudeProvider(string? apiKey = null)
    {
        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
    }

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

    public async Task<string> CompleteAsync(
        string modelId, string systemPrompt, string userPrompt,
        Action<string>? onOutput = null, CancellationToken ct = default)
    {
        var system = new List<TextBlockParam> { new() { Text = systemPrompt } };
        List<MessageParam> messages = [new() { Role = Role.User, Content = userPrompt }];

        // Fable 5 (thinking always on) and Haiku must omit the thinking parameter
        // entirely: assigning null serializes as `"thinking": null`, which the
        // API rejects with "thinking: Input should be an object".
        var parameters = AdaptiveThinkingModels.Contains(modelId)
            ? new MessageCreateParams
            {
                Model = modelId,
                MaxTokens = 64000,
                System = system,
                Messages = messages,
                Thinking = new ThinkingConfigAdaptive(),
            }
            : new MessageCreateParams
            {
                Model = modelId,
                MaxTokens = 64000,
                System = system,
                Messages = messages,
            };

        // Stream to avoid HTTP timeouts on long generations, accumulate text blocks.
        // Thinking deltas are surfaced to onOutput only — they are not part of the
        // result — and the log marks where thinking ends and the file output begins
        // (models without thinking jump straight to "writing output").
        var sb = new StringBuilder();
        string? phase = null;
        void Emit(string kind, string content)
        {
            if (onOutput is null) return;
            if (phase != kind)
            {
                onOutput($"{(phase is null ? "" : "\n\n")}── {kind} ──\n");
                phase = kind;
            }
            onOutput(content);
        }

        await foreach (var streamEvent in _client.Messages.CreateStreaming(parameters).WithCancellation(ct))
        {
            if (!streamEvent.TryPickContentBlockDelta(out var delta)) continue;

            // Text deltas build the result but are not echoed to the console —
            // the log shows the model's thinking only.
            if (delta.Delta.TryPickText(out var text))
            {
                sb.Append(text.Text);
            }
            if (delta.Delta.TryPickThinking(out var thinkingDelta))
            {
                Emit("thinking", thinkingDelta.Thinking);
            }
        }

        return sb.ToString();
    }
}
