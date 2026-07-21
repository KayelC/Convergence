using Convergence.Content;
using Convergence.Execution;

namespace Convergence.TurnEconomy;

public sealed record ActionTokenTurnEconomySnapshot : BattleTurnEconomySnapshot
{
    public ActionTokenTurnEconomySnapshot(int fullTokens, int partialTokens)
        : base(ActionTokenTurnEconomy.EconomyId, CheckedTotal(fullTokens, partialTokens))
    {
        FullTokens = fullTokens;
        PartialTokens = partialTokens;
    }

    public int FullTokens { get; }
    public int PartialTokens { get; }

    private static int CheckedTotal(int fullTokens, int partialTokens)
    {
        if (fullTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fullTokens));
        }

        if (partialTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partialTokens));
        }

        return checked(fullTokens + partialTokens);
    }
}

/// <summary>
/// Optional turn economy that tracks full and partial action tokens. The
/// encounter runner depends only on <see cref="IBattleTurnEconomy"/>.
/// </summary>
public sealed class ActionTokenTurnEconomy : IBattleTurnEconomy
{
    public static ContentId EconomyId { get; } = ContentId.Parse("action_token");

    private int _fullTokens;
    private int _partialTokens;

    public int FullTokens => _fullTokens;
    public int PartialTokens => _partialTokens;
    public bool HasTurnsRemaining() => _fullTokens + _partialTokens > 0;

    public void StartPhase(int activeActorCount)
    {
        if (activeActorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeActorCount));
        }

        _fullTokens = activeActorCount;
        _partialTokens = 0;
    }

    public int GetTotalTokenCount() => _fullTokens + _partialTokens;

    public BattleTurnEconomySnapshot CaptureSnapshot() =>
        new ActionTokenTurnEconomySnapshot(_fullTokens, _partialTokens);

    public void Apply(ActionTurnConsumption consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        switch (consumption.Kind)
        {
            case ActionTurnConsumptionKind.None:
                return;
            case ActionTurnConsumptionKind.Pass:
                Pass();
                return;
            case ActionTurnConsumptionKind.TurnEconomy:
                ConsumeAction(consumption.TurnEconomy ?? throw new InvalidOperationException(
                    "Turn-economy consumption is missing its resolution."));
                return;
            case ActionTurnConsumptionKind.TerminatePhase:
                TerminatePhase();
                return;
            case ActionTurnConsumptionKind.Normal:
                ConsumeAction(new TurnEconomyResolution(TurnEconomyOutcome.Normal, false, false));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(consumption));
        }
    }

    public void ConsumeAction(TurnEconomyResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!HasTurnsRemaining())
        {
            return;
        }

        if (resolution.TerminatesPhase ||
            resolution.Outcome is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb)
        {
            TerminatePhase();
            return;
        }

        switch (resolution.Outcome)
        {
            case TurnEconomyOutcome.Miss:
            case TurnEconomyOutcome.Null:
                ConsumeTokens(2);
                return;
            case TurnEconomyOutcome.Weakness:
            case TurnEconomyOutcome.Critical:
                if (_fullTokens > 0)
                {
                    _fullTokens--;
                    _partialTokens++;
                }
                else
                {
                    _partialTokens--;
                }

                return;
            case TurnEconomyOutcome.Normal:
                ConsumeNormalAction();
                return;
            case TurnEconomyOutcome.Repel:
            case TurnEconomyOutcome.Absorb:
                throw new InvalidOperationException("Terminating outcomes must be handled before normal consumption.");
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution));
        }
    }

    private void ConsumeNormalAction()
    {
        if (_partialTokens > 0)
        {
            _partialTokens--;
        }
        else
        {
            _fullTokens--;
        }
    }

    public void Pass()
    {
        if (_partialTokens > 0)
        {
            _partialTokens--;
        }
        else if (_fullTokens > 0)
        {
            _fullTokens--;
            _partialTokens++;
        }
    }

    public void TerminatePhase()
    {
        _fullTokens = 0;
        _partialTokens = 0;
    }

    private void ConsumeTokens(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_partialTokens > 0)
            {
                _partialTokens--;
            }
            else if (_fullTokens > 0)
            {
                _fullTokens--;
            }
        }
    }
}
