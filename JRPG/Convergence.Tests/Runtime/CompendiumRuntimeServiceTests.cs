using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class CompendiumRuntimeServiceTests
{
    [Fact]
    public void RegisterActor_CapturesAnImmutableCatalogIdentifiedSnapshot()
    {
        TestContext context = CreateContext();
        CatalogBattleActor actor = context.CreateActor("owned_ashling");
        var service = context.CreateService();

        CompendiumActorRegistrationResult result = service.RegisterActor(
            new CompendiumStateSnapshot(),
            actor.State.ToSnapshot());

        Assert.True(result.Applied);
        Assert.Equal(CompendiumRegistrationCode.Added, result.Code);
        CompendiumEntrySnapshot entry = Assert.Single(result.After.Entries);
        Assert.Equal(context.Entity.Id, entry.EntityId);
        Assert.Equal(context.Entity.Id, entry.SpeciesId);
        Assert.Equal(actor.State.Progression.Level, entry.Level);
        Assert.Equal(actor.State.Progression.UnspentStatPoints, entry.UnspentStatPoints);
        Assert.Equal(actor.State.ToSnapshot().Skills.LearnedSkillIds, entry.SkillIds);
        Assert.Equal(actor.State.ToSnapshot().Skills.EquippedSkillIds, entry.EquippedSkillIds);

        RuntimeMutationResult mutation = new RuntimeResourceTransactionService().AddResource(
            actor.State,
            Id("hp"),
            -49);
        Assert.True(mutation.Applied);
        Assert.Equal(5, entry.Stats[Id("vitality")]);
        Assert.Equal(CompendiumRegistrationCode.Added, result.Code);
    }

    [Fact]
    public void RegisterActor_RejectsMissingOrIneligibleCatalogEntitiesWithoutMutation()
    {
        TestContext eligible = CreateContext();
        CatalogBattleActor actor = eligible.CreateActor("owned_ashling");
        var missingService = new CompendiumRuntimeService(
            new EmptyEntityRepository(),
            eligible.ActorFactory,
            new StandardResourceGrowthPolicy());
        CompendiumStateSnapshot state = new();

        CompendiumActorRegistrationResult missing = missingService.RegisterActor(state, actor.State.ToSnapshot());

        Assert.False(missing.Applied);
        Assert.Same(state, missing.After);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.EntityMissing, Assert.Single(missing.Diagnostics).Code);

        TestContext ineligible = CreateContext(compendiumEligible: false);
        CompendiumActorRegistrationResult rejected = ineligible.CreateService().RegisterActor(
            state,
            ineligible.CreateActor("ineligible_actor").State.ToSnapshot());
        Assert.False(rejected.Applied);
        Assert.Same(state, rejected.After);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.EntityNotEligible, Assert.Single(rejected.Diagnostics).Code);
    }

    [Fact]
    public void Recall_IsAtomicAndRestoresRegisteredProgressionStatsSkillsAndFullResources()
    {
        TestContext context = CreateContext();
        var service = context.CreateService();
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        RuntimeActorSnapshot sourceSnapshot = source.State.ToSnapshot();
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            sourceSnapshot).After;
        RuntimePartyStockSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallStockKind.Demon));

        Assert.True(result.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.Applied, result.Code);
        Assert.Equal(wallet.Macca - result.Cost, result.AfterWallet.Macca);
        RuntimeActorReferenceSnapshot stockEntry = Assert.Single(result.AfterPartyStock.DemonStock);
        Assert.Equal(RuntimeInstanceId.Parse("recalled_ashling"), stockEntry.InstanceId);
        CatalogBattleActor recalled = Assert.IsType<CatalogBattleActor>(result.Actor);
        RuntimeActorSnapshot recalledSnapshot = recalled.State.ToSnapshot();
        Assert.Equal(sourceSnapshot.Progression, recalledSnapshot.Progression);
        Assert.Equal(sourceSnapshot.Stats.BaseStats, recalledSnapshot.Stats.BaseStats);
        Assert.Equal(sourceSnapshot.Skills.LearnedSkillIds, recalledSnapshot.Skills.LearnedSkillIds);
        Assert.Equal(sourceSnapshot.Skills.EquippedSkillIds, recalledSnapshot.Skills.EquippedSkillIds);
        Assert.All(recalledSnapshot.Resources, resource => Assert.Equal(resource.Maximum, resource.Current));
        Assert.Empty(recalledSnapshot.BattleStatus.Ailments);
        Assert.Empty(recalledSnapshot.Equipment.EquippedItemIds);
        Assert.Empty(party.DemonStock);
        Assert.Equal(10_000, wallet.Macca);
    }

    [Theory]
    [InlineData(true, 10_000, CompendiumRecallTransactionCode.DuplicateOwned)]
    [InlineData(false, 0, CompendiumRecallTransactionCode.InsufficientCurrency)]
    public void Recall_RejectionsPreservePartyAndWallet(
        bool alreadyOwned,
        int macca,
        CompendiumRecallTransactionCode expected)
    {
        TestContext context = CreateContext();
        var service = context.CreateService();
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            source.State.ToSnapshot()).After;
        RuntimePartyStockSnapshot party = EmptyParty(
            demonStock: alreadyOwned
                ? [Reference(source)]
                : []);
        RuntimeWalletSnapshot wallet = new(macca);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallStockKind.Demon));

        Assert.False(result.Applied);
        Assert.Equal(expected, result.Code);
        Assert.Same(party, result.AfterPartyStock);
        Assert.Same(wallet, result.AfterWallet);
        Assert.Null(result.Actor);
    }

    [Fact]
    public void Recall_UsesTheSelectedStockPolicyAndReportsCapacityBeforeMutation()
    {
        TestContext context = CreateContext();
        var partyTransitions = new PartyStockTransitionService(new FixedStockCapacityPolicy(0));
        CompendiumRuntimeService service = context.CreateService(partyTransitions);
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            source.State.ToSnapshot()).After;
        RuntimePartyStockSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult full = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallStockKind.Persona));

        Assert.Equal(CompendiumRecallTransactionCode.StockFull, full.Code);
        Assert.Same(party, full.AfterPartyStock);
        Assert.Same(wallet, full.AfterWallet);
    }

    [Fact]
    public void Recall_RejectsOverflowingCostWithoutMutation()
    {
        TestContext context = CreateContext();
        CompendiumRuntimeService service = context.CreateService();
        var compendium = new CompendiumStateSnapshot(
        [
            new CompendiumEntrySnapshot(
                context.Entity.Id,
                context.Entity.DisplayName,
                level: 1,
                stats:
                [
                    new KeyValuePair<ContentId, int>(Id("vitality"), int.MaxValue)
                ])
        ]);
        RuntimePartyStockSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(int.MaxValue);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallStockKind.Demon));

        Assert.Equal(CompendiumRecallTransactionCode.InvalidRecallCost, result.Code);
        Assert.Same(party, result.AfterPartyStock);
        Assert.Same(wallet, result.AfterWallet);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.InvalidRecallCost, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void FamiliarKnowledgeImport_RevealsTypedCatalogDefensesWithoutMutatingOtherKnowledge()
    {
        TestContext context = CreateContext();
        RuntimeKnowledgeSnapshot playerKnowledge = new();
        RuntimeKnowledgeSnapshot encounterAiKnowledge = new();
        var service = new FamiliarEntityKnowledgeService(context.Catalog);

        FamiliarKnowledgeImportResult result = service.Import(
            playerKnowledge,
            [context.Entity.Id]);

        Assert.True(result.IsSuccess);
        Assert.Equal([context.Entity.Id], result.ImportedEntityIds);
        Assert.Equal(7, result.After.ElementalAffinities.Count);
        Assert.DoesNotContain(result.After.ElementalAffinities, entry => entry.Element == DamageElement.Almighty);
        Assert.Contains(result.After.ElementalAffinities, entry =>
            entry.EntityId == context.Entity.Id &&
            entry.Element == DamageElement.Ice &&
            entry.Affinity == ElementalAffinity.Weak);
        Assert.Contains(result.After.ElementalAffinities, entry =>
            entry.EntityId == context.Entity.Id &&
            entry.Element == DamageElement.Wind &&
            entry.Affinity == ElementalAffinity.Normal);
        Assert.Contains(result.After.AilmentResistances, entry =>
            entry.EntityId == context.Entity.Id &&
            entry.AilmentId == context.Ailment.Id &&
            entry.Resistance == ResistanceLevel.Resistant);
        Assert.Contains(result.After.InstantDeathResistances, entry =>
            entry.EntityId == context.Entity.Id &&
            entry.Channel == InstantDeathChannel.Dark &&
            entry.Resistance == ResistanceLevel.Immune);
        Assert.Empty(playerKnowledge.ElementalAffinities);
        Assert.Empty(encounterAiKnowledge.ElementalAffinities);

        var mutableCopy = Assert.IsAssignableFrom<IList<RuntimeElementalAffinityKnowledgeSnapshot>>(
            result.After.ElementalAffinities);
        Assert.Throws<NotSupportedException>(() => mutableCopy.Add(
            new RuntimeElementalAffinityKnowledgeSnapshot(context.Entity.Id, DamageElement.Fire, ElementalAffinity.Weak)));
    }

    [Fact]
    public void FamiliarKnowledgeImportRegistered_UsesOnlyRegisteredEntitiesAndReportsMissingOnes()
    {
        TestContext context = CreateContext();
        var state = new CompendiumStateSnapshot(
        [
            new CompendiumEntrySnapshot(context.Entity.Id, "Ashling", 3),
            new CompendiumEntrySnapshot(Id("missing.pack:entity"), "Missing", 1)
        ]);

        FamiliarKnowledgeImportResult result = new FamiliarEntityKnowledgeService(context.Catalog)
            .ImportRegistered(new RuntimeKnowledgeSnapshot(), state);

        Assert.Equal([context.Entity.Id], result.ImportedEntityIds);
        FamiliarKnowledgeImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(FamiliarKnowledgeImportDiagnosticCode.EntityMissing, diagnostic.Code);
        Assert.Equal(Id("missing.pack:entity"), diagnostic.EntityId);
    }

    private static TestContext CreateContext(bool compendiumEligible = true)
    {
        ContentId entityId = Id("test.pack:ashling");
        ContentId ailmentId = Id("test.pack:poison");
        var entity = new EntityDefinition(
            entityId,
            "Ashling",
            "Framework Compendium test entity.",
            Id("demon"),
            Id("test.pack:spirit"),
            rank: 1,
            baseLevel: 3,
            new EntityCapabilitiesDefinition(true, true, compendiumEligible),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            new Dictionary<ContentId, int>
            {
                [Id("strength")] = 4,
                [Id("magic")] = 6,
                [Id("vitality")] = 5,
                [Id("agility")] = 4,
                [Id("luck")] = 3
            },
            elementalAffinities:
            [
                new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Fire, ElementalAffinity.Resist),
                new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Ice, ElementalAffinity.Weak)
            ],
            ailmentResistances:
            [
                new KeyValuePair<ContentId, ResistanceLevel>(ailmentId, ResistanceLevel.Resistant)
            ],
            instantDeathResistances:
            [
                new KeyValuePair<InstantDeathChannel, ResistanceLevel>(InstantDeathChannel.Dark, ResistanceLevel.Immune)
            ]);
        var ailment = new AilmentDefinition(
            ailmentId,
            "Poison",
            "Test ailment.",
            new PermanentDurationDefinition(),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());
        var catalog = new GameDataCatalog(
            contentPacks: [],
            skills: [],
            entities: [new KeyValuePair<ContentId, EntityDefinition>(entity.Id, entity)],
            races: [],
            ailments: [new KeyValuePair<ContentId, AilmentDefinition>(ailment.Id, ailment)],
            items: []);
        var actorFactory = new CatalogBattleActorFactory(catalog, catalog, new TestInitializationPolicy());
        return new TestContext(entity, ailment, catalog, actorFactory);
    }

    private static RuntimePartyStockSnapshot EmptyParty(
        IEnumerable<RuntimeActorReferenceSnapshot>? demonStock = null) =>
        new(
            new RuntimeActorReferenceSnapshot(
                RuntimeInstanceId.Parse("owner"),
                Id("test.pack:owner"),
                "Owner"),
            ownerLevel: 10,
            activeParty:
            [
                new RuntimeActorReferenceSnapshot(
                    RuntimeInstanceId.Parse("owner"),
                    Id("test.pack:owner"),
                    "Owner")
            ],
            demonStock: demonStock);

    private static RuntimeActorReferenceSnapshot Reference(CatalogBattleActor actor) =>
        new(actor.State.InstanceId, actor.Entity.Id, actor.Entity.DisplayName);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed record TestContext(
        EntityDefinition Entity,
        AilmentDefinition Ailment,
        GameDataCatalog Catalog,
        CatalogBattleActorFactory ActorFactory)
    {
        public CatalogBattleActor CreateActor(string instanceId) =>
            ActorFactory.Create(new CatalogBattleActorCreationRequest(
                Entity.Id,
                RuntimeInstanceId.Parse(instanceId),
                Id("player_team"),
                Entity.BaseLevel,
                new RuntimeProgressionSnapshot(Entity.BaseLevel, 4, 9, 2),
                Id("player_controller"),
                RuntimeActorDeployment.Reserve,
                IsActive: false)).RequireActor();

        public CompendiumRuntimeService CreateService(IPartyStockTransitionService? partyStock = null) =>
            new(
                Catalog,
                ActorFactory,
                new StandardResourceGrowthPolicy(),
                partyStock: partyStock);
    }

    private sealed class TestInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            new(
                Id("hp"),
                [
                    new BattleResourceState(Id("hp"), 50, 50),
                    new BattleResourceState(Id("sp"), 20, 20)
                ],
                new Dictionary<ContentId, decimal>
                {
                    [Id("hp")] = 25,
                    [Id("sp")] = 5
                });
    }

    private sealed class EmptyEntityRepository : IEntityDefinitionRepository
    {
        public bool TryGetEntity(ContentId id, out EntityDefinition? definition)
        {
            definition = null;
            return false;
        }

        public EntityDefinition GetRequiredEntity(ContentId id) => throw new KeyNotFoundException();
    }

    private sealed class FixedStockCapacityPolicy(int capacity) : IStockCapacityPolicy
    {
        public int GetCapacity(int ownerLevel) => capacity;
    }
}
