using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexFusionResultEvidence(
    string ScenarioId,
    RuntimeInstanceId FirstParentInstanceId,
    ContentId FirstParentEntityId,
    RuntimeInstanceId SecondParentInstanceId,
    ContentId SecondParentEntityId,
    FusionRuntimeOperation Operation,
    ContentId? ResultEntityId,
    bool IsAccident,
    IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics);

internal sealed class TrainingAnnexFusionController
{
    private readonly IHostEventSink<string> _eventSink;

    public TrainingAnnexFusionController(IHostEventSink<string> eventSink)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public async ValueTask<IReadOnlyList<TrainingAnnexFusionResultEvidence>> CalculateAsync(
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(roster);

        var repository = new CatalogFusionContentRepository(catalog);
        var resolver = new FusionResultResolver(repository, new TrainingAnnexFusionRandomSource());
        TrainingAnnexRuntimeActor ashling = FindActor(roster, TrainingAnnexHostSupport.DemonAshlingInstance);
        TrainingAnnexRuntimeActor bramble = FindActor(roster, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance);

        TrainingAnnexFusionResultEvidence direct = await ResolveAsync(
            catalog,
            resolver,
            "direct_entity_result",
            ashling,
            bramble,
            cancellationToken).ConfigureAwait(false);
        TrainingAnnexFusionResultEvidence rank = await ResolveAsync(
            catalog,
            resolver,
            "race_rank_offset_result",
            roster.Player,
            bramble,
            cancellationToken).ConfigureAwait(false);

        return Array.AsReadOnly([direct, rank]);
    }

    private async ValueTask<TrainingAnnexFusionResultEvidence> ResolveAsync(
        GameDataCatalog catalog,
        IFusionResultResolver resolver,
        string scenarioId,
        TrainingAnnexRuntimeActor first,
        TrainingAnnexRuntimeActor second,
        CancellationToken cancellationToken)
    {
        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            ToFusionParticipant(first),
            ToFusionParticipant(second),
            MoonPhase: 0));
        var evidence = new TrainingAnnexFusionResultEvidence(
            scenarioId,
            first.Actor.State.InstanceId,
            first.Actor.Entity.Id,
            second.Actor.State.InstanceId,
            second.Actor.Entity.Id,
            result.Operation,
            result.ResultEntityId,
            result.IsAccident,
            result.Diagnostics);

        await _eventSink.PublishAsync(
            FormatResult(catalog, first, second, evidence),
            cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private static FusionParticipantSnapshot ToFusionParticipant(TrainingAnnexRuntimeActor actor) =>
        new(
            actor.Actor.State.InstanceId,
            actor.Actor.Entity.Id,
            actor.Actor.Entity.DisplayName,
            actor.Actor.Entity.RaceId,
            actor.Actor.Entity.Rank,
            actor.Level,
            actor.Actor.SkillLoadout.Select(skill => skill.Id),
            actor.Actor.Entity.Stats);

    private static string FormatResult(
        GameDataCatalog catalog,
        TrainingAnnexRuntimeActor first,
        TrainingAnnexRuntimeActor second,
        TrainingAnnexFusionResultEvidence evidence)
    {
        if (evidence.ResultEntityId is ContentId resultId &&
            catalog.TryGetEntity(resultId, out EntityDefinition? resultEntity) &&
            resultEntity is not null)
        {
            return "Fusion result: "
                + $"{first.Actor.Entity.DisplayName} + {second.Actor.Entity.DisplayName} -> "
                + $"{resultEntity.DisplayName} ({FormatOperation(evidence.Operation)}; {evidence.ScenarioId}).";
        }

        string diagnostics = evidence.Diagnostics.Count == 0
            ? "no result"
            : string.Join(", ", evidence.Diagnostics.Select(diagnostic => diagnostic.Code.ToString()));
        return "Fusion result: "
            + $"{first.Actor.Entity.DisplayName} + {second.Actor.Entity.DisplayName} failed "
            + $"({evidence.ScenarioId}; {diagnostics}).";
    }

    private static string FormatOperation(FusionRuntimeOperation operation) =>
        operation switch
        {
            FusionRuntimeOperation.CreateNewEntity => "create_entity",
            FusionRuntimeOperation.RankUpParent => "rank_up",
            FusionRuntimeOperation.RankDownParent => "rank_down",
            FusionRuntimeOperation.StatBoost => "stat_boost",
            _ => "no_fusion"
        };

    private static TrainingAnnexRuntimeActor FindActor(
        TrainingAnnexActorRoster roster,
        RuntimeInstanceId instanceId) =>
        roster.AllActors.First(actor => actor.Actor.State.InstanceId == instanceId);

    private sealed class TrainingAnnexFusionRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => maximumExclusive - 1;
        public decimal NextUnitDecimal() => 0.99m;
    }
}
