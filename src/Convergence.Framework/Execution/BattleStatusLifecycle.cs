using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Execution;

public enum BattleTurnStartOutcome
{
    CanAct,
    Skip,
    LimitedAction,
    ForcedBasicAttack,
    ForcedConfusion,
    FleeBattle,
    RecallToRoster
}

public enum BattleAilmentApplicationStatus
{
    Applied = 0,
    TargetDefeated = 1,
    GuardBlocked = 2,
    Immune = 3,
    Missed = 4,
    TransitionRejected = 5,
    ApplicationGateRejected = 6
}

public enum BattleStatusDepartureReason
{
    DeploymentSwap,
    Defeat,
    Flee,
    RosterRecall,
    BattleEnd,
    FieldTransition
}

public enum BattleStatusLifecycleEventKind
{
    GuardCleared,
    TurnRestricted,
    AilmentApplied,
    AilmentBlocked,
    AilmentMissed,
    AilmentRemoved,
    AilmentRecovered,
    AilmentExpired,
    ResourceChanged,
    StatusExpired,
    StatStageChanged,
    PassiveTriggered,
    CleanupApplied,
    StatModifierChanged,
    AilmentRefreshed,
    AilmentReplaced,
    DurationAdvanced,
    StatusRemoved,
    PassiveEvaluated,
    PassiveEffectResolved
}

public sealed record BattleStatusLifecycleEvent(
    BattleStatusLifecycleEventKind Kind,
    RuntimeInstanceId ActorId,
    ContentId? RelatedId = null,
    decimal? Value = null,
    string? Detail = null,
    StatModifierEvent? ModifierEvent = null)
{
    public RuntimeInstanceId? SourceActorId { get; init; }
    public ContentId? SourceId { get; init; }
    public BattleAilmentApplicationGateDecision? AilmentGateDecision { get; init; }
    public BattleAilmentTransitionResult? AilmentTransition { get; init; }
    public BattleDurationTickResult? DurationTransition { get; init; }
    public BattleStatusRemovalResult? RemovalTransition { get; init; }
    public PassiveTriggerExecutionResult? PassiveActivation { get; init; }
    public EffectExecutionResult? EffectResult { get; init; }
    public BattleStatusDepartureReason? DepartureReason { get; init; }
}

public sealed record BattleTurnStartLifecycleRequest(
    RuntimeActorState Actor,
    bool CanRecallToRoster = false);

public sealed record BattleTurnStartRestriction
{
    public BattleTurnStartRestriction(
        BattleTurnStartOutcome outcome,
        IEnumerable<ContentId>? allowedActionIds = null,
        IEnumerable<ContentId>? sourceAilmentIds = null)
    {
        Outcome = EnumDomain.RequireDefined(outcome, nameof(outcome));
        AllowedActionIds = SnapshotIds(allowedActionIds, nameof(allowedActionIds));
        SourceAilmentIds = SnapshotIds(sourceAilmentIds, nameof(sourceAilmentIds));

        if (outcome == BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count == 0)
        {
            throw new ArgumentException(
                "A limited-action restriction must contain at least one allowed action.",
                nameof(allowedActionIds));
        }

        if (outcome != BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count > 0)
        {
            throw new ArgumentException(
                "Allowed action IDs are valid only for a limited-action restriction.",
                nameof(allowedActionIds));
        }
    }

    public BattleTurnStartOutcome Outcome { get; }
    public IReadOnlyList<ContentId> AllowedActionIds { get; }
    public IReadOnlyList<ContentId> SourceAilmentIds { get; }

    public static BattleTurnStartRestriction CanAct { get; } =
        new(BattleTurnStartOutcome.CanAct);

    private static IReadOnlyList<ContentId> SnapshotIds(
        IEnumerable<ContentId>? ids,
        string parameterName)
    {
        ContentId[] snapshot = (ids ?? []).Distinct().ToArray();
        if (snapshot.Any(id => !id.IsValid))
        {
            throw new ArgumentException(
                "Turn-start restriction IDs must be valid.",
                parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record BattleTurnStartLifecycleResult
{
    public BattleTurnStartLifecycleResult(
        BattleTurnStartOutcome outcome,
        IEnumerable<BattleStatusLifecycleEvent> events)
        : this(new BattleTurnStartRestriction(outcome), events)
    {
    }

    public BattleTurnStartLifecycleResult(
        BattleTurnStartRestriction restriction,
        IEnumerable<BattleStatusLifecycleEvent> events)
    {
        Restriction = restriction ?? throw new ArgumentNullException(nameof(restriction));
        Events = Array.AsReadOnly(events?.ToArray() ?? throw new ArgumentNullException(nameof(events)));
    }

    public BattleTurnStartRestriction Restriction { get; }
    public BattleTurnStartOutcome Outcome => Restriction.Outcome;
    public IReadOnlyList<ContentId> AllowedActionIds => Restriction.AllowedActionIds;
    public IReadOnlyList<BattleStatusLifecycleEvent> Events { get; }
}

public sealed record CustomAilmentTurnBehaviorRequest(
    RuntimeActorState Actor,
    AilmentDefinition Ailment,
    bool CanRecallToRoster);

public sealed record CustomAilmentTurnBehaviorResult
{
    public CustomAilmentTurnBehaviorResult(
        BattleTurnStartOutcome outcome,
        IEnumerable<ContentId>? allowedActionIds = null)
    {
        Outcome = EnumDomain.RequireDefined(outcome, nameof(outcome));
        ContentId[] actionIds = (allowedActionIds ?? []).Distinct().ToArray();
        if (actionIds.Any(id => !id.IsValid))
        {
            throw new ArgumentException(
                "Custom allowed action IDs must be valid.",
                nameof(allowedActionIds));
        }

        AllowedActionIds = Array.AsReadOnly(actionIds);

        if (outcome == BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count == 0)
        {
            throw new ArgumentException(
                "A custom limited-action result must contain at least one allowed action.",
                nameof(allowedActionIds));
        }

        if (outcome != BattleTurnStartOutcome.LimitedAction && AllowedActionIds.Count > 0)
        {
            throw new ArgumentException(
                "Custom allowed action IDs are valid only for a limited-action result.",
                nameof(allowedActionIds));
        }
    }

    public BattleTurnStartOutcome Outcome { get; }
    public IReadOnlyList<ContentId> AllowedActionIds { get; }
}

public interface ICustomAilmentTurnBehaviorHandler
{
    CustomAilmentTurnBehaviorResult Resolve(
        CustomAilmentTurnBehaviorDefinition behavior,
        CustomAilmentTurnBehaviorRequest request);
}

public interface IBattleTurnRestrictionPolicy
{
    BattleTurnStartRestriction Resolve(IReadOnlyList<BattleTurnStartRestriction> restrictions);
}

public sealed class MostRestrictiveBattleTurnPolicy : IBattleTurnRestrictionPolicy
{
    public BattleTurnStartRestriction Resolve(IReadOnlyList<BattleTurnStartRestriction> restrictions)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        BattleTurnStartRestriction[] effective = restrictions
            .Where(restriction => restriction.Outcome != BattleTurnStartOutcome.CanAct)
            .ToArray();
        if (effective.Length == 0)
        {
            return BattleTurnStartRestriction.CanAct;
        }

        int highestPrecedence = effective.Max(restriction => Precedence(restriction.Outcome));
        BattleTurnStartRestriction[] strongest = effective
            .Where(restriction => Precedence(restriction.Outcome) == highestPrecedence)
            .OrderBy(SourceKey, StringComparer.Ordinal)
            .ToArray();

        if (strongest[0].Outcome != BattleTurnStartOutcome.LimitedAction)
        {
            return strongest[0];
        }

        ContentId[] allowed = strongest[0].AllowedActionIds
            .Where(actionId => strongest.Skip(1).All(restriction => restriction.AllowedActionIds.Contains(actionId)))
            .ToArray();
        ContentId[] sources = strongest.SelectMany(restriction => restriction.SourceAilmentIds).Distinct().ToArray();
        return allowed.Length == 0
            ? new BattleTurnStartRestriction(BattleTurnStartOutcome.Skip, sourceAilmentIds: sources)
            : new BattleTurnStartRestriction(BattleTurnStartOutcome.LimitedAction, allowed, sources);
    }

    private static string SourceKey(BattleTurnStartRestriction restriction) =>
        restriction.SourceAilmentIds.Count == 0
            ? string.Empty
            : restriction.SourceAilmentIds[0].ToString();

    private static int Precedence(BattleTurnStartOutcome outcome) => outcome switch
    {
        BattleTurnStartOutcome.RecallToRoster => 6,
        BattleTurnStartOutcome.FleeBattle => 6,
        BattleTurnStartOutcome.Skip => 5,
        BattleTurnStartOutcome.ForcedConfusion => 4,
        BattleTurnStartOutcome.ForcedBasicAttack => 3,
        BattleTurnStartOutcome.LimitedAction => 2,
        BattleTurnStartOutcome.CanAct => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}

public sealed record BattleTurnEndLifecycleRequest
{
    public BattleTurnEndLifecycleRequest(
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        ContentId contextId,
        ContentId eventId,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null,
        StatModifierLifecycleBoundary? statModifierBoundary = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        ContextId = contextId;
        EventId = eventId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        StatModifierBoundary = statModifierBoundary;
        if (statModifierBoundary is not null && statModifierBoundary.EventId != eventId)
        {
            throw new ArgumentException(
                "The stat-modifier boundary must match the turn-end event ID.",
                nameof(statModifierBoundary));
        }
    }

    public RuntimeActorState Actor { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId ContextId { get; }
    public ContentId EventId { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public StatModifierLifecycleBoundary? StatModifierBoundary { get; }
}

public sealed record BattleTurnEndLifecycleResult
{
    private readonly IReadOnlyList<BattleStatusLifecycleEvent> _events =
        Array.Empty<BattleStatusLifecycleEvent>();
    private readonly IReadOnlyList<PassiveTriggerExecutionResult> _passiveActivations =
        Array.Empty<PassiveTriggerExecutionResult>();

    public BattleTurnEndLifecycleResult(
        IReadOnlyList<BattleStatusLifecycleEvent> Events,
        IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations)
    {
        this.Events = Events;
        this.PassiveActivations = PassiveActivations;
    }

    public IReadOnlyList<BattleStatusLifecycleEvent> Events
    {
        get => _events;
        init => _events = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations
    {
        get => _passiveActivations;
        init => _passiveActivations = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public void Deconstruct(
        out IReadOnlyList<BattleStatusLifecycleEvent> Events,
        out IReadOnlyList<PassiveTriggerExecutionResult> PassiveActivations)
    {
        Events = this.Events;
        PassiveActivations = this.PassiveActivations;
    }
}

public sealed record BattleAilmentApplicationRequest
{
    public BattleAilmentApplicationRequest(
        RuntimeActorState actor,
        RuntimeActorState target,
        AilmentDefinition ailment,
        int chance,
        StatusLifetimeDefinition? lifetime = null,
        IEnumerable<RuntimeActorState>? participants = null,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null,
        SkillDefinition? skill = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Ailment = ailment ?? throw new ArgumentNullException(nameof(ailment));
        AuthoredPercentage.RequireValid(
            chance,
            nameof(chance),
            "Authored ailment chance");
        Chance = chance;
        Lifetime = lifetime;
        RuntimeActorState[] participantSnapshot = (participants ?? [actor, target]).ToArray();
        if (participantSnapshot.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Ailment application participants cannot contain null actors.",
                nameof(participants));
        }

        Participants = Array.AsReadOnly(participantSnapshot
            .Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance)
            .ToArray());
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        Skill = skill;
        SourceId = skill?.Id;
    }

    public RuntimeActorState Actor { get; }
    public RuntimeActorState Target { get; }
    public AilmentDefinition Ailment { get; }
    public int Chance { get; }
    public StatusLifetimeDefinition? Lifetime { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId? BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public SkillDefinition? Skill { get; }
    private ContentId? _sourceId;
    public ContentId? SourceId
    {
        get => _sourceId;
        init
        {
            if (value is ContentId sourceId && !sourceId.IsValid)
            {
                throw new ArgumentException("Source ID must be valid when supplied.", nameof(value));
            }

            _sourceId = value;
        }
    }
}

public sealed record BattleAilmentApplicationResult
{
    private readonly IReadOnlyList<BattleStatusLifecycleEvent> _events =
        Array.Empty<BattleStatusLifecycleEvent>();

    public BattleAilmentApplicationResult(
        BattleAilmentApplicationStatus Status,
        IReadOnlyList<BattleStatusLifecycleEvent> Events)
        : this(Status, Events, transition: null)
    {
    }

    public BattleAilmentApplicationResult(
        BattleAilmentApplicationStatus Status,
        IReadOnlyList<BattleStatusLifecycleEvent> Events,
        BattleAilmentTransitionResult? transition)
    {
        this.Status = Status;
        this.Events = Events;
        Transition = transition;
    }

    public BattleAilmentApplicationStatus Status { get; init; }
    public IReadOnlyList<BattleStatusLifecycleEvent> Events
    {
        get => _events;
        init => _events = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public bool Applied => Status == BattleAilmentApplicationStatus.Applied;
    public BattleAilmentTransitionResult? Transition { get; }
    public BattleAilmentApplicationGateDecision? GateDecision { get; init; }

    public void Deconstruct(
        out BattleAilmentApplicationStatus Status,
        out IReadOnlyList<BattleStatusLifecycleEvent> Events)
    {
        Status = this.Status;
        Events = this.Events;
    }
}

public interface IBattleAilmentApplicationService
{
    BattleAilmentApplicationResult Apply(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services);
}

public sealed class BattleAilmentApplicationService : IBattleAilmentApplicationService
{
    public BattleAilmentApplicationResult Apply(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services) =>
        BattleAilmentApplicationTransaction.Execute(
            request,
            services,
            ApplyStaged);

    internal BattleAilmentApplicationResult ApplyStaged(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        RuntimeActorState target = request.Target;
        AilmentDefinition ailment = request.Ailment;
        StatusLifetimeDefinition lifetime = request.Lifetime ?? ailment.DefaultLifetime;
        RuntimeStatusLifetimeDomain.RequireValid(lifetime, nameof(request));
        if (target.IsDefeated)
        {
            return Blocked(request, BattleAilmentApplicationStatus.TargetDefeated, "target_defeated");
        }

        BattleAilmentApplicationGateDecision? gateDecision = services.AilmentApplicationGate.Evaluate(
            new BattleAilmentApplicationGateRequest(
                request.Actor,
                target,
                ailment,
                request.Participants,
                request.SourceId));
        if (gateDecision is null)
        {
            throw new InvalidOperationException("The ailment application gate returned no decision.");
        }
        if (!gateDecision.Allowed)
        {
            BattleAilmentApplicationStatus status = gateDecision.Reason switch
            {
                BattleAilmentApplicationGateReason.Guarding => BattleAilmentApplicationStatus.GuardBlocked,
                BattleAilmentApplicationGateReason.PolicyRejected =>
                    BattleAilmentApplicationStatus.ApplicationGateRejected,
                _ => throw new InvalidOperationException(
                    "The ailment application gate returned an invalid blocked decision.")
            };
            return Blocked(request, status, gateDecision.Reason.ToString(), gateDecision);
        }

        ResistanceLevel resistance = AilmentResistanceResolver.Resolve(target.DefenseProfile, ailment.Id);
        var conditionContext = new BattleConditionContext(
            target,
            request.Actor,
            request.Participants,
            request.BattleKindId,
            request.MoonPhaseId,
            services);
        resistance = services.RuleModifiers.ResolveAilmentResistance(
            target,
            ailment.Id,
            resistance,
            new RuleModifierContext(conditionContext, request.Skill));
        if (resistance == ResistanceLevel.Immune)
        {
            return Blocked(request, BattleAilmentApplicationStatus.Immune, "immune");
        }

        int chance = request.Chance;
        if (!services.AilmentPolicy.ShouldApply(
                new AilmentApplicationPolicyRequest(
                    request.Actor,
                    target,
                    chance,
                    ailment,
                    resistance)))
        {
            return new BattleAilmentApplicationResult(
                BattleAilmentApplicationStatus.Missed,
                [
                    new BattleStatusLifecycleEvent(
                        BattleStatusLifecycleEventKind.AilmentMissed,
                        target.InstanceId,
                        ailment.Id,
                        Detail: chance.ToString())
                    {
                        SourceActorId = request.Actor.InstanceId,
                        SourceId = request.SourceId
                    }
                ]);
        }

        BattleAilmentTransitionResult transition = ResolveTransition(
            target,
            ailment,
            lifetime,
            services.AilmentTransitions);
        if (!transition.Applied)
        {
            return new BattleAilmentApplicationResult(
                BattleAilmentApplicationStatus.TransitionRejected,
                [new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentBlocked,
                    target.InstanceId,
                    ailment.Id,
                    Detail: transition.RejectionReason.ToString())
                {
                    SourceActorId = request.Actor.InstanceId,
                    SourceId = request.SourceId,
                    AilmentTransition = transition
                }],
                transition);
        }

        return new BattleAilmentApplicationResult(
            BattleAilmentApplicationStatus.Applied,
            TransitionEvents(request, transition),
            transition);
    }

    private static BattleAilmentTransitionResult ResolveTransition(
        RuntimeActorState target,
        AilmentDefinition ailment,
        StatusLifetimeDefinition lifetime,
        IBattleAilmentTransitionPolicy policy)
    {
        target.Ailments.TryGetValue(ailment.Id, out ActiveAilmentState? existingSame);
        ActiveAilmentState[] exclusiveConflicts = ailment.ExclusivityGroupId is ContentId groupId
            ? target.Ailments.Values
                .Where(active => active.Definition.Id != ailment.Id &&
                                 active.Definition.ExclusivityGroupId == groupId)
                .OrderBy(active => active.Definition.Id.ToString(), StringComparer.Ordinal)
                .ToArray()
            : [];
        var policyRequest = new BattleAilmentTransitionPolicyRequest(
            ailment.Id,
            lifetime,
            existingSame is null
                ? null
                : new BattleAilmentStateSnapshot(existingSame.Definition.Id, existingSame.Lifetime),
            exclusiveConflicts.Select(active =>
                new BattleAilmentStateSnapshot(active.Definition.Id, active.Lifetime)));
        BattleAilmentTransitionDecision? decision = policy.Resolve(policyRequest);
        if (decision is null || !DecisionMatchesState(decision.Operation, existingSame, exclusiveConflicts))
        {
            return RejectedTransition(
                ailment.Id,
                BattleAilmentTransitionRejectionReason.InvalidPolicyDecision);
        }
        if (decision.Operation == BattleAilmentTransitionOperation.Reject)
        {
            return RejectedTransition(ailment.Id, decision.RejectionReason);
        }
        if (decision.Operation == BattleAilmentTransitionOperation.ReplaceExclusive &&
            exclusiveConflicts.Any(active =>
                !active.Lifetime.Allows(StatusRemovalCause.ExclusivityReplacement)))
        {
            return RejectedTransition(
                ailment.Id,
                BattleAilmentTransitionRejectionReason.ReplacementProtected);
        }

        var changes = new List<BattleAilmentStateChange>();
        if (decision.Operation == BattleAilmentTransitionOperation.ReplaceExclusive)
        {
            foreach (ActiveAilmentState conflict in exclusiveConflicts)
            {
                IReadOnlyList<ContentId> removed = target.RemoveAilments(
                    StatusRemovalCause.ExclusivityReplacement,
                    active => active.Definition.Id == conflict.Definition.Id);
                if (removed.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Exclusive ailment '{conflict.Definition.Id}' could not be removed after validation.");
                }

                changes.Add(new BattleAilmentStateChange(
                    BattleAilmentStateChangeKind.Removed,
                    conflict.Definition.Id,
                    conflict.Lifetime,
                    after: null,
                    removalCause: StatusRemovalCause.ExclusivityReplacement));
            }
        }

        if (existingSame is null)
        {
            target.ApplyAilment(ailment, lifetime);
            changes.Add(new BattleAilmentStateChange(
                BattleAilmentStateChangeKind.Added,
                ailment.Id,
                before: null,
                after: lifetime));
        }
        else
        {
            target.ApplyAilment(ailment, lifetime);
            changes.Add(new BattleAilmentStateChange(
                BattleAilmentStateChangeKind.Refreshed,
                ailment.Id,
                existingSame.Lifetime,
                lifetime));
        }

        BattleAilmentTransitionOutcome outcome = decision.Operation switch
        {
            BattleAilmentTransitionOperation.ApplyNew => BattleAilmentTransitionOutcome.Applied,
            BattleAilmentTransitionOperation.RefreshExisting => BattleAilmentTransitionOutcome.Refreshed,
            BattleAilmentTransitionOperation.ReplaceExclusive => BattleAilmentTransitionOutcome.Replaced,
            _ => throw new InvalidOperationException("Rejected ailment transitions cannot reach mutation.")
        };
        return new BattleAilmentTransitionResult(outcome, ailment.Id, changes);
    }

    private static bool DecisionMatchesState(
        BattleAilmentTransitionOperation operation,
        ActiveAilmentState? existingSame,
        IReadOnlyList<ActiveAilmentState> exclusiveConflicts) =>
        operation switch
        {
            BattleAilmentTransitionOperation.ApplyNew =>
                existingSame is null && exclusiveConflicts.Count == 0,
            BattleAilmentTransitionOperation.RefreshExisting =>
                existingSame is not null && exclusiveConflicts.Count == 0,
            BattleAilmentTransitionOperation.ReplaceExclusive => exclusiveConflicts.Count > 0,
            BattleAilmentTransitionOperation.Reject => true,
            _ => false
        };

    private static BattleAilmentTransitionResult RejectedTransition(
        ContentId ailmentId,
        BattleAilmentTransitionRejectionReason reason) =>
        new(BattleAilmentTransitionOutcome.Rejected, ailmentId, rejectionReason: reason);

    private static IReadOnlyList<BattleStatusLifecycleEvent> TransitionEvents(
        BattleAilmentApplicationRequest request,
        BattleAilmentTransitionResult transition)
    {
        var events = new List<BattleStatusLifecycleEvent>();
        foreach (BattleAilmentStateChange change in transition.StateChanges
                     .Where(change => change.Kind == BattleAilmentStateChangeKind.Removed))
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.AilmentRemoved,
                request.Target.InstanceId,
                change.AilmentId,
                Detail: StatusRemovalCause.ExclusivityReplacement.ToString())
            {
                SourceActorId = request.Actor.InstanceId,
                SourceId = request.SourceId,
                AilmentTransition = transition,
                RemovalTransition = new BattleStatusRemovalResult(
                    change.AilmentId,
                    BattleDurationStateKind.Ailment,
                    StatusRemovalCause.ExclusivityReplacement)
            });
        }

        BattleStatusLifecycleEventKind appliedKind = transition.Outcome switch
        {
            BattleAilmentTransitionOutcome.Applied => BattleStatusLifecycleEventKind.AilmentApplied,
            BattleAilmentTransitionOutcome.Refreshed => BattleStatusLifecycleEventKind.AilmentRefreshed,
            BattleAilmentTransitionOutcome.Replaced => BattleStatusLifecycleEventKind.AilmentReplaced,
            _ => throw new InvalidOperationException("A rejected transition has no application event.")
        };
        events.Add(new BattleStatusLifecycleEvent(
            appliedKind,
            request.Target.InstanceId,
            transition.AilmentId)
        {
            SourceActorId = request.Actor.InstanceId,
            SourceId = request.SourceId,
            AilmentTransition = transition
        });
        return Array.AsReadOnly(events.ToArray());
    }

    private static BattleAilmentApplicationResult Blocked(
        BattleAilmentApplicationRequest request,
        BattleAilmentApplicationStatus status,
        string detail,
        BattleAilmentApplicationGateDecision? gateDecision = null) =>
        new(
            status,
            [
                new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentBlocked,
                    request.Target.InstanceId,
                    request.Ailment.Id,
                    Detail: detail)
                {
                    SourceActorId = request.Actor.InstanceId,
                    SourceId = request.SourceId,
                    AilmentGateDecision = gateDecision
                }
            ])
        {
            GateDecision = gateDecision
        };
}

internal static class BattleAilmentApplicationTransaction
{
    public static BattleAilmentApplicationResult Execute(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AilmentApplications is BattleAilmentApplicationService standard
            ? Execute(request, services, standard.ApplyStaged)
            : Execute(request, services, services.AilmentApplications.Apply);
    }

    public static BattleAilmentApplicationResult Execute(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services,
        Func<BattleAilmentApplicationRequest, BattleExecutionServices, BattleAilmentApplicationResult> apply)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(apply);

        RuntimeActorState[] transactionParticipants = request.Participants
            .Append(request.Actor)
            .Append(request.Target)
            .ToArray();
        var transaction = new RuntimeActorExecutionTransaction(request.Actor, transactionParticipants);
        var stagedRequest = new BattleAilmentApplicationRequest(
            transaction.Actor,
            transaction.GetStaged(request.Target),
            request.Ailment,
            request.Chance,
            request.Lifetime,
            request.Participants.Select(transaction.GetStaged),
            request.BattleKindId,
            request.MoonPhaseId,
            request.Skill)
        {
            SourceId = request.SourceId
        };
        BattleAilmentApplicationResult result = apply(stagedRequest, services)
            ?? throw new InvalidOperationException("The ailment application service returned no result.");
        ValidateResult(stagedRequest, result);
        if (result.Applied)
        {
            transaction.Commit();
        }

        return result;
    }

    private static void ValidateResult(
        BattleAilmentApplicationRequest request,
        BattleAilmentApplicationResult result)
    {
        if (!Enum.IsDefined(result.Status))
        {
            throw new InvalidOperationException(
                $"The ailment application service returned undefined status '{result.Status}'.");
        }
        if (result.Events.Any(statusEvent => statusEvent is null))
        {
            throw new InvalidOperationException("The ailment application service returned a null event.");
        }
        if (result.Applied)
        {
            if (result.Transition is not { Applied: true } transition ||
                transition.AilmentId != request.Ailment.Id ||
                !request.Target.Ailments.ContainsKey(request.Ailment.Id))
            {
                throw new InvalidOperationException(
                    "An applied ailment result must contain a matching accepted transition and staged target state.");
            }
        }
        else if (result.Transition is { Applied: true })
        {
            throw new InvalidOperationException(
                "A rejected ailment result cannot contain an accepted transition.");
        }
    }
}

public sealed record BattleStatusCleanupRequest(
    RuntimeActorState Actor,
    BattleStatusDepartureReason Reason);

public sealed record BattleActionEndLifecycleRequest
{
    public BattleActionEndLifecycleRequest(
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<StatModifierLifecycleBoundary>? statModifierBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(participants);
        RuntimeActorState[] snapshot = participants.ToArray();
        if (snapshot.Any(actor => actor is null))
        {
            throw new ArgumentException("Duration lifecycle participants cannot contain null actors.", nameof(participants));
        }

        Participants = Array.AsReadOnly(
            snapshot.Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance).ToArray());
        StatModifierBoundaries = SnapshotBoundaries(statModifierBoundaries);
    }

    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public IReadOnlyList<StatModifierLifecycleBoundary> StatModifierBoundaries { get; }

    private static IReadOnlyList<StatModifierLifecycleBoundary> SnapshotBoundaries(
        IEnumerable<StatModifierLifecycleBoundary>? boundaries)
    {
        StatModifierLifecycleBoundary[] snapshot = (boundaries ?? []).ToArray();
        if (snapshot.Any(boundary => boundary is null) ||
            snapshot.Select(boundary => boundary.EventId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Stat-modifier action boundaries must be non-null and unique by event ID.",
                nameof(boundaries));
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record BattleStatusLifecycleResult
{
    public BattleStatusLifecycleResult(IEnumerable<BattleStatusLifecycleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Events = Array.AsReadOnly(events.ToArray());
    }

    public IReadOnlyList<BattleStatusLifecycleEvent> Events { get; }
}

public interface IBattleDurationLifecycleService
{
    BattleStatusLifecycleResult ProcessClock(
        BattleLifecycleClockRequest request,
        IStatModifierPolicyService statModifiers);

    BattleStatusLifecycleResult ProcessActionEnd(
        BattleActionEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers);

    BattleStatusLifecycleResult Cleanup(
        BattleStatusCleanupRequest request,
        IStatModifierPolicyService statModifiers);
}

public interface IBattleStatusLifecycleService : IBattleDurationLifecycleService
{
    BattleTurnStartLifecycleResult ProcessTurnStart(BattleTurnStartLifecycleRequest request);

    BattleTurnEndLifecycleResult ProcessTurnEnd(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services);

    BattleAilmentApplicationResult TryApplyAilment(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services);

    BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        BattleExecutionServices services,
        DurationDefinition? duration = null,
        StatModifierLifecycleBoundary? activeBoundary = null);

}

public sealed class BattleDurationLifecycleService : IBattleDurationLifecycleService
{
    private readonly IBattleReserveLifecyclePolicy _reserveLifecyclePolicy;

    public BattleDurationLifecycleService()
        : this(SuspendReserveLifecyclePolicy.Instance)
    {
    }

    public BattleDurationLifecycleService(
        IBattleReserveLifecyclePolicy reserveLifecyclePolicy)
    {
        _reserveLifecyclePolicy = reserveLifecyclePolicy ?? throw new ArgumentNullException(nameof(reserveLifecyclePolicy));
    }

    public BattleStatusLifecycleResult ProcessClock(
        BattleLifecycleClockRequest request,
        IStatModifierPolicyService statModifiers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(statModifiers);
        if (request.Participants.Count == 0)
        {
            return new BattleStatusLifecycleResult([]);
        }

        var transaction = new RuntimeActorExecutionTransaction(
            request.Participants[0],
            request.Participants);
        RuntimeActorState[] staged = request.Participants
            .Select(transaction.GetStaged)
            .ToArray();
        RuntimeActorState[] selected = SelectClockParticipants(staged, request.Boundary);
        var advanceReserve = new Dictionary<RuntimeInstanceId, bool>();
        foreach (RuntimeActorState actor in selected)
        {
            advanceReserve[actor.InstanceId] = !actor.IsDeployed &&
                request.Boundary.Kind is BattleLifecycleClockKind.TeamPhase or BattleLifecycleClockKind.Round &&
                _reserveLifecyclePolicy.ShouldAdvance(new BattleReserveLifecycleRequest(
                    actor.InstanceId,
                    actor.TeamId,
                    request.Boundary));
        }

        RuntimeActorState[] advancing = selected
            .Where(actor => actor.IsDeployed || advanceReserve[actor.InstanceId] ||
                            request.Boundary.Kind == BattleLifecycleClockKind.Action)
            .ToArray();
        var events = new List<BattleStatusLifecycleEvent>();
        if (request.Boundary is ActionLifecycleClockBoundary)
        {
            events.AddRange(Expire(advancing, actor => actor.ExpireInstantDurations()).Events);
        }
        else if (request.Boundary is TeamPhaseLifecycleClockBoundary phase)
        {
            events.AddRange(Expire(
                advancing,
                actor => actor.ExpirePhaseDurations(phase.PhaseId)).Events);
        }

        if (request.Boundary.EventId is ContentId eventId)
        {
            foreach (RuntimeActorState actor in advancing)
            {
                AddDurationTickEvents(
                    actor,
                    eventId,
                    advanceReserve[actor.InstanceId],
                    events);
            }
        }

        events.AddRange(TickModifiers(
            advancing,
            request.StatModifierBoundaries,
            statModifiers,
            actor => advanceReserve[actor.InstanceId]));
        transaction.Commit();
        return new BattleStatusLifecycleResult(events);
    }

    public BattleStatusLifecycleResult ProcessActionEnd(
        BattleActionEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(statModifiers);
        return ProcessParticipants(
            request.Participants,
            request.StatModifierBoundaries,
            statModifiers,
            actor => actor.ExpireInstantDurations());
    }

    public BattleStatusLifecycleResult Cleanup(
        BattleStatusCleanupRequest request,
        IStatModifierPolicyService statModifiers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(statModifiers);
        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        var transaction = new RuntimeActorExecutionTransaction(actor, [actor]);
        RuntimeActorState staged = transaction.Actor;
        bool guardWasActive = staged.IsGuarding;
        staged.SetGuarding(false);
        IReadOnlyList<BattleDurationTickResult> battleExpirations = [];
        if (request.Reason == BattleStatusDepartureReason.BattleEnd)
        {
            battleExpirations = staged.ExpireBattleDurations();
        }
        StatusRemovalCause removalCause = MapRemovalCause(request.Reason);
        IReadOnlyList<BattleStatusRemovalResult> removals = staged.RemoveStatuses(removalCause);

        RuntimeStatModifierStateSnapshot state = staged.ResolveStatModifierState(statModifiers);
        StatModifierTransitionResult modifierResult = statModifiers.Cleanup(
            new StatModifierCleanupRequest(state, MapCleanupScope(request.Reason)));
        RequireAccepted(modifierResult);
        if (modifierResult.StateChanged)
        {
            staged.ReplaceStatModifierState(statModifiers, modifierResult.After);
        }

        var events = new List<BattleStatusLifecycleEvent>();
        if (guardWasActive)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.GuardCleared,
                staged.InstanceId));
        }
        events.AddRange(battleExpirations.Select(expiration => DurationEvent(staged.InstanceId, expiration)));
        events.AddRange(removals.Select(removal => RemovalEvent(staged.InstanceId, removal)));
        events.AddRange(MapModifierEvents(staged.InstanceId, modifierResult));
        events.Add(new BattleStatusLifecycleEvent(
            BattleStatusLifecycleEventKind.CleanupApplied,
            staged.InstanceId,
            Detail: request.Reason.ToString())
        {
            DepartureReason = request.Reason
        });
        transaction.Commit();

        return new BattleStatusLifecycleResult(events);
    }

    private static BattleStatusLifecycleResult ProcessParticipants(
        IReadOnlyList<RuntimeActorState> participants,
        IReadOnlyList<StatModifierLifecycleBoundary> boundaries,
        IStatModifierPolicyService statModifiers,
        Func<RuntimeActorState, IReadOnlyList<BattleDurationTickResult>> expire)
    {
        if (participants.Count == 0)
        {
            return new BattleStatusLifecycleResult([]);
        }

        var transaction = new RuntimeActorExecutionTransaction(participants[0], participants);
        RuntimeActorState[] staged = participants.Select(transaction.GetStaged).ToArray();
        var events = new List<BattleStatusLifecycleEvent>();
        events.AddRange(Expire(staged, expire).Events);
        events.AddRange(TickModifiers(staged, boundaries, statModifiers));
        transaction.Commit();
        return new BattleStatusLifecycleResult(events);
    }

    private static BattleStatusLifecycleResult Expire(
        IEnumerable<RuntimeActorState> participants,
        Func<RuntimeActorState, IReadOnlyList<BattleDurationTickResult>> expire)
    {
        var events = new List<BattleStatusLifecycleEvent>();
        foreach (RuntimeActorState actor in participants)
        {
            foreach (BattleDurationTickResult duration in expire(actor))
            {
                events.Add(new BattleStatusLifecycleEvent(
                    duration.StateKind == BattleDurationStateKind.Ailment
                        ? BattleStatusLifecycleEventKind.AilmentExpired
                        : BattleStatusLifecycleEventKind.StatusExpired,
                    actor.InstanceId,
                    duration.Id,
                    Detail: $"{duration.StateKind}:{duration.PreviousDuration.Kind}")
                {
                    DurationTransition = duration
                });
            }
        }

        return new BattleStatusLifecycleResult(events);
    }

    internal static IReadOnlyList<BattleStatusLifecycleEvent> TickModifiers(
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<StatModifierLifecycleBoundary> boundaries,
        IStatModifierPolicyService statModifiers,
        Func<RuntimeActorState, bool>? advanceReserveState = null)
    {
        var events = new List<BattleStatusLifecycleEvent>();
        foreach (RuntimeActorState actor in participants)
        {
            RuntimeStatModifierStateSnapshot state = actor.ResolveStatModifierState(statModifiers);
            foreach (StatModifierLifecycleBoundary boundary in boundaries)
            {
                StatModifierTransitionResult result = statModifiers.Tick(
                    new StatModifierTickRequest(
                        state,
                        boundary,
                        actor.IsDeployed || (advanceReserveState?.Invoke(actor) ?? false)));
                RequireAccepted(result);
                state = result.After;
                events.AddRange(MapModifierEvents(actor.InstanceId, result));
            }

            if (!ReferenceEquals(state, actor.StatModifierState) &&
                (actor.StatModifierState is not null || state.Tracks.Count > 0))
            {
                actor.ReplaceStatModifierState(statModifiers, state);
            }
        }

        return Array.AsReadOnly(events.ToArray());
    }

    private static RuntimeActorState[] SelectClockParticipants(
        IEnumerable<RuntimeActorState> participants,
        BattleLifecycleClockBoundary boundary)
    {
        RuntimeActorState[] snapshot = participants.ToArray();
        if (boundary is not ActorTurnLifecycleClockBoundary actorTurn)
        {
            return snapshot;
        }

        RuntimeActorState[] matching = snapshot
            .Where(actor => actor.InstanceId == actorTurn.ActorId)
            .ToArray();
        return matching.Length == 1
            ? matching
            : throw new InvalidOperationException(
                $"Actor-turn lifecycle clock expected exactly one participant '{actorTurn.ActorId}', " +
                $"but found {matching.Length}.");
    }

    private static void AddDurationTickEvents(
        RuntimeActorState actor,
        ContentId eventId,
        bool advanceReserveState,
        List<BattleStatusLifecycleEvent> events)
    {
        foreach (BattleDurationTickResult tick in actor.TickAilmentDurations(eventId, advanceReserveState))
        {
            events.Add(DurationEvent(actor.InstanceId, tick));
        }

        foreach (BattleDurationTickResult tick in actor.TickTimedStatuses(eventId, advanceReserveState))
        {
            events.Add(DurationEvent(actor.InstanceId, tick));
        }
    }

    internal static BattleStatusLifecycleEvent DurationEvent(
        RuntimeInstanceId actorId,
        BattleDurationTickResult transition) =>
        new(
            transition.Expired
                ? transition.StateKind == BattleDurationStateKind.Ailment
                    ? BattleStatusLifecycleEventKind.AilmentExpired
                    : BattleStatusLifecycleEventKind.StatusExpired
                : BattleStatusLifecycleEventKind.DurationAdvanced,
            actorId,
            transition.Id,
            Detail: transition.StateKind.ToString())
        {
            DurationTransition = transition
        };

    internal static BattleStatusLifecycleEvent RemovalEvent(
        RuntimeInstanceId actorId,
        BattleStatusRemovalResult transition) =>
        new(
            transition.StateKind == BattleDurationStateKind.Ailment
                ? BattleStatusLifecycleEventKind.AilmentRemoved
                : BattleStatusLifecycleEventKind.StatusRemoved,
            actorId,
            transition.Id,
            Detail: transition.Cause.ToString())
        {
            RemovalTransition = transition
        };

    internal static IReadOnlyList<BattleStatusLifecycleEvent> MapModifierEvents(
        RuntimeInstanceId actorId,
        StatModifierTransitionResult result) =>
        Array.AsReadOnly(result.Events.Select(@event => new BattleStatusLifecycleEvent(
            @event.Kind == StatModifierEventKind.AggregateStageChanged
                ? BattleStatusLifecycleEventKind.StatStageChanged
                : @event.Kind == StatModifierEventKind.ContributionExpired
                    ? BattleStatusLifecycleEventKind.StatusExpired
                    : BattleStatusLifecycleEventKind.StatModifierChanged,
            actorId,
            @event.ModifierTrackId,
            @event.CurrentStage - @event.PreviousStage,
            @event.Kind.ToString(),
            @event)).ToArray());

    private static StatModifierCleanupScope MapCleanupScope(BattleStatusDepartureReason reason) =>
        reason switch
        {
            BattleStatusDepartureReason.DeploymentSwap or BattleStatusDepartureReason.RosterRecall =>
                StatModifierCleanupScope.Swap,
            BattleStatusDepartureReason.Defeat or BattleStatusDepartureReason.Flee =>
                StatModifierCleanupScope.ActorDeparture,
            BattleStatusDepartureReason.BattleEnd => StatModifierCleanupScope.EncounterEnd,
            BattleStatusDepartureReason.FieldTransition => StatModifierCleanupScope.FieldTransition,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };

    private static StatusRemovalCause MapRemovalCause(BattleStatusDepartureReason reason) =>
        reason switch
        {
            BattleStatusDepartureReason.DeploymentSwap => StatusRemovalCause.DeploymentSwap,
            BattleStatusDepartureReason.Defeat => StatusRemovalCause.Defeat,
            BattleStatusDepartureReason.Flee => StatusRemovalCause.Flee,
            BattleStatusDepartureReason.RosterRecall => StatusRemovalCause.RosterRecall,
            BattleStatusDepartureReason.BattleEnd => StatusRemovalCause.BattleEnd,
            BattleStatusDepartureReason.FieldTransition => StatusRemovalCause.FieldTransition,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };

    private static void RequireAccepted(StatModifierTransitionResult result)
    {
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                "Stat-modifier lifecycle transition was rejected: " +
                string.Join("; ", result.Diagnostics.Select(value => value.Message)));
        }
    }
}

public sealed class BattleStatusLifecycleService : IBattleStatusLifecycleService
{
    private readonly IRandomSource _random;
    private readonly IReadOnlyDictionary<ContentId, ICustomAilmentTurnBehaviorHandler> _customTurnBehaviorHandlers;
    private readonly IBattleTurnRestrictionPolicy _turnRestrictionPolicy;
    private readonly IBattleDurationLifecycleService _durationLifecycle;

    public BattleStatusLifecycleService(
        IRandomSource random,
        IEnumerable<KeyValuePair<ContentId, ICustomAilmentTurnBehaviorHandler>>? customTurnBehaviorHandlers = null,
        IBattleTurnRestrictionPolicy? turnRestrictionPolicy = null)
        : this(
            random,
            customTurnBehaviorHandlers,
            turnRestrictionPolicy,
            SuspendReserveLifecyclePolicy.Instance)
    {
    }

    public BattleStatusLifecycleService(
        IRandomSource random,
        IEnumerable<KeyValuePair<ContentId, ICustomAilmentTurnBehaviorHandler>>? customTurnBehaviorHandlers,
        IBattleTurnRestrictionPolicy? turnRestrictionPolicy,
        IBattleReserveLifecyclePolicy reserveLifecyclePolicy)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _customTurnBehaviorHandlers = new ReadOnlyDictionary<ContentId, ICustomAilmentTurnBehaviorHandler>(
            (customTurnBehaviorHandlers ?? []).ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? throw new ArgumentException(
                    $"Custom ailment turn-behavior handler '{pair.Key}' cannot be null.",
                    nameof(customTurnBehaviorHandlers))));
        _turnRestrictionPolicy = turnRestrictionPolicy ?? new MostRestrictiveBattleTurnPolicy();
        _durationLifecycle = new BattleDurationLifecycleService(
            reserveLifecyclePolicy ?? throw new ArgumentNullException(nameof(reserveLifecyclePolicy)));
    }

    public BattleTurnStartLifecycleResult ProcessTurnStart(BattleTurnStartLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        var transaction = new RuntimeActorExecutionTransaction(actor, [actor]);
        BattleTurnStartLifecycleResult result = ProcessTurnStartCore(
            new BattleTurnStartLifecycleRequest(transaction.Actor, request.CanRecallToRoster));
        transaction.Commit();
        return result;
    }

    private BattleTurnStartLifecycleResult ProcessTurnStartCore(BattleTurnStartLifecycleRequest request)
    {
        RuntimeActorState actor = request.Actor;
        var events = new List<BattleStatusLifecycleEvent>();

        if (actor.IsGuarding)
        {
            actor.SetGuarding(false);
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.GuardCleared,
                actor.InstanceId));
        }

        KeyValuePair<ContentId, ActiveAilmentState>[] scheduledAilments =
            actor.Ailments.ToArray();
        var scheduledRestrictions = new List<BattleTurnStartRestriction>();
        foreach ((ContentId ailmentId, ActiveAilmentState scheduledAilment) in scheduledAilments)
        {
            if (!actor.Ailments.TryGetValue(
                    ailmentId,
                    out ActiveAilmentState? currentAilment) ||
                !ReferenceEquals(currentAilment, scheduledAilment))
            {
                continue;
            }

            scheduledRestrictions.Add(ResolveTurnStartRestriction(
                actor,
                currentAilment.Definition,
                request.CanRecallToRoster));
        }

        BattleTurnStartRestriction restriction = _turnRestrictionPolicy.Resolve(
                scheduledRestrictions)
            ?? throw new InvalidOperationException("The battle turn-restriction policy returned null.");
        if (restriction.Outcome != BattleTurnStartOutcome.CanAct)
        {
            ContentId? sourceAilmentId = restriction.SourceAilmentIds.Count > 0
                ? restriction.SourceAilmentIds[0]
                : null;
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.TurnRestricted,
                actor.InstanceId,
                sourceAilmentId,
                Detail: RestrictionDetail(restriction)));
        }

        return new BattleTurnStartLifecycleResult(restriction, events);
    }

    public BattleTurnEndLifecycleResult ProcessTurnEnd(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        RuntimeActorState actor = request.Actor ?? throw new ArgumentNullException(nameof(request.Actor));
        RuntimeActorState[] participants = request.Participants?.ToArray()
            ?? throw new ArgumentNullException(nameof(request.Participants));
        RuntimeActorState[] transactionActors = participants
            .Append(actor)
            .Distinct<RuntimeActorState>(ReferenceEqualityComparer.Instance)
            .ToArray();
        var transaction = new RuntimeActorExecutionTransaction(actor, transactionActors);
        var stagedRequest = new BattleTurnEndLifecycleRequest(
            transaction.Actor,
            participants.Select(transaction.GetStaged),
            request.ContextId,
            request.EventId,
            request.BattleKindId,
            request.MoonPhaseId,
            request.StatModifierBoundary);
        BattleTurnEndLifecycleResult result = ProcessTurnEndCore(stagedRequest, services);
        transaction.Commit();
        return result;
    }

    private BattleTurnEndLifecycleResult ProcessTurnEndCore(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services)
    {
        RuntimeActorState actor = request.Actor;
        RuntimeActorState[] participants = request.Participants.ToArray();
        var events = new List<BattleStatusLifecycleEvent>();
        var passiveActivations = new List<PassiveTriggerExecutionResult>();

        if (!actor.IsDeployed)
        {
            return new BattleTurnEndLifecycleResult(events, passiveActivations);
        }

        PassiveTriggerDispatchResult dispatch = services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                request.EventId,
                actor,
                participants,
                [actor],
                request.ContextId,
                request.BattleKindId,
                request.MoonPhaseId,
                request.StatModifierBoundary is null ? [] : [request.StatModifierBoundary]),
            services);
        passiveActivations.AddRange(dispatch.Activations);
        AddPassiveEvents(events, actor.InstanceId, dispatch);

        ExecuteAilmentTriggers(request, services, participants, events);
        ProcessAilmentRecovery(actor, request.EventId, events);
        ProcessDurationTicks(
            actor,
            request.EventId,
            request.StatModifierBoundary,
            services.StatModifiers,
            events);

        return new BattleTurnEndLifecycleResult(events, passiveActivations);
    }

    public BattleAilmentApplicationResult TryApplyAilment(
        BattleAilmentApplicationRequest request,
        BattleExecutionServices services) =>
        BattleAilmentApplicationTransaction.Execute(request, services);

    public BattleStatusLifecycleResult ProcessActionEnd(
        BattleActionEndLifecycleRequest request,
        IStatModifierPolicyService statModifiers) =>
        _durationLifecycle.ProcessActionEnd(request, statModifiers);

    public BattleStatusLifecycleResult ProcessClock(
        BattleLifecycleClockRequest request,
        IStatModifierPolicyService statModifiers) =>
        _durationLifecycle.ProcessClock(request, statModifiers);

    public BattleStatusLifecycleResult ApplyStatStage(
        RuntimeActorState target,
        ContentId modifierTrackId,
        int delta,
        BattleExecutionServices services,
        DurationDefinition? duration = null,
        StatModifierLifecycleBoundary? activeBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(services);
        var definition = new ModifyStatStageEffectDefinition(
            [modifierTrackId],
            delta,
            duration);
        var environment = new EffectExecutionEnvironment(
            ContentId.Parse("stat_modifier_lifecycle"),
            activeStatModifierBoundaries: activeBoundary is null ? [] : [activeBoundary]);
        StatModifierApplicationEvaluation evaluation = StatModifierExecution.Apply(
            target,
            definition,
            environment,
            services.StatModifiers);
        if (!evaluation.Accepted)
        {
            throw new InvalidOperationException(
                "Stat-modifier lifecycle application was rejected: " + evaluation.RejectionDetail);
        }

        return new BattleStatusLifecycleResult(evaluation.Transitions
            .SelectMany(transition =>
                BattleDurationLifecycleService.MapModifierEvents(target.InstanceId, transition)));
    }

    public BattleStatusLifecycleResult Cleanup(
        BattleStatusCleanupRequest request,
        IStatModifierPolicyService statModifiers) =>
        _durationLifecycle.Cleanup(request, statModifiers);

    private BattleTurnStartRestriction ResolveTurnStartRestriction(
        RuntimeActorState actor,
        AilmentDefinition ailment,
        bool canRecallToRoster)
    {
        AilmentTurnBehaviorDefinition behavior = ailment.TurnBehavior;
        CustomAilmentTurnBehaviorResult? customResult = behavior is CustomAilmentTurnBehaviorDefinition custom
            ? ResolveCustomBehavior(custom, actor, ailment, canRecallToRoster)
            : null;
        BattleTurnStartOutcome outcome = behavior switch
        {
            NormalAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.CanAct,
            SkipAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.Skip,
            LimitedActionsAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.LimitedAction,
            ForcedBasicAttackAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.ForcedBasicAttack,
            ConfusedActionAilmentTurnBehaviorDefinition => BattleTurnStartOutcome.ForcedConfusion,
            ChanceSkipAilmentTurnBehaviorDefinition chanceSkip =>
                Roll(chanceSkip.SkipChance) ? BattleTurnStartOutcome.Skip : BattleTurnStartOutcome.CanAct,
            ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear => ResolveFearOutcome(fear, canRecallToRoster),
            CustomAilmentTurnBehaviorDefinition => customResult!.Outcome,
            _ => throw new ArgumentOutOfRangeException(
                nameof(behavior),
                behavior,
                $"Unsupported ailment turn behavior '{behavior.GetType().Name}'.")
        };

        IReadOnlyList<ContentId> allowedActionIds = behavior switch
        {
            LimitedActionsAilmentTurnBehaviorDefinition limited => limited.AllowedActionIds,
            CustomAilmentTurnBehaviorDefinition => customResult!.AllowedActionIds,
            _ => []
        };

        return new BattleTurnStartRestriction(outcome, allowedActionIds, [ailment.Id]);
    }

    private CustomAilmentTurnBehaviorResult ResolveCustomBehavior(
        CustomAilmentTurnBehaviorDefinition behavior,
        RuntimeActorState actor,
        AilmentDefinition ailment,
        bool canRecallToRoster)
    {
        if (!_customTurnBehaviorHandlers.TryGetValue(
                behavior.HandlerId,
                out ICustomAilmentTurnBehaviorHandler? handler))
        {
            throw new InvalidOperationException(
                $"No custom ailment turn-behavior handler is registered for '{behavior.HandlerId}' " +
                $"while resolving ailment '{ailment.Id}'.");
        }

        return handler.Resolve(
            behavior,
            new CustomAilmentTurnBehaviorRequest(actor, ailment, canRecallToRoster))
            ?? throw new InvalidOperationException(
                $"Custom ailment turn-behavior handler '{behavior.HandlerId}' returned null.");
    }

    private static string RestrictionDetail(BattleTurnStartRestriction restriction)
    {
        string allowed = restriction.AllowedActionIds.Count == 0
            ? string.Empty
            : ":" + string.Join(",", restriction.AllowedActionIds);
        return restriction.Outcome + allowed;
    }

    private BattleTurnStartOutcome ResolveFearOutcome(
        ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear,
        bool canRecallToRoster)
    {
        if (!Enum.IsDefined(fear.CompanionFleeOutcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fear),
                fear.CompanionFleeOutcome,
                "Companion flee outcome must be defined.");
        }

        AuthoredPercentage.RequireCombinedMaximum(
            fear.FleeChance,
            fear.SkipChance,
            nameof(fear),
            "Authored flee and skip chances");
        if (fear.FleeChance == 100)
        {
            return canRecallToRoster && fear.CompanionFleeOutcome == CompanionFleeOutcome.RecallToRoster
                ? BattleTurnStartOutcome.RecallToRoster
                : BattleTurnStartOutcome.FleeBattle;
        }
        if (fear.FleeChance == 0 && fear.SkipChance == 0)
        {
            return BattleTurnStartOutcome.CanAct;
        }
        if (fear.FleeChance == 0 && fear.SkipChance == 100)
        {
            return BattleTurnStartOutcome.Skip;
        }

        int roll = RandomSourceContract.NextInt32(_random, 0, 100);
        if (roll < fear.FleeChance)
        {
            return canRecallToRoster && fear.CompanionFleeOutcome == CompanionFleeOutcome.RecallToRoster
                ? BattleTurnStartOutcome.RecallToRoster
                : BattleTurnStartOutcome.FleeBattle;
        }

        return roll < fear.FleeChance + fear.SkipChance
            ? BattleTurnStartOutcome.Skip
            : BattleTurnStartOutcome.CanAct;
    }

    private bool Roll(int chance)
    {
        AuthoredPercentage.RequireValid(chance, nameof(chance), "Authored chance");
        return chance switch
        {
            0 => false,
            100 => true,
            _ => RandomSourceContract.NextInt32(_random, 0, 100) < chance
        };
    }

    private static void ExecuteAilmentTriggers(
        BattleTurnEndLifecycleRequest request,
        BattleExecutionServices services,
        IReadOnlyList<RuntimeActorState> participants,
        List<BattleStatusLifecycleEvent> events)
    {
        RuntimeActorState actor = request.Actor;
        KeyValuePair<ContentId, ActiveAilmentState>[] scheduledAilments =
            actor.Ailments.ToArray();
        foreach ((ContentId ailmentId, ActiveAilmentState scheduled) in scheduledAilments)
        {
            if (!actor.Ailments.TryGetValue(ailmentId, out ActiveAilmentState? active) ||
                !ReferenceEquals(active, scheduled))
            {
                continue;
            }

            foreach (PassiveTriggerDefinition trigger in active.Definition.Triggers.Where(trigger => trigger.EventId == request.EventId))
            {
                IReadOnlyList<RuntimeActorState> targets = PassiveTriggerTargetResolver.Resolve(
                    trigger.Targeting,
                    actor,
                    participants,
                    [actor]);
                foreach (RuntimeActorState target in targets)
                {
                    var conditionContext = new BattleConditionContext(
                        actor,
                        target,
                        participants,
                        request.BattleKindId,
                        request.MoonPhaseId,
                        services);
                    if (!BattleConditionEvaluator.Evaluate(trigger.When, conditionContext))
                    {
                        continue;
                    }

                    var actionRequest = new EffectActionExecutionRequest(
                        active.Definition.Id,
                        actor,
                        participants,
                        new EffectExecutionEnvironment(
                            request.ContextId,
                            request.BattleKindId,
                            request.MoonPhaseId,
                            request.StatModifierBoundary is null ? [] : [request.StatModifierBoundary]),
                        new TargetingDefinition(
                            TargetRelation.Any,
                            TargetSelection.Single,
                            TargetLifeState.Any,
                            AllowSelf: true),
                        [target.InstanceId]);

                    OrderedEffectExecution execution = new OrderedEffectExecutor(
                        services,
                        services.EffectExecutors).Execute(
                        actionRequest,
                        trigger.Effects,
                        new ResolvedRuntimeTargetSet([target]));
                    AddEffectEvents(events, actor.InstanceId, active.Definition.Id, execution.Effects);
                    events.AddRange(execution.CompletionLifecycleEvents);

                    if (execution.StopsAction)
                    {
                        return;
                    }
                }
            }
        }
    }

    private void ProcessAilmentRecovery(
        RuntimeActorState actor,
        ContentId eventId,
        List<BattleStatusLifecycleEvent> events)
    {
        foreach (ActiveAilmentState active in actor.Ailments.Values.ToArray())
        {
            if (active.Definition.Recovery.RemoveOnEventIds.Contains(eventId))
            {
                IReadOnlyList<ContentId> removed = actor.RemoveAilments(
                    StatusRemovalCause.RecoveryEvent,
                    candidate => candidate.Definition.Id == active.Definition.Id);
                if (removed.Count > 0)
                {
                    events.Add(new BattleStatusLifecycleEvent(
                        BattleStatusLifecycleEventKind.AilmentRemoved,
                        actor.InstanceId,
                        active.Definition.Id,
                        Detail: "event")
                    {
                        RemovalTransition = new BattleStatusRemovalResult(
                            active.Definition.Id,
                            BattleDurationStateKind.Ailment,
                            StatusRemovalCause.RecoveryEvent)
                    });
                }

                continue;
            }

            NaturalAilmentRecoveryDefinition? natural = active.Definition.Recovery.Natural;
            if (natural is null)
            {
                continue;
            }

            decimal stat = actor.Stats.GetValueOrDefault(natural.StatId);
            int chance = ResolveNaturalRecoveryChance(natural, stat);
            if (!Roll(chance))
            {
                continue;
            }

            IReadOnlyList<ContentId> naturallyRemoved = actor.RemoveAilments(
                StatusRemovalCause.NaturalRecovery,
                candidate => candidate.Definition.Id == active.Definition.Id);
            if (naturallyRemoved.Count > 0)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.AilmentRecovered,
                    actor.InstanceId,
                    active.Definition.Id,
                    chance,
                    "natural")
                {
                    RemovalTransition = new BattleStatusRemovalResult(
                        active.Definition.Id,
                        BattleDurationStateKind.Ailment,
                        StatusRemovalCause.NaturalRecovery)
                });
            }
        }
    }

    private static int ResolveNaturalRecoveryChance(
        NaturalAilmentRecoveryDefinition recovery,
        decimal stat)
    {
        AuthoredPercentage.RequireValid(
            recovery.BaseChance,
            nameof(recovery),
            "Authored natural-recovery chance");
        decimal baseChance = recovery.BaseChance;
        if (recovery.StatMultiplier < 0m)
        {
            throw new InvalidOperationException("Natural-recovery stat multiplier cannot be negative.");
        }
        if (baseChance >= 100m || stat <= 0m || recovery.StatMultiplier == 0m)
        {
            return decimal.ToInt32(Math.Floor(baseChance));
        }

        try
        {
            decimal chance = checked(baseChance + checked(stat * recovery.StatMultiplier));
            return decimal.ToInt32(Math.Clamp(Math.Floor(chance), 0m, 100m));
        }
        catch (OverflowException)
        {
            return 100;
        }
    }

    private static void ProcessDurationTicks(
        RuntimeActorState actor,
        ContentId eventId,
        StatModifierLifecycleBoundary? statModifierBoundary,
        IStatModifierPolicyService statModifiers,
        List<BattleStatusLifecycleEvent> events)
    {
        foreach (BattleDurationTickResult tick in actor.TickAilmentDurations(eventId))
        {
            events.Add(BattleDurationLifecycleService.DurationEvent(actor.InstanceId, tick));
        }

        foreach (BattleDurationTickResult tick in actor.TickTimedStatuses(eventId))
        {
            events.Add(BattleDurationLifecycleService.DurationEvent(actor.InstanceId, tick));
        }

        if (statModifierBoundary is not null)
        {
            events.AddRange(BattleDurationLifecycleService.TickModifiers(
                [actor],
                [statModifierBoundary],
                statModifiers));
        }
    }

    internal static void AddPassiveEvents(
        List<BattleStatusLifecycleEvent> events,
        RuntimeInstanceId ownerId,
        PassiveTriggerDispatchResult dispatch)
    {
        foreach (PassiveTriggerExecutionResult activation in dispatch.Activations)
        {
            events.Add(new BattleStatusLifecycleEvent(
                activation.Outcome == PassiveTriggerOutcome.Executed
                    ? BattleStatusLifecycleEventKind.PassiveTriggered
                    : BattleStatusLifecycleEventKind.PassiveEvaluated,
                activation.TargetId,
                activation.SkillId,
                Detail: activation.EventId.ToString())
            {
                SourceActorId = ownerId,
                SourceId = activation.SkillId,
                PassiveActivation = activation
            });
            if (activation.Outcome == PassiveTriggerOutcome.Executed)
            {
                AddEffectEvents(events, ownerId, activation.SkillId, activation.Effects);
                events.AddRange(activation.CompletionLifecycleEvents);
            }
        }
    }

    private static void AddEffectEvents(
        List<BattleStatusLifecycleEvent> events,
        RuntimeInstanceId sourceActorId,
        ContentId sourceId,
        IReadOnlyList<EffectExecutionResult> effects)
    {
        foreach (EffectExecutionResult effect in effects)
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.PassiveEffectResolved,
                effect.TargetId ?? sourceActorId,
                sourceId,
                effect.Value,
                effect.Detail)
            {
                SourceActorId = sourceActorId,
                SourceId = sourceId,
                EffectResult = effect
            });
            foreach (ExecutionResourceChange change in effect.ResourceChanges)
            {
                events.Add(new BattleStatusLifecycleEvent(
                    BattleStatusLifecycleEventKind.ResourceChanged,
                    change.ActorId,
                    change.ResourceId,
                    change.Delta,
                    effect.Detail)
                {
                    SourceActorId = sourceActorId,
                    SourceId = sourceId,
                    EffectResult = effect
                });
            }
            events.AddRange(effect.LifecycleEvents);
        }
    }
}
