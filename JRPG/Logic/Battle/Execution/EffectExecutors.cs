using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Battle.Execution;

internal abstract class TargetedEffectExecutor
{
    protected static BattleActorState Target(EffectExecutionContext context) =>
        context.Target ?? throw new InvalidOperationException(
            $"Effect '{context.Effect.GetType().Name}' requires a target.");

    protected static EffectExecutionResult Success(
        EffectExecutionContext context,
        decimal? value = null,
        ContentId? relatedId = null,
        PressTurnOutcome pressTurn = PressTurnOutcome.Normal,
        bool critical = false,
        string? detail = null,
        bool escape = false) =>
        new(context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Success,
            pressTurn, critical, value, relatedId, detail, escape);

    protected static EffectExecutionResult Failure(
        EffectExecutionContext context,
        PressTurnOutcome pressTurn = PressTurnOutcome.Miss,
        string? detail = null,
        ContentId? relatedId = null) =>
        new(context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Failure,
            pressTurn, Detail: detail, RelatedId: relatedId);

    protected static EffectExecutionResult Interrupted(
        EffectExecutionContext context,
        PressTurnOutcome pressTurn,
        decimal? value = null,
        string? detail = null) =>
        new(context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Interrupted,
            pressTurn, Value: value, Detail: detail);
}

internal sealed class DamageEffectExecutor : TargetedEffectExecutor, IEffectExecutor<DamageEffectDefinition>
{
    public EffectExecutionResult Execute(DamageEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        ElementalAffinity affinity = target.GetElementalAffinity(definition.Element);
        IReadOnlyList<DamageHitResolution> hits = context.Services.DamagePolicy.Resolve(
            new DamagePolicyRequest(context.Actor, target, definition, affinity));
        DamageHitResolution[] landed = hits.Where(hit => hit.Hit).ToArray();
        if (landed.Length == 0)
        {
            return Failure(context, PressTurnOutcome.Miss, "All damage hits missed.");
        }

        decimal total = landed.Sum(hit => Math.Max(0, hit.Damage));
        bool critical = landed.Any(hit => hit.Critical);
        switch (affinity)
        {
            case ElementalAffinity.Null:
                return Failure(context, PressTurnOutcome.Null, "The damage was nullified.");
            case ElementalAffinity.Repel:
            {
                decimal reflected = -context.Actor.AddResource(context.Actor.VitalResourceId, -total);
                return Interrupted(context, PressTurnOutcome.Repel, reflected, "The damage was reflected.");
            }
            case ElementalAffinity.Absorb:
            {
                decimal absorbed = target.AddResource(target.VitalResourceId, total);
                return Interrupted(context, PressTurnOutcome.Absorb, absorbed, "The damage was absorbed.");
            }
            default:
            {
                decimal dealt = -target.AddResource(target.VitalResourceId, -total);
                ApplyDrain(definition.Drain, context.Actor, context.Services, dealt);
                PressTurnOutcome outcome = affinity == ElementalAffinity.Weak
                    ? PressTurnOutcome.Weakness
                    : critical ? PressTurnOutcome.Critical : PressTurnOutcome.Normal;
                return Success(context, dealt, pressTurn: outcome, critical: critical);
            }
        }
    }

    private static void ApplyDrain(
        DamageDrainMode drain,
        BattleActorState actor,
        BattleExecutionServices services,
        decimal amount)
    {
        if (drain == DamageDrainMode.Hp && actor.TryGetResource(services.HpResourceId, out _))
        {
            actor.AddResource(services.HpResourceId, amount);
        }
        else if (drain == DamageDrainMode.Sp && actor.TryGetResource(services.SpResourceId, out _))
        {
            actor.AddResource(services.SpResourceId, amount);
        }
    }
}

internal sealed class InstantKillEffectExecutor : TargetedEffectExecutor, IEffectExecutor<InstantKillEffectDefinition>
{
    public EffectExecutionResult Execute(InstantKillEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        InstantDeathResistanceResolution resistance = InstantDeathResistanceResolver.Resolve(
            target.DefenseProfile,
            definition.ResistanceCheck);
        bool success = context.Services.InstantDeathPolicy.ShouldDefeat(
            new InstantDeathPolicyRequest(context.Actor, target, definition, resistance));
        if (!success)
        {
            return Failure(context, PressTurnOutcome.Miss, "The instant-death attempt failed.");
        }

        decimal removed = -target.SetResource(target.VitalResourceId, 0);
        return Success(context, removed);
    }
}

internal sealed class ApplyAilmentEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ApplyAilmentEffectDefinition>
{
    public EffectExecutionResult Execute(ApplyAilmentEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        if (target.IsDefeated || !context.Services.Ailments.TryGetAilment(definition.AilmentId, out AilmentDefinition? ailment) || ailment is null)
        {
            return Failure(context, detail: "The ailment target or definition is unavailable.", relatedId: definition.AilmentId);
        }

        ResistanceLevel resistance = AilmentResistanceResolver.Resolve(target.DefenseProfile, definition.AilmentId);
        if (!context.Services.AilmentPolicy.ShouldApply(
                new AilmentApplicationPolicyRequest(context.Actor, target, definition, ailment, resistance)))
        {
            return Failure(context, detail: "The ailment application failed.", relatedId: definition.AilmentId);
        }

        target.ApplyAilment(ailment, definition.Duration ?? ailment.DefaultDuration);
        return Success(context, relatedId: definition.AilmentId);
    }
}

internal sealed class RestoreResourceEffectExecutor : TargetedEffectExecutor, IEffectExecutor<RestoreResourceEffectDefinition>
{
    public EffectExecutionResult Execute(RestoreResourceEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        if (!target.TryGetResource(definition.ResourceId, out _))
        {
            return Failure(context, detail: "The target does not expose the requested resource.", relatedId: definition.ResourceId);
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "restore_resource"),
            context.Services);
        decimal restored = target.AddResource(definition.ResourceId, amount);
        return Success(context, restored, definition.ResourceId);
    }
}

internal sealed class RemoveAilmentEffectExecutor : TargetedEffectExecutor, IEffectExecutor<RemoveAilmentEffectDefinition>
{
    public EffectExecutionResult Execute(RemoveAilmentEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        HashSet<ContentId> ailmentIds = new(definition.AilmentIds);
        HashSet<ContentId> groupIds = new(definition.AilmentGroupIds);
        IReadOnlyList<ContentId> removed = target.RemoveAilments(active =>
            definition.Scope == AilmentRemovalScope.AllRemovable ||
            ailmentIds.Contains(active.Definition.Id) ||
            active.Definition.GroupIds.Any(groupIds.Contains));
        return Success(context, removed.Count, detail: string.Join(",", removed));
    }
}

internal sealed class ReviveEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ReviveEffectDefinition>
{
    public EffectExecutionResult Execute(ReviveEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        if (!target.IsDefeated || definition.ResourceId != target.VitalResourceId || !target.TryGetResource(definition.ResourceId, out _))
        {
            return Failure(context, detail: "Revival requires a defeated target and its vital resource.");
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "revive"),
            context.Services);
        decimal restored = target.SetResource(definition.ResourceId, amount);
        return target.IsDefeated
            ? Failure(context, detail: "The revival amount did not restore the target.")
            : Success(context, restored, definition.ResourceId);
    }
}

internal sealed class ModifyStatStageEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ModifyStatStageEffectDefinition>
{
    public EffectExecutionResult Execute(ModifyStatStageEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        foreach (ContentId id in definition.ModifierTrackIds)
        {
            target.ChangeStatStage(id, definition.StageDelta, definition.Duration);
        }
        return Success(context, definition.StageDelta, detail: string.Join(",", definition.ModifierTrackIds));
    }
}

internal sealed class GrantChargeEffectExecutor : TargetedEffectExecutor, IEffectExecutor<GrantChargeEffectDefinition>
{
    public EffectExecutionResult Execute(GrantChargeEffectDefinition definition, EffectExecutionContext context)
    {
        Target(context).GrantCharge(definition.Charge, definition.Multiplier, definition.Duration);
        return Success(context, definition.Multiplier, detail: definition.Charge.ToString());
    }
}

internal sealed class GrantShieldEffectExecutor : TargetedEffectExecutor, IEffectExecutor<GrantShieldEffectDefinition>
{
    public EffectExecutionResult Execute(GrantShieldEffectDefinition definition, EffectExecutionContext context)
    {
        Target(context).GrantShield(definition.Shield, definition.Duration);
        return Success(context, detail: definition.Shield.ToString());
    }
}

internal sealed class OverrideAffinityEffectExecutor : TargetedEffectExecutor, IEffectExecutor<OverrideAffinityEffectDefinition>
{
    public EffectExecutionResult Execute(OverrideAffinityEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        foreach (DamageElement element in definition.Elements)
        {
            target.OverrideAffinity(element, definition.Affinity, definition.Duration);
        }
        return Success(context, detail: definition.Affinity.ToString());
    }
}

internal sealed class RemoveStatusEffectExecutor : TargetedEffectExecutor, IEffectExecutor<RemoveStatusEffectDefinition>
{
    public EffectExecutionResult Execute(RemoveStatusEffectDefinition definition, EffectExecutionContext context)
    {
        Target(context).RemoveStatuses(definition.StatusKinds, definition.StatusIds);
        return Success(context);
    }
}

internal sealed class ReduceResourceEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ReduceResourceEffectDefinition>
{
    public EffectExecutionResult Execute(ReduceResourceEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        if (!target.TryGetResource(definition.ResourceId, out BattleResourceState? resource) || resource is null)
        {
            return Failure(context, detail: "The target does not expose the requested resource.", relatedId: definition.ResourceId);
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "reduce_resource"),
            context.Services);
        decimal floor = definition.CanReduceToZero ? 0 : Math.Min(1, resource.Maximum);
        decimal reduced = -target.SetResource(definition.ResourceId, Math.Max(floor, resource.Current - amount));
        return Success(context, reduced, definition.ResourceId);
    }
}

internal sealed class SetResourceEffectExecutor : TargetedEffectExecutor, IEffectExecutor<SetResourceEffectDefinition>
{
    public EffectExecutionResult Execute(SetResourceEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        if (!target.TryGetResource(definition.ResourceId, out _))
        {
            return Failure(context, detail: "The target does not expose the requested resource.", relatedId: definition.ResourceId);
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "set_resource"),
            context.Services);
        decimal changed = target.SetResource(definition.ResourceId, amount);
        return Success(context, changed, definition.ResourceId);
    }
}

internal sealed class AnalyzeEffectExecutor : TargetedEffectExecutor, IEffectExecutor<AnalyzeEffectDefinition>
{
    public EffectExecutionResult Execute(AnalyzeEffectDefinition definition, EffectExecutionContext context)
    {
        BattleActorState target = Target(context);
        context.Actor.Reveal(target.InstanceId, definition.Layers);
        return Success(context, detail: string.Join(",", definition.Layers));
    }
}

internal sealed class EscapeEffectExecutor : TargetedEffectExecutor, IEffectExecutor<EscapeEffectDefinition>
{
    public EffectExecutionResult Execute(EscapeEffectDefinition definition, EffectExecutionContext context)
    {
        if (!context.Services.EscapeRuleHandlers.TryGetValue(definition.EligibilityRuleId, out IEscapeRuleHandler? handler))
        {
            return Failure(context, detail: "No escape rule handler is registered.", relatedId: definition.EligibilityRuleId);
        }

        bool success = handler.CanEscape(definition, context) &&
            (definition.Chance is null || context.Services.ChancePolicy.Roll(
                new ChancePolicyRequest(definition.Chance.Value, context.Actor, context.Target, "escape")));
        return success
            ? Success(context, relatedId: definition.EligibilityRuleId, escape: true)
            : Failure(context, detail: "The escape request failed.", relatedId: definition.EligibilityRuleId);
    }
}

internal sealed class CustomEffectExecutor : TargetedEffectExecutor, IEffectExecutor<CustomEffectDefinition>
{
    public EffectExecutionResult Execute(CustomEffectDefinition definition, EffectExecutionContext context)
    {
        if (!context.Services.CustomEffectHandlers.TryGetValue(definition.HandlerId, out ICustomEffectHandler? handler))
        {
            return Failure(context, detail: "No custom effect handler is registered.", relatedId: definition.HandlerId);
        }

        EffectExecutionResult result = handler.Execute(definition, context);
        return result with
        {
            EffectIndex = context.EffectIndex,
            TargetId = context.Target?.InstanceId
        };
    }
}
