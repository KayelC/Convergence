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

internal sealed record TrainingAnnexFusionPlanningEvidence(
    string ScenarioId,
    ContentId? ResultEntityId,
    int MaximumInheritanceSlots,
    int SacrificialMaximumInheritanceSlots,
    IReadOnlyList<ContentId> NaturalSkillIds,
    IReadOnlyList<ContentId> PickableSkillIds,
    IReadOnlyList<FusionInheritanceEntry> DisplaySkills,
    IReadOnlyList<ContentId> AccidentInheritedSkillIds,
    ContentId MutationSourceSkillId,
    ContentId MutationResultSkillId);

internal sealed record TrainingAnnexFusionCalculationResult(
    IReadOnlyList<TrainingAnnexFusionResultEvidence> Results,
    IReadOnlyList<TrainingAnnexFusionPlanningEvidence> Planning);

internal sealed class TrainingAnnexFusionController
{
    private readonly IHostEventSink<string> _eventSink;

    public TrainingAnnexFusionController(IHostEventSink<string> eventSink)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public async ValueTask<TrainingAnnexFusionCalculationResult> CalculateAsync(
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

        TrainingAnnexFusionPlanningEvidence planning = await PlanAsync(
            catalog,
            repository,
            roster.Player,
            bramble,
            ashling,
            cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexFusionCalculationResult(
            Array.AsReadOnly([direct, rank]),
            Array.AsReadOnly([planning]));
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

    private async ValueTask<TrainingAnnexFusionPlanningEvidence> PlanAsync(
        GameDataCatalog catalog,
        IFusionContentRepository repository,
        TrainingAnnexRuntimeActor first,
        TrainingAnnexRuntimeActor second,
        TrainingAnnexRuntimeActor sacrifice,
        CancellationToken cancellationToken)
    {
        var resolver = new FusionResultResolver(repository, new TrainingAnnexFusionRandomSource());
        var planner = new FusionPlanningService(
            repository,
            resolver,
            new TrainingAnnexFusionAccidentRandomSource());
        FusionParticipantSnapshot firstParent = ToFusionParticipant(first);
        FusionParticipantSnapshot secondParent = ToFusionParticipant(second);
        FusionParticipantSnapshot sacrificeParent = ToFusionParticipant(sacrifice);

        FusionPlanningResult basePlan = planner.CreatePlan(new FusionPlanningRequest(
            firstParent,
            secondParent,
            Sacrifice: null,
            IsSacrificial: false,
            MoonPhase: 0));
        FusionPlanningResult sacrificialPlan = planner.CreatePlan(new FusionPlanningRequest(
            firstParent,
            secondParent,
            sacrificeParent,
            IsSacrificial: true,
            MoonPhase: 0));

        IReadOnlyList<ContentId> accidentInheritedSkillIds =
            planner.CreateAccidentInheritance([TrainingAnnexHostSupport.EchoStrike], maximumSlots: 1);
        ContentId mutationResult = accidentInheritedSkillIds.Count == 0
            ? TrainingAnnexHostSupport.EchoStrike
            : accidentInheritedSkillIds[0];
        var evidence = new TrainingAnnexFusionPlanningEvidence(
            "inheritance_slots_mutation_accident",
            basePlan.ResultEntity?.Id,
            basePlan.MaximumInheritanceSlots,
            sacrificialPlan.MaximumInheritanceSlots,
            basePlan.NaturalSkillIds,
            basePlan.PickableSkillIds,
            basePlan.DisplaySkills,
            accidentInheritedSkillIds,
            TrainingAnnexHostSupport.EchoStrike,
            mutationResult);

        await _eventSink.PublishAsync(
            FormatPlanning(catalog, evidence),
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

    private static string FormatPlanning(
        GameDataCatalog catalog,
        TrainingAnnexFusionPlanningEvidence evidence)
    {
        string resultName = evidence.ResultEntityId is ContentId resultId &&
            catalog.TryGetEntity(resultId, out EntityDefinition? resultEntity) &&
            resultEntity is not null
                ? resultEntity.DisplayName
                : "no result";
        string pickable = FormatSkillNames(catalog, evidence.PickableSkillIds);
        string blocked = FormatBlockedSkills(catalog, evidence.DisplaySkills);
        string mutationSource = SkillName(catalog, evidence.MutationSourceSkillId);
        string mutationResult = SkillName(catalog, evidence.MutationResultSkillId);
        return "Fusion planning: "
            + $"{resultName}; slots {evidence.MaximumInheritanceSlots}, "
            + $"sacrificial slots {evidence.SacrificialMaximumInheritanceSlots}; "
            + $"pickable {pickable}; blocked {blocked}; "
            + $"accident sample {mutationSource} -> {mutationResult}.";
    }

    private static string FormatSkillNames(GameDataCatalog catalog, IReadOnlyList<ContentId> skillIds) =>
        skillIds.Count == 0
            ? "none"
            : string.Join(", ", skillIds.Select(id => SkillName(catalog, id)));

    private static string FormatBlockedSkills(
        GameDataCatalog catalog,
        IReadOnlyList<FusionInheritanceEntry> displaySkills)
    {
        string[] blocked = displaySkills
            .Where(entry => !entry.IsSelectable)
            .Select(entry => $"{SkillName(catalog, entry.SkillId)}:{entry.ReasonCode}")
            .ToArray();
        return blocked.Length == 0 ? "none" : string.Join(", ", blocked);
    }

    private static string SkillName(GameDataCatalog catalog, ContentId skillId) =>
        catalog.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null
            ? skill.DisplayName
            : skillId.ToString();

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

    private sealed class TrainingAnnexFusionAccidentRandomSource : IRandomSource
    {
        private readonly Queue<int> _values = new([0, 0, 0]);

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = _values.Count == 0 ? minimumInclusive : _values.Dequeue();
            return Math.Clamp(value, minimumInclusive, maximumExclusive - 1);
        }

        public decimal NextUnitDecimal() => 0m;
    }
}
