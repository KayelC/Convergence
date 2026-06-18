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

        Assert.Equal(10, catalog.Skills.Count);
        Assert.Equal(5, catalog.Entities.Count);
        Assert.Equal(3, catalog.Races.Count);
        Assert.Equal(5, catalog.Items.Count);
        Assert.Equal(3, catalog.Ailments.Count);
        Assert.Equal(4, catalog.Equipment.Count);
        Assert.Single(catalog.Shops);
        Assert.Single(catalog.Negotiations);
        Assert.Equal(3, catalog.Encounters.Count);
        Assert.Single(catalog.Dungeons);
        Assert.Equal(2, catalog.FusionRecipes.Count);
        Assert.Equal(8, catalog.Rulesets.Count);

        Assert.All(catalog.Skills.Keys, AssertPackQualified);
        Assert.All(catalog.Entities.Keys, AssertPackQualified);
        Assert.All(catalog.Races.Keys, AssertPackQualified);
        Assert.All(catalog.Ailments.Keys, AssertPackQualified);
        Assert.All(catalog.Items.Keys, AssertPackQualified);
        Assert.All(catalog.Equipment.Keys, AssertPackQualified);
        Assert.All(catalog.Shops.Keys, AssertPackQualified);
        Assert.All(catalog.Negotiations.Keys, AssertPackQualified);
        Assert.All(catalog.Encounters.Keys, AssertPackQualified);
        Assert.All(catalog.Dungeons.Keys, AssertPackQualified);
        Assert.All(catalog.FusionRecipes.Keys, AssertPackQualified);
        Assert.All(catalog.Rulesets.Keys, AssertPackQualified);
        Assert.Throws<ArgumentException>(() => catalog.GetRequiredEntity(Id("echo_adept")));

        RaceDefinition race = catalog.GetRequiredRace(Qualified("annex_spirit"));
        RaceDefinition beast = catalog.GetRequiredRace(Qualified("annex_beast"));
        RaceDefinition construct = catalog.GetRequiredRace(Qualified("annex_construct"));
        EntityDefinition actor = catalog.GetRequiredEntity(Qualified("echo_adept"));
        EntityDefinition enemy = catalog.GetRequiredEntity(Qualified("ashling"));
        EntityDefinition runner = catalog.GetRequiredEntity(Qualified("bramble_runner"));
        EntityDefinition shell = catalog.GetRequiredEntity(Qualified("ward_shell"));
        SkillDefinition active = catalog.GetRequiredSkill(Qualified("echo_strike"));
        SkillDefinition ice = catalog.GetRequiredSkill(Qualified("frost_tip"));
        SkillDefinition cure = catalog.GetRequiredSkill(Qualified("clear_toxin"));
        SkillDefinition ailment = catalog.GetRequiredSkill(Qualified("toxin_touch"));
        SkillDefinition buff = catalog.GetRequiredSkill(Qualified("focus_call"));
        SkillDefinition debuff = catalog.GetRequiredSkill(Qualified("soften_guard"));
        SkillDefinition passive = catalog.GetRequiredSkill(Qualified("steady_breath"));
        AilmentDefinition poison = catalog.GetRequiredAilment(Qualified("sample_poison"));
        ItemDefinition item = catalog.GetRequiredItem(Qualified("annex_tonic"));
        ItemDefinition cleanse = catalog.GetRequiredItem(Qualified("cleanse_drop"));
        ItemDefinition revive = catalog.GetRequiredItem(Qualified("revival_pin"));
        EquipmentDefinition weapon = catalog.GetRequiredEquipment(Qualified("practice_blade"));
        ShopCatalogDefinition shop = catalog.GetRequiredShop(Qualified("training_supply"));
        NegotiationDefinition negotiation = catalog.GetRequiredNegotiation(Qualified("steady_sample"));
        EncounterDefinition encounter = catalog.GetRequiredEncounter(Qualified("ashling_drill"));
        EncounterDefinition mixed = catalog.GetRequiredEncounter(Qualified("mixed_drill"));
        FusionRecipeDefinition fusion = catalog.GetRequiredFusionRecipe(Qualified("ashling_bramble_shell"));
        DungeonDefinition dungeon = catalog.GetRequiredDungeon(Qualified("training_annex"));

        Assert.Equal("Annex Spirit", race.DisplayName);
        Assert.Equal("Annex Beast", beast.DisplayName);
        Assert.Equal("Annex Construct", construct.DisplayName);
        Assert.Equal(Qualified("annex_spirit"), actor.RaceId);
        Assert.Equal(Qualified("annex_spirit"), enemy.RaceId);
        Assert.Equal(Qualified("annex_beast"), runner.RaceId);
        Assert.Equal(Qualified("annex_construct"), shell.RaceId);
        Assert.Equal([Qualified("frost_tip"), Qualified("echo_strike"), Qualified("steady_breath")], actor.BaseSkillIds);
        Assert.Contains(actor.SkillUnlocks, unlock => unlock.Level == 4 && unlock.SkillId == Qualified("mend"));
        Assert.Equal([Qualified("ash_spark")], enemy.BaseSkillIds);
        Assert.Contains(enemy.SkillUnlocks, unlock => unlock.Level == 3 && unlock.SkillId == Qualified("toxin_touch"));

        Assert.Equal(SkillActivation.Active, active.Activation);
        Assert.Equal(InheritanceGroup.Physical, active.InheritanceGroup);
        Assert.Equal(DamageElement.Physical, Assert.IsType<DamageEffectDefinition>(Assert.Single(active.Effects)).Element);
        Assert.Equal([Id("battle")], active.Availability!.ContextIds);
        Assert.Equal(DamageElement.Ice, Assert.IsType<DamageEffectDefinition>(Assert.Single(ice.Effects)).Element);
        Assert.IsType<RemoveAilmentEffectDefinition>(Assert.Single(cure.Effects));
        Assert.Equal(Qualified("sample_poison"), Assert.IsType<ApplyAilmentEffectDefinition>(Assert.Single(ailment.Effects)).AilmentId);
        Assert.IsType<ModifyStatStageEffectDefinition>(Assert.Single(buff.Effects));
        Assert.Equal(-1, Assert.IsType<ModifyStatStageEffectDefinition>(Assert.Single(debuff.Effects)).StageDelta);

        Assert.Equal(SkillActivation.Passive, passive.Activation);
        Assert.Equal(InheritanceGroup.Passive, passive.InheritanceGroup);
        PassiveTriggerDefinition trigger = Assert.Single(passive.Triggers);
        Assert.Equal(Id("owner_turn_end"), trigger.EventId);
        var restore = Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(trigger.Effects));
        Assert.Equal(Id("hp"), restore.ResourceId);

        Assert.Equal(Qualified("sample_poison"), poison.Id);
        Assert.IsType<NormalAilmentTurnBehaviorDefinition>(poison.TurnBehavior);

        Assert.Equal(ItemKind.Consumable, item.ItemKind);
        Assert.Equal([Id("battle"), Id("field")], item.Usage!.ContextIds);
        Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(item.Usage.Effects));
        Assert.IsType<RemoveAilmentEffectDefinition>(Assert.Single(cleanse.Usage!.Effects));
        Assert.IsType<ReviveEffectDefinition>(Assert.Single(revive.Usage!.Effects));

        Assert.Equal(EquipmentSlot.Weapon, weapon.Slot);
        Assert.Equal(DamageElement.Physical, weapon.Weapon!.BasicAttack.Element);
        Assert.Equal(Id("training_supply"), shop.CategoryId);
        Assert.Equal(4, shop.Offers.Count);
        Assert.Equal(Id("steady_sample"), negotiation.PersonalityId);
        Assert.Equal([Qualified("annex_spirit"), Qualified("annex_beast")], negotiation.DefaultRaceIds);

        EncounterFormationDefinition formation = Assert.Single(encounter.Formations);
        Assert.Equal(Id("training_annex"), encounter.EnvironmentId);
        Assert.Equal(Id("standard_reward"), formation.RewardPolicyId);
        EncounterMemberDefinition member = Assert.Single(formation.Members);
        Assert.Equal(Qualified("ashling"), member.EntityId);
        Assert.Equal(2, member.Level);
        Assert.Equal(2, Assert.Single(mixed.Formations).Members.Count);

        Assert.Equal(FusionResultOperationKind.CreateEntity, fusion.Result.Operation);
        Assert.Equal(Qualified("ward_shell"), fusion.Result.ResultEntityId);

        DungeonBlockDefinition block = Assert.Single(dungeon.Blocks);
        Assert.Equal(Qualified("annex_floor"), block.Id);
        Assert.Equal([Qualified("ashling_drill"), Qualified("mixed_drill")], block.EncounterPoolIds);
        Assert.Equal(3, block.FixedFloors.Count);
        DungeonFixedFloorDefinition safeFloor = block.FixedFloors[0];
        Assert.Equal(3, safeFloor.Floor);
        Assert.Equal(DungeonFixedFloorKind.SafeRoom, safeFloor.Kind);
        Assert.Equal(Id("return_to_lobby"), safeFloor.TransitionRuleId);
        DungeonFixedFloorDefinition battleFloor = block.FixedFloors[1];
        Assert.Equal(4, battleFloor.Floor);
        Assert.Equal(DungeonFixedFloorKind.Battle, battleFloor.Kind);
        Assert.Equal(Qualified("shell_check"), battleFloor.EncounterId);
        DungeonFixedFloorDefinition barrierFloor = block.FixedFloors[2];
        Assert.Equal(5, barrierFloor.Floor);
        Assert.Equal(DungeonFixedFloorKind.BlockEnd, barrierFloor.Kind);
        Assert.Equal(Id("training_barrier"), barrierFloor.BarrierRuleId);
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
                "training_annex_slice.ailments.json",
                "training_annex_slice.skills.json",
                "training_annex_slice.entities.json",
                "training_annex_slice.items.json",
                "training_annex_slice.equipment.json",
                "training_annex_slice.shops.json",
                "training_annex_slice.negotiations.json",
                "training_annex_slice.encounters.json",
                "training_annex_slice.dungeons.json",
                "training_annex_slice.fusion.json",
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
            "training_annex_slice.ailments.json",
            "training_annex_slice.skills.json",
            "training_annex_slice.entities.json",
            "training_annex_slice.items.json",
            "training_annex_slice.equipment.json",
            "training_annex_slice.shops.json",
            "training_annex_slice.negotiations.json",
            "training_annex_slice.encounters.json",
            "training_annex_slice.dungeons.json",
            "training_annex_slice.fusion.json",
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
            .RegisterModifierTrack("attack", "defense")
            .RegisterEntityKind("demon")
            .RegisterAlignment("neutral")
            .RegisterNegotiationPersonality("steady_sample")
            .RegisterAilmentGroup("major_ailment", "toxin", "rest", "immobilize")
            .RegisterEvent("owner_turn_end")
            .RegisterShopCategory("training_supply")
            .RegisterNegotiationDemand("sample_macca")
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
                "return_to_lobby",
                "training_barrier",
                "standard_accident",
                "standard_mutation")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<ApplyAilmentEffectDefinition>()
            .SupportEffect<ModifyStatStageEffectDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
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
