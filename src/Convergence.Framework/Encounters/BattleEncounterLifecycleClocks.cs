using System.Collections.ObjectModel;
using Convergence.Content;

namespace Convergence.Encounters;

/// <summary>Maps one encounter team to its distinct authored phase and lifecycle event IDs.</summary>
public sealed record BattleTeamPhaseClockDefinition
{
    public BattleTeamPhaseClockDefinition(ContentId teamId, ContentId phaseId, ContentId eventId)
    {
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Team ID must be valid.", nameof(teamId));
        }
        if (!phaseId.IsValid)
        {
            throw new ArgumentException("Phase ID must be valid.", nameof(phaseId));
        }
        if (!eventId.IsValid)
        {
            throw new ArgumentException("Team-phase lifecycle event ID must be valid.", nameof(eventId));
        }

        TeamId = teamId;
        PhaseId = phaseId;
        EventId = eventId;
    }

    public ContentId TeamId { get; }
    public ContentId PhaseId { get; }
    public ContentId EventId { get; }
}

/// <summary>
/// Resolves encounter structure to authored lifecycle clocks. Team and phase IDs
/// remain distinct vocabularies and are never inferred from one another.
/// </summary>
public interface IBattleEncounterLifecycleClockPolicy
{
    BattleTeamPhaseClockDefinition ResolveTeamPhase(ContentId teamId);
    ContentId RoundEndEventId { get; }
}

/// <summary>Supplied immutable team-phase map with one explicit round clock.</summary>
public sealed class ExplicitBattleEncounterLifecycleClockPolicy : IBattleEncounterLifecycleClockPolicy
{
    private readonly IReadOnlyDictionary<ContentId, BattleTeamPhaseClockDefinition> _teamPhases;

    public ExplicitBattleEncounterLifecycleClockPolicy(
        IEnumerable<BattleTeamPhaseClockDefinition> teamPhases,
        ContentId roundEndEventId)
    {
        ArgumentNullException.ThrowIfNull(teamPhases);
        BattleTeamPhaseClockDefinition[] snapshot = teamPhases.ToArray();
        if (snapshot.Any(value => value is null))
        {
            throw new ArgumentException("Team-phase clock definitions cannot contain null values.", nameof(teamPhases));
        }
        if (snapshot.Select(value => value.TeamId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Each encounter team may have only one phase-clock mapping.", nameof(teamPhases));
        }
        if (!roundEndEventId.IsValid)
        {
            throw new ArgumentException("Round-end lifecycle event ID must be valid.", nameof(roundEndEventId));
        }

        _teamPhases = new ReadOnlyDictionary<ContentId, BattleTeamPhaseClockDefinition>(
            snapshot.ToDictionary(value => value.TeamId));
        RoundEndEventId = roundEndEventId;
    }

    public ContentId RoundEndEventId { get; }
    public IReadOnlyDictionary<ContentId, BattleTeamPhaseClockDefinition> TeamPhases => _teamPhases;

    public BattleTeamPhaseClockDefinition ResolveTeamPhase(ContentId teamId)
    {
        if (!teamId.IsValid)
        {
            throw new ArgumentException("Team ID must be valid.", nameof(teamId));
        }

        return _teamPhases.TryGetValue(teamId, out BattleTeamPhaseClockDefinition? definition)
            ? definition
            : throw new InvalidOperationException(
                $"No lifecycle phase clock is registered for encounter team '{teamId}'.");
    }
}
