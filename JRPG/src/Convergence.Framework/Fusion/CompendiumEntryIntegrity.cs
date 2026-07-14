using Convergence.Content;
using Convergence.Catalog;

namespace Convergence.Fusion;

internal enum CompendiumEntryIntegrityCode
{
    InvalidContentId,
    DuplicateLearnedSkill,
    DuplicateEquippedSkill,
    InvalidStatValue,
    MissingStat,
    UnknownStat,
    MissingSkill,
    EquippedSkillNotLearned
}

internal enum CompendiumEntryIntegrityField
{
    Stats,
    LearnedSkills,
    EquippedSkills
}

internal sealed record CompendiumEntryIntegrityDiagnostic(
    CompendiumEntryIntegrityCode Code,
    CompendiumEntryIntegrityField Field,
    string Message,
    ContentId ContentId,
    int? Index = null);

internal static class CompendiumEntryIntegrity
{
    public static IReadOnlyList<CompendiumEntryIntegrityDiagnostic> Validate(
        CompendiumEntrySnapshot entry,
        EntityDefinition? entity,
        ISkillDefinitionRepository skills)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(skills);

        var diagnostics = new List<CompendiumEntryIntegrityDiagnostic>();
        ValidateStats(entry, entity, diagnostics);
        ValidateLearnedSkills(entry, skills, diagnostics);
        ValidateEquippedSkills(entry, skills, diagnostics);
        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private static void ValidateStats(
        CompendiumEntrySnapshot entry,
        EntityDefinition? entity,
        ICollection<CompendiumEntryIntegrityDiagnostic> diagnostics)
    {
        foreach ((ContentId statId, int value) in entry.Stats)
        {
            if (!statId.IsValid)
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.InvalidContentId,
                    CompendiumEntryIntegrityField.Stats,
                    "Compendium stat ID cannot be empty.",
                    statId));
                continue;
            }

            if (value < 0)
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.InvalidStatValue,
                    CompendiumEntryIntegrityField.Stats,
                    $"Compendium stat '{statId}' cannot be negative.",
                    statId));
            }

            if (entity is not null && !entity.Stats.ContainsKey(statId))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.UnknownStat,
                    CompendiumEntryIntegrityField.Stats,
                    $"Compendium stat '{statId}' is not authored for entity '{entry.EntityId}'.",
                    statId));
            }
        }

        // An empty block requests catalog defaults; a persisted override must be complete.
        if (entity is null || entry.Stats.Count == 0)
        {
            return;
        }

        foreach (ContentId statId in entity.Stats.Keys)
        {
            if (!entry.Stats.ContainsKey(statId))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.MissingStat,
                    CompendiumEntryIntegrityField.Stats,
                    $"Compendium stat override is missing authored stat '{statId}'.",
                    statId));
            }
        }
    }

    private static void ValidateLearnedSkills(
        CompendiumEntrySnapshot entry,
        ISkillDefinitionRepository skills,
        ICollection<CompendiumEntryIntegrityDiagnostic> diagnostics)
    {
        var seen = new HashSet<ContentId>();
        for (int index = 0; index < entry.SkillIds.Count; index++)
        {
            ContentId skillId = entry.SkillIds[index];
            if (!skillId.IsValid)
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.InvalidContentId,
                    CompendiumEntryIntegrityField.LearnedSkills,
                    "Compendium learned skill ID cannot be empty.",
                    skillId,
                    index));
                continue;
            }

            if (!seen.Add(skillId))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.DuplicateLearnedSkill,
                    CompendiumEntryIntegrityField.LearnedSkills,
                    $"Compendium learned skill '{skillId}' appears more than once.",
                    skillId,
                    index));
            }

            if (!skills.TryGetSkill(skillId, out _))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.MissingSkill,
                    CompendiumEntryIntegrityField.LearnedSkills,
                    $"Compendium learned skill '{skillId}' is not present in the catalog.",
                    skillId,
                    index));
            }
        }
    }

    private static void ValidateEquippedSkills(
        CompendiumEntrySnapshot entry,
        ISkillDefinitionRepository skills,
        ICollection<CompendiumEntryIntegrityDiagnostic> diagnostics)
    {
        var seen = new HashSet<ContentId>();
        for (int index = 0; index < entry.EquippedSkillIds.Count; index++)
        {
            ContentId skillId = entry.EquippedSkillIds[index];
            if (!skillId.IsValid)
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.InvalidContentId,
                    CompendiumEntryIntegrityField.EquippedSkills,
                    "Compendium equipped skill ID cannot be empty.",
                    skillId,
                    index));
                continue;
            }

            if (!seen.Add(skillId))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.DuplicateEquippedSkill,
                    CompendiumEntryIntegrityField.EquippedSkills,
                    $"Compendium equipped skill '{skillId}' appears more than once.",
                    skillId,
                    index));
            }

            if (!entry.SkillIds.Contains(skillId))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.EquippedSkillNotLearned,
                    CompendiumEntryIntegrityField.EquippedSkills,
                    $"Compendium equipped skill '{skillId}' is not present in learned skills.",
                    skillId,
                    index));
            }

            if (!skills.TryGetSkill(skillId, out _))
            {
                diagnostics.Add(new CompendiumEntryIntegrityDiagnostic(
                    CompendiumEntryIntegrityCode.MissingSkill,
                    CompendiumEntryIntegrityField.EquippedSkills,
                    $"Compendium equipped skill '{skillId}' is not present in the catalog.",
                    skillId,
                    index));
            }
        }
    }
}
