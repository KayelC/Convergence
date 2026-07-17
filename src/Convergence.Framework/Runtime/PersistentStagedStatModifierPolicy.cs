using Convergence.Content;

namespace Convergence.Runtime;

/// <summary>
/// Supplies encounter-persistent, bounded stat stages with no natural duration expiry.
/// </summary>
public sealed class PersistentStagedStatModifierPolicy : IStatModifierPolicy
{
    public PersistentStagedStatModifierPolicy(
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
                "Persistent staged modifiers require a negative minimum stage.");
        }

        if (maximumStage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumStage),
                "Persistent staged modifiers require a positive maximum stage.");
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
            if (track.Contributions.Count != 1)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Persistent staged tracks must retain exactly one net contribution."));
                continue;
            }

            RuntimeStatModifierContributionSnapshot contribution = track.Contributions[0];
            if (track.ResolvedStage == 0 ||
                track.ResolvedStage < MinimumStage ||
                track.ResolvedStage > MaximumStage)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    $"Persistent stage must be nonzero and between {MinimumStage} and {MaximumStage}."));
            }

            if (contribution.StageDelta != track.ResolvedStage)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Persistent contribution must equal the track's resolved stage."));
            }

            if (contribution.Duration is not null)
            {
                diagnostics.Add(Incompatible(
                    track.ModifierTrackId,
                    "Persistent staged contributions cannot retain a duration."));
            }
        }

        return new StatModifierValidationResult(diagnostics);
    }

    public StatModifierPolicyDecision Apply(StatModifierApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.State.TryGetTrack(request.ModifierTrackId, out RuntimeStatModifierTrackSnapshot? currentTrack);
        int currentStage = currentTrack?.ResolvedStage ?? 0;
        long requestedStage = (long)currentStage + request.StageDelta;
        int nextStage = (int)Math.Clamp(requestedStage, MinimumStage, MaximumStage);
        if (nextStage == currentStage)
        {
            return StatModifierPolicyDecision.Accept(request.State);
        }

        var tracks = request.State.Tracks
            .Where(track => track.ModifierTrackId != request.ModifierTrackId)
            .ToList();
        if (nextStage != 0)
        {
            if (!TryResolveSequence(request.State, currentTrack, out long sequence))
            {
                return StatModifierPolicyDecision.Reject(new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.NumericOverflow,
                    "No further stat-modifier contribution sequence can be allocated.",
                    request.ModifierTrackId));
            }

            tracks.Add(new RuntimeStatModifierTrackSnapshot(
                request.ModifierTrackId,
                nextStage,
                [new RuntimeStatModifierContributionSnapshot(sequence, nextStage)]));
        }

        return StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId, tracks));
    }

    public StatModifierPolicyDecision Tick(StatModifierTickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return StatModifierPolicyDecision.Accept(request.State);
    }

    public StatModifierPolicyDecision Remove(StatModifierRemovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        HashSet<ContentId> selectedTracks = request.ModifierTrackIds.ToHashSet();
        HashSet<long> selectedContributions = request.ContributionSequences.ToHashSet();
        RuntimeStatModifierTrackSnapshot[] retained = request.State.Tracks
            .Where(track => !ShouldRemove(track, request.Mode, selectedTracks, selectedContributions))
            .ToArray();
        if (retained.Length == request.State.Tracks.Count)
        {
            return StatModifierPolicyDecision.Accept(request.State);
        }

        return StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId, retained));
    }

    public StatModifierPolicyDecision Cleanup(StatModifierCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Scope == StatModifierCleanupScope.Swap || request.State.Tracks.Count == 0
            ? StatModifierPolicyDecision.Accept(request.State)
            : StatModifierPolicyDecision.Accept(new RuntimeStatModifierStateSnapshot(PolicyId));
    }

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

    private static bool TryResolveSequence(
        RuntimeStatModifierStateSnapshot state,
        RuntimeStatModifierTrackSnapshot? currentTrack,
        out long sequence)
    {
        if (currentTrack is not null)
        {
            sequence = currentTrack.Contributions[0].Sequence;
            return true;
        }

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

    private static StatModifierDiagnostic Incompatible(ContentId modifierTrackId, string message) =>
        new(StatModifierDiagnosticCode.IncompatibleState, message, modifierTrackId);
}
