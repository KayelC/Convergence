using Convergence.Content;

namespace Convergence.Runtime;

public enum StatModifierOperationKind
{
    Application,
    Tick,
    Removal,
    Cleanup
}

public enum StatModifierTransitionCode
{
    Applied,
    Unchanged,
    Rejected
}

public enum StatModifierDiagnosticCode
{
    InvalidPolicyId,
    PolicyMismatch,
    InvalidModifierTrackId,
    DuplicateModifierTrack,
    InvalidContributionSequence,
    DuplicateContributionSequence,
    InvalidStageDelta,
    InvalidDuration,
    InvalidEventId,
    InvalidRemovalRequest,
    InvalidCleanupScope,
    NumericOverflow,
    PolicyRejected,
    PolicyFaulted,
    InvalidPolicyResult,
    IncompatibleState,
    InvalidLifecycleBoundary,
    AlreadyInEffect
}

public enum StatModifierEventKind
{
    ContributionAdded,
    ContributionUpdated,
    ContributionRemoved,
    ContributionExpired,
    AggregateStageChanged,
    TrackRemoved
}

public enum StatModifierRemovalMode
{
    Positive,
    Negative,
    SelectedTracks,
    SelectedContributions,
    All
}

public enum StatModifierCleanupScope
{
    Swap,
    ActorDeparture,
    EncounterEnd,
    FieldTransition,
    RecoveryEvent
}

public sealed class StatModifierDiagnostic
{
    public StatModifierDiagnostic(
        StatModifierDiagnosticCode code,
        string message,
        ContentId? modifierTrackId = null,
        long? contributionSequence = null)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Stat-modifier diagnostic message cannot be empty.", nameof(message));
        }

        if (modifierTrackId is ContentId trackId && !trackId.IsValid)
        {
            throw new ArgumentException("Related modifier track ID cannot be empty.", nameof(modifierTrackId));
        }

        if (contributionSequence is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contributionSequence),
                "Related contribution sequence must be positive.");
        }

        Code = code;
        Message = message;
        ModifierTrackId = modifierTrackId;
        ContributionSequence = contributionSequence;
    }

    public StatModifierDiagnosticCode Code { get; }
    public string Message { get; }
    public ContentId? ModifierTrackId { get; }
    public long? ContributionSequence { get; }
}

public sealed class StatModifierLifecycleBoundary
{
    public StatModifierLifecycleBoundary(ContentId eventId, long sequence)
    {
        EventId = eventId;
        Sequence = sequence;
    }

    public ContentId EventId { get; }
    public long Sequence { get; }
}

public sealed class RuntimeStatModifierContributionSnapshot
{
    public RuntimeStatModifierContributionSnapshot(
        long sequence,
        int stageDelta,
        DurationDefinition? duration = null,
        StatModifierLifecycleBoundary? lastLifecycleBoundary = null)
    {
        Sequence = sequence;
        StageDelta = stageDelta;
        Duration = duration;
        LastLifecycleBoundary = lastLifecycleBoundary;
    }

    public long Sequence { get; }
    public int StageDelta { get; }
    public DurationDefinition? Duration { get; }
    public StatModifierLifecycleBoundary? LastLifecycleBoundary { get; }
}

public sealed class RuntimeStatModifierTrackSnapshot
{
    public RuntimeStatModifierTrackSnapshot(
        ContentId modifierTrackId,
        int resolvedStage,
        IEnumerable<RuntimeStatModifierContributionSnapshot> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        ModifierTrackId = modifierTrackId;
        ResolvedStage = resolvedStage;
        Contributions = Array.AsReadOnly(contributions
            .OrderBy(contribution => contribution.Sequence)
            .ToArray());
    }

    public ContentId ModifierTrackId { get; }
    public int ResolvedStage { get; }
    public IReadOnlyList<RuntimeStatModifierContributionSnapshot> Contributions { get; }
}

public sealed class RuntimeStatModifierStateSnapshot
{
    public RuntimeStatModifierStateSnapshot(
        ContentId policyId,
        IEnumerable<RuntimeStatModifierTrackSnapshot>? tracks = null)
    {
        PolicyId = policyId;
        Tracks = Array.AsReadOnly((tracks ?? [])
            .OrderBy(track => track.ModifierTrackId.ToString(), StringComparer.Ordinal)
            .ToArray());
    }

    public ContentId PolicyId { get; }
    public IReadOnlyList<RuntimeStatModifierTrackSnapshot> Tracks { get; }

    public bool TryGetTrack(
        ContentId modifierTrackId,
        out RuntimeStatModifierTrackSnapshot? track)
    {
        track = Tracks.FirstOrDefault(candidate => candidate.ModifierTrackId == modifierTrackId);
        return track is not null;
    }
}

public sealed class StatModifierApplicationRequest
{
    public StatModifierApplicationRequest(
        RuntimeStatModifierStateSnapshot state,
        ContentId modifierTrackId,
        int stageDelta,
        DurationDefinition? duration = null,
        bool isActorDeployed = true,
        StatModifierLifecycleBoundary? activeLifecycleBoundary = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        ModifierTrackId = modifierTrackId;
        StageDelta = stageDelta;
        Duration = duration;
        IsActorDeployed = isActorDeployed;
        ActiveLifecycleBoundary = activeLifecycleBoundary;
    }

    public RuntimeStatModifierStateSnapshot State { get; }
    public ContentId ModifierTrackId { get; }
    public int StageDelta { get; }
    public DurationDefinition? Duration { get; }
    public bool IsActorDeployed { get; }
    public StatModifierLifecycleBoundary? ActiveLifecycleBoundary { get; }
}

public sealed class StatModifierTickRequest
{
    public StatModifierTickRequest(
        RuntimeStatModifierStateSnapshot state,
        StatModifierLifecycleBoundary lifecycleBoundary,
        bool isActorDeployed)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        LifecycleBoundary = lifecycleBoundary ?? throw new ArgumentNullException(nameof(lifecycleBoundary));
        IsActorDeployed = isActorDeployed;
    }

    public RuntimeStatModifierStateSnapshot State { get; }
    public StatModifierLifecycleBoundary LifecycleBoundary { get; }
    public bool IsActorDeployed { get; }
}

public sealed class StatModifierRemovalRequest
{
    public StatModifierRemovalRequest(
        RuntimeStatModifierStateSnapshot state,
        StatModifierRemovalMode mode,
        IEnumerable<ContentId>? modifierTrackIds = null,
        IEnumerable<long>? contributionSequences = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Mode = mode;
        ModifierTrackIds = Array.AsReadOnly((modifierTrackIds ?? [])
            .Distinct()
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToArray());
        ContributionSequences = Array.AsReadOnly((contributionSequences ?? [])
            .Distinct()
            .OrderBy(sequence => sequence)
            .ToArray());
    }

    public RuntimeStatModifierStateSnapshot State { get; }
    public StatModifierRemovalMode Mode { get; }
    public IReadOnlyList<ContentId> ModifierTrackIds { get; }
    public IReadOnlyList<long> ContributionSequences { get; }
}

public sealed class StatModifierCleanupRequest
{
    public StatModifierCleanupRequest(
        RuntimeStatModifierStateSnapshot state,
        StatModifierCleanupScope scope)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Scope = scope;
    }

    public RuntimeStatModifierStateSnapshot State { get; }
    public StatModifierCleanupScope Scope { get; }
}

public sealed class StatModifierEvent
{
    internal StatModifierEvent(
        StatModifierEventKind kind,
        ContentId modifierTrackId,
        int previousStage,
        int currentStage,
        long? contributionSequence = null,
        int? stageDelta = null,
        DurationDefinition? previousDuration = null,
        DurationDefinition? currentDuration = null,
        StatModifierLifecycleBoundary? previousLifecycleBoundary = null,
        StatModifierLifecycleBoundary? currentLifecycleBoundary = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!modifierTrackId.IsValid)
        {
            throw new ArgumentException("Modifier event track ID cannot be empty.", nameof(modifierTrackId));
        }

        if (contributionSequence is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contributionSequence),
                "Modifier event contribution sequence must be positive.");
        }

        Kind = kind;
        ModifierTrackId = modifierTrackId;
        PreviousStage = previousStage;
        CurrentStage = currentStage;
        ContributionSequence = contributionSequence;
        StageDelta = stageDelta;
        PreviousDuration = previousDuration;
        CurrentDuration = currentDuration;
        PreviousLifecycleBoundary = previousLifecycleBoundary;
        CurrentLifecycleBoundary = currentLifecycleBoundary;
    }

    public StatModifierEventKind Kind { get; }
    public ContentId ModifierTrackId { get; }
    public int PreviousStage { get; }
    public int CurrentStage { get; }
    public long? ContributionSequence { get; }
    public int? StageDelta { get; }
    public DurationDefinition? PreviousDuration { get; }
    public DurationDefinition? CurrentDuration { get; }
    public StatModifierLifecycleBoundary? PreviousLifecycleBoundary { get; }
    public StatModifierLifecycleBoundary? CurrentLifecycleBoundary { get; }
}

public sealed class StatModifierValidationResult
{
    public StatModifierValidationResult(IEnumerable<StatModifierDiagnostic>? diagnostics = null)
    {
        StatModifierDiagnostic[] snapshot = (diagnostics ?? []).ToArray();
        if (snapshot.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Stat-modifier diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        Diagnostics = Array.AsReadOnly(snapshot);
    }

    public bool IsValid => Diagnostics.Count == 0;
    public IReadOnlyList<StatModifierDiagnostic> Diagnostics { get; }

    public static StatModifierValidationResult Valid { get; } = new();
}

public sealed class StatModifierPolicyDecision
{
    private StatModifierPolicyDecision(
        bool accepted,
        RuntimeStatModifierStateSnapshot? after = null,
        IEnumerable<StatModifierDiagnostic>? diagnostics = null)
    {
        if (accepted && after is null)
        {
            throw new ArgumentException(
                "An accepted stat-modifier decision must provide resulting state.",
                nameof(after));
        }

        if (!accepted && after is not null)
        {
            throw new ArgumentException(
                "A rejected stat-modifier decision cannot provide resulting state.",
                nameof(after));
        }

        StatModifierDiagnostic[] snapshot = (diagnostics ?? []).ToArray();
        if (snapshot.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Stat-modifier diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        Accepted = accepted;
        After = after;
        Diagnostics = Array.AsReadOnly(snapshot);
    }

    public bool Accepted { get; }
    public RuntimeStatModifierStateSnapshot? After { get; }
    public IReadOnlyList<StatModifierDiagnostic> Diagnostics { get; }

    public static StatModifierPolicyDecision Accept(RuntimeStatModifierStateSnapshot after) =>
        new(true, after);

    public static StatModifierPolicyDecision Reject(
        StatModifierDiagnostic diagnostic,
        params StatModifierDiagnostic[] additionalDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(additionalDiagnostics);
        return new(false, diagnostics: [diagnostic, .. additionalDiagnostics]);
    }
}

public sealed class StatModifierTransitionResult
{
    internal StatModifierTransitionResult(
        StatModifierOperationKind operation,
        StatModifierTransitionCode code,
        RuntimeStatModifierStateSnapshot before,
        RuntimeStatModifierStateSnapshot after,
        IEnumerable<StatModifierDiagnostic>? diagnostics = null,
        IEnumerable<StatModifierEvent>? events = null)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        StatModifierDiagnostic[] diagnosticSnapshot = (diagnostics ?? []).ToArray();
        StatModifierEvent[] eventSnapshot = (events ?? []).ToArray();
        if (diagnosticSnapshot.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Stat-modifier diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        if (eventSnapshot.Any(@event => @event is null))
        {
            throw new ArgumentException("Stat-modifier events cannot contain null entries.", nameof(events));
        }

        Operation = operation;
        Code = code;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Diagnostics = Array.AsReadOnly(diagnosticSnapshot);
        Events = Array.AsReadOnly(eventSnapshot);
    }

    public StatModifierOperationKind Operation { get; }
    public StatModifierTransitionCode Code { get; }
    public bool Accepted => Code != StatModifierTransitionCode.Rejected;
    public bool StateChanged => Code == StatModifierTransitionCode.Applied;
    public RuntimeStatModifierStateSnapshot Before { get; }
    public RuntimeStatModifierStateSnapshot After { get; }
    public IReadOnlyList<StatModifierDiagnostic> Diagnostics { get; }
    public IReadOnlyList<StatModifierEvent> Events { get; }
}

public interface IStatModifierPolicy
{
    ContentId PolicyId { get; }

    StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state);

    StatModifierPolicyDecision Apply(StatModifierApplicationRequest request);

    StatModifierPolicyDecision Tick(StatModifierTickRequest request);

    StatModifierPolicyDecision Remove(StatModifierRemovalRequest request);

    StatModifierPolicyDecision Cleanup(StatModifierCleanupRequest request);
}

public interface IStatModifierPolicyService
{
    ContentId PolicyId { get; }

    StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state);

    StatModifierTransitionResult AssessApplication(StatModifierApplicationRequest request);

    StatModifierTransitionResult Apply(StatModifierApplicationRequest request);

    StatModifierTransitionResult Tick(StatModifierTickRequest request);

    StatModifierTransitionResult Remove(StatModifierRemovalRequest request);

    StatModifierTransitionResult Cleanup(StatModifierCleanupRequest request);
}

public sealed class StatModifierPolicyService : IStatModifierPolicyService
{
    private readonly IStatModifierPolicy _policy;

    public StatModifierPolicyService(IStatModifierPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (!policy.PolicyId.IsValid)
        {
            throw new ArgumentException("Stat-modifier policy ID cannot be empty.", nameof(policy));
        }
    }

    public ContentId PolicyId => _policy.PolicyId;

    public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        List<StatModifierDiagnostic> diagnostics = ValidateNeutralState(state);
        if (!state.PolicyId.IsValid)
        {
            return new StatModifierValidationResult(diagnostics);
        }

        if (state.PolicyId != PolicyId)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.PolicyMismatch,
                $"Modifier state belongs to policy '{state.PolicyId}', not selected policy '{PolicyId}'."));
            return new StatModifierValidationResult(diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            return new StatModifierValidationResult(diagnostics);
        }

        try
        {
            StatModifierValidationResult policyResult = _policy.ValidateState(state)
                ?? throw new InvalidOperationException("The stat-modifier policy returned null validation state.");
            diagnostics.AddRange(policyResult.Diagnostics);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            diagnostics.Add(PolicyFault(exception));
        }

        return new StatModifierValidationResult(diagnostics);
    }

    public StatModifierTransitionResult AssessApplication(StatModifierApplicationRequest request) =>
        EvaluateApplication(request);

    public StatModifierTransitionResult Apply(StatModifierApplicationRequest request) =>
        EvaluateApplication(request);

    public StatModifierTransitionResult Tick(StatModifierTickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<StatModifierDiagnostic> diagnostics = ValidateLifecycleBoundary(request.LifecycleBoundary);
        if (diagnostics.Count > 0)
        {
            return Rejected(StatModifierOperationKind.Tick, request.State, diagnostics);
        }

        return Evaluate(
            StatModifierOperationKind.Tick,
            request.State,
            () => _policy.Tick(request));
    }

    public StatModifierTransitionResult Remove(StatModifierRemovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<StatModifierDiagnostic> requestDiagnostics = ValidateRemovalRequest(request);
        if (requestDiagnostics.Count > 0)
        {
            return Rejected(StatModifierOperationKind.Removal, request.State, requestDiagnostics);
        }

        return Evaluate(
            StatModifierOperationKind.Removal,
            request.State,
            () => _policy.Remove(request));
    }

    public StatModifierTransitionResult Cleanup(StatModifierCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Scope))
        {
            return Rejected(
                StatModifierOperationKind.Cleanup,
                request.State,
                new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.InvalidCleanupScope,
                    $"Cleanup scope '{request.Scope}' is not defined."));
        }

        return Evaluate(
            StatModifierOperationKind.Cleanup,
            request.State,
            () => _policy.Cleanup(request));
    }

    private StatModifierTransitionResult EvaluateApplication(StatModifierApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<StatModifierDiagnostic>();
        if (!request.ModifierTrackId.IsValid)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidModifierTrackId,
                "Modifier track ID cannot be empty."));
        }

        if (request.StageDelta == 0)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidStageDelta,
                "A stat-modifier application must request a nonzero stage delta.",
                ValidTrackOrNull(request.ModifierTrackId)));
        }

        AddDurationDiagnostic(request.Duration, request.ModifierTrackId, null, diagnostics);
        AddLifecycleBoundaryDiagnostics(
            request.ActiveLifecycleBoundary,
            request.Duration,
            request.ModifierTrackId,
            null,
            diagnostics);
        if (diagnostics.Count > 0)
        {
            return Rejected(StatModifierOperationKind.Application, request.State, diagnostics);
        }

        return Evaluate(
            StatModifierOperationKind.Application,
            request.State,
            () => _policy.Apply(request));
    }

    private StatModifierTransitionResult Evaluate(
        StatModifierOperationKind operation,
        RuntimeStatModifierStateSnapshot before,
        Func<StatModifierPolicyDecision> evaluate)
    {
        StatModifierValidationResult beforeValidation = ValidateState(before);
        if (!beforeValidation.IsValid)
        {
            return Rejected(operation, before, beforeValidation.Diagnostics);
        }

        StatModifierPolicyDecision decision;
        try
        {
            decision = evaluate()
                ?? throw new InvalidOperationException("The stat-modifier policy returned a null decision.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Rejected(operation, before, PolicyFault(exception));
        }

        if (!decision.Accepted)
        {
            IReadOnlyList<StatModifierDiagnostic> diagnostics = decision.Diagnostics.Count > 0
                ? decision.Diagnostics
                : [new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.PolicyRejected,
                    $"Stat-modifier policy '{PolicyId}' rejected the {operation.ToString().ToLowerInvariant()} request.")];
            return Rejected(operation, before, diagnostics);
        }

        if (decision.Diagnostics.Count > 0 || decision.After is null)
        {
            return Rejected(
                operation,
                before,
                new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.InvalidPolicyResult,
                    "An accepted stat-modifier policy decision must provide state without rejection diagnostics."));
        }

        StatModifierValidationResult afterValidation = ValidateState(decision.After);
        if (!afterValidation.IsValid)
        {
            return Rejected(
                operation,
                before,
                [
                    new StatModifierDiagnostic(
                        StatModifierDiagnosticCode.InvalidPolicyResult,
                        "The stat-modifier policy returned invalid or incompatible state."),
                    .. afterValidation.Diagnostics
                ]);
        }

        bool stateChanged = !StatModifierStateEquality.Equals(before, decision.After);
        StatModifierEvent[] events = stateChanged
            ? StatModifierEventDiff.Create(operation, before, decision.After)
            : [];
        return new StatModifierTransitionResult(
            operation,
            stateChanged ? StatModifierTransitionCode.Applied : StatModifierTransitionCode.Unchanged,
            before,
            decision.After,
            events: events);
    }

    private static List<StatModifierDiagnostic> ValidateNeutralState(
        RuntimeStatModifierStateSnapshot state)
    {
        var diagnostics = new List<StatModifierDiagnostic>();
        if (!state.PolicyId.IsValid)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidPolicyId,
                "Modifier state policy ID cannot be empty."));
        }

        var tracks = new HashSet<ContentId>();
        var sequences = new HashSet<long>();
        foreach (RuntimeStatModifierTrackSnapshot track in state.Tracks)
        {
            if (!track.ModifierTrackId.IsValid)
            {
                diagnostics.Add(new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.InvalidModifierTrackId,
                    "Modifier state contains an empty track ID."));
            }
            else if (!tracks.Add(track.ModifierTrackId))
            {
                diagnostics.Add(new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.DuplicateModifierTrack,
                    $"Modifier track '{track.ModifierTrackId}' appears more than once.",
                    track.ModifierTrackId));
            }

            if (track.Contributions.Count == 0)
            {
                diagnostics.Add(new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.IncompatibleState,
                    $"Modifier track '{track.ModifierTrackId}' has no retained contributions.",
                    ValidTrackOrNull(track.ModifierTrackId)));
            }

            long rawStage = 0;
            foreach (RuntimeStatModifierContributionSnapshot contribution in track.Contributions)
            {
                if (contribution.Sequence <= 0)
                {
                    diagnostics.Add(new StatModifierDiagnostic(
                        StatModifierDiagnosticCode.InvalidContributionSequence,
                        "Modifier contribution sequence must be positive.",
                        ValidTrackOrNull(track.ModifierTrackId)));
                }
                else if (!sequences.Add(contribution.Sequence))
                {
                    diagnostics.Add(new StatModifierDiagnostic(
                        StatModifierDiagnosticCode.DuplicateContributionSequence,
                        $"Modifier contribution sequence '{contribution.Sequence}' appears more than once.",
                        ValidTrackOrNull(track.ModifierTrackId),
                        contribution.Sequence));
                }

                if (contribution.StageDelta == 0)
                {
                    diagnostics.Add(new StatModifierDiagnostic(
                        StatModifierDiagnosticCode.InvalidStageDelta,
                        "Retained stat-modifier contributions cannot have a zero stage delta.",
                        ValidTrackOrNull(track.ModifierTrackId),
                        ValidSequenceOrNull(contribution.Sequence)));
                }

                rawStage += contribution.StageDelta;
                AddDurationDiagnostic(
                    contribution.Duration,
                    track.ModifierTrackId,
                    contribution.Sequence,
                    diagnostics);
                AddLifecycleBoundaryDiagnostics(
                    contribution.LastLifecycleBoundary,
                    contribution.Duration,
                    track.ModifierTrackId,
                    contribution.Sequence,
                    diagnostics);
            }

            if (rawStage is < int.MinValue or > int.MaxValue)
            {
                diagnostics.Add(new StatModifierDiagnostic(
                    StatModifierDiagnosticCode.NumericOverflow,
                    $"Raw modifier contributions for track '{track.ModifierTrackId}' exceed the supported integer range.",
                    ValidTrackOrNull(track.ModifierTrackId)));
            }
        }

        return diagnostics;
    }

    private static List<StatModifierDiagnostic> ValidateRemovalRequest(
        StatModifierRemovalRequest request)
    {
        var diagnostics = new List<StatModifierDiagnostic>();
        if (!Enum.IsDefined(request.Mode))
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidRemovalRequest,
                $"Removal mode '{request.Mode}' is not defined."));
            return diagnostics;
        }

        if (request.ModifierTrackIds.Any(id => !id.IsValid))
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidRemovalRequest,
                "Selected modifier track IDs cannot be empty."));
        }

        if (request.ContributionSequences.Any(sequence => sequence <= 0))
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidRemovalRequest,
                "Selected contribution sequences must be positive."));
        }

        bool selectorsMatchMode = request.Mode switch
        {
            StatModifierRemovalMode.SelectedTracks =>
                request.ModifierTrackIds.Count > 0 && request.ContributionSequences.Count == 0,
            StatModifierRemovalMode.SelectedContributions =>
                request.ModifierTrackIds.Count == 0 && request.ContributionSequences.Count > 0,
            _ => request.ModifierTrackIds.Count == 0 && request.ContributionSequences.Count == 0
        };
        if (!selectorsMatchMode)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidRemovalRequest,
                "Removal selectors must be supplied only for their matching selected removal mode."));
        }

        return diagnostics;
    }

    private static void AddDurationDiagnostic(
        DurationDefinition? duration,
        ContentId modifierTrackId,
        long? contributionSequence,
        ICollection<StatModifierDiagnostic> diagnostics)
    {
        if (duration is null)
        {
            return;
        }

        bool valid = duration switch
        {
            InstantDurationDefinition instant => instant.Kind == DurationKind.Instant,
            TurnDurationDefinition turns => turns.Kind == DurationKind.Turns &&
                turns.Value > 0 &&
                turns.TickEventId.IsValid,
            PhaseDurationDefinition phase => phase.Kind == DurationKind.Phase && phase.PhaseId.IsValid,
            BattleDurationDefinition battle => battle.Kind == DurationKind.Battle,
            PermanentDurationDefinition permanent => permanent.Kind == DurationKind.Permanent,
            _ => false
        };
        if (!valid)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidDuration,
                $"Modifier duration '{duration.GetType().Name}' is not runtime-valid.",
                ValidTrackOrNull(modifierTrackId),
                ValidSequenceOrNull(contributionSequence)));
        }
    }

    private static List<StatModifierDiagnostic> ValidateLifecycleBoundary(
        StatModifierLifecycleBoundary boundary)
    {
        var diagnostics = new List<StatModifierDiagnostic>();
        if (!boundary.EventId.IsValid)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidLifecycleBoundary,
                "Stat-modifier lifecycle boundary event ID cannot be empty."));
        }

        if (boundary.Sequence <= 0)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidLifecycleBoundary,
                "Stat-modifier lifecycle boundary sequence must be positive."));
        }

        return diagnostics;
    }

    private static void AddLifecycleBoundaryDiagnostics(
        StatModifierLifecycleBoundary? boundary,
        DurationDefinition? duration,
        ContentId modifierTrackId,
        long? contributionSequence,
        ICollection<StatModifierDiagnostic> diagnostics)
    {
        if (boundary is null)
        {
            return;
        }

        bool validBoundary = boundary.EventId.IsValid && boundary.Sequence > 0;
        bool matchesDuration = duration is TurnDurationDefinition turns &&
            turns.TickEventId == boundary.EventId;
        if (!validBoundary || !matchesDuration)
        {
            diagnostics.Add(new StatModifierDiagnostic(
                StatModifierDiagnosticCode.InvalidLifecycleBoundary,
                "A lifecycle boundary must be valid and match its counted duration event.",
                ValidTrackOrNull(modifierTrackId),
                ValidSequenceOrNull(contributionSequence)));
        }
    }

    private static ContentId? ValidTrackOrNull(ContentId modifierTrackId) =>
        modifierTrackId.IsValid ? modifierTrackId : null;

    private static long? ValidSequenceOrNull(long? contributionSequence) =>
        contributionSequence is > 0 ? contributionSequence : null;

    private static StatModifierDiagnostic PolicyFault(Exception exception) =>
        new(
            StatModifierDiagnosticCode.PolicyFaulted,
            $"The stat-modifier policy faulted: {exception.GetType().Name}: {exception.Message}");

    private static StatModifierTransitionResult Rejected(
        StatModifierOperationKind operation,
        RuntimeStatModifierStateSnapshot before,
        params StatModifierDiagnostic[] diagnostics) =>
        Rejected(operation, before, (IEnumerable<StatModifierDiagnostic>)diagnostics);

    private static StatModifierTransitionResult Rejected(
        StatModifierOperationKind operation,
        RuntimeStatModifierStateSnapshot before,
        IEnumerable<StatModifierDiagnostic> diagnostics) =>
        new(
            operation,
            StatModifierTransitionCode.Rejected,
            before,
            before,
            diagnostics);
}

internal static class StatModifierStateEquality
{
    internal static bool Equals(
        RuntimeStatModifierStateSnapshot left,
        RuntimeStatModifierStateSnapshot right)
    {
        if (left.PolicyId != right.PolicyId || left.Tracks.Count != right.Tracks.Count)
        {
            return false;
        }

        for (int trackIndex = 0; trackIndex < left.Tracks.Count; trackIndex++)
        {
            RuntimeStatModifierTrackSnapshot leftTrack = left.Tracks[trackIndex];
            RuntimeStatModifierTrackSnapshot rightTrack = right.Tracks[trackIndex];
            if (leftTrack.ModifierTrackId != rightTrack.ModifierTrackId ||
                leftTrack.ResolvedStage != rightTrack.ResolvedStage ||
                leftTrack.Contributions.Count != rightTrack.Contributions.Count)
            {
                return false;
            }

            for (int contributionIndex = 0;
                 contributionIndex < leftTrack.Contributions.Count;
                 contributionIndex++)
            {
                RuntimeStatModifierContributionSnapshot leftContribution =
                    leftTrack.Contributions[contributionIndex];
                RuntimeStatModifierContributionSnapshot rightContribution =
                    rightTrack.Contributions[contributionIndex];
                if (leftContribution.Sequence != rightContribution.Sequence ||
                    leftContribution.StageDelta != rightContribution.StageDelta ||
                    !Equals(leftContribution.Duration, rightContribution.Duration) ||
                    !LifecycleBoundaryEquals(
                        leftContribution.LastLifecycleBoundary,
                        rightContribution.LastLifecycleBoundary))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool LifecycleBoundaryEquals(
        StatModifierLifecycleBoundary? left,
        StatModifierLifecycleBoundary? right) =>
        left is null
            ? right is null
            : right is not null &&
              left.EventId == right.EventId &&
              left.Sequence == right.Sequence;
}

internal static class StatModifierPolicyUtilities
{
    internal static bool TryAllocateSequence(
        RuntimeStatModifierStateSnapshot state,
        out long sequence)
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

    internal static StatModifierLifecycleBoundary? ResolveRestartBoundary(
        TurnDurationDefinition incomingDuration,
        StatModifierLifecycleBoundary? activeBoundary,
        RuntimeStatModifierContributionSnapshot? currentContribution)
    {
        if (activeBoundary is not null)
        {
            return activeBoundary;
        }

        return currentContribution?.Duration is TurnDurationDefinition currentDuration &&
               currentDuration.TickEventId == incomingDuration.TickEventId
            ? currentContribution.LastLifecycleBoundary
            : null;
    }

    internal static bool HasStaleActiveBoundary(
        RuntimeStatModifierStateSnapshot state,
        StatModifierLifecycleBoundary? activeBoundary) =>
        activeBoundary is not null &&
        state.Tracks
            .SelectMany(track => track.Contributions)
            .Any(contribution =>
                contribution.LastLifecycleBoundary is StatModifierLifecycleBoundary last &&
                last.EventId == activeBoundary.EventId &&
                activeBoundary.Sequence < last.Sequence);

    internal static bool IsAlreadyObserved(
        StatModifierLifecycleBoundary? previous,
        StatModifierLifecycleBoundary current) =>
        previous is not null &&
        previous.EventId == current.EventId &&
        previous.Sequence == current.Sequence;
}

internal static class StatModifierEventDiff
{
    internal static StatModifierEvent[] Create(
        StatModifierOperationKind operation,
        RuntimeStatModifierStateSnapshot before,
        RuntimeStatModifierStateSnapshot after)
    {
        Dictionary<ContentId, RuntimeStatModifierTrackSnapshot> beforeTracks =
            before.Tracks.ToDictionary(track => track.ModifierTrackId);
        Dictionary<ContentId, RuntimeStatModifierTrackSnapshot> afterTracks =
            after.Tracks.ToDictionary(track => track.ModifierTrackId);
        ContentId[] trackIds = beforeTracks.Keys
            .Concat(afterTracks.Keys)
            .Distinct()
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        var events = new List<StatModifierEvent>();

        foreach (ContentId trackId in trackIds)
        {
            beforeTracks.TryGetValue(trackId, out RuntimeStatModifierTrackSnapshot? beforeTrack);
            afterTracks.TryGetValue(trackId, out RuntimeStatModifierTrackSnapshot? afterTrack);
            int previousStage = beforeTrack?.ResolvedStage ?? 0;
            int currentStage = afterTrack?.ResolvedStage ?? 0;
            Dictionary<long, RuntimeStatModifierContributionSnapshot> beforeContributions =
                (beforeTrack?.Contributions ?? [])
                .ToDictionary(contribution => contribution.Sequence);
            Dictionary<long, RuntimeStatModifierContributionSnapshot> afterContributions =
                (afterTrack?.Contributions ?? [])
                .ToDictionary(contribution => contribution.Sequence);

            foreach (long sequence in beforeContributions.Keys
                         .Except(afterContributions.Keys)
                         .OrderBy(value => value))
            {
                RuntimeStatModifierContributionSnapshot contribution = beforeContributions[sequence];
                events.Add(new StatModifierEvent(
                    operation == StatModifierOperationKind.Tick
                        ? StatModifierEventKind.ContributionExpired
                        : StatModifierEventKind.ContributionRemoved,
                    trackId,
                    previousStage,
                    currentStage,
                    sequence,
                    contribution.StageDelta,
                    contribution.Duration,
                    previousLifecycleBoundary: contribution.LastLifecycleBoundary));
            }

            foreach (long sequence in afterContributions.Keys
                         .Except(beforeContributions.Keys)
                         .OrderBy(value => value))
            {
                RuntimeStatModifierContributionSnapshot contribution = afterContributions[sequence];
                events.Add(new StatModifierEvent(
                    StatModifierEventKind.ContributionAdded,
                    trackId,
                    previousStage,
                    currentStage,
                    sequence,
                    contribution.StageDelta,
                    currentDuration: contribution.Duration,
                    currentLifecycleBoundary: contribution.LastLifecycleBoundary));
            }

            foreach (long sequence in beforeContributions.Keys
                         .Intersect(afterContributions.Keys)
                         .OrderBy(value => value))
            {
                RuntimeStatModifierContributionSnapshot previous = beforeContributions[sequence];
                RuntimeStatModifierContributionSnapshot current = afterContributions[sequence];
                if (previous.StageDelta == current.StageDelta &&
                    Equals(previous.Duration, current.Duration) &&
                    LifecycleBoundaryEquals(
                        previous.LastLifecycleBoundary,
                        current.LastLifecycleBoundary))
                {
                    continue;
                }

                events.Add(new StatModifierEvent(
                    StatModifierEventKind.ContributionUpdated,
                    trackId,
                    previousStage,
                    currentStage,
                    sequence,
                    current.StageDelta,
                    previous.Duration,
                    current.Duration,
                    previous.LastLifecycleBoundary,
                    current.LastLifecycleBoundary));
            }

            if (previousStage != currentStage)
            {
                events.Add(new StatModifierEvent(
                    StatModifierEventKind.AggregateStageChanged,
                    trackId,
                    previousStage,
                    currentStage));
            }

            if (beforeTrack is not null && afterTrack is null)
            {
                events.Add(new StatModifierEvent(
                    StatModifierEventKind.TrackRemoved,
                    trackId,
                    previousStage,
                    0));
            }
        }

        return events.ToArray();
    }

    private static bool LifecycleBoundaryEquals(
        StatModifierLifecycleBoundary? left,
        StatModifierLifecycleBoundary? right) =>
        left is null
            ? right is null
            : right is not null &&
              left.EventId == right.EventId &&
              left.Sequence == right.Sequence;
}
