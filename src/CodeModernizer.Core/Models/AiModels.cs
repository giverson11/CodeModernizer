namespace CodeModernizer.Core.Models;

public sealed record AiModelInfo(string Id, string DisplayName);

public sealed record AiProviderInfo(string Id, string DisplayName, IReadOnlyList<AiModelInfo> Models);
