using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class OriginalCleanContentSliceTests
{
    private const string Pack = "convergence.training_annex_slice";

    [Fact]
    public void TrainingAnnexSlice_LoadsAsSelfContainedOriginalCleanContent()
    {
        GameDataCatalog catalog = LoadCatalog();

        Assert.Equal(3, catalog.Skills.Count);
        Assert.Equal(2, catalog.Entities.Count);
        Assert.Single(catalog.Races);
        Assert.Single(catalog.Items);
        Assert.Single(catalog.Encounters);
        Assert.Single(catalog.Dungeons);
        Assert.Equal(8, catalog.Rulesets.Count);
        Assert.Empty(catalog.Ailments);
        Assert.Empty(catalog.Equipment);
        Assert.Empty(catalog.Shops);
        Assert.Empty(catalog.Negotiations);
        Assert.Empty(catalog.FusionRecipes);

        Assert.All(catalog.Skills.Keys, AssertPackQualified);
        Assert.All(catalog.Entities.Keys, AssertPackQualified);
        Assert.All(catalog.Races.Keys, AssertPackQualified);
        Assert.All(catalog.Items.Keys, AssertPackQualified);
        Assert.All(catalog.Encounters.Keys, AssertPackQualified);
        Assert.All(catalog.Dungeons.Keys, AssertPackQualified);
        Assert.All(catalog.Rulesets.Keys, AssertPackQualified);
        Assert.Throws<ArgumentException>(() => catalog.GetRequiredEntity(Id("echo_adept")));

        RaceDefinition race = catalog.GetRequiredRace(Qualified("annex_spirit"));
        EntityDefinition actor = catalog.GetRequiredEntity(Qualified("echo_adept"));
        EntityDefinition enemy = catalog.GetRequiredEntity(Qualified("ashling"));
        SkillDefinition active = catalog.GetRequiredSkill(Qualified("echo_strike"));
        SkillDefinition passive = catalog.GetRequiredSkill(Qualified("steady_breath"));
        ItemDefinition item = catalog.GetRequiredItem(Qualified("annex_tonic"));
        EncounterDefinition encounter = catalog.GetRequiredEncounter(Qualified("ashling_drill"));
        DungeonDefinition dungeon = catalog.GetRequiredDungeon(Qualified("training_annex"));

        Assert.Equal("Annex Spirit", race.DisplayName);
        Assert.Equal(Qualified("annex_spirit"), actor.RaceId);
        Assert.Equal(Qualified("annex_spirit"), enemy.RaceId);
        Assert.Equal([Qualified("echo_strike"), Qualified("steady_breath")], actor.BaseSkillIds);
        Assert.Equal([Qualified("ash_spark")], enemy.BaseSkillIds);

        Assert.Equal(SkillActivation.Active, active.Activation);
        Assert.Equal(InheritanceGroup.Physical, active.InheritanceGroup);
        Assert.Equal(DamageElement.Physical, Assert.IsType<DamageEffectDefinition>(Assert.Single(active.Effects)).Element);
        Assert.Equal([Id("battle")], active.Availability!.ContextIds);

        Assert.Equal(SkillActivation.Passive, passive.Activation);
        Assert.Equal(InheritanceGroup.Passive, passive.InheritanceGroup);
        PassiveTriggerDefinition trigger = Assert.Single(passive.Triggers);
        Assert.Equal(Id("owner_turn_end"), trigger.EventId);
        var restore = Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(trigger.Effects));
        Assert.Equal(Id("hp"), restore.ResourceId);

        Assert.Equal(ItemKind.Consumable, item.ItemKind);
        Assert.Equal([Id("battle"), Id("field")], item.Usage!.ContextIds);
        Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(item.Usage.Effects));

        EncounterFormationDefinition formation = Assert.Single(encounter.Formations);
        Assert.Equal(Id("training_annex"), encounter.EnvironmentId);
        Assert.Equal(Id("standard_reward"), formation.RewardPolicyId);
        EncounterMemberDefinition member = Assert.Single(formation.Members);
        Assert.Equal(Qualified("ashling"), member.EntityId);
        Assert.Equal(2, member.Level);

        DungeonBlockDefinition block = Assert.Single(dungeon.Blocks);
        Assert.Equal(Qualified("annex_floor"), block.Id);
        Assert.Equal([Qualified("ashling_drill")], block.EncounterPoolIds);
        DungeonFixedFloorDefinition fixedFloor = Assert.Single(block.FixedFloors);
        Assert.Equal(3, fixedFloor.Floor);
        Assert.Equal(DungeonFixedFloorKind.SafeRoom, fixedFloor.Kind);
        Assert.Equal(Id("return_to_lobby"), fixedFloor.TransitionRuleId);
    }

    [Fact]
    public void TrainingAnnexSlice_RulesetsBindToStandardFrameworkServices()
    {
        GameDataCatalog catalog = LoadCatalog();
        var resolver = new RuntimeRulesetBindingResolver();

        ProductionCombatRuleset damage = resolver.BindProductionCombatRuleset(
            catalog,
            Qualified("standard_damage"),
            new SequenceRandomSource())
            .RequireService();
        Assert.Equal(1.5m, damage.Config.WeakDamageMultiplier);
        Assert.Equal(0.5m, damage.Config.ResistDamageMultiplier);

        IBattleRewardService rewards = resolver.BindBattleRewardService(
            catalog,
            Qualified("standard_reward"),
            damage)
            .RequireService();
        BattleRewardResult reward = rewards.Calculate(new BattleRewardRequest(
            [
                new BattleRewardEnemySnapshot(
                    Qualified("ashling"),
                    2,
                    3,
                    5,
                    3,
                    4,
                    3)
            ],
            [new BattleRewardRecipientSnapshot(Qualified("echo_adept"), IsAlive: true, HasActiveForm: true)]));
        Assert.True(reward.TotalExperience > 0);
        Assert.True(reward.TotalMacca > 0);

        Assert.IsType<StandardStatResolutionPolicy>(resolver.BindStatResolutionPolicy(
            catalog,
            Qualified("standard_stat")).RequireService());
        Assert.IsType<StandardResourceGrowthPolicy>(resolver.BindGrowthServices(
            catalog,
            Qualified("standard_growth")).RequireService().ResourceGrowthPolicy);
        Assert.IsType<LegacyStockCapacityPolicy>(resolver.BindStockCapacityPolicy(
            catalog,
            Qualified("standard_stock_capacity")).RequireService());
        Assert.NotNull(resolver.BindResourceManagementServices(
            catalog,
            Qualified("standard_economy")).RequireService().Inventory);
        Assert.NotNull(resolver.BindPressTurnFactory(
            catalog,
            Qualified("standard_press_turn")).RequireService());
        Assert.Equal(StandardRulesetPolicyIds.StandardMoonPhase, resolver.BindMoonPhaseRuleset(
            catalog,
            Qualified("standard_moon_phase")).RequireService().PolicyId);
    }

    [Fact]
    public void TrainingAnnexSlice_ManifestUsesOnlyTheOriginalSliceDocuments()
    {
        string root = FindJsonRoot();
        ContentPackManifest manifest = new SkillSystemJsonDeserializer().DeserializeManifest(
            File.ReadAllText(Path.Combine(root, "training_annex_slice.manifest.json")),
            "training_annex_slice.manifest.json");

        Assert.Equal(Pack, manifest.Id);
        Assert.Empty(manifest.Dependencies);
        Assert.Equal(
            [
                "training_annex_slice.races.json",
                "training_annex_slice.skills.json",
                "training_annex_slice.entities.json",
                "training_annex_slice.items.json",
                "training_annex_slice.encounters.json",
                "training_annex_slice.dungeons.json",
                "training_annex_slice.rulesets.json"
            ],
            manifest.Documents.Select(document => document.Path).ToArray());
    }

    private static GameDataCatalog LoadCatalog()
    {
        string root = FindJsonRoot();
        ContentPackTextBundle bundle = Bundle(root,
            "training_annex_slice.manifest.json",
            "training_annex_slice.races.json",
            "training_annex_slice.skills.json",
            "training_annex_slice.entities.json",
            "training_annex_slice.items.json",
            "training_annex_slice.encounters.json",
            "training_annex_slice.dungeons.json",
            "training_annex_slice.rulesets.json");

        CatalogLoadResult result = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
            Registrations(),
            [bundle]));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(error => $"{error.Code} {error.JsonPath}: {error.Message}")));
        return result.RequireCatalog();
    }

    private static ContentPackTextBundle Bundle(string root, string manifestName, params string[] documentNames) =>
        new(
            manifestName,
            File.ReadAllText(Path.Combine(root, manifestName)),
            documentNames.Select(name => new ContentDocumentText(
                name,
                name,
                File.ReadAllText(Path.Combine(root, name)))));

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEntityKind("demon")
            .RegisterEvent("owner_turn_end")
            .RegisterEncounterEnvironment("training_annex")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "standard_press_turn",
                "standard_stock_capacity",
                "standard_economy",
                "standard_moon_phase",
                "return_to_lobby")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .Build();

    private static void AssertPackQualified(ContentId id) =>
        Assert.StartsWith(Pack + ":", id.ToString(), StringComparison.Ordinal);

    private static ContentId Qualified(string localId) => Id($"{Pack}:{localId}");

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static string FindJsonRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
        return Path.Combine(root, "Data", "Jsons");
    }

    private sealed class SequenceRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }
}
