using Convergence.Content;

namespace Convergence.Runtime;

/// <summary>
/// Supplies one counted modifier signal per track using the neutral, weak, and
/// strong states represented by -2 through +2.
/// </summary>
public sealed class TimedExclusiveStatModifierPolicy : IStatModifierPolicy
{
    public const int MinimumSignal = -2;
    public const int MaximumSignal = 2;

    public TimedExclusiveStatModifierPolicy(ContentId policyId)
    {
        if (!policyId.IsValid)
        {
            throw new ArgumentException("Stat-modifier policy ID cannot be empty.", nameof(policyId));
        }

        PolicyId = policyId;
    }

    public ContentId PolicyId { get; }

    public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var diagnostics = new List<StatModifierDiagnostic>();
        foreach (RuntimeStatModifierTrackSnapshot track in state.Tracks)
        {
            if (track.Contributions.Count != 1)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Timed-exclusive tracks must retain exactly one contribution."));
                continue;
            }

            RuntimeStatModifierContributionSnapshot contribution = track.Contributions[0];
            if (!IsSignal(track.ResolvedStage))
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Timed-exclusive stage must be one of -2, -1, 1, or 2."));
            }

            if (contribution.StageDelta != track.ResolvedStage)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Timed-exclusive contribution must equal the track's resolved signal."));
            }

            if (contribution.Duration is not TurnDurationDefinition)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Timed-exclusive contributions require one counted duration."));
            }
        }

        return new StatModifierValidationResult(diagnostics);
    }

    public StatModifierPolicyDecision Apply(StatModifierApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsSignal(request.StageDelta))
        {
            return Reject(
                StatModifierDiagnosticCode.InvalidStageDelta,
                "Timed-exclusive applications must request -2, -1, 1, or 2.",
                request.ModifierTrackId);
        }

        if (request.Duration is not TurnDurationDefinition incomingDuration)
        {
            return Reject(
                StatModifierDiagnosticCode.InvalidDuration,
                "Timed-exclusive applications require a counted duration.",
                request.ModifierTrackId);
        }

        request.State.TryGetTrack(request.ModifierTrackId, out RuntimeStatModifierTrackSnapshot? currentTrack);
        RuntimeStatModifierContributionSnapshot? currentContribution = currentTrack?.Contributions[0];
        if (HasStaleActiveBoundary(currentContribution, request.ActiveLifecycleBoundary))
        {
            return Reject(
                StatModifierDiagnosticCode.InvalidLifecycleBoundary,
                "The active lifecycle boundary precedes the retained modifier boundary.",
                request.ModifierTrackId,
                currentContribution?.Sequence);
        }

        if (currentContribution is null)
        {
            if (!TryAllocateSequence(request.State, out long sequence))
            {
                return Reject(
                    StatModifierDiagnosticCode.NumericOverflow,
                    "No further stat-modifier contribution sequence can be allocated.",
                    request.ModifierTrackId);
            }

            return AcceptTrack(
                request,
                sequence,
                request.StageDelta,
                incomingDuration,
                request.ActiveLifecycleBoundary);
        }

        int currentSignal = currentContribution.StageDelta;
        int incomingSignal = request.StageDelta;
        if (Math.Sign(currentSignal) == Math.Sign(incomingSignal))
        {
            if (Math.Abs(incomingSignal) < Math.Abs(currentSignal))
            {
                return Reject(
                    StatModifierDiagnosticCode.AlreadyInEffect,
                    "A stronger timed-exclusive modifier is already in effect.",
                    request.ModifierTrackId,
                    currentContribution.Sequence);
            }

            StatModifierLifecycleBoundary? incomingBoundary = ResolveIncomingBoundary(
                incomingDuration,
                request.ActiveLifecycleBoundary,
                currentContribution);
            return AcceptTrack(
                request,
                currentContribution.Sequence,
                incomingSignal,
                incomingDuration,
                incomingBoundary);
        }

        int combinedSignal = currentSignal + incomingSignal;
        if (combinedSignal == 0)
        {
            return StatModifierPolicyDecision.Accept(RemoveTrack(request.State, request.ModifierTrackId));
        }

        bool existingSignalWins = Math.Sign(combinedSignal) == Math.Sign(currentSignal);
        TurnDurationDefinition survivingDuration = existingSignalWins
            ? (TurnDurationDefinition)currentContribution.Duration!
            : incomingDuration;
        StatModifierLifecycleBoundary? survivingBoundary = existingSignalWins
            ? currentContribution.LastLifecycleBoundary
            : ResolveIncomingBoundary(incomingDuration, request.ActiveLifecycleBoundary, currentContribution);
        return AcceptTrack(
            request,
            currentContribution.Sequence,
            combinedSignal,
            survivingDuration,
            survivingBoundary);
    }

    public StatModifierPolicyDecision Tick(StatModifierTickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (RuntimeStatModifierTrackSnapshot track in request.State.Tracks)
        {
            RuntimeStatModifierContributionSnapshot contribution = track.Contributions[0];
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

        var retained = new List<RuntimeStatModifierTrackSnapshot>();
        bool changed = false;
        foreach (RuntimeStatModifierTrackSnapshot track in request.State.Tracks)
        {
            RuntimeStatModifierContributionSnapshot contribution = track.Contributions[0];
            var duration = (TurnDurationDefinition)contribution.Duration!;
            if (duration.TickEventId != request.LifecycleBoundary.EventId ||
                IsAlreadyObserved(contribution.LastLifecycleBoundary, request.LifecycleBoundary))
            {
                retained.Add(track);
                continue;
            }

            if (!request.IsActorDeployed && duration.SuspendWhileReserve)
            {
                retained.Add(UpdatedTrack(track, contribution, duration, request.LifecycleBoundary));
                changed = true;
                continue;
            }

            if (duration.Value == 1)
            {
                changed = true;
                continue;
            }

            var decremented = new TurnDurationDefinition(
                duration.Value - 1,
                duration.TickEventId,
                duration.SuspendWhileReserve);
            retained.Add(UpdatedTrack(track, contribution, decremented, request.LifecycleBoundary));
            changed = true;
        }

        return StatModifierPolicyDecision.Accept(changed
            ? new RuntimeStatModifierStateSnapshot(PolicyId, retained)
            : request.State);
    }

    public StatModifierPolicyDecision Remove(StatModifierRemovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        HashSet<ContentId> selectedTracks = request.ModifierTrackIds.ToHashSet();
        HashSet<long> selectedContributions = request.ContributionSequences.ToHashSet();
        RuntimeStatModifierTrackSnapshot[] retained = request.State.Tracks
            .Where(track => !ShouldRemove(track, request.Mode, selectedTracks, selectedContributions))
            .ToArray();
        return retained.Length == request.State.Tracks.Count
            ? StatModifierPolicyDecision.Accept(request.State)
            : StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId, retained));
    }

    public StatModifierPolicyDecision Cleanup(StatModifierCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Scope == StatModifierCleanupScope.Swap || request.State.Tracks.Count == 0
            ? StatModifierPolicyDecision.Accept(request.State)
            : StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId));
    }

    private StatModifierPolicyDecision AcceptTrack(
        StatModifierApplicationRequest request,
        long sequence,
        int signal,
        TurnDurationDefinition duration,
        StatModifierLifecycleBoundary? lifecycleBoundary)
    {
        var contribution = new RuntimeStatModifierContributionSnapshot(
            sequence,
            signal,
            duration,
            lifecycleBoundary);
        var track = new RuntimeStatModifierTrackSnapshot(
            request.ModifierTrackId,
            signal,
            [contribution]);
        RuntimeStatModifierTrackSnapshot[] tracks =
        [
            .. request.State.Tracks.Where(candidate => candidate.ModifierTrackId != request.ModifierTrackId),
            track
        ];
        return StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId, tracks));
    }

    private static RuntimeStatModifierTrackSnapshot UpdatedTrack(
        RuntimeStatModifierTrackSnapshot track,
        RuntimeStatModifierContributionSnapshot contribution,
        TurnDurationDefinition duration,
        StatModifierLifecycleBoundary lifecycleBoundary) =>
        new(
            track.ModifierTrackId,
            track.ResolvedStage,
            [new RuntimeStatModifierContributionSnapshot(
                contribution.Sequence,
                contribution.StageDelta,
                duration,
                lifecycleBoundary)]);

    private static RuntimeStatModifierStateSnapshot RemoveTrack(
        RuntimeStatModifierStateSnapshot state,
        ContentId modifierTrackId) =>
        new(
            state.PolicyId,
            state.Tracks.Where(track => track.ModifierTrackId != modifierTrackId));

    private static StatModifierLifecycleBoundary? ResolveIncomingBoundary(
        TurnDurationDefinition incomingDuration,
        StatModifierLifecycleBoundary? activeBoundary,
        RuntimeStatModifierContributionSnapshot currentContribution)
    {
        if (activeBoundary is not null)
        {
            return activeBoundary;
        }

        return currentContribution.Duration is TurnDurationDefinition currentDuration &&
               currentDuration.TickEventId == incomingDuration.TickEventId
            ? currentContribution.LastLifecycleBoundary
            : null;
    }

    private static bool HasStaleActiveBoundary(
        RuntimeStatModifierContributionSnapshot? currentContribution,
        StatModifierLifecycleBoundary? activeBoundary) =>
        currentContribution?.LastLifecycleBoundary is StatModifierLifecycleBoundary last &&
        activeBoundary is not null &&
        last.EventId == activeBoundary.EventId &&
        activeBoundary.Sequence < last.Sequence;

    private static bool IsAlreadyObserved(
        StatModifierLifecycleBoundary? previous,
        StatModifierLifecycleBoundary current) =>
        previous is not null &&
        previous.EventId == current.EventId &&
        previous.Sequence == current.Sequence;

    private static bool ShouldRemove(
        RuntimeStatModifierTrackSnapshot track,
        StatModifierRemovalMode mode,
        IReadOnlySet<ContentId> selectedTracks,
        IReadOnlySet<long> selectedContributions) =>
        mode switch
        {
            StatModifierRemovalMode.Positive => track.ResolvedStage > 0,
            StatModifierRemovalMode.Negative => track.ResolvedStage < 0,
            StatModifierRemovalMode.SelectedTracks => selectedTracks.Contains(track.ModifierTrackId),
            StatModifierRemovalMode.SelectedContributions =>
                track.Contributions.Any(contribution => selectedContributions.Contains(contribution.Sequence)),
            StatModifierRemovalMode.All => true,
            _ => false
        };

    private static bool TryAllocateSequence(RuntimeStatModifierStateSnapshot state, out long sequence)
    {
        long maximum = state.Tracks
            .SelectMany(track => track.Contributions)
            .Select(contribution => contribution.Sequence)
            .DefaultIfEmpty(0)
            .Max();
        if (maximum == long.MaxValue)
        {
            sequence = 0;
            return false;
        }

        sequence = maximum + 1;
        return true;
    }

    private static bool IsSignal(int stage) =>
        stage is >= MinimumSignal and <= MaximumSignal && stage != 0;

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
