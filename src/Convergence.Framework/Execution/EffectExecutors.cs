using Convergence.Content;
using Convergence.Battle;
using Convergence.Knowledge;
using Convergence.Runtime;

namespace Convergence.Execution;

internal abstract class TargetedEffectExecutor
{
    protected static RuntimeActorState Target(EffectExecutionContext context) =>
        context.Target ?? throw new InvalidOperationException(
            $"Effect '{context.Effect.GetType().Name}' requires a target.");

    protected static EffectExecutionResult Success(
        EffectExecutionContext context,
        decimal? value = null,
        ContentId? relatedId = null,
        TurnEconomyOutcome turnEconomy = TurnEconomyOutcome.Normal,
        bool critical = false,
        string? detail = null,
        bool escape = false,
        IReadOnlyList<PassiveTriggerExecutionResult>? passiveActivations = null,
        ElementalAffinity? resolvedAffinity = null,
        IReadOnlyList<ExecutionResourceChange>? resourceChanges = null,
        IReadOnlyList<StatModifierTransitionResult>? statModifierTransitions = null,
        IReadOnlyList<DamageHitExecutionEvidence>? damageHits = null,
        IReadOnlyList<BattleStatusLifecycleEvent>? lifecycleEvents = null,
        BattleAnalysisResult? analysis = null) =>
        new EffectExecutionResult(
            context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Success,
            turnEconomy, critical, value, relatedId, detail, escape, passiveActivations, resolvedAffinity,
            HostActionRequestIds: null,
            StatModifierTransitions: statModifierTransitions,
            DamageHits: damageHits)
        {
            ResourceChanges = resourceChanges ?? [],
            LifecycleEvents = AggregateLifecycleEvents(passiveActivations, lifecycleEvents),
            Analysis = analysis
        };

    protected static EffectExecutionResult Failure(
        EffectExecutionContext context,
        TurnEconomyOutcome turnEconomy = TurnEconomyOutcome.Miss,
        string? detail = null,
        ContentId? relatedId = null,
        ElementalAffinity? resolvedAffinity = null,
        IReadOnlyList<StatModifierTransitionResult>? statModifierTransitions = null,
        IReadOnlyList<DamageHitExecutionEvidence>? damageHits = null,
        IReadOnlyList<BattleStatusLifecycleEvent>? lifecycleEvents = null) =>
        new(context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Failure,
            turnEconomy, Detail: detail, RelatedId: relatedId, ResolvedAffinity: resolvedAffinity,
            StatModifierTransitions: statModifierTransitions, DamageHits: damageHits)
        {
            LifecycleEvents = lifecycleEvents ?? []
        };

    protected static EffectExecutionResult Interrupted(
        EffectExecutionContext context,
        TurnEconomyOutcome turnEconomy,
        decimal? value = null,
        string? detail = null,
        IReadOnlyList<PassiveTriggerExecutionResult>? passiveActivations = null,
        ElementalAffinity? resolvedAffinity = null,
        IReadOnlyList<ExecutionResourceChange>? resourceChanges = null,
        IReadOnlyList<DamageHitExecutionEvidence>? damageHits = null) =>
        new EffectExecutionResult(
            context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Interrupted,
            turnEconomy, Value: value, Detail: detail, PassiveActivations: passiveActivations,
            ResolvedAffinity: resolvedAffinity,
            DamageHits: damageHits)
        {
            ResourceChanges = resourceChanges ?? [],
            LifecycleEvents = AggregateLifecycleEvents(passiveActivations, lifecycleEvents: null)
        };

    protected static IReadOnlyList<ExecutionResourceChange> ResourceChanges(
        RuntimeActorState actor,
        ContentId resourceId,
        decimal delta) =>
        delta == 0
            ? []
            : [new ExecutionResourceChange(actor.InstanceId, resourceId, delta)];

    protected static IEnumerable<ExecutionResourceChange> PassiveResourceChanges(
        IEnumerable<PassiveTriggerExecutionResult> activations) =>
        activations.SelectMany(activation => activation.Effects)
            .SelectMany(effect => effect.ResourceChanges);

    private static IReadOnlyList<BattleStatusLifecycleEvent> AggregateLifecycleEvents(
        IEnumerable<PassiveTriggerExecutionResult>? activations,
        IEnumerable<BattleStatusLifecycleEvent>? lifecycleEvents) =>
        Array.AsReadOnly((lifecycleEvents ?? [])
            .Concat((activations ?? [])
                .SelectMany(activation =>
                    activation.Effects
                        .SelectMany(effect => effect.LifecycleEvents)
                        .Concat(activation.CompletionLifecycleEvents)))
            .ToArray());

    protected static IReadOnlyList<PassiveTriggerExecutionResult> DispatchDefeatPrevention(
        EffectExecutionContext context,
        RuntimeActorState owner)
    {
        if (!owner.IsDefeated)
        {
            return [];
        }

        PassiveTriggerDispatchResult dispatch = context.Services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                context.Services.OwnerWouldBeDefeatedEventId,
                owner,
                context.Request.Participants,
                [owner],
                context.Request.ContextId,
                context.Request.BattleKindId,
                context.Request.MoonPhaseId),
            context.Services);
        return dispatch.Activations;
    }
}

internal sealed class DamageEffectExecutor : TargetedEffectExecutor, IEffectExecutor<DamageEffectDefinition>
{
    public EffectExecutionResult Execute(DamageEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        var defenseConditionContext = new BattleConditionContext(
            target,
            context.Actor,
            context.Request.Participants,
            context.Request.BattleKindId,
            context.Request.MoonPhaseId,
            context.Services,
            [definition.Element]);
        var attackConditionContext = new BattleConditionContext(
            context.Actor,
            target,
            context.Request.Participants,
            context.Request.BattleKindId,
            context.Request.MoonPhaseId,
            context.Services,
            [definition.Element]);
        var attackModifierContext = new RuleModifierContext(
            attackConditionContext,
            context.Request.Skill);
        var defenseModifierContext = new RuleModifierContext(
            defenseConditionContext,
            context.Request.Skill);
        ElementalAffinity authoredAffinity = target.DefenseProfile.GetElementalAffinity(definition.Element);
        IReadOnlyList<ElementalAffinity> passiveReplacements =
            context.Services.RuleModifiers.ResolveElementalAffinityReplacements(
                target,
                definition.Element,
                defenseModifierContext);
        BattleDefenseInfluence defenseInfluences = ResolveElementalDefenseInfluences(
            target,
            definition.Element,
            passiveReplacements);
        ElementalAffinity affinity = target.GetElementalAffinity(definition.Element, passiveReplacements);
        ChargeDamageModifier charge = context.Services.Charges.ResolveDamageModifier(
            context.Actor,
            definition.Element);
        DamagePolicyResolution resolution = context.Services.DamagePolicy.Resolve(
            new DamagePolicyRequest(
                context.Actor,
                target,
                definition,
                affinity,
                charge.Multiplier,
                charge.ChargeKind,
                context.Services.RuleModifiers.GetApplicableNumericModifiers(
                    context.Actor,
                    NumericRuleModifierType.Accuracy,
                    attackModifierContext),
                context.Services.RuleModifiers.GetApplicableNumericModifiers(
                    target,
                    NumericRuleModifierType.Evasion,
                    defenseModifierContext),
                context.Services.RuleModifiers.GetApplicableNumericModifiers(
                    context.Actor,
                    NumericRuleModifierType.CriticalChance,
                    attackModifierContext)));
        affinity = resolution.ResolvedAffinity;
        DamageHitResolution[] hits = resolution.Hits.ToArray();
        EffectExecutionResult result;
        if (hits.All(hit => !hit.Hit))
        {
            DamageHitExecutionEvidence[] misses = hits
                .Select(hit => Evidence(context, target, hit, affinity))
                .ToArray();
            result = Failure(
                context,
                TurnEconomyOutcome.Miss,
                "All damage hits missed.",
                resolvedAffinity: affinity,
                damageHits: misses);
        }
        else
        {
            result = affinity switch
            {
                ElementalAffinity.Null => Failure(
                    context,
                    TurnEconomyOutcome.Null,
                    "The damage was nullified.",
                    resolvedAffinity: affinity,
                    damageHits: hits.Select(hit => Evidence(context, target, hit, affinity)).ToArray()),
                ElementalAffinity.Repel => ResolveReflectedHits(
                    context,
                    target,
                    hits,
                    affinity,
                    attackModifierContext,
                    defenseModifierContext),
                ElementalAffinity.Absorb => ResolveAbsorbedHits(
                    context,
                    target,
                    hits,
                    affinity,
                    attackModifierContext,
                    defenseModifierContext),
                _ => ResolveLandedHits(
                    context,
                    definition,
                    target,
                    hits,
                    affinity,
                    attackModifierContext,
                    defenseModifierContext)
            };
        }

        result = result with
        {
            KnowledgeObservations =
            [
                BattleKnowledgeObservation.Elemental(
                    context.Request.SourceId,
                    context.Actor.InstanceId,
                    target.InstanceId,
                    target.CombatProfileIdentity,
                    context.EffectIndex,
                    definition.Element,
                    hits.Any(hit => hit.Hit),
                    authoredAffinity,
                    affinity,
                    defenseInfluences)
            ]
        };

        return charge.IsCharged
            ? result with { ParticipatingCharge = charge }
            : result;
    }

    private static BattleDefenseInfluence ResolveElementalDefenseInfluences(
        RuntimeActorState target,
        DamageElement element,
        IReadOnlyCollection<ElementalAffinity> passiveReplacements)
    {
        if (element == DamageElement.Almighty)
        {
            return BattleDefenseInfluence.None;
        }

        BattleDefenseInfluence influences = target.IsGuarding
            ? BattleDefenseInfluence.Guard
            : BattleDefenseInfluence.None;
        ShieldKind matchingShield = element == DamageElement.Physical
            ? ShieldKind.Physical
            : ShieldKind.Magical;
        if (target.Shields.ContainsKey(matchingShield))
        {
            influences |= BattleDefenseInfluence.Shield;
        }
        if (target.AffinityBreaks.ContainsKey(element))
        {
            influences |= BattleDefenseInfluence.AffinityBreak;
        }
        if (target.AffinityOverrides.ContainsKey(element))
        {
            influences |= BattleDefenseInfluence.AffinityOverride;
        }
        if (passiveReplacements.Count > 0)
        {
            influences |= BattleDefenseInfluence.PassiveModifier;
        }

        return influences;
    }

    private static EffectExecutionResult ResolveLandedHits(
        EffectExecutionContext context,
        DamageEffectDefinition definition,
        RuntimeActorState target,
        IReadOnlyList<DamageHitResolution> hits,
        ElementalAffinity affinity,
        RuleModifierContext attackModifierContext,
        RuleModifierContext defenseModifierContext)
    {
        var activations = new List<PassiveTriggerExecutionResult>();
        var changes = new List<ExecutionResourceChange>();
        var evidence = new List<DamageHitExecutionEvidence>(hits.Count);
        decimal totalDealt = 0m;
        bool committedCritical = false;

        foreach (DamageHitResolution hit in hits)
        {
            if (!hit.Hit || target.IsDefeated)
            {
                evidence.Add(Evidence(context, target, hit, affinity));
                continue;
            }

            committedCritical |= hit.Critical;

            decimal amount = ResolveHitDamage(
                context,
                target,
                hit,
                attackModifierContext,
                defenseModifierContext);
            decimal damageDelta = target.AddResource(target.VitalResourceId, -amount);
            decimal dealt = -damageDelta;
            totalDealt = CombatArithmetic.SaturatingAdd(totalDealt, dealt);
            changes.AddRange(ResourceChanges(target, target.VitalResourceId, damageDelta));

            IReadOnlyList<PassiveTriggerExecutionResult> hitActivations =
                DispatchDefeatPrevention(context, target);
            activations.AddRange(hitActivations);
            changes.AddRange(PassiveResourceChanges(hitActivations));
            changes.AddRange(ApplyDrain(definition.Drain, context.Actor, context.Services, dealt));
            evidence.Add(Evidence(
                context,
                target,
                hit,
                affinity,
                target,
                target.VitalResourceId,
                damageDelta));
        }

        TurnEconomyOutcome outcome = affinity == ElementalAffinity.Weak
            ? TurnEconomyOutcome.Weakness
            : committedCritical ? TurnEconomyOutcome.Critical : TurnEconomyOutcome.Normal;
        return Success(
            context,
            totalDealt,
            turnEconomy: outcome,
            critical: committedCritical,
            passiveActivations: Array.AsReadOnly(activations.ToArray()),
            resolvedAffinity: affinity,
            resourceChanges: Array.AsReadOnly(changes.ToArray()),
            damageHits: Array.AsReadOnly(evidence.ToArray()));
    }

    private static EffectExecutionResult ResolveReflectedHits(
        EffectExecutionContext context,
        RuntimeActorState target,
        IReadOnlyList<DamageHitResolution> hits,
        ElementalAffinity affinity,
        RuleModifierContext attackModifierContext,
        RuleModifierContext defenseModifierContext)
    {
        var activations = new List<PassiveTriggerExecutionResult>();
        var changes = new List<ExecutionResourceChange>();
        var evidence = new List<DamageHitExecutionEvidence>(hits.Count);
        decimal totalReflected = 0m;

        foreach (DamageHitResolution hit in hits)
        {
            if (!hit.Hit || context.Actor.IsDefeated)
            {
                evidence.Add(Evidence(context, target, hit, affinity));
                continue;
            }

            decimal amount = ResolveHitDamage(
                context,
                target,
                hit,
                attackModifierContext,
                defenseModifierContext);
            decimal reflectedDelta = context.Actor.AddResource(context.Actor.VitalResourceId, -amount);
            totalReflected = CombatArithmetic.SaturatingAdd(totalReflected, -reflectedDelta);
            changes.AddRange(ResourceChanges(context.Actor, context.Actor.VitalResourceId, reflectedDelta));

            IReadOnlyList<PassiveTriggerExecutionResult> hitActivations =
                DispatchDefeatPrevention(context, context.Actor);
            activations.AddRange(hitActivations);
            changes.AddRange(PassiveResourceChanges(hitActivations));
            evidence.Add(Evidence(
                context,
                target,
                hit,
                affinity,
                context.Actor,
                context.Actor.VitalResourceId,
                reflectedDelta));
        }

        return Interrupted(
            context,
            TurnEconomyOutcome.Repel,
            totalReflected,
            "The damage was reflected.",
            Array.AsReadOnly(activations.ToArray()),
            affinity,
            Array.AsReadOnly(changes.ToArray()),
            Array.AsReadOnly(evidence.ToArray()));
    }

    private static EffectExecutionResult ResolveAbsorbedHits(
        EffectExecutionContext context,
        RuntimeActorState target,
        IReadOnlyList<DamageHitResolution> hits,
        ElementalAffinity affinity,
        RuleModifierContext attackModifierContext,
        RuleModifierContext defenseModifierContext)
    {
        var changes = new List<ExecutionResourceChange>();
        var evidence = new List<DamageHitExecutionEvidence>(hits.Count);
        decimal totalAbsorbed = 0m;

        foreach (DamageHitResolution hit in hits)
        {
            if (!hit.Hit)
            {
                evidence.Add(Evidence(context, target, hit, affinity));
                continue;
            }

            decimal amount = ResolveHitDamage(
                context,
                target,
                hit,
                attackModifierContext,
                defenseModifierContext);
            decimal absorbedDelta = target.AddResource(target.VitalResourceId, amount);
            totalAbsorbed = CombatArithmetic.SaturatingAdd(totalAbsorbed, absorbedDelta);
            changes.AddRange(ResourceChanges(target, target.VitalResourceId, absorbedDelta));
            evidence.Add(Evidence(
                context,
                target,
                hit,
                affinity,
                target,
                target.VitalResourceId,
                absorbedDelta));
        }

        return Interrupted(
            context,
            TurnEconomyOutcome.Absorb,
            totalAbsorbed,
            "The damage was absorbed.",
            resolvedAffinity: affinity,
            resourceChanges: Array.AsReadOnly(changes.ToArray()),
            damageHits: Array.AsReadOnly(evidence.ToArray()));
    }

    private static decimal ResolveHitDamage(
        EffectExecutionContext context,
        RuntimeActorState target,
        DamageHitResolution hit,
        RuleModifierContext attackModifierContext,
        RuleModifierContext defenseModifierContext)
    {
        decimal amount = Math.Max(0m, hit.Damage);
        amount = Math.Max(0m, context.Services.RuleModifiers.ResolveNumeric(
            context.Actor,
            NumericRuleModifierType.DamageDealt,
            amount,
            attackModifierContext));
        return Math.Max(0m, context.Services.RuleModifiers.ResolveNumeric(
            target,
            NumericRuleModifierType.DamageTaken,
            amount,
            defenseModifierContext));
    }

    private static DamageHitExecutionEvidence Evidence(
        EffectExecutionContext context,
        RuntimeActorState target,
        DamageHitResolution hit,
        ElementalAffinity affinity,
        RuntimeActorState? affectedActor = null,
        ContentId? affectedResourceId = null,
        decimal appliedResourceDelta = 0m)
    {
        var definition = (DamageEffectDefinition)context.Effect;
        EffectLocalId? contactSourceEffectId = null;
        int? contactSourceEffectIndex = null;
        if (definition.ContactMode == DamageContactMode.SharedContact)
        {
            EffectDependencyEvaluation dependency = context.DependencyEvaluation ??
                throw new InvalidOperationException(
                    "Shared-contact damage executed without dependency evidence.");
            contactSourceEffectId = dependency.SourceEffectId;
            contactSourceEffectIndex = dependency.SourceEffectIndex;
        }

        return new DamageHitExecutionEvidence(
            context.Request.SourceId,
            context.Actor.InstanceId,
            target.InstanceId,
            context.EffectIndex,
            hit,
            affinity,
            affectedActor?.InstanceId,
            affectedResourceId,
            appliedResourceDelta,
            definition.ContactMode,
            contactSourceEffectId,
            contactSourceEffectIndex);
    }

    private static IReadOnlyList<ExecutionResourceChange> ApplyDrain(
        DamageDrainMode drain,
        RuntimeActorState actor,
        BattleExecutionServices services,
        decimal amount)
    {
        if (drain == DamageDrainMode.Hp && actor.TryGetResource(services.HpResourceId, out _))
        {
            decimal delta = actor.AddResource(services.HpResourceId, amount);
            return ResourceChanges(actor, services.HpResourceId, delta);
        }

        if (drain == DamageDrainMode.Sp && actor.TryGetResource(services.SpResourceId, out _))
        {
            decimal delta = actor.AddResource(services.SpResourceId, amount);
            return ResourceChanges(actor, services.SpResourceId, delta);
        }

        return [];
    }
}

internal sealed class InstantKillEffectExecutor : TargetedEffectExecutor, IEffectExecutor<InstantKillEffectDefinition>
{
    public EffectExecutionResult Execute(InstantKillEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        InstantDeathResistanceResolution resistance = InstantDeathResistanceResolver.Resolve(
            target.DefenseProfile,
            definition.ResistanceCheck);
        var request = new InstantDeathPolicyRequest(context.Actor, target, definition, resistance);
        InstantDeathExecutionResolution? typedResolution =
            (context.Services.InstantDeathPolicy as ITypedInstantDeathExecutionPolicy)?.Resolve(request);
        bool success = typedResolution?.Defeated ?? context.Services.InstantDeathPolicy.ShouldDefeat(request);
        if (!success)
        {
            return Failure(context, TurnEconomyOutcome.Normal, "The instant-death attempt had no effect.") with
            {
                KnowledgeObservations =
                [
                    InstantDeathObservation(
                        context,
                        target,
                        resistance,
                        defeated: false,
                        typedResolution?.Reason == InstantDefeatResolutionReason.ResistanceBlocked)
                ]
            };
        }

        decimal resourceDelta = target.SetResource(target.VitalResourceId, 0);
        decimal removed = -resourceDelta;
        IReadOnlyList<PassiveTriggerExecutionResult> activations = DispatchDefeatPrevention(context, target);
        ExecutionResourceChange[] resourceChanges = ResourceChanges(
                target,
                target.VitalResourceId,
                resourceDelta)
            .Concat(PassiveResourceChanges(activations))
            .ToArray();
        return Success(
            context,
            removed,
            passiveActivations: activations,
            resourceChanges: resourceChanges) with
        {
            KnowledgeObservations = [InstantDeathObservation(context, target, resistance, defeated: true, resistanceBlocked: false)]
        };
    }

    private static BattleKnowledgeObservation InstantDeathObservation(
        EffectExecutionContext context,
        RuntimeActorState target,
        InstantDeathResistanceResolution resistance,
        bool defeated,
        bool resistanceBlocked) =>
        BattleKnowledgeObservation.InstantDeath(
            context.Request.SourceId,
            context.Actor.InstanceId,
            target.InstanceId,
            target.CombatProfileIdentity,
            context.EffectIndex,
            resistance.Channel,
            resistance.BypassesResistance,
            defeated,
            resistance.Resistance,
            resistance.Resistance,
            resistanceBlocked);
}

internal sealed class ApplyAilmentEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ApplyAilmentEffectDefinition>
{
    public EffectExecutionResult Execute(ApplyAilmentEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        if (target.IsDefeated || !context.Services.Ailments.TryGetAilment(definition.AilmentId, out AilmentDefinition? ailment) || ailment is null)
        {
            return Failure(context, detail: "The ailment target or definition is unavailable.", relatedId: definition.AilmentId);
        }

        ResistanceLevel authoredResistance = target.DefenseProfile.GetAilmentResistance(definition.AilmentId);
        var resistanceConditionContext = new BattleConditionContext(
            target,
            context.Actor,
            context.Request.Participants,
            context.Request.BattleKindId,
            context.Request.MoonPhaseId,
            context.Services);
        ResistanceLevel effectiveResistance = context.Services.RuleModifiers.ResolveAilmentResistance(
            target,
            definition.AilmentId,
            authoredResistance,
            new RuleModifierContext(resistanceConditionContext, context.Request.Skill));
        BattleAilmentApplicationResult application = BattleAilmentApplicationTransaction.Execute(
            new BattleAilmentApplicationRequest(
                context.Actor,
                target,
                ailment,
                definition.Chance,
                definition.Lifetime ?? ailment.DefaultLifetime,
                participants: context.Request.Participants,
                battleKindId: context.Request.BattleKindId,
                moonPhaseId: context.Request.MoonPhaseId,
                skill: context.Request.Skill)
            {
                SourceId = context.Request.SourceId
            },
            context.Services);
        if (!application.Applied)
        {
            return Failure(
                context,
                TurnEconomyOutcome.Normal,
                detail: $"The ailment application was {application.Status}.",
                relatedId: definition.AilmentId,
                lifecycleEvents: application.Events) with
            {
                KnowledgeObservations =
                [AilmentObservation(
                    context,
                    target,
                    definition.AilmentId,
                    application.Status,
                    authoredResistance,
                    effectiveResistance)]
            };
        }

        return Success(
            context,
            relatedId: definition.AilmentId,
            lifecycleEvents: application.Events) with
        {
            KnowledgeObservations =
            [AilmentObservation(
                context,
                target,
                definition.AilmentId,
                application.Status,
                authoredResistance,
                effectiveResistance)]
        };
    }

    private static BattleKnowledgeObservation AilmentObservation(
        EffectExecutionContext context,
        RuntimeActorState target,
        ContentId ailmentId,
        BattleAilmentApplicationStatus status,
        ResistanceLevel authoredResistance,
        ResistanceLevel effectiveResistance)
    {
        BattleDefenseInfluence influences = BattleDefenseInfluence.None;
        ResistanceLevel? observedResistance = effectiveResistance;
        if (status == BattleAilmentApplicationStatus.GuardBlocked)
        {
            influences |= BattleDefenseInfluence.Guard;
            observedResistance = null;
        }
        if (effectiveResistance != authoredResistance)
        {
            influences |= BattleDefenseInfluence.PassiveModifier;
        }

        return BattleKnowledgeObservation.Ailment(
            context.Request.SourceId,
            context.Actor.InstanceId,
            target.InstanceId,
            target.CombatProfileIdentity,
            context.EffectIndex,
            ailmentId,
            status,
            authoredResistance,
            observedResistance,
            influences);
    }
}

internal sealed class RestoreResourceEffectExecutor : TargetedEffectExecutor, IEffectExecutor<RestoreResourceEffectDefinition>
{
    public EffectExecutionResult Execute(RestoreResourceEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        if (!target.TryGetResource(definition.ResourceId, out _))
        {
            return Failure(context, detail: "The target does not expose the requested resource.", relatedId: definition.ResourceId);
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "restore_resource"),
            context.Services);
        var conditionContext = new BattleConditionContext(
            context.Actor,
            target,
            context.Request.Participants,
            context.Request.BattleKindId,
            context.Request.MoonPhaseId,
            context.Services,
            context.EffectElement is DamageElement element ? [element] : []);
        amount = Math.Max(0, context.Services.RuleModifiers.ResolveNumeric(
            context.Actor,
            NumericRuleModifierType.HealingGiven,
            amount,
            new RuleModifierContext(conditionContext, context.Request.Skill, definition.ResourceId)));
        amount = Math.Max(0, context.Services.RuleModifiers.ResolveNumeric(
            target,
            NumericRuleModifierType.HealingReceived,
            amount,
            new RuleModifierContext(conditionContext, context.Request.Skill, definition.ResourceId)));
        decimal restored = target.AddResource(definition.ResourceId, amount);
        return Success(
            context,
            restored,
            definition.ResourceId,
            resourceChanges: ResourceChanges(target, definition.ResourceId, restored));
    }
}

internal sealed class RemoveAilmentEffectExecutor : TargetedEffectExecutor, IEffectExecutor<RemoveAilmentEffectDefinition>
{
    public EffectExecutionResult Execute(RemoveAilmentEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        HashSet<ContentId> ailmentIds = new(definition.AilmentIds);
        HashSet<ContentId> groupIds = new(definition.AilmentGroupIds);
        IReadOnlyList<ContentId> removed = target.RemoveAilments(
            StatusRemovalCause.CureEffect,
            active =>
                definition.Scope == AilmentRemovalScope.AllRemovable ||
                ailmentIds.Contains(active.Definition.Id) ||
                active.Definition.GroupIds.Any(groupIds.Contains));
        BattleStatusLifecycleEvent[] events = removed
            .Select(ailmentId => new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.AilmentRemoved,
                target.InstanceId,
                ailmentId,
                Detail: StatusRemovalCause.CureEffect.ToString())
            {
                SourceActorId = context.Actor.InstanceId,
                SourceId = context.Request.SourceId,
                RemovalTransition = new BattleStatusRemovalResult(
                    ailmentId,
                    BattleDurationStateKind.Ailment,
                    StatusRemovalCause.CureEffect)
            })
            .ToArray();
        return Success(
            context,
            removed.Count,
            detail: string.Join(",", removed),
            lifecycleEvents: events);
    }
}

internal sealed class ReviveEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ReviveEffectDefinition>
{
    public EffectExecutionResult Execute(ReviveEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        if (!target.IsDefeated || definition.ResourceId != target.VitalResourceId || !target.TryGetResource(definition.ResourceId, out _))
        {
            return Failure(context, detail: "Revival requires a defeated target and its vital resource.");
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "revive"),
            context.Services);
        var conditionContext = new BattleConditionContext(
            context.Actor,
            target,
            context.Request.Participants,
            context.Request.BattleKindId,
            context.Request.MoonPhaseId,
            context.Services);
        amount = Math.Max(0, context.Services.RuleModifiers.ResolveNumeric(
            context.Actor,
            NumericRuleModifierType.HealingGiven,
            amount,
            new RuleModifierContext(conditionContext, context.Request.Skill, definition.ResourceId)));
        amount = Math.Max(0, context.Services.RuleModifiers.ResolveNumeric(
            target,
            NumericRuleModifierType.HealingReceived,
            amount,
            new RuleModifierContext(conditionContext, context.Request.Skill, definition.ResourceId)));
        decimal restored = target.SetResource(definition.ResourceId, amount);
        return target.IsDefeated
            ? Failure(context, detail: "The revival amount did not restore the target.")
            : Success(
                context,
                restored,
                definition.ResourceId,
                resourceChanges: ResourceChanges(target, definition.ResourceId, restored));
    }
}

internal sealed class ModifyStatStageEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ModifyStatStageEffectDefinition>
{
    public EffectExecutionResult Execute(ModifyStatStageEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        StatModifierApplicationEvaluation evaluation = StatModifierExecution.Apply(
            target,
            definition,
            context.Request.Environment,
            context.Services.StatModifiers);
        if (!evaluation.Accepted)
        {
            return Failure(
                context,
                detail: evaluation.RejectionDetail,
                statModifierTransitions: evaluation.Transitions);
        }

        if (!evaluation.StateChanged)
        {
            return Failure(
                context,
                detail: "The selected stat-modifier policy produced no state change.",
                statModifierTransitions: evaluation.Transitions);
        }

        return Success(
            context,
            evaluation.AggregateStageDelta,
            detail: string.Join(",", definition.ModifierTrackIds),
            statModifierTransitions: evaluation.Transitions);
    }
}

internal sealed class GrantChargeEffectExecutor : TargetedEffectExecutor, IEffectExecutor<GrantChargeEffectDefinition>
{
    public EffectExecutionResult Execute(GrantChargeEffectDefinition definition, EffectExecutionContext context)
    {
        ChargeApplicationResult result = context.Services.Charges.Apply(new ChargeApplicationRequest(
            Target(context),
            definition.Charge,
            definition.Multiplier,
            definition.Lifetime ?? StandardStatusLifetimes.DeploymentTransient));
        return result.Applied
            ? Success(context, definition.Multiplier, detail: definition.Charge.ToString())
            : Failure(
                context,
                TurnEconomyOutcome.Normal,
                string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }
}

internal sealed class GrantShieldEffectExecutor : TargetedEffectExecutor, IEffectExecutor<GrantShieldEffectDefinition>
{
    public EffectExecutionResult Execute(GrantShieldEffectDefinition definition, EffectExecutionContext context)
    {
        Target(context).GrantShield(
            definition.Shield,
            definition.Lifetime ?? StandardStatusLifetimes.DeploymentTransient);
        return Success(context, detail: definition.Shield.ToString());
    }
}

internal sealed class BreakAffinityEffectExecutor : TargetedEffectExecutor, IEffectExecutor<BreakAffinityEffectDefinition>
{
    public EffectExecutionResult Execute(BreakAffinityEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        foreach (DamageElement element in definition.Elements)
        {
            target.BreakAffinity(element, definition.Lifetime);
        }

        return Success(context, detail: string.Join(",", definition.Elements));
    }
}

internal sealed class OverrideAffinityEffectExecutor : TargetedEffectExecutor, IEffectExecutor<OverrideAffinityEffectDefinition>
{
    public EffectExecutionResult Execute(OverrideAffinityEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        foreach (DamageElement element in definition.Elements)
        {
            target.OverrideAffinity(element, definition.Affinity, definition.Lifetime);
        }
        return Success(context, detail: definition.Affinity.ToString());
    }
}

internal sealed class RemoveStatusEffectExecutor : TargetedEffectExecutor, IEffectExecutor<RemoveStatusEffectDefinition>
{
    public EffectExecutionResult Execute(RemoveStatusEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        HashSet<StatusEffectKind> kinds = definition.StatusKinds.ToHashSet();
        StatModifierTransitionResult? modifierTransition = StatModifierExecution.Remove(
            target,
            kinds,
            context.Services.StatModifiers);
        if (modifierTransition is { Accepted: false })
        {
            return Failure(
                context,
                detail: string.Join("; ", modifierTransition.Diagnostics.Select(value => value.Message)),
                statModifierTransitions: [modifierTransition]);
        }

        IReadOnlyList<BattleStatusRemovalResult> removals = target.RemoveNonModifierStatuses(
            kinds,
            definition.StatusIds,
            StatusRemovalCause.DispelEffect);
        BattleStatusLifecycleEvent[] lifecycleEvents = removals
            .Select(removal => new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.StatusRemoved,
                target.InstanceId,
                removal.Id,
                Detail: removal.Cause.ToString())
            {
                SourceActorId = context.Actor.InstanceId,
                SourceId = context.Request.SourceId,
                RemovalTransition = removal
            })
            .ToArray();
        int modifierChanges = modifierTransition?.Events.Count(@event =>
            @event.Kind is StatModifierEventKind.ContributionRemoved or StatModifierEventKind.TrackRemoved) ?? 0;
        int total = removals.Count + modifierChanges;
        return total == 0 && modifierTransition?.StateChanged != true
            ? Failure(
                context,
                detail: "No matching removable status was active.",
                statModifierTransitions: modifierTransition is null ? [] : [modifierTransition])
            : Success(
                context,
                total,
                statModifierTransitions: modifierTransition is null ? [] : [modifierTransition],
                lifecycleEvents: lifecycleEvents);
    }
}

internal sealed class ReduceResourceEffectExecutor : TargetedEffectExecutor, IEffectExecutor<ReduceResourceEffectDefinition>
{
    public EffectExecutionResult Execute(ReduceResourceEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        if (!target.TryGetResource(definition.ResourceId, out BattleResourceState? resource) || resource is null)
        {
            return Failure(context, detail: "The target does not expose the requested resource.", relatedId: definition.ResourceId);
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "reduce_resource"),
            context.Services);
        decimal floor = definition.CanReduceToZero ? 0 : Math.Min(1, resource.Maximum);
        decimal resourceDelta = target.SetResource(
            definition.ResourceId,
            Math.Max(floor, resource.Current - amount));
        decimal reduced = -resourceDelta;
        return Success(
            context,
            reduced,
            definition.ResourceId,
            resourceChanges: ResourceChanges(target, definition.ResourceId, resourceDelta));
    }
}

internal sealed class SetResourceEffectExecutor : TargetedEffectExecutor, IEffectExecutor<SetResourceEffectDefinition>
{
    public EffectExecutionResult Execute(SetResourceEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        if (!target.TryGetResource(definition.ResourceId, out _))
        {
            return Failure(context, detail: "The target does not expose the requested resource.", relatedId: definition.ResourceId);
        }

        decimal amount = BattleAmountResolver.Resolve(
            definition.Amount,
            new AmountResolutionContext(context.Actor, target, definition.ResourceId, "set_resource"),
            context.Services);
        decimal changed = target.SetResource(definition.ResourceId, amount);
        return Success(
            context,
            changed,
            definition.ResourceId,
            resourceChanges: ResourceChanges(target, definition.ResourceId, changed));
    }
}

internal sealed class AnalyzeEffectExecutor : TargetedEffectExecutor, IEffectExecutor<AnalyzeEffectDefinition>
{
    public EffectExecutionResult Execute(AnalyzeEffectDefinition definition, EffectExecutionContext context)
    {
        RuntimeActorState target = Target(context);
        BattleAnalysisResult analysis = context.Services.BattleAnalysis.Analyze(
            new BattleAnalysisRequest(
                context.Actor,
                target,
                definition.Layers,
                context.Services.SpResourceId));
        return Success(context, detail: string.Join(",", definition.Layers), analysis: analysis);
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
