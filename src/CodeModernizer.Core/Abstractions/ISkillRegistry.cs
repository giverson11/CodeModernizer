using CodeModernizer.Core.Models;

namespace CodeModernizer.Core.Abstractions;

public interface ISkillRegistry
{
    IReadOnlyList<ModernizationSkill> Skills { get; }
    ModernizationSkill Get(string skillId);
}
