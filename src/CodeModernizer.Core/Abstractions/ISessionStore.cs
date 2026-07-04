using CodeModernizer.Core.Models;

namespace CodeModernizer.Core.Abstractions;

public interface ISessionStore
{
    void Add(ModernizationSession session);
    ModernizationSession? Get(string sessionId);
    IReadOnlyList<ModernizationSession> All();
}
