using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Battle.Execution;

/// <summary>
/// Immutable state exposed by a turn-economy implementation. Hosts may use a
/// concrete subtype for presentation, while encounter orchestration relies
/// only on the generic remaining-action count.
/// </summary>
public abstract record BattleTurnEconomySnapshot
{
    protected BattleTurnEconomySnapshot(ContentId economyId, int remainingActions)
    {
        if (remainingActions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingActions));
        }

        EconomyId = economyId;
        RemainingActions = remainingActions;
    }

    public ContentId EconomyId { get; }
    public int RemainingActions { get; }
}

public sealed record StandardActionTurnEconomySnapshot : BattleTurnEconomySnapshot
{
    public StandardActionTurnEconomySnapshot(int remainingActions)
        : base(StandardActionTurnEconomy.EconomyId, remainingActions)
    {
    }
}

/// <summary>
/// Defines how an encounter phase allocates and consumes action opportunities.
/// Implementations are stateful per phase and must return immutable snapshots.
/// </summary>
public interface IBattleTurnEconomy
{
    void StartPhase(int activeActorCount);
    bool HasTurnsRemaining();
    BattleTurnEconomySnapshot CaptureSnapshot();
    void Apply(ActionTurnConsumption consumption);
}

/// <summary>
/// Neutral economy that grants one action to each actor present at phase start.
/// It is available to games that do not opt into a specialized turn system.
/// </summary>
public sealed class StandardActionTurnEconomy : IBattleTurnEconomy
{
    public static ContentId EconomyId { get; } = ContentId.Parse("standard_actions");

    private int _remainingActions;

    public bool HasTurnsRemaining() => _remainingActions > 0;

    public void StartPhase(int activeActorCount)
    {
        if (activeActorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeActorCount));
        }

        _remainingActions = activeActorCount;
    }

    public BattleTurnEconomySnapshot CaptureSnapshot() =>
        new StandardActionTurnEconomySnapshot(_remainingActions);

    public void Apply(ActionTurnConsumption consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        if (!HasTurnsRemaining() || consumption.Kind == ActionTurnConsumptionKind.None)
        {
            return;
        }

        _remainingActions = consumption.Kind == ActionTurnConsumptionKind.TerminatePhase
            ? 0
            : _remainingActions - 1;
    }
}

/// <summary>
/// Mandatory finite limits for one encounter phase. These are safety bounds,
/// not balance rules: hosts choose values appropriate to their action system.
/// </summary>
public sealed record BattlePhaseProgressPolicy
{
    public BattlePhaseProgressPolicy(int maximumCommands, int maximumConsecutiveFreeActions)
    {
        if (maximumCommands <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCommands), "Maximum commands must be positive.");
        }

        if (maximumConsecutiveFreeActions < 0 || maximumConsecutiveFreeActions >= maximumCommands)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumConsecutiveFreeActions),
                "The free-action limit must be nonnegative and lower than the command limit.");
        }

        MaximumCommands = maximumCommands;
        MaximumConsecutiveFreeActions = maximumConsecutiveFreeActions;
    }

    public int MaximumCommands { get; }
    public int MaximumConsecutiveFreeActions { get; }
}
