using System.Collections.Concurrent;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;

namespace CodeModernizer.Infrastructure.Sessions;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, ModernizationSession> _sessions = new();

    public void Add(ModernizationSession session) => _sessions[session.Id] = session;

    public ModernizationSession? Get(string sessionId) =>
        _sessions.GetValueOrDefault(sessionId);

    public IReadOnlyList<ModernizationSession> All() =>
        _sessions.Values.OrderByDescending(s => s.CreatedAt).ToList();
}
