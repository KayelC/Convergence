using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

/// <summary>
/// Supplies bounded stat stages derived from independently timed signed
/// contributions.
/// </summary>
public sealed class TimedContributionStatModifierPolicy : IStatModifierPolicy
{
    public TimedContributionStatModifierPolicy(
        ContentId policyId,
        int minimumStage = -4,
        int maximumStage = 4)
    {
        if (!policyId.IsValid)
        {
            throw new ArgumentException("Stat-modifier policy ID cannot be empty.", nameof(policyId));
        }

        if (minimumStage >= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumStage),
                "Timed contributions require a negative minimum stage.");
        }
        if (minimumStage < BattleStatStageRange.Minimum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumStage),
                minimumStage,
                $"The supplied timed-contribution policy supports a minimum stage no lower than " +
                $"{BattleStatStageRange.Minimum}.");
        }

        if (maximumStage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumStage),
                "Timed contributions require a positive maximum stage.");
        }
        if (maximumStage > BattleStatStageRange.Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumStage),
                maximumStage,
                $"The supplied timed-contribution policy supports a maximum stage no higher than " +
                $"{BattleStatStageRange.Maximum}.");
        }

        PolicyId = policyId;
        MinimumStage = minimumStage;
        MaximumStage = maximumStage;
    }

    public ContentId PolicyId { get; }
    public int MinimumStage { get; }
    public int MaximumStage { get; }

    public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var diagnostics = new List<StatModifierDiagnostic>();
        foreach (RuntimeStatModifierTrackSnapshot track in state.Tracks)
        {
            if (track.Contributions.Count == 0)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Timed-contribution tracks must retain at least one contribution."));
                continue;
            }

            foreach (RuntimeStatModifierContributionSnapshot contribution in track.Contributions)
            {
                if (contribution.StageDelta < MinimumStage ||
                    contribution.StageDelta > MaximumStage)
                {
                    diagnostics.Add(Incompatible(
                        track.ModifierTrackId,
                        $"Timed contribution must be between {MinimumStage} and {MaximumStage}."));
                }

                if (contribution.Duration is not TurnDurationDefinition)
                {
                    diagnostics.Add(Incompatible(
                        track.ModifierTrackId,
                        "Timed contributions require counted durations."));
                }
            }

            int expectedStage = ResolveStage(track.Contributions);
            if (track.ResolvedStage != expectedStage)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Timed-contribution aggregate does not match its retained contributions."));
            }
        }

        return new StatModifierValidationResult(diagnostics);
    }

    public StatModifierPolicyDecision Apply(StatModifierApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.StageDelta < MinimumStage || request.StageDelta > MaximumStage)
        {
            return Reject(
                StatModifierDiagnosticCode.InvalidStageDelta,
                $"Timed contributions must be between {MinimumStage} and {MaximumStage}.",
                request.ModifierTrackId);
        }

        if (request.Duration is not TurnDurationDefinition incomingDuration)
        {
            return Reject(
                StatModifierDiagnosticCode.InvalidDuration,
                "Timed contributions require a counted duration.",
                request.ModifierTrackId);
        }

        if (StatModifierPolicyUtilities.HasStaleActiveBoundary(
                request.State,
                request.ActiveLifecycleBoundary))
        {
            return Reject(
                StatModifierDiagnosticCode.InvalidLifecycleBoundary,
                "The active lifecycle boundary precedes retained modifier state.",
                request.ModifierTrackId);
        }

        request.State.TryGetTrack(request.ModifierTrackId, out RuntimeStatModifierTrackSnapshot? currentTrack);
        bool isSameDirectionCap = currentTrack is not null &&
            ((currentTrack.ResolvedStage == MaximumStage && request.StageDelta > 0) ||
             (currentTrack.ResolvedStage == MinimumStage && request.StageDelta < 0));
        if (isSameDirectionCap)
        {
            RuntimeStatModifierContributionSnapshot oldest = currentTrack!.Contributions
                .Where(contribution => Math.Sign(contribution.StageDelta) == Math.Sign(request.StageDelta))
                .OrderBy(contribution => contribution.Sequence)
                .First();
            StatModifierLifecycleBoundary? boundary =
                StatModifierPolicyUtilities.ResolveRestartBoundary(
                    incomingDuration,
                    request.ActiveLifecycleBoundary,
                    oldest);
            RuntimeStatModifierContributionSnapshot[] refreshed = currentTrack.Contributions
                .Select(contribution => contribution.Sequence == oldest.Sequence
                    ? new RuntimeStatModifierContributionSnapshot(
                        contribution.Sequence,
                        contribution.StageDelta,
                        incomingDuration,
                        boundary)
                    : contribution)
                .ToArray();
            return AcceptTrack(request.State, new RuntimeStatModifierTrackSnapshot(
                request.ModifierTrackId,
                currentTrack.ResolvedStage,
                refreshed));
        }

        if (!StatModifierPolicyUtilities.TryAllocateSequence(request.State, out long sequence))
        {
            return Reject(
                StatModifierDiagnosticCode.NumericOverflow,
                "No further stat-modifier contribution sequence can be allocated.",
                request.ModifierTrackId);
        }

        RuntimeStatModifierContributionSnapshot[] contributions =
        [
            .. currentTrack?.Contributions ?? [],
            new RuntimeStatModifierContributionSnapshot(
                sequence,
                request.StageDelta,
                incomingDuration,
                request.ActiveLifecycleBoundary)
        ];
        return AcceptTrack(request.State, new RuntimeStatModifierTrackSnapshot(
            request.ModifierTrackId,
            ResolveStage(contributions),
            contributions));
    }

    public StatModifierPolicyDecision Tick(StatModifierTickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (RuntimeStatModifierTrackSnapshot track in request.State.Tracks)
        {
            foreach (RuntimeStatModifierContributionSnapshot contribution in track.Contributions)
            {
                var duration = (TurnDurationDefinition)contribution.Duration!;
                if (duration.TickEventId != request.LifecycleBoundary.EventId ||
                    contribution.LastLifecycleBoundary is not StatModifierLifecycleBoundary last ||
                    last.EventId != request.LifecycleBoundary.EventId)
                {
                    continue;
                }

                if (request.LifecycleBoundary.Sequence < last.Sequence)
                {
                    return Reject(
                        StatModifierDiagnosticCode.InvalidLifecycleBoundary,
                        "Lifecycle boundaries must be delivered in monotonic order.",
                        track.ModifierTrackId,
                        contribution.Sequence);
                }
            }
        }

        var retainedTracks = new List<RuntimeStatModifierTrackSnapshot>();
        bool stateChanged = false;
        foreach (RuntimeStatModifierTrackSnapshot track in request.State.Tracks)
        {
            var retainedContributions = new List<RuntimeStatModifierContributionSnapshot>();
            bool trackChanged = false;
            foreach (RuntimeStatModifierContributionSnapshot contribution in track.Contributions)
            {
                var duration = (TurnDurationDefinition)contribution.Duration!;
                if (duration.TickEventId != request.LifecycleBoundary.EventId ||
                    StatModifierPolicyUtilities.IsAlreadyObserved(
                        contribution.LastLifecycleBoundary,
                        request.LifecycleBoundary))
                {
                    retainedContributions.Add(contribution);
                    continue;
                }

                trackChanged = true;
                if (!request.IsActorDeployed && duration.SuspendWhileReserve)
                {
                    retainedContributions.Add(UpdateContribution(
                        contribution,
                        duration,
                        request.LifecycleBoundary));
                    continue;
                }

                if (duration.Value == 1)
                {
                    continue;
                }

                retainedContributions.Add(UpdateContribution(
                    contribution,
                    new TurnDurationDefinition(
                        duration.Value - 1,
                        duration.TickEventId,
                        duration.SuspendWhileReserve),
                    request.LifecycleBoundary));
            }

            if (!trackChanged)
            {
                retainedTracks.Add(track);
                continue;
            }

            stateChanged = true;
            if (retainedContributions.Count > 0)
            {
                retainedTracks.Add(new RuntimeStatModifierTrackSnapshot(
                    track.ModifierTrackId,
                    ResolveStage(retainedContributions),
                    retainedContributions));
            }
        }

        return StatModifierPolicyDecision.Accept(stateChanged
            ? new RuntimeStatModifierStateSnapshot(PolicyId, retainedTracks)
            : request.State);
    }

    public StatModifierPolicyDecision Remove(StatModifierRemovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        HashSet<ContentId> selectedTracks = request.ModifierTrackIds.ToHashSet();
        HashSet<long> selectedContributions = request.ContributionSequences.ToHashSet();
        var retainedTracks = new List<RuntimeStatModifierTrackSnapshot>();
        bool stateChanged = false;
        foreach (RuntimeStatModifierTrackSnapshot track in request.State.Tracks)
        {
            if (request.Mode is StatModifierRemovalMode.All ||
                request.Mode == StatModifierRemovalMode.SelectedTracks &&
                selectedTracks.Contains(track.ModifierTrackId))
            {
                stateChanged = true;
                continue;
            }

            RuntimeStatModifierContributionSnapshot[] retained = track.Contributions
                .Where(contribution => !ShouldRemoveContribution(
                    contribution,
                    request.Mode,
                    selectedContributions))
                .ToArray();
            if (retained.Length == track.Contributions.Count)
            {
                retainedTracks.Add(track);
                continue;
            }

            stateChanged = true;
            if (retained.Length > 0)
            {
                retainedTracks.Add(new RuntimeStatModifierTrackSnapshot(
                    track.ModifierTrackId,
                    ResolveStage(retained),
                    retained));
            }
        }

        return StatModifierPolicyDecision.Accept(stateChanged
            ? new RuntimeStatModifierStateSnapshot(PolicyId, retainedTracks)
            : request.State);
    }

    public StatModifierPolicyDecision Cleanup(StatModifierCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Scope == StatModifierCleanupScope.Swap || request.State.Tracks.Count == 0
            ? StatModifierPolicyDecision.Accept(request.State)
            : StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId));
    }

    private StatModifierPolicyDecision AcceptTrack(
        RuntimeStatModifierStateSnapshot state,
        RuntimeStatModifierTrackSnapshot replacement)
    {
        RuntimeStatModifierTrackSnapshot[] tracks =
        [
            .. state.Tracks.Where(track => track.ModifierTrackId != replacement.ModifierTrackId),
            replacement
        ];
        return StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId, tracks));
    }

    private int ResolveStage(IEnumerable<RuntimeStatModifierContributionSnapshot> contributions)
    {
        long rawStage = contributions.Sum(contribution => (long)contribution.StageDelta);
        return (int)Math.Clamp(rawStage, MinimumStage, MaximumStage);
    }

    private static RuntimeStatModifierContributionSnapshot UpdateContribution(
        RuntimeStatModifierContributionSnapshot contribution,
        TurnDurationDefinition duration,
        StatModifierLifecycleBoundary boundary) =>
        new(
            contribution.Sequence,
            contribution.StageDelta,
            duration,
            boundary);

    private static bool ShouldRemoveContribution(
        RuntimeStatModifierContributionSnapshot contribution,
        StatModifierRemovalMode mode,
        IReadOnlySet<long> selectedContributions) =>
        mode switch
        {
            StatModifierRemovalMode.Positive => contribution.StageDelta > 0,
            StatModifierRemovalMode.Negative => contribution.StageDelta < 0,
            StatModifierRemovalMode.SelectedContributions =>
                selectedContributions.Contains(contribution.Sequence),
            _ => false
        };

    private static StatModifierPolicyDecision Reject(
        StatModifierDiagnosticCode code,
        string message,
        ContentId modifierTrackId,
        long? contributionSequence = null) =>
        StatModifierPolicyDecision.Reject(
            new StatModifierDiagnostic(code, message, modifierTrackId, contributionSequence));

    private static StatModifierDiagnostic Incompatible(ContentId modifierTrackId, string message) =>
        new(StatModifierDiagnosticCode.IncompatibleState, message, modifierTrackId);
}
