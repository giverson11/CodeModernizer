using System.Text.Json;
using CodeModernizer.Core.Abstractions;
using CodeModernizer.Core.Models;

namespace CodeModernizer.Infrastructure.Skills;

/// <summary>
/// Loads skills from a directory tree. Each skill is a folder containing:
///   skill.json      - manifest (id, language, targetVersion, fileExtensions, prompt file names)
///   prompt.md       - system prompt for the per-file modernizer agent
///   review-prompt.md - system prompt for the whole-program overview/review model
/// </summary>
public sealed class FileSkillRegistry : ISkillRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<ModernizationSkill> _skills = [];

    public FileSkillRegistry(string skillsDirectory)
    {
        if (!Directory.Exists(skillsDirectory))
            throw new DirectoryNotFoundException($"Skills directory not found: {skillsDirectory}");

        foreach (var dir in Directory.EnumerateDirectories(skillsDirectory))
        {
            var manifestPath = Path.Combine(dir, "skill.json");
            if (!File.Exists(manifestPath)) continue;

            var manifest = JsonSerializer.Deserialize<SkillManifest>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidDataException($"Invalid skill manifest: {manifestPath}");

            _skills.Add(new ModernizationSkill(
                manifest.Id,
                manifest.DisplayName,
                manifest.Language,
                manifest.TargetVersion,
                manifest.FileExtensions,
                File.ReadAllText(Path.Combine(dir, manifest.PromptFile)),
                File.ReadAllText(Path.Combine(dir, manifest.ReviewPromptFile))));
        }
    }

    public IReadOnlyList<ModernizationSkill> Skills => _skills;

    public ModernizationSkill Get(string skillId) =>
        _skills.FirstOrDefault(s => s.Id == skillId)
        ?? throw new KeyNotFoundException($"Unknown skill '{skillId}'.");

    private sealed record SkillManifest(
        string Id,
        string DisplayName,
        string Language,
        string TargetVersion,
        List<string> FileExtensions,
        string PromptFile,
        string ReviewPromptFile);
}
