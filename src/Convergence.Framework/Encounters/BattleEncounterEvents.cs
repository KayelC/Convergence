using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

public enum BattleEncounterEventKind
{
    ActorCreated,
    BattleStarted,
    InitiativeRolled,
    RoundStarted,
    PhaseStarted,
    TurnStarted,
    TurnRestricted,
    CommandSelected,
    CommandPassed,
    ActionExecuted,
    ActionRejected,
    EffectResolved,
    PassiveActivated,
    StatusChanged,
    ResourceChanged,
    TurnEconomyChanged,
    EncounterPresenceChanged,
    ActorDefeated,
    PhaseEnded,
    BattleFaulted,
    BattleEnded,
    HostActionRequested,
    TurnEnded,
    RoundEnded
}

public enum BattleEncounterTurnEndReason
{
    CommandCommitted,
    ActorUnavailable,
    EncounterTerminated
}

public abstract record BattleEncounterEventPayload;

public sealed record BattleActorCreatedEventPayload(
    RuntimeInstanceId ActorId,
    ContentId EntityId,
    ContentId TeamId) : BattleEncounterEventPayload;

public sealed record BattleStartedEventPayload : BattleEncounterEventPayload
{
    public BattleStartedEventPayload(
        ContentId contextId,
        ContentId battleKindId,
        ContentId? moonPhaseId,
        int roundLimit,
        IEnumerable<RuntimeInstanceId> actorIds,
        IEnumerable<ContentId> teamIds)
    {
        if (roundLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundLimit), "Round limit must be positive.");
        }

        ContextId = contextId;
        BattleKindId = battleKindId;
        MoonPhaseId = moonPhaseId;
        RoundLimit = roundLimit;
        ActorIds = Array.AsReadOnly((actorIds ?? throw new ArgumentNullException(nameof(actorIds))).ToArray());
        TeamIds = Array.AsReadOnly((teamIds ?? throw new ArgumentNullException(nameof(teamIds))).ToArray());
    }

    public ContentId ContextId { get; }
    public ContentId BattleKindId { get; }
    public ContentId? MoonPhaseId { get; }
    public int RoundLimit { get; }
    public IReadOnlyList<RuntimeInstanceId> ActorIds { get; }
    public IReadOnlyList<ContentId> TeamIds { get; }
}

public sealed record BattleInitiativeRolledEventPayload : BattleEncounterEventPayload
{
    public BattleInitiativeRolledEventPayload(IEnumerable<ContentId> teamOrder)
    {
        TeamOrder = Array.AsReadOnly(
            (teamOrder ?? throw new ArgumentNullException(nameof(teamOrder))).ToArray());
    }

    public IReadOnlyList<ContentId> TeamOrder { get; }
}

public sealed record BattleRoundStartedEventPayload : BattleEncounterEventPayload
{
    public BattleRoundStartedEventPayload(int roundNumber)
    {
        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber), "Round number must be positive.");
        }

        RoundNumber = roundNumber;
    }

    public int RoundNumber { get; }
}

public sealed record BattlePhaseStartedEventPayload : BattleEncounterEventPayload
{
    public BattlePhaseStartedEventPayload(
        ContentId TeamId,
        BattleTurnEconomySnapshot TurnEconomyState)
    {
        BattleTurnEconomyEventPayloadValidator.ValidateTeamSnapshot(TeamId, TurnEconomyState);
        this.TeamId = TeamId;
        this.TurnEconomyState = TurnEconomyState;
    }

    public ContentId TeamId { get; init; }
    public BattleTurnEconomySnapshot TurnEconomyState { get; init; }

    public void Deconstruct(out ContentId TeamId, out BattleTurnEconomySnapshot TurnEconomyState)
    {
        TeamId = this.TeamId;
        TurnEconomyState = this.TurnEconomyState;
    }
}

public sealed record BattleTurnStartedEventPayload(
    RuntimeInstanceId ActorId,
    ContentId TeamId) : BattleEncounterEventPayload;

public sealed record BattleTurnRestrictedEventPayload(
    RuntimeInstanceId ActorId,
    BattleTurnStartRestriction Restriction) : BattleEncounterEventPayload;

public sealed record BattleCommandSelectedEventPayload(
    RuntimeInstanceId ActorId,
    ContentId ActionId,
    RuntimeInstanceId? TargetId = null) : BattleEncounterEventPayload;

public sealed record BattleCommandPassedEventPayload(
    RuntimeInstanceId ActorId,
    BattleTurnStartOutcome? RestrictionOutcome = null) : BattleEncounterEventPayload;

public sealed record BattleActionExecutedEventPayload(
    BattleActionEventKind ActionEventKind,
    RuntimeInstanceId? ActorId = null,
    RuntimeInstanceId? TargetId = null,
    ContentId? SourceId = null,
    decimal? Value = null) : BattleEncounterEventPayload;

public sealed record BattleActionRejectedEventPayload(
    RuntimeInstanceId ActorId,
    BattleEncounterCommandStatus Status,
    ContentId? ActionId = null) : BattleEncounterEventPayload;

public sealed record BattleEffectResolvedEventPayload(
    RuntimeInstanceId SourceActorId,
    ContentId SourceId,
    EffectExecutionResult Result) : BattleEncounterEventPayload;

public sealed record BattlePassiveActivatedEventPayload(
    RuntimeInstanceId ActorId,
    ContentId SkillId,
    PassiveTriggerOutcome? Outcome = null,
    int? TriggerIndex = null,
    ContentId? EventId = null) : BattleEncounterEventPayload
{
    public PassiveTriggerExecutionResult? Result { get; init; }
}

public sealed record BattleStatusChangedEventPayload(
    BattleStatusLifecycleEvent StatusEvent) : BattleEncounterEventPayload;

public sealed record BattleResourceChangedEventPayload(
    RuntimeInstanceId SourceActorId,
    RuntimeInstanceId AffectedActorId,
    decimal Delta,
    ContentId? ResourceId = null,
    ContentId? SourceId = null) : BattleEncounterEventPayload;

public sealed record BattleTurnEconomyChangedEventPayload : BattleEncounterEventPayload
{
    public BattleTurnEconomyChangedEventPayload(
        RuntimeInstanceId ActorId,
        BattleTurnEconomySnapshot Before,
        BattleTurnEconomySnapshot After,
        ActionTurnConsumption Consumption)
    {
        BattleTurnEconomyEventPayloadValidator.ValidateTransition(ActorId, Before, After, Consumption);
        this.ActorId = ActorId;
        this.Before = Before;
        this.After = After;
        this.Consumption = Consumption;
    }

    public RuntimeInstanceId ActorId { get; init; }
    public BattleTurnEconomySnapshot Before { get; init; }
    public BattleTurnEconomySnapshot After { get; init; }
    public ActionTurnConsumption Consumption { get; init; }

    public void Deconstruct(
        out RuntimeInstanceId ActorId,
        out BattleTurnEconomySnapshot Before,
        out BattleTurnEconomySnapshot After,
        out ActionTurnConsumption Consumption)
    {
        ActorId = this.ActorId;
        Before = this.Before;
        After = this.After;
        Consumption = this.Consumption;
    }
}

public sealed record BattleTurnEndedEventPayload : BattleEncounterEventPayload
{
    public BattleTurnEndedEventPayload(
        RuntimeInstanceId actorId,
        ContentId teamId,
        BattleEncounterTurnEndReason reason,
        BattleTurnEconomySnapshot turnEconomyState,
        ActionTurnConsumption? turnConsumption = null)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Actor runtime ID must be valid.", nameof(actorId));
        }

        if (!teamId.IsValid)
        {
            throw new ArgumentException("Team ID must be valid.", nameof(teamId));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        ArgumentNullException.ThrowIfNull(turnEconomyState);
        bool commandCommitted = reason == BattleEncounterTurnEndReason.CommandCommitted;
        if (commandCommitted != (turnConsumption is not null))
        {
            throw new ArgumentException(
                "Only a committed command turn end carries turn consumption.",
                nameof(turnConsumption));
        }

        ActorId = actorId;
        TeamId = teamId;
        Reason = reason;
        TurnEconomyState = turnEconomyState;
        TurnConsumption = turnConsumption;
    }

    public RuntimeInstanceId ActorId { get; }
    public ContentId TeamId { get; }
    public BattleEncounterTurnEndReason Reason { get; }
    public BattleTurnEconomySnapshot TurnEconomyState { get; }
    public ActionTurnConsumption? TurnConsumption { get; }
}

public sealed record BattleEncounterPresenceChangedEventPayload(
    RuntimeInstanceId ActorId,
    bool IsDeployed,
    ContentId TeamId) : BattleEncounterEventPayload;

public sealed record BattleActorDefeatedEventPayload(
    RuntimeInstanceId ActorId,
    ContentId TeamId) : BattleEncounterEventPayload;

public sealed record BattlePhaseEndedEventPayload : BattleEncounterEventPayload
{
    public BattlePhaseEndedEventPayload(
        ContentId TeamId,
        BattleTurnEconomySnapshot TurnEconomyState)
    {
        BattleTurnEconomyEventPayloadValidator.ValidateTeamSnapshot(TeamId, TurnEconomyState);
        this.TeamId = TeamId;
        this.TurnEconomyState = TurnEconomyState;
    }

    public ContentId TeamId { get; init; }
    public BattleTurnEconomySnapshot TurnEconomyState { get; init; }

    public void Deconstruct(out ContentId TeamId, out BattleTurnEconomySnapshot TurnEconomyState)
    {
        TeamId = this.TeamId;
        TurnEconomyState = this.TurnEconomyState;
    }
}

public sealed record BattleRoundEndedEventPayload : BattleEncounterEventPayload
{
    public BattleRoundEndedEventPayload(int roundNumber)
    {
        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber), "Round number must be positive.");
        }

        RoundNumber = roundNumber;
    }

    public int RoundNumber { get; }
}

public sealed record BattleFaultedEventPayload(
    BattleEncounterFaultCode FaultCode,
    RuntimeInstanceId? ActorId = null,
    ContentId? TeamId = null,
    string? PortName = null) : BattleEncounterEventPayload;

public sealed record BattleEndedEventPayload : BattleEncounterEventPayload
{
    public BattleEndedEventPayload(
        BattleEncounterOutcome outcome,
        ContentId? winningTeamId,
        int completedRounds,
        BattleEncounterFaultCode? faultCode = null)
        : this(
            outcome,
            winningTeamId,
            completedRounds == 0 ? null : completedRounds,
            completedRounds,
            faultCode)
    {
    }

    public BattleEndedEventPayload(
        BattleEncounterOutcome outcome,
        ContentId? winningTeamId,
        int? finalRoundNumber,
        int completedRounds,
        BattleEncounterFaultCode? faultCode = null)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (winningTeamId is ContentId winner && !winner.IsValid)
        {
            throw new ArgumentException(
                "Winning team ID must be valid when supplied.",
                nameof(winningTeamId));
        }

        if (faultCode is BattleEncounterFaultCode suppliedFaultCode &&
            !Enum.IsDefined(suppliedFaultCode))
        {
            throw new ArgumentOutOfRangeException(nameof(faultCode));
        }

        bool requiresWinner =
            outcome is BattleEncounterOutcome.Victory or BattleEncounterOutcome.Defeat;
        if (requiresWinner != (winningTeamId is not null))
        {
            throw new ArgumentException(
                requiresWinner
                    ? $"{outcome} requires a winning team ID."
                    : $"{outcome} cannot carry a winning team ID.",
                nameof(winningTeamId));
        }

        bool requiresFaultCode = outcome == BattleEncounterOutcome.Faulted;
        if (requiresFaultCode != (faultCode is not null))
        {
            throw new ArgumentException(
                requiresFaultCode
                    ? "A faulted battle end requires a fault code."
                    : "Only a faulted battle end can carry a fault code.",
                nameof(faultCode));
        }

        if (completedRounds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedRounds));
        }

        if (finalRoundNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalRoundNumber),
                "A final round number must be positive when present.");
        }

        if (finalRoundNumber is null && completedRounds != 0)
        {
            throw new ArgumentException(
                "Completed rounds require a final round number.",
                nameof(completedRounds));
        }

        if (finalRoundNumber is int finalRound && completedRounds > finalRound)
        {
            throw new ArgumentException(
                "Completed rounds cannot exceed the final round number.",
                nameof(completedRounds));
        }

        Outcome = outcome;
        WinningTeamId = winningTeamId;
        FinalRoundNumber = finalRoundNumber;
        CompletedRounds = completedRounds;
        FaultCode = faultCode;
    }

    public BattleEncounterOutcome Outcome { get; }
    public ContentId? WinningTeamId { get; }
    public int? FinalRoundNumber { get; }
    public int CompletedRounds { get; }
    public BattleEncounterFaultCode? FaultCode { get; }
}

public sealed record BattleHostActionRequestedEventPayload(
    RuntimeInstanceId ActorId,
    ContentId ActionId,
    RuntimeInstanceId? TargetId = null) : BattleEncounterEventPayload;

public sealed record BattleEncounterEvent
{
    public BattleEncounterEvent(
        int sequence,
        BattleEncounterEventKind kind,
        BattleEncounterEventPayload payload,
        string? debugText = null)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Event sequence cannot be negative.");
        }

        Sequence = sequence;
        Kind = kind;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        DebugText = debugText;
        if (!PayloadMatchesKind(kind, payload))
        {
            throw new ArgumentException(
                $"Payload '{payload.GetType().Name}' does not match event kind '{kind}'.",
                nameof(payload));
        }

        ValidateTurnEconomyPayload(payload);
    }

    public int Sequence { get; init; }
    public BattleEncounterEventKind Kind { get; }
    public BattleEncounterEventPayload Payload { get; }
    public string? DebugText { get; }

    public RuntimeInstanceId? ActorId => Payload switch
    {
        BattleActorCreatedEventPayload value => value.ActorId,
        BattleTurnStartedEventPayload value => value.ActorId,
        BattleTurnRestrictedEventPayload value => value.ActorId,
        BattleCommandSelectedEventPayload value => value.ActorId,
        BattleCommandPassedEventPayload value => value.ActorId,
        BattleActionExecutedEventPayload value => value.ActorId,
        BattleActionRejectedEventPayload value => value.ActorId,
        BattleEffectResolvedEventPayload value => value.SourceActorId,
        BattlePassiveActivatedEventPayload value => value.ActorId,
        BattleStatusChangedEventPayload value => value.StatusEvent.ActorId,
        BattleResourceChangedEventPayload value => value.SourceActorId,
        BattleTurnEconomyChangedEventPayload value => value.ActorId,
        BattleTurnEndedEventPayload value => value.ActorId,
        BattleEncounterPresenceChangedEventPayload value => value.ActorId,
        BattleActorDefeatedEventPayload value => value.ActorId,
        BattleFaultedEventPayload value => value.ActorId,
        BattleHostActionRequestedEventPayload value => value.ActorId,
        _ => null
    };

    public RuntimeInstanceId? TargetId => Payload switch
    {
        BattleCommandSelectedEventPayload value => value.TargetId,
        BattleActionExecutedEventPayload value => value.TargetId,
        BattleEffectResolvedEventPayload value => value.Result.TargetId,
        BattleResourceChangedEventPayload value => value.AffectedActorId,
        BattleHostActionRequestedEventPayload value => value.TargetId,
        _ => null
    };

    public ContentId? SourceId => Payload switch
    {
        BattleCommandSelectedEventPayload value => value.ActionId,
        BattleActionExecutedEventPayload value => value.SourceId,
        BattleActionRejectedEventPayload value => value.ActionId,
        BattleEffectResolvedEventPayload value => value.SourceId,
        BattlePassiveActivatedEventPayload value => value.SkillId,
        BattleStatusChangedEventPayload value => value.StatusEvent.RelatedId,
        BattleResourceChangedEventPayload value => value.SourceId ?? value.ResourceId,
        BattleEndedEventPayload value => value.WinningTeamId,
        BattleHostActionRequestedEventPayload value => value.ActionId,
        _ => null
    };

    public decimal? Value => Payload switch
    {
        BattleActionExecutedEventPayload value => value.Value,
        BattleEffectResolvedEventPayload value => value.Result.Value,
        BattleStatusChangedEventPayload value => value.StatusEvent.Value,
        BattleResourceChangedEventPayload value => value.Delta,
        _ => null
    };

    public BattleTurnEconomySnapshot? TurnEconomyState => Payload switch
    {
        BattlePhaseStartedEventPayload value => value.TurnEconomyState,
        BattleTurnEconomyChangedEventPayload value => value.After,
        BattleTurnEndedEventPayload value => value.TurnEconomyState,
        BattlePhaseEndedEventPayload value => value.TurnEconomyState,
        _ => null
    };

    public BattleEncounterFaultCode? FaultCode => Payload switch
    {
        BattleFaultedEventPayload value => value.FaultCode,
        BattleEndedEventPayload value => value.FaultCode,
        _ => null
    };

    private static bool PayloadMatchesKind(
        BattleEncounterEventKind kind,
        BattleEncounterEventPayload payload) =>
        kind switch
        {
            BattleEncounterEventKind.ActorCreated => payload is BattleActorCreatedEventPayload,
            BattleEncounterEventKind.BattleStarted => payload is BattleStartedEventPayload,
            BattleEncounterEventKind.InitiativeRolled => payload is BattleInitiativeRolledEventPayload,
            BattleEncounterEventKind.RoundStarted => payload is BattleRoundStartedEventPayload,
            BattleEncounterEventKind.PhaseStarted => payload is BattlePhaseStartedEventPayload,
            BattleEncounterEventKind.TurnStarted => payload is BattleTurnStartedEventPayload,
            BattleEncounterEventKind.TurnRestricted => payload is BattleTurnRestrictedEventPayload,
            BattleEncounterEventKind.CommandSelected => payload is BattleCommandSelectedEventPayload,
            BattleEncounterEventKind.CommandPassed => payload is BattleCommandPassedEventPayload,
            BattleEncounterEventKind.ActionExecuted => payload is BattleActionExecutedEventPayload,
            BattleEncounterEventKind.ActionRejected => payload is BattleActionRejectedEventPayload,
            BattleEncounterEventKind.EffectResolved => payload is BattleEffectResolvedEventPayload,
            BattleEncounterEventKind.PassiveActivated => payload is BattlePassiveActivatedEventPayload,
            BattleEncounterEventKind.StatusChanged => payload is BattleStatusChangedEventPayload,
            BattleEncounterEventKind.ResourceChanged => payload is BattleResourceChangedEventPayload,
            BattleEncounterEventKind.TurnEconomyChanged => payload is BattleTurnEconomyChangedEventPayload,
            BattleEncounterEventKind.TurnEnded => payload is BattleTurnEndedEventPayload,
            BattleEncounterEventKind.EncounterPresenceChanged => payload is BattleEncounterPresenceChangedEventPayload,
            BattleEncounterEventKind.ActorDefeated => payload is BattleActorDefeatedEventPayload,
            BattleEncounterEventKind.PhaseEnded => payload is BattlePhaseEndedEventPayload,
            BattleEncounterEventKind.RoundEnded => payload is BattleRoundEndedEventPayload,
            BattleEncounterEventKind.BattleFaulted => payload is BattleFaultedEventPayload,
            BattleEncounterEventKind.BattleEnded => payload is BattleEndedEventPayload,
            BattleEncounterEventKind.HostActionRequested => payload is BattleHostActionRequestedEventPayload,
            _ => false
        };

    private static void ValidateTurnEconomyPayload(BattleEncounterEventPayload payload)
    {
        switch (payload)
        {
            case BattlePhaseStartedEventPayload started:
                BattleTurnEconomyEventPayloadValidator.ValidateTeamSnapshot(
                    started.TeamId,
                    started.TurnEconomyState);
                break;
            case BattleTurnEconomyChangedEventPayload changed:
                BattleTurnEconomyEventPayloadValidator.ValidateTransition(
                    changed.ActorId,
                    changed.Before,
                    changed.After,
                    changed.Consumption);
                break;
            case BattleTurnEndedEventPayload ended:
                BattleTurnEconomyEventPayloadValidator.ValidateTeamSnapshot(
                    ended.TeamId,
                    ended.TurnEconomyState);
                break;
            case BattlePhaseEndedEventPayload ended:
                BattleTurnEconomyEventPayloadValidator.ValidateTeamSnapshot(
                    ended.TeamId,
                    ended.TurnEconomyState);
                break;
        }
    }
}

internal static class BattleTurnEconomyEventPayloadValidator
{
    public static void ValidateTeamSnapshot(
        ContentId teamId,
        BattleTurnEconomySnapshot turnEconomyState)
    {
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Team ID must be valid.", nameof(teamId));
        }

        ArgumentNullException.ThrowIfNull(turnEconomyState);
    }

    public static void ValidateTransition(
        RuntimeInstanceId actorId,
        BattleTurnEconomySnapshot before,
        BattleTurnEconomySnapshot after,
        ActionTurnConsumption consumption)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Actor runtime ID must be valid.", nameof(actorId));
        }

        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(consumption);

        if (before.EconomyId != after.EconomyId)
        {
            throw new ArgumentException(
                "Turn-economy transition snapshots must use the same economy ID.",
                nameof(after));
        }

        if (before.GetType() != after.GetType())
        {
            throw new ArgumentException(
                "Turn-economy transition snapshots must use the same concrete type.",
                nameof(after));
        }
    }
}

internal static class BattleEncounterEventOwnership
{
    public static void RequirePortOwned(
        IEnumerable<BattleEncounterEvent> events,
        string portName)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("An encounter port name is required.", nameof(portName));
        }

        foreach (BattleEncounterEvent battleEvent in events)
        {
            if (battleEvent is null)
            {
                throw new InvalidOperationException(
                    $"Encounter port '{portName}' returned a null event.");
            }

            if (!IsPortOwned(battleEvent.Kind))
            {
                throw new InvalidOperationException(
                    $"Encounter port '{portName}' cannot publish runner-owned or unclassified " +
                    $"event kind '{battleEvent.Kind}'.");
            }
        }
    }

    private static bool IsPortOwned(BattleEncounterEventKind kind) =>
        kind is BattleEncounterEventKind.CommandSelected
            or BattleEncounterEventKind.CommandPassed
            or BattleEncounterEventKind.ActionExecuted
            or BattleEncounterEventKind.ActionRejected
            or BattleEncounterEventKind.EffectResolved
            or BattleEncounterEventKind.PassiveActivated
            or BattleEncounterEventKind.StatusChanged
            or BattleEncounterEventKind.ResourceChanged
            or BattleEncounterEventKind.EncounterPresenceChanged
            or BattleEncounterEventKind.HostActionRequested;
}
