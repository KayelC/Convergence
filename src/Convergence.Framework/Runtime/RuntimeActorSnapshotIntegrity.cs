using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

internal enum RuntimeActorSnapshotIntegrityCode
{
    InvalidRuntimeInstanceId,
    InvalidContentId,
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
    StatStageOutOfRange,
    BaseStatOutOfRange,
    EffectiveStatOutOfRange,
    BaseResourceValueOutOfRange,
    RetainedDurationKindInvalid,
    TurnDurationValueOutOfRange,
    TurnDurationTickEventIdInvalid,
    PhaseDurationPhaseIdInvalid
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
        IEnumerable<ContentId>? availableAilmentIds,
        IReadOnlySet<ContentId>? registeredEventIds = null,
        IReadOnlySet<ContentId>? registeredPhaseIds = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var diagnostics = new List<RuntimeActorSnapshotIntegrityDiagnostic>();
        ValidateIdentifiers(snapshot, diagnostics);
        ValidateUnique(
            snapshot.Resources,
            resource => resource.ResourceId,
            RuntimeActorSnapshotIntegrityCode.DuplicateResource,
            "$.resources",
            "resource",
            key => key,
            diagnostics);
        ValidateStatValues(
            snapshot.Stats.BaseStats,
            RuntimeActorSnapshotIntegrityCode.BaseStatOutOfRange,
            "$.stats.baseStats",
            "Base stat",
            diagnostics);
        ValidateStatValues(
            snapshot.Stats.EffectiveStats,
            RuntimeActorSnapshotIntegrityCode.EffectiveStatOutOfRange,
            "$.stats.effectiveStats",
            "Effective stat",
            diagnostics);
        ValidateBaseResourceValues(snapshot.BaseResourceValues, diagnostics);

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
        ValidateBattleStatusDurations(
            snapshot.BattleStatus,
            registeredEventIds,
            registeredPhaseIds,
            diagnostics);
        ValidateAnalysis(snapshot.BattleStatus.Analysis, diagnostics);
        ValidatePassives(snapshot.BattleActivations, loadedPassiveSkillIds, diagnostics);

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private static void ValidateIdentifiers(
        RuntimeActorSnapshot snapshot,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        ValidateRuntimeInstanceId(snapshot.Identity.InstanceId, "$.identity.instanceId", diagnostics);
        ValidateContentId(snapshot.Identity.EntityDefinitionId, "$.identity.entityDefinitionId", diagnostics);
        ValidateContentId(snapshot.Identity.ActorKindId, "$.identity.actorKindId", diagnostics);
        ValidateContentId(snapshot.Ownership.ControllerId, "$.ownership.controllerId", diagnostics);
        ValidateContentId(snapshot.Ownership.TeamId, "$.ownership.teamId", diagnostics);
        if (snapshot.Ownership.OwnerInstanceId is RuntimeInstanceId ownerId)
        {
            ValidateRuntimeInstanceId(ownerId, "$.ownership.ownerInstanceId", diagnostics);
        }

        ValidateContentId(snapshot.VitalResourceId, "$.vitalResourceId", diagnostics);
        for (int index = 0; index < snapshot.Resources.Count; index++)
        {
            ValidateContentId(snapshot.Resources[index].ResourceId, $"$.resources[{index}].resourceId", diagnostics);
        }

        ValidateContentIdKeys(snapshot.BaseResourceValues.Keys, "$.baseResourceValues", diagnostics);
        ValidateContentIdKeys(snapshot.Stats.BaseStats.Keys, "$.stats.baseStats", diagnostics);
        ValidateContentIdKeys(snapshot.Stats.EffectiveStats.Keys, "$.stats.effectiveStats", diagnostics);
        ValidateContentIds(snapshot.Skills.LearnedSkillIds, "$.skills.learnedSkillIds", diagnostics);
        ValidateContentIds(snapshot.Skills.EquippedSkillIds, "$.skills.equippedSkillIds", diagnostics);
        ValidateContentIds(snapshot.CapabilityIds, "$.capabilityIds", diagnostics);

        if (snapshot.Rosters.ActiveHostedEntity is RuntimeActorReferenceSnapshot activeHostedEntity)
        {
            ValidateActorReference(activeHostedEntity, "$.rosters.activeHostedEntity", diagnostics);
        }
        ValidateActorReferences(snapshot.Rosters.HostedEntityRoster, "$.rosters.hostedEntityRoster", diagnostics);
        ValidateActorReferences(snapshot.Rosters.CompanionRoster, "$.rosters.companionRoster", diagnostics);

        foreach ((EquipmentSlot slot, ContentId equipmentId) in snapshot.Equipment.EquippedItemIds)
        {
            ValidateContentId(equipmentId, $"$.equipment.equippedItemIds.{slot.ToString().ToLowerInvariant()}", diagnostics);
        }

        for (int index = 0; index < snapshot.BattleStatus.Ailments.Count; index++)
        {
            ValidateContentId(snapshot.BattleStatus.Ailments[index].Id,
                $"$.battleStatus.ailments[{index}].id", diagnostics);
        }
        for (int index = 0; index < snapshot.BattleStatus.Statuses.Count; index++)
        {
            ValidateContentId(snapshot.BattleStatus.Statuses[index].Id,
                $"$.battleStatus.statuses[{index}].id", diagnostics);
        }
        for (int index = 0; index < snapshot.BattleStatus.StatStages.Count; index++)
        {
            ValidateContentId(snapshot.BattleStatus.StatStages[index].ModifierTrackId,
                $"$.battleStatus.statStages[{index}].modifierTrackId", diagnostics);
        }
        for (int index = 0; index < snapshot.BattleStatus.Analysis.Count; index++)
        {
            ValidateRuntimeInstanceId(snapshot.BattleStatus.Analysis[index].TargetInstanceId,
                $"$.battleStatus.analysis[{index}].targetInstanceId", diagnostics);
        }

        for (int index = 0; index < snapshot.BattleActivations.PassiveSkillStates.Count; index++)
        {
            ValidateContentId(snapshot.BattleActivations.PassiveSkillStates[index].SkillId,
                $"$.battleActivations.passiveSkillStates[{index}].skillId", diagnostics);
        }
        for (int index = 0; index < snapshot.BattleActivations.PassiveActivations.Count; index++)
        {
            RuntimePassiveActivationSnapshot activation = snapshot.BattleActivations.PassiveActivations[index];
            ValidateContentId(activation.SkillId,
                $"$.battleActivations.passiveActivations[{index}].skillId", diagnostics);
            ValidateContentId(activation.EventId,
                $"$.battleActivations.passiveActivations[{index}].eventId", diagnostics);
        }
    }

    private static void ValidateActorReferences(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        string path,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        for (int index = 0; index < references.Count; index++)
        {
            ValidateActorReference(references[index], $"{path}[{index}]", diagnostics);
        }
    }

    private static void ValidateActorReference(
        RuntimeActorReferenceSnapshot reference,
        string path,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        ValidateRuntimeInstanceId(reference.InstanceId, path + ".instanceId", diagnostics);
        ValidateContentId(reference.EntityDefinitionId, path + ".entityDefinitionId", diagnostics);
    }

    private static void ValidateContentIdKeys(
        IEnumerable<ContentId> ids,
        string path,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        int index = 0;
        foreach (ContentId id in ids)
        {
            ValidateContentId(id, $"{path}[{index}]", diagnostics);
            index++;
        }
    }

    private static void ValidateContentIds(
        IReadOnlyList<ContentId> ids,
        string path,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        for (int index = 0; index < ids.Count; index++)
        {
            ValidateContentId(ids[index], $"{path}[{index}]", diagnostics);
        }
    }

    private static void ValidateContentId(
        ContentId id,
        string path,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        if (id.IsValid)
        {
            return;
        }

        diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
            RuntimeActorSnapshotIntegrityCode.InvalidContentId,
            "Content ID cannot be empty.",
            path,
            id));
    }

    private static void ValidateRuntimeInstanceId(
        RuntimeInstanceId id,
        string path,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        if (id.IsValid)
        {
            return;
        }

        diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
            RuntimeActorSnapshotIntegrityCode.InvalidRuntimeInstanceId,
            "Runtime instance ID cannot be empty.",
            path));
    }

    private static void ValidateBattleStatusDurations(
        RuntimeBattleStatusSnapshot status,
        IReadOnlySet<ContentId>? registeredEventIds,
        IReadOnlySet<ContentId>? registeredPhaseIds,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        for (int index = 0; index < status.Ailments.Count; index++)
        {
            RuntimeTimedStateSnapshot ailment = status.Ailments[index];
            ValidateRetainedDuration(
                ailment.Duration,
                $"$.battleStatus.ailments[{index}].duration",
                ailment.Id,
                registeredEventIds,
                registeredPhaseIds,
                diagnostics);
        }

        for (int index = 0; index < status.Statuses.Count; index++)
        {
            RuntimeTimedStateSnapshot other = status.Statuses[index];
            ValidateRetainedDuration(
                other.Duration,
                $"$.battleStatus.statuses[{index}].duration",
                other.Id,
                registeredEventIds,
                registeredPhaseIds,
                diagnostics);
        }

        for (int index = 0; index < status.StatStages.Count; index++)
        {
            RuntimeStatStageSnapshot stage = status.StatStages[index];
            if (stage.Duration is not null)
            {
                ValidateRetainedDuration(
                    stage.Duration,
                    $"$.battleStatus.statStages[{index}].duration",
                    stage.ModifierTrackId,
                    registeredEventIds,
                    registeredPhaseIds,
                    diagnostics);
            }
        }

        for (int index = 0; index < status.Charges.Count; index++)
        {
            DurationDefinition? duration = status.Charges[index].Duration;
            if (duration is not null)
            {
                ValidateRetainedDuration(
                    duration,
                    $"$.battleStatus.charges[{index}].duration",
                    contentId: null,
                    registeredEventIds,
                    registeredPhaseIds,
                    diagnostics);
            }
        }

        for (int index = 0; index < status.Shields.Count; index++)
        {
            DurationDefinition? duration = status.Shields[index].Duration;
            if (duration is not null)
            {
                ValidateRetainedDuration(
                    duration,
                    $"$.battleStatus.shields[{index}].duration",
                    contentId: null,
                    registeredEventIds,
                    registeredPhaseIds,
                    diagnostics);
            }
        }

        for (int index = 0; index < status.AffinityBreaks.Count; index++)
        {
            ValidateRetainedDuration(
                status.AffinityBreaks[index].Duration,
                $"$.battleStatus.affinityBreaks[{index}].duration",
                contentId: null,
                registeredEventIds,
                registeredPhaseIds,
                diagnostics);
        }

        for (int index = 0; index < status.AffinityOverrides.Count; index++)
        {
            ValidateRetainedDuration(
                status.AffinityOverrides[index].Duration,
                $"$.battleStatus.affinityOverrides[{index}].duration",
                contentId: null,
                registeredEventIds,
                registeredPhaseIds,
                diagnostics);
        }
    }

    private static void ValidateRetainedDuration(
        DurationDefinition duration,
        string path,
        ContentId? contentId,
        IReadOnlySet<ContentId>? registeredEventIds,
        IReadOnlySet<ContentId>? registeredPhaseIds,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        switch (duration)
        {
            case TurnDurationDefinition turns:
                if (turns.Value <= 0)
                {
                    diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                        RuntimeActorSnapshotIntegrityCode.TurnDurationValueOutOfRange,
                        "A retained turn duration must have at least one remaining turn.",
                        path + ".value",
                        contentId));
                }

                if (!IsValidContentId(turns.TickEventId))
                {
                    diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                        RuntimeActorSnapshotIntegrityCode.TurnDurationTickEventIdInvalid,
                        "A retained turn duration must identify a valid tick event.",
                        path + ".tickEventId",
                        contentId));
                }
                else if (registeredEventIds is not null && !registeredEventIds.Contains(turns.TickEventId))
                {
                    diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                        RuntimeActorSnapshotIntegrityCode.TurnDurationTickEventIdInvalid,
                        $"Tick event '{turns.TickEventId}' is not registered by the current content catalog.",
                        path + ".tickEventId",
                        contentId));
                }
                break;

            case PhaseDurationDefinition phase:
                if (!IsValidContentId(phase.PhaseId))
                {
                    diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                        RuntimeActorSnapshotIntegrityCode.PhaseDurationPhaseIdInvalid,
                        "A retained phase duration must identify a valid phase.",
                        path + ".phaseId",
                        contentId));
                }
                else if (registeredPhaseIds is not null && !registeredPhaseIds.Contains(phase.PhaseId))
                {
                    diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                        RuntimeActorSnapshotIntegrityCode.PhaseDurationPhaseIdInvalid,
                        $"Phase '{phase.PhaseId}' is not registered by the current content catalog.",
                        path + ".phaseId",
                        contentId));
                }
                break;

            case BattleDurationDefinition:
            case PermanentDurationDefinition:
                break;

            case InstantDurationDefinition:
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.RetainedDurationKindInvalid,
                    "Instant duration state cannot be restored because it must expire at the action boundary.",
                    path + ".kind",
                    contentId));
                break;

            default:
                diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                    RuntimeActorSnapshotIntegrityCode.RetainedDurationKindInvalid,
                    $"Duration type '{duration.GetType().Name}' cannot represent retained runtime state.",
                    path + ".kind",
                    contentId));
                break;
        }
    }

    private static bool IsValidContentId(ContentId id) => id.IsValid;

    private static void ValidateStatValues(
        IReadOnlyDictionary<ContentId, decimal> values,
        RuntimeActorSnapshotIntegrityCode code,
        string path,
        string label,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        foreach ((ContentId statId, decimal value) in values)
        {
            if (RuntimeActorNumericDomain.IsValidStatValue(value))
            {
                continue;
            }

            diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                code,
                $"{label} '{statId}' must be between {RuntimeActorNumericDomain.MinimumStatValue} and " +
                $"{RuntimeActorNumericDomain.MaximumStatValue} inclusive.",
                $"{path}.{statId}",
                statId));
        }
    }

    private static void ValidateBaseResourceValues(
        IReadOnlyDictionary<ContentId, decimal> values,
        ICollection<RuntimeActorSnapshotIntegrityDiagnostic> diagnostics)
    {
        foreach ((ContentId resourceId, decimal value) in values)
        {
            if (RuntimeActorNumericDomain.IsValidBaseResourceValue(value))
            {
                continue;
            }

            diagnostics.Add(new RuntimeActorSnapshotIntegrityDiagnostic(
                RuntimeActorSnapshotIntegrityCode.BaseResourceValueOutOfRange,
                $"Base resource '{resourceId}' cannot be negative.",
                $"$.baseResourceValues.{resourceId}",
                resourceId));
        }
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
