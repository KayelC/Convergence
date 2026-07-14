using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class EncounterPreparationServiceTests
{
    [Fact]
    public void Prepare_HydratesTheExplicitFormationInAuthoredOrder()
    {
        GameDataCatalog catalog = Catalog(
            [Entity("ashling")],
            Encounter("drill", new EncounterMemberDefinition(Qualified("ashling"), 2, 2)));
        var service = Service(catalog);

        EncounterPreparationResult result = service.Prepare(new RuntimeEncounterTriggerRequest(
            Id("placed_enemy_01"),
            Qualified("drill"),
            Id("enemy_team"),
            RuntimeInstanceId.Parse("placed_enemy_01")));

        Assert.True(result.IsSuccess);
        PreparedEncounter prepared = result.RequirePreparedEncounter();
        Assert.Equal(Id("placed_enemy_01"), prepared.TriggerId);
        Assert.Equal(Qualified("drill"), prepared.Encounter.Id);
        Assert.Equal(
            [RuntimeInstanceId.Parse("placed_enemy_01_ashling_1"), RuntimeInstanceId.Parse("placed_enemy_01_ashling_2")],
            prepared.Actors.Select(actor => actor.State.InstanceId));
        Assert.All(prepared.Actors, actor => Assert.Equal(Qualified("ashling"), actor.Entity.Id));
        Assert.Equal(
            [
                EncounterPreparationEventKind.TriggerReceived,
                EncounterPreparationEventKind.ActorPrepared,
                EncounterPreparationEventKind.ActorPrepared,
                EncounterPreparationEventKind.EncounterPrepared
            ],
            result.Events.Select(entry => entry.Kind));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CatalogBattleActor>)prepared.Actors).Add(prepared.Actors[0]));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EncounterPreparationEvent>)result.Events).Add(result.Events[0]));
    }

    [Fact]
    public void Prepare_UsesTheHostTriggerAndFormationSelectionWithoutNavigationState()
    {
        var first = new EncounterFormationDefinition(
            1,
            false,
            [new EncounterMemberDefinition(Qualified("ashling"), 2)]);
        var second = new EncounterFormationDefinition(
            1,
            true,
            [new EncounterMemberDefinition(Qualified("sentinel"), 5)]);
        GameDataCatalog catalog = Catalog(
            [Entity("ashling"), Entity("sentinel")],
            new EncounterDefinition(
                Qualified("choice"),
                "Choice",
                "Two explicit formations.",
                formations: [first, second]));
        var service = Service(catalog);

        PreparedEncounter prepared = service.Prepare(new RuntimeEncounterTriggerRequest(
            Id("boss_scene_trigger"),
            Qualified("choice"),
            Id("enemy_team"),
            RuntimeInstanceId.Parse("boss_scene"),
            FormationIndex: 1)).RequirePreparedEncounter();

        Assert.True(prepared.Formation.IsBoss);
        CatalogBattleActor actor = Assert.Single(prepared.Actors);
        Assert.Equal(Qualified("sentinel"), actor.Entity.Id);
        Assert.Equal(25, actor.State.GetRequiredResource(Id("hp")).Maximum);
    }

    [Fact]
    public void Prepare_ReportsPlanningFailureWithoutHydratingActors()
    {
        GameDataCatalog catalog = Catalog([Entity("ashling")], Encounter("drill"));

        EncounterPreparationResult result = Service(catalog).Prepare(new RuntimeEncounterTriggerRequest(
            Id("missing_trigger"),
            Qualified("missing"),
            Id("enemy_team"),
            RuntimeInstanceId.Parse("missing_trigger")));

        Assert.False(result.IsSuccess);
        Assert.Null(result.PreparedEncounter);
        EncounterPreparationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(EncounterPreparationDiagnosticCode.StartPlanningFailed, diagnostic.Code);
        Assert.Equal(EncounterStartDiagnosticCode.EncounterMissing, diagnostic.StartPlanningCode);
        Assert.Equal(
            [EncounterPreparationEventKind.TriggerReceived, EncounterPreparationEventKind.EncounterRejected],
            result.Events.Select(entry => entry.Kind));
        Assert.Throws<EncounterPreparationException>(() => result.RequirePreparedEncounter());
    }

    [Fact]
    public void Prepare_RejectsTheWholeEncounterWhenAnyActorCannotBeHydrated()
    {
        GameDataCatalog catalog = Catalog(
            [Entity("ashling")],
            Encounter(
                "mixed",
                new EncounterMemberDefinition(Qualified("ashling"), 2),
                new EncounterMemberDefinition(Qualified("missing_entity"), 2)));

        EncounterPreparationResult result = Service(catalog).Prepare(new RuntimeEncounterTriggerRequest(
            Id("mixed_trigger"),
            Qualified("mixed"),
            Id("enemy_team"),
            RuntimeInstanceId.Parse("mixed_trigger")));

        Assert.False(result.IsSuccess);
        Assert.Null(result.PreparedEncounter);
        EncounterPreparationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(EncounterPreparationDiagnosticCode.ActorCreationFailed, diagnostic.Code);
        Assert.Equal(CatalogBattleActorDiagnosticCode.EntityMissing, diagnostic.ActorCreationCode);
        Assert.Equal(
            [EncounterPreparationEventKind.TriggerReceived, EncounterPreparationEventKind.EncounterRejected],
            result.Events.Select(entry => entry.Kind));
    }

    [Fact]
    public void EncounterPlanner_RejectsDefaultIdsBeforeCatalogLookupOrInstanceGeneration()
    {
        GameDataCatalog catalog = Catalog([Entity("ashling")], Encounter("drill"));
        var planner = new CatalogEncounterStartPlanner(catalog);

        EncounterStartPlanResult result = planner.Plan(new EncounterStartRequest(
            default,
            default,
            default));

        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.Equal(EncounterStartDiagnosticCode.IdentifierInvalid, diagnostic.Code));
    }

    private static CatalogEncounterPreparationService Service(GameDataCatalog catalog) =>
        new(
            new CatalogEncounterStartPlanner(catalog),
            new CatalogBattleActorFactory(catalog, catalog, new TestInitializationPolicy()));

    private static GameDataCatalog Catalog(
        IEnumerable<EntityDefinition> entities,
        EncounterDefinition encounter) =>
        new(
            skills: [],
            entities: entities.Select(entity => KeyValuePair.Create(entity.Id, entity)),
            races: [],
            ailments: [],
            items: [],
            encounters: [KeyValuePair.Create(encounter.Id, encounter)]);

    private static EntityDefinition Entity(string localId) =>
        new(
            Qualified(localId),
            localId,
            "Test encounter entity.",
            Id("demon"),
            Qualified("test_race"),
            rank: 1,
            baseLevel: 1,
            capabilities: new EntityCapabilitiesDefinition(false, false, false),
            inheritanceRules: new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            stats: [KeyValuePair.Create(Id("vitality"), 2)]);

    private static EncounterDefinition Encounter(
        string localId,
        params EncounterMemberDefinition[] members) =>
        new(
            Qualified(localId),
            localId,
            "Test encounter.",
            formations:
            [
                new EncounterFormationDefinition(
                    1,
                    false,
                    members.Length == 0
                        ? [new EncounterMemberDefinition(Qualified("ashling"), 2)]
                        : members)
            ]);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static ContentId Qualified(string value) => ContentId.Parse($"sample.pack:{value}");

    private sealed class TestInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            new(Id("hp"), [new BattleResourceState(Id("hp"), 20 + level, 20 + level)]);
    }
}
