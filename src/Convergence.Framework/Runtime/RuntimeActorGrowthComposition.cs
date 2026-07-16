using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeActorGrowthCompositionStatus
{
    Applied,
    GrowthRejected,
    StatCompositionRejected,
    CommitRejected
}

public enum RuntimeActorGrowthCompositionDiagnosticCode
{
    GrowthRejected,
    StatCompositionRejected,
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
        RuntimeActorStatCompositionRequest statComposition)
    {
        Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        StatComposition = statComposition ?? throw new ArgumentNullException(nameof(statComposition));
    }

    public LevelGrowthResult Growth { get; }
    public RuntimeActorStatCompositionRequest StatComposition { get; }
}

public sealed record RuntimeActorGrowthCompositionResult
{
    public RuntimeActorGrowthCompositionResult(
        RuntimeActorGrowthCompositionStatus status,
        RuntimeActorSnapshot before,
        RuntimeActorSnapshot after,
        RuntimeMutationResult growthMutation,
        RuntimeActorStatCompositionResult? statComposition = null,
        IEnumerable<RuntimeActorGrowthCompositionDiagnostic>? diagnostics = null)
    {
        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        GrowthMutation = growthMutation ?? throw new ArgumentNullException(nameof(growthMutation));
        StatComposition = statComposition;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeActorGrowthCompositionStatus Status { get; }
    public bool Applied => Status == RuntimeActorGrowthCompositionStatus.Applied;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public RuntimeMutationResult GrowthMutation { get; }
    public RuntimeActorStatCompositionResult? StatComposition { get; }
    public IReadOnlyList<RuntimeActorGrowthCompositionDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorGrowthCompositionService
{
    RuntimeActorGrowthCompositionResult Apply(RuntimeActorGrowthCompositionRequest request);
}

public sealed class RuntimeActorGrowthCompositionService : IRuntimeActorGrowthCompositionService
{
    private readonly IRuntimeActorStatCompositionService _statComposition;
    private readonly RuntimeProgressionTransactionService _progression = new();

    public RuntimeActorGrowthCompositionService(
        IRuntimeActorStatCompositionService? statComposition = null)
    {
        _statComposition = statComposition ?? new RuntimeActorStatCompositionService();
    }

    public RuntimeActorGrowthCompositionResult Apply(RuntimeActorGrowthCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeActorState actor = request.StatComposition.Actor;
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

        RuntimeActorStatCompositionRequest compositionRequest = request.StatComposition;
        var stagedCompositionRequest = new RuntimeActorStatCompositionRequest(
            stagedActor,
            compositionRequest.SourceKind,
            compositionRequest.MissingHostedEntityBehavior,
            compositionRequest.ActiveHostedEntity,
            compositionRequest.PartyRoster,
            compositionRequest.EquipmentStatModifiers);
        RuntimeActorStatCompositionResult composition = _statComposition.Compose(
            stagedCompositionRequest);
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
        RuntimeActorStatCompositionResult composition) =>
        new(
            RuntimeActorGrowthCompositionStatus.StatCompositionRejected,
            before,
            before,
            growthMutation,
            composition,
            composition.Diagnostics.Select(diagnostic =>
                new RuntimeActorGrowthCompositionDiagnostic(
                    RuntimeActorGrowthCompositionDiagnosticCode.StatCompositionRejected,
                    diagnostic.Message,
                    diagnostic.StatId is { } statId
                        ? $"$.stats.effectiveStats['{statId}']"
                        : "$.stats")));
}
