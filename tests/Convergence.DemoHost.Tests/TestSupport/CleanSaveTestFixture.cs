using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.DemoHost.Tests.TestSupport;

internal static class CleanSaveTestFixture
{
    internal static RuntimeSaveGameSnapshot CreateSaveSnapshot(
        IEnumerable<RuntimeActorSnapshot>? actors = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        IEnumerable<RuntimeCheckpointEntrySnapshot>? checkpoints = null,
        RuntimePartyRosterSnapshot? partyRoster = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeEquipmentSnapshot? equipment = null,
        RuntimeFieldSnapshot? field = null,
        CompendiumStateSnapshot? compendium = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        int contractVersion = RuntimeSaveGameSnapshot.CurrentContractVersion,
        bool includeDefaultField = true)
    {
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"),
            learnedSkills:
            [
                Id("convergence.clean_battle_demo:frost_lance_demo"),
                Id("convergence.skill_system_redesign_sample:ice_boost_sample")
            ]);
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);

        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("1.0.0"),
            [
                new ContentPackIdentity("convergence.skill_system_redesign_sample", SemanticVersion.Parse("0.4.0")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("0.4.0")),
                new ContentPackIdentity("convergence.shared_effects_demo", SemanticVersion.Parse("0.4.0")),
                new ContentPackIdentity("convergence.catalog_surface_sample", SemanticVersion.Parse("0.4.0"))
            ],
            actors ?? [frost, ember],
            partyRoster ?? new RuntimePartyRosterSnapshot(
                frostRef,
                activeParty: [frostRef],
                activeHostedEntity: emberRef,
                hostedEntityRoster: [emberRef],
                companionRoster: [frostRef]),
            inventory ?? new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(Id("convergence.shared_effects_demo:medicine_demo"), 2)],
                [
                    new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(
                        EquipmentSlot.Weapon,
                        [Id("convergence.catalog_surface_sample:shortsword_sample")])
                ]),
            equipment ?? new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(
                    EquipmentSlot.Weapon,
                    Id("convergence.catalog_surface_sample:shortsword_sample"))
            ]),
            new RuntimeWalletSnapshot(1234),
            field ?? (includeDefaultField
                ? new RuntimeFieldSnapshot(
                    new RuntimeNavigationSnapshot(Id("convergence.catalog_surface_sample:sample_depths_floor_5")),
                    new RuntimeDungeonTraversalSnapshot(
                        Id("convergence.catalog_surface_sample:sample_depths"),
                        Id("convergence.catalog_surface_sample:floor_5"),
                        visitedNodeIds:
                        [
                            Id("convergence.catalog_surface_sample:floor_1"),
                            Id("convergence.catalog_surface_sample:floor_5")
                        ],
                        unlockedCheckpointIds:
                        [
                            Id("convergence.catalog_surface_sample:terminal_1"),
                            Id("convergence.catalog_surface_sample:terminal_5")
                        ],
                        defeatedBossIds: [Id("convergence.catalog_surface_sample:entry_block_training_sample")]))
                : null),
            compendium ?? new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    Id("convergence.clean_battle_demo:frost_duelist_demo"),
                    "Frost Duelist",
                    5,
                    [
                        new KeyValuePair<ContentId, int>(Id("strength"), 4),
                        new KeyValuePair<ContentId, int>(Id("magic"), 8),
                        new KeyValuePair<ContentId, int>(Id("vitality"), 5),
                        new KeyValuePair<ContentId, int>(Id("agility"), 6),
                        new KeyValuePair<ContentId, int>(Id("luck"), 4)
                    ],
                    [Id("convergence.clean_battle_demo:frost_lance_demo")])
            ]),
            knowledge ?? new RuntimeKnowledgeSnapshot(
                elementalAffinities:
                [
                    new RuntimeElementalAffinityKnowledgeSnapshot(
                        Id("convergence.clean_battle_demo:ember_duelist_demo"),
                        DamageElement.Ice,
                        ElementalAffinity.Weak)
                ],
                ailmentResistances:
                [
                    new RuntimeAilmentResistanceKnowledgeSnapshot(
                        Id("convergence.clean_battle_demo:ember_duelist_demo"),
                        Id("convergence.shared_effects_demo:poison_demo"),
                        ResistanceLevel.Normal)
                ]),
            new RuntimeSessionProgressSnapshot(
                moonPhaseId: null,
                elapsedTicks: 42,
                counters: [new KeyValuePair<ContentId, long>(Id("battles_won"), 1)],
                flags: [Id("tutorial_complete")]),
            new RuntimeCheckpointLogSnapshot(checkpoints ??
            [
                new(0, RuntimeCheckpointKind.SaveCreated, "Save created."),
                new(1, RuntimeCheckpointKind.ActorRestored, "Frost restored.", RuntimeInstanceId.Parse("frost"))
            ]),
            hostContext ?? [new KeyValuePair<ContentId, string>(Id("scene"), "clean_save_demo")],
            contractVersion);
    }

    internal static RuntimeActorSnapshot CreateActor(
        RuntimeInstanceId instanceId,
        ContentId entityId,
        IEnumerable<ContentId>? learnedSkills = null,
        IEnumerable<RuntimeTimedStateSnapshot>? ailments = null) =>
        new(
            new RuntimeActorIdentitySnapshot(instanceId, entityId, Id("companion"), entityId.ToString()),
            new RuntimeActorAffiliationSnapshot(Id("host"), Id("player_team")),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeProgressionSnapshot(5, 0, 0, 0),
            [
                new RuntimeResourceSnapshot(Id("hp"), 50, 75),
                new RuntimeResourceSnapshot(Id("sp"), 20, 30)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 8)],
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 8)]),
            new RuntimeSkillStateSnapshot(
                learnedSkills ?? [Id("convergence.clean_battle_demo:frost_lance_demo")],
                learnedSkills ?? [Id("convergence.clean_battle_demo:frost_lance_demo")]),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(ailments: ailments),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 40)],
            Id("hp"));

    internal static GameDataCatalog LoadCatalog()
    {
        ContentPackTextBundle reference = Bundle(
            "skill_system_redesign.manifest.sample.json",
            "skill_system_redesign.entities.sample.json",
            "skill_system_redesign.skills.sample.json",
            "skill_system_redesign.races.sample.json");
        ContentPackTextBundle battle = Bundle(
            "clean_battle_demo.manifest.json",
            "clean_battle_demo.races.json",
            "clean_battle_demo.skills.json",
            "clean_battle_demo.entities.json");
        ContentPackTextBundle shared = Bundle(
            "shared_effects_demo.manifest.json",
            "shared_effects_demo.ailments.json",
            "shared_effects_demo.skills.json",
            "shared_effects_demo.entities.json",
            "shared_effects_demo.items.json");
        ContentPackTextBundle surface = Bundle(
            "catalog_surface_sample.manifest.json",
            "catalog_surface_sample.equipment.json",
            "catalog_surface_sample.shops.json",
            "catalog_surface_sample.negotiations.json",
            "catalog_surface_sample.encounters.json",
            "catalog_surface_sample.dungeons.json",
            "catalog_surface_sample.fusion.json",
            "catalog_surface_sample.rulesets.json");

        CatalogLoadResult result = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
            Registrations(),
            [reference, battle, shared, surface]));

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(error => $"{error.Code} {error.JsonPath}: {error.Message}")));
        }

        return result.RequireCatalog();
    }

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

    private static ContentPackTextBundle Bundle(string manifestName, params string[] documentNames)
    {
        string jsonRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        return new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(TestContentPath.Resolve(jsonRoot, manifestName)),
            documentNames.Select(name => new ContentDocumentText(
                name,
                name,
                File.ReadAllText(TestContentPath.Resolve(jsonRoot, name)))));
    }

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEntityKind("companion")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterPhase("player_phase")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", EmptyParameterValidator.Instance)
            .RegisterShopCategory("weapon_shop")
            .RegisterNegotiationPersonality("playful")
            .RegisterNegotiationDemand("credits")
            .RegisterEncounterEnvironment("entry_block")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "persistent_staged",
                "timed_exclusive",
                "timed_contribution",
                "standard_action_token",
                "standard_roster_capacity",
                "standard_economy",
                "return_to_lobby",
                "standard_accident",
                "standard_mutation")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<EscapeEffectDefinition>()
            .SupportEffect<CustomEffectDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .Build();

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class EmptyParameterValidator : IContentParameterValidator
    {
        internal static EmptyParameterValidator Instance { get; } = new();

        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) => [];
    }
}
