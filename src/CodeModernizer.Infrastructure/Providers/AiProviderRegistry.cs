using CodeModernizer.Core.Abstractions;

namespace CodeModernizer.Infrastructure.Providers;

public sealed class AiProviderRegistry(IEnumerable<IAiProvider> providers) : IAiProviderRegistry
{
    public IReadOnlyList<IAiProvider> Providers { get; } = providers.ToList();

    public IAiProvider Get(string providerId) =>
        Providers.FirstOrDefault(p => p.Id == providerId)
        ?? throw new KeyNotFoundException($"Unknown AI provider '{providerId}'.");
}
