using CodeModernizer.Core.Models;

namespace CodeModernizer.Core.Abstractions;

/// <summary>
/// A chat-completion provider (Claude today; others can be registered later).
/// </summary>
public interface IAiProvider
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyList<AiModelInfo> Models { get; }

    /// <param name="onOutput">Invoked with each streamed output fragment (text and,
    /// where supported, thinking) as it arrives; used for live progress display.</param>
    Task<string> CompleteAsync(
        string modelId, string systemPrompt, string userPrompt,
        Action<string>? onOutput = null, CancellationToken ct = default);
}

public interface IAiProviderRegistry
{
    IReadOnlyList<IAiProvider> Providers { get; }
    IAiProvider Get(string providerId);
}
