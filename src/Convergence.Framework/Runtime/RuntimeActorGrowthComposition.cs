using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeActorGrowthCompositionStatus
{
    Applied,
    GrowthRejected,
    CombatProfileCompositionRejected,
    CommitRejected
}

public enum RuntimeActorGrowthCompositionDiagnosticCode
{
    GrowthRejected,
    CombatProfileCompositionRejected,
    CommitFailed
}

public sealed record RuntimeActorGrowthCompositionDiagnostic(
    RuntimeActorGrowthCompositionDiagnosticCode Code,
    string Message,
    string Path);

public sealed record RuntimeActorGrowthCompositionRequest
{
    public RuntimeActorGrowthCompositionRequest(
        LevelGrowthResult growth,
        RuntimeActorCombatProfileCompositionRequest combatProfileComposition)
    {
        Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        CombatProfileComposition = combatProfileComposition ??
            throw new ArgumentNullException(nameof(combatProfileComposition));
    }

    public LevelGrowthResult Growth { get; }
    public RuntimeActorCombatProfileCompositionRequest CombatProfileComposition { get; }
}

public sealed record RuntimeActorGrowthCompositionResult
{
    public RuntimeActorGrowthCompositionResult(
        RuntimeActorGrowthCompositionStatus status,
        RuntimeActorSnapshot before,
        RuntimeActorSnapshot after,
        RuntimeMutationResult growthMutation,
        RuntimeActorCombatProfileCompositionResult? combatProfileComposition = null,
        IEnumerable<RuntimeActorGrowthCompositionDiagnostic>? diagnostics = null)
    {
        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        GrowthMutation = growthMutation ?? throw new ArgumentNullException(nameof(growthMutation));
        CombatProfileComposition = combatProfileComposition;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeActorGrowthCompositionStatus Status { get; }
    public bool Applied => Status == RuntimeActorGrowthCompositionStatus.Applied;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public RuntimeMutationResult GrowthMutation { get; }
    public RuntimeActorCombatProfileCompositionResult? CombatProfileComposition { get; }
    public IReadOnlyList<RuntimeActorGrowthCompositionDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorGrowthCompositionService
{
    RuntimeActorGrowthCompositionResult Apply(RuntimeActorGrowthCompositionRequest request);
}

public sealed class RuntimeActorGrowthCompositionService : IRuntimeActorGrowthCompositionService
{
    private readonly IRuntimeActorCombatProfileCompositionService _combatProfileComposition;
    private readonly RuntimeProgressionTransactionService _progression = new();

    public RuntimeActorGrowthCompositionService(
        IRuntimeActorCombatProfileCompositionService combatProfileComposition)
    {
        _combatProfileComposition = combatProfileComposition ??
            throw new ArgumentNullException(nameof(combatProfileComposition));
    }

    public RuntimeActorGrowthCompositionResult Apply(RuntimeActorGrowthCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeActorState actor = request.CombatProfileComposition.Actor;
        RuntimeActorSnapshot before = actor.ToSnapshot();
        RuntimeActorState stagedActor = actor.CreateExecutionClone();
        RuntimeMutationResult growthMutation;
        try
        {
            growthMutation = _progression.ApplyLevelGrowth(stagedActor, request.Growth);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            growthMutation = new RuntimeMutationResult(
                RuntimeMutationStatus.Rejected,
                before,
                before,
                [
                    new RuntimeMutationDiagnostic(
                        RuntimeMutationErrorCode.ProgressionMutationRejected,
                        $"Growth could not be staged: {exception.Message}",
                        "$.progression")
                ]);
        }
        if (!growthMutation.Applied)
        {
            return RejectedGrowth(before, growthMutation);
        }

        RuntimeActorCombatProfileCompositionRequest compositionRequest =
            request.CombatProfileComposition;
        RuntimeActorState[] stagedRuntimeActors = compositionRequest.RuntimeActors
            .Select(candidate => candidate.InstanceId == actor.InstanceId ? stagedActor : candidate)
            .ToArray();
        var stagedCompositionRequest = new RuntimeActorCombatProfileCompositionRequest(
            stagedActor,
            compositionRequest.SourceKind,
            compositionRequest.MissingHostedEntityBehavior,
            compositionRequest.PartyRoster,
            stagedRuntimeActors,
            compositionRequest.EquipmentStatModifiers);
        RuntimeActorCombatProfileCompositionResult composition =
            _combatProfileComposition.Compose(stagedCompositionRequest);
        if (!composition.Applied)
        {
            return RejectedComposition(before, growthMutation, composition);
        }

        try
        {
            actor.ApplyExecutionStateFrom(stagedActor);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new RuntimeActorGrowthCompositionResult(
                RuntimeActorGrowthCompositionStatus.CommitRejected,
                before,
                before,
                growthMutation,
                composition,
                [
                    new RuntimeActorGrowthCompositionDiagnostic(
                        RuntimeActorGrowthCompositionDiagnosticCode.CommitFailed,
                        $"Composed growth could not be committed: {exception.Message}",
                        "$")
                ]);
        }

        return new RuntimeActorGrowthCompositionResult(
            RuntimeActorGrowthCompositionStatus.Applied,
            before,
            actor.ToSnapshot(),
            growthMutation,
            composition);
    }

    private static RuntimeActorGrowthCompositionResult RejectedGrowth(
        RuntimeActorSnapshot before,
        RuntimeMutationResult growthMutation) =>
        new(
            RuntimeActorGrowthCompositionStatus.GrowthRejected,
            before,
            before,
            growthMutation,
            diagnostics: growthMutation.Diagnostics.Select(diagnostic =>
                new RuntimeActorGrowthCompositionDiagnostic(
                    RuntimeActorGrowthCompositionDiagnosticCode.GrowthRejected,
                    diagnostic.Message,
                    diagnostic.Path ?? "$.progression")));

    private static RuntimeActorGrowthCompositionResult RejectedComposition(
        RuntimeActorSnapshot before,
        RuntimeMutationResult growthMutation,
        RuntimeActorCombatProfileCompositionResult composition) =>
        new(
            RuntimeActorGrowthCompositionStatus.CombatProfileCompositionRejected,
            before,
            before,
            growthMutation,
            composition,
            composition.Diagnostics.Select(diagnostic =>
                new RuntimeActorGrowthCompositionDiagnostic(
                    RuntimeActorGrowthCompositionDiagnosticCode.CombatProfileCompositionRejected,
                    diagnostic.Message,
                    diagnostic.StatId is { } statId
                        ? $"$.stats.effectiveStats['{statId}']"
                        : "$.stats")));
}
