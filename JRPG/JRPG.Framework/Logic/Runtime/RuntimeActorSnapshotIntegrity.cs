using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Execution;

namespace JRPGPrototype.Logic.Runtime;

internal enum RuntimeActorSnapshotIntegrityCode
{
    DuplicateResource,
    DuplicateLearnedSkill,
    DuplicateEquippedSkill,
    EquippedSkillNotLearned,
    DuplicateCapability,
    DuplicateAilment,
    MissingAilmentDefinition,
    DuplicateStatus,
    DuplicateStatStage,
    DuplicateCharge,
    DuplicateShield,
    DuplicateAffinityBreak,
    InvalidAffinityBreakElement,
    DuplicateAffinityOverride,
    DuplicateAnalysisTarget,
    DuplicateAnalysisLayer,
    DuplicatePassiveSkillState,
    PassiveSkillStateNotLoaded,
    DuplicatePassiveActivation,
    PassiveActivationSkillNotLoaded,
    StatStageOutOfRange
}

internal sealed record RuntimeActorSnapshotIntegrityDiagnostic(
    RuntimeActorSnapshotIntegrityCode Code,
    string Message,
    string Path,
    ContentId? ContentId = null);

internal static class RuntimeActorSnapshotIntegrity
{
    public static IReadOnlyList<RuntimeActorSnapshotIntegrityDiagnostic> ValidateForRestore(
        RuntimeActorSnapshot snapshot,
        IEnumerable<ContentId>? loadedPassiveSkillIds,
        IEnumerable<ContentId>? availableAilmentIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var diagnostics = new List<RuntimeActorSnapshotIntegrityDiagnostic>();
        ValidateUnique(
            snapshot.Resources,
            resource => resource.ResourceId,
            RuntimeActorSnapshotIntegrityCode.DuplicateResource,
            "$.resources",
            "resource",
            key => key,
            diagnostics);

        ValidateUnique(
            snapshot.Skills.LearnedSkillIds,
            skillId => skillId,
            RuntimeActorSnapshotIntegrityCode.DuplicateLearnedSkill,
            "$.skills.learnedSkillIds",
            "learned skill",
            key => key,
            diagnostics);
        ValidateUnique(
            snapshot.Skills.EquippedSkillIds,
            skillId => skillId,
            RuntimeActorSnapshotIntegrityCode.DuplicateEquippedSkill,
            "$.skills.equippedSkillIds",
            "equipped skill",
            key => key,
            diagnostics);

        HashSet<ContentId> learnedSkillIds = snapshot.Skills.LearnedSkillIds.ToHashSet();
        for (int index = 0; index < snapshot.Skills.EquippedSkillIds.Count; index++)
        {
            ContentId skillId = snapshot.Skills.EquippedSkillIds[index];
            if (learnedSkillIds.Contains(skillId))
            {
                continue;
            }

            diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                RuntimeActorSnapshotIntegrityCode.EquippedSkillNotLearned,
                $"Equipped skill '{skillId}' is not present in the actor's learned skills.",
                $"$.skills.equippedSkillIds[{index}]",
                skillId));
        }

        ValidateUnique(
            snapshot.CapabilityIds,
            capabilityId => capabilityId,
            RuntimeActorSnapshotIntegrityCode.DuplicateCapability,
            "$.capabilityIds",
            "capability",
            key => key,
            diagnostics);

        HashSet<ContentId> availableAilments = (availableAilmentIds ?? []).ToHashSet();
        var seenAilments = new HashSet<ContentId>();
        for (int index = 0; index < snapshot.BattleStatus.Ailments.Count; index++)
        {
            RuntimeTimedStateSnapshot ailment = snapshot.BattleStatus.Ailments[index];
            if (!seenAilments.Add(ailment.Id))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.DuplicateAilment,
                    $"Ailment '{ailment.Id}' appears more than once.",
                    $"$.battleStatus.ailments[{index}]",
                    ailment.Id));
            }

            if (!availableAilments.Contains(ailment.Id))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.MissingAilmentDefinition,
                    $"Ailment '{ailment.Id}' has no definition available during actor restoration.",
                    $"$.battleStatus.ailments[{index}]",
                    ailment.Id));
            }
        }

        ValidateUnique(
            snapshot.BattleStatus.Statuses,
            status => status.Id,
            RuntimeActorSnapshotIntegrityCode.DuplicateStatus,
            "$.battleStatus.statuses",
            "status",
            key => key,
            diagnostics);
        ValidateUnique(
            snapshot.BattleStatus.StatStages,
            stage => stage.ModifierTrackId,
            RuntimeActorSnapshotIntegrityCode.DuplicateStatStage,
            "$.battleStatus.statStages",
            "stat-stage track",
            key => key,
            diagnostics);
        for (int index = 0; index < snapshot.BattleStatus.StatStages.Count; index++)
        {
            RuntimeStatStageSnapshot stage = snapshot.BattleStatus.StatStages[index];
            if (!BattleStatStageRange.Contains(stage.Stage))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.StatStageOutOfRange,
                    $"Stat stage '{stage.Stage}' for track '{stage.ModifierTrackId}' must be between " +
                    $"{BattleStatStageRange.Minimum} and {BattleStatStageRange.Maximum}.",
                    $"$.battleStatus.statStages[{index}].stage",
                    stage.ModifierTrackId));
            }
        }
        ValidateUnique(
            snapshot.BattleStatus.Charges,
            charge => charge.Kind,
            RuntimeActorSnapshotIntegrityCode.DuplicateCharge,
            "$.battleStatus.charges",
            "charge kind",
            _ => null,
            diagnostics);
        ValidateUnique(
            snapshot.BattleStatus.Shields,
            shield => shield.Kind,
            RuntimeActorSnapshotIntegrityCode.DuplicateShield,
            "$.battleStatus.shields",
            "shield kind",
            _ => null,
            diagnostics);
        ValidateUnique(
            snapshot.BattleStatus.AffinityBreaks,
            affinityBreak => affinityBreak.Element,
            RuntimeActorSnapshotIntegrityCode.DuplicateAffinityBreak,
            "$.battleStatus.affinityBreaks",
            "affinity-break element",
            _ => null,
            diagnostics);
        for (int index = 0; index < snapshot.BattleStatus.AffinityBreaks.Count; index++)
        {
            if (snapshot.BattleStatus.AffinityBreaks[index].Element == DamageElement.Almighty)
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.InvalidAffinityBreakElement,
                    "Almighty cannot receive an affinity Break.",
                    $"$.battleStatus.affinityBreaks[{index}].element"));
            }
        }
        ValidateUnique(
            snapshot.BattleStatus.AffinityOverrides,
            affinity => affinity.Element,
            RuntimeActorSnapshotIntegrityCode.DuplicateAffinityOverride,
            "$.battleStatus.affinityOverrides",
            "affinity-override element",
            _ => null,
            diagnostics);
        ValidateAnalysis(snapshot.BattleStatus.Analysis, diagnostics);
        ValidatePassives(snapshot.BattleActivations, loadedPassiveSkillIds, diagnostics);

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private static void ValidateAnalysis(
        IReadOnlyList<RuntimeAnalysisSnapshot> analysisEntries,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        var seenTargets = new HashSet<RuntimeInstanceId>();
        for (int index = 0; index < analysisEntries.Count; index++)
        {
            RuntimeAnalysisSnapshot analysis = analysisEntries[index];
            if (!seenTargets.Add(analysis.TargetInstanceId))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.DuplicateAnalysisTarget,
                    $"Analysis target '{analysis.TargetInstanceId}' appears more than once.",
                    $"$.battleStatus.analysis[{index}]"));
            }

            var seenLayers = new HashSet<AnalysisLayer>();
            for (int layerIndex = 0; layerIndex < analysis.Layers.Count; layerIndex++)
            {
                AnalysisLayer layer = analysis.Layers[layerIndex];
                if (seenLayers.Add(layer))
                {
                    continue;
                }

                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.DuplicateAnalysisLayer,
                    $"Analysis layer '{layer}' appears more than once for target '{analysis.TargetInstanceId}'.",
                    $"$.battleStatus.analysis[{index}].layers[{layerIndex}]"));
            }
        }
    }

    private static void ValidatePassives(
        RuntimeBattleActivationSnapshot battleActivations,
        IEnumerable<ContentId>? loadedPassiveSkillIds,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        HashSet<ContentId> loadedPassives = (loadedPassiveSkillIds ?? []).ToHashSet();
        var seenStates = new HashSet<ContentId>();
        for (int index = 0; index < battleActivations.PassiveSkillStates.Count; index++)
        {
            RuntimePassiveSkillStateSnapshot state = battleActivations.PassiveSkillStates[index];
            if (!seenStates.Add(state.SkillId))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.DuplicatePassiveSkillState,
                    $"Passive state for skill '{state.SkillId}' appears more than once.",
                    $"$.battleActivations.passiveSkillStates[{index}]",
                    state.SkillId));
            }

            if (!loadedPassives.Contains(state.SkillId))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.PassiveSkillStateNotLoaded,
                    $"Passive state references skill '{state.SkillId}', which is not loaded as an equipped passive.",
                    $"$.battleActivations.passiveSkillStates[{index}]",
                    state.SkillId));
            }
        }

        var seenActivations = new HashSet<(ContentId SkillId, ContentId EventId, int TriggerIndex)>();
        for (int index = 0; index < battleActivations.PassiveActivations.Count; index++)
        {
            RuntimePassiveActivationSnapshot activation = battleActivations.PassiveActivations[index];
            var key = (activation.SkillId, activation.EventId, activation.TriggerIndex);
            if (!seenActivations.Add(key))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.DuplicatePassiveActivation,
                    $"Passive activation '{activation.SkillId}/{activation.EventId}/{activation.TriggerIndex}' appears more than once.",
                    $"$.battleActivations.passiveActivations[{index}]",
                    activation.SkillId));
            }

            if (!loadedPassives.Contains(activation.SkillId))
            {
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.PassiveActivationSkillNotLoaded,
                    $"Passive activation references skill '{activation.SkillId}', which is not loaded as an equipped passive.",
                    $"$.battleActivations.passiveActivations[{index}]",
                    activation.SkillId));
            }
        }
    }

    private static void ValidateUnique<TValue, TKey>(
        IReadOnlyList<TValue> values,
        Func<TValue, TKey> keySelector,
        RuntimeActorSnapshotIntegrityCode code,
        string path,
        string label,
        Func<TKey, ContentId?> contentIdSelector,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
        where TKey : notnull
    {
        var seen = new HashSet<TKey>();
        for (int index = 0; index < values.Count; index++)
        {
            TKey key = keySelector(values[index]);
            if (seen.Add(key))
            {
                continue;
            }

            diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                code,
                $"Actor {label} '{key}' appears more than once.",
                $"{path}[{index}]",
                contentIdSelector(key)));
        }
    }
}
