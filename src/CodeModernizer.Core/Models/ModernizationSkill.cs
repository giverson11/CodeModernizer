namespace CodeModernizer.Core.Models;

/// <summary>
/// A modernization skill targets one language + version pair. Skills are loaded
/// from the skills directory, so adding support for a new language/version is a
/// matter of dropping in a new folder with a manifest and prompt files.
/// </summary>
public sealed record ModernizationSkill(
    string Id,
    string DisplayName,
    string Language,
    string TargetVersion,
    IReadOnlyList<string> FileExtensions,
    string ModernizePrompt,
    string ReviewPrompt);
