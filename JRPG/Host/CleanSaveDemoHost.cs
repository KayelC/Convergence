using System.Text.Json;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host;

internal sealed class CleanSaveDemoHost
{
    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;

    public CleanSaveDemoHost(TextWriter output, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Data", "Jsons")),
            new TextWriterEventSink(output))
    {
    }

    internal CleanSaveDemoHost(IContentPackTextSource contentSource, IHostEventSink<string> eventSink)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public int Run() => RunAsync().GetAwaiter().GetResult();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        CatalogLoadResult load;
        try
        {
            load = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
                BuildRegistrations(),
                [
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "skill_system_redesign.manifest.sample.json",
                        [
                            "skill_system_redesign.races.sample.json",
                            "skill_system_redesign.skills.sample.json",
                            "skill_system_redesign.entities.sample.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "clean_battle_demo.manifest.json",
                        [
                            "clean_battle_demo.races.json",
                            "clean_battle_demo.skills.json",
                            "clean_battle_demo.entities.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "shared_effects_demo.manifest.json",
                        [
                            "shared_effects_demo.ailments.json",
                            "shared_effects_demo.skills.json",
                            "shared_effects_demo.entities.json",
                            "shared_effects_demo.items.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "catalog_surface_sample.manifest.json",
                        [
                            "catalog_surface_sample.equipment.json",
                            "catalog_surface_sample.shops.json",
                            "catalog_surface_sample.negotiations.json",
                            "catalog_surface_sample.encounters.json",
                            "catalog_surface_sample.dungeons.json",
                            "catalog_surface_sample.fusion.json",
                            "catalog_surface_sample.rulesets.json"
                        ]), cancellationToken)
                ]));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _eventSink.PublishAsync($"Content read failed: {exception.Message}", cancellationToken);
            return 2;
        }

        if (!load.IsSuccess || load.Catalog is null)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}",
                    cancellationToken);
            }
            return 3;
        }

        GameDataCatalog catalog = load.Catalog;
        RuntimeSaveGameSnapshot snapshot = BuildDemoSnapshot();
        RuntimeSaveValidator validator = new();
        RuntimeSaveValidationResult before = validator.Validate(snapshot, catalog);
        if (!before.IsValid)
        {
            await PublishDiagnosticsAsync(before.Diagnostics, cancellationToken);
            return 4;
        }

        string json = CleanSaveJsonCodec.Serialize(snapshot);
        RuntimeSaveGameSnapshot restored;
        try
        {
            restored = CleanSaveJsonCodec.Deserialize(json);
        }
        catch (Exception exception)
        {
            await _eventSink.PublishAsync($"Save round-trip failed: {exception.Message}", cancellationToken);
            return 5;
        }

        RuntimeSaveValidationResult after = validator.Validate(restored, catalog);
        if (!after.IsValid)
        {
            await PublishDiagnosticsAsync(after.Diagnostics, cancellationToken);
            return 4;
        }

        RuntimeActorSnapshot[] restoredActors = restored.Actors
            .Select(actor => RuntimeActorStateSet.FromSnapshot(actor).ToSnapshot())
            .ToArray();

        await _eventSink.PublishAsync(
            $"001 [save] Created runtime save snapshot v{restored.ContractVersion} with {restoredActors.Length} actor(s).",
            cancellationToken);
        await _eventSink.PublishAsync(
            $"002 [serialize] Host-owned JSON round-trip completed with {json.Length} character(s).",
            cancellationToken);
        await _eventSink.PublishAsync(
            $"003 [validate] Restored snapshot validated with {after.Diagnostics.Count} diagnostic(s).",
            cancellationToken);
        string fieldSummary = restored.Field?.DungeonProgress is RuntimeDungeonProgressSnapshot dungeonProgress
            ? $"floor {dungeonProgress.CurrentFloor}"
            : restored.Field is null
                ? "no field state"
                : $"location {restored.Field.Navigation.CurrentLocationId}";
        await _eventSink.PublishAsync(
            $"004 [restore] Restored {restoredActors.Length} actor(s), {restored.Inventory.ItemQuantities.Count} item stack(s), {fieldSummary}.",
            cancellationToken);
        await _eventSink.PublishAsync("005 [outcome] Clean save demo completed successfully.", cancellationToken);
        return 0;
    }

    internal static RuntimeSaveGameSnapshot BuildDemoSnapshot()
    {
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
            [
                ContentId.Parse("convergence.clean_battle_demo:frost_lance_demo"),
                ContentId.Parse("convergence.skill_system_redesign_sample:ice_boost_sample")
            ]);
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
            [ContentId.Parse("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);

        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("1.0.0"),
            [frost, ember],
            new RuntimePartyStockSnapshot(
                frostRef,
                5,
                activeParty: [frostRef],
                reserveMembers: [emberRef],
                activeForm: frostRef,
                demonStock: [frostRef, emberRef]),
            new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(ContentId.Parse("convergence.shared_effects_demo:medicine_demo"), 2)],
                [
                    new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(
                        EquipmentSlot.Weapon,
                        [ContentId.Parse("convergence.catalog_surface_sample:shortsword_sample")])
                ]),
            new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(
                    EquipmentSlot.Weapon,
                    ContentId.Parse("convergence.catalog_surface_sample:shortsword_sample"))
            ]),
            new RuntimeWalletSnapshot(1234),
            new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(ContentId.Parse("convergence.catalog_surface_sample:tartarus_floor_5")),
                new RuntimeDungeonProgressSnapshot(
                    ContentId.Parse("convergence.catalog_surface_sample:tartarus_sample"),
                    currentFloor: 5,
                    maxFloorReached: 10,
                    unlockedTerminals: [1, 5],
                    defeatedBossIds: [ContentId.Parse("convergence.catalog_surface_sample:thebel_training_sample")])),
            new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
                    "Frost Duelist",
                    5,
                    [new KeyValuePair<ContentId, int>(ContentId.Parse("magic"), 8)],
                    [ContentId.Parse("convergence.clean_battle_demo:frost_lance_demo")])
            ]),
            new RuntimeKnowledgeSnapshot(
                elementalAffinities:
                [
                    new RuntimeElementalAffinityKnowledgeSnapshot(
                        ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
                        DamageElement.Ice,
                        ElementalAffinity.Weak)
                ],
                ailmentResistances:
                [
                    new RuntimeAilmentResistanceKnowledgeSnapshot(
                        ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
                        ContentId.Parse("convergence.shared_effects_demo:poison_demo"),
                        ResistanceLevel.Normal)
                ]),
            new RuntimeSessionProgressSnapshot(
                ContentId.Parse("new_moon"),
                elapsedTicks: 42,
                counters: [new KeyValuePair<ContentId, long>(ContentId.Parse("battles_won"), 1)],
                flags: [ContentId.Parse("tutorial_complete")]),
            new RuntimeCheckpointLogSnapshot(
            [
                new(0, RuntimeCheckpointKind.SaveCreated, "Save created."),
                new(1, RuntimeCheckpointKind.ActorRestored, "Frost restored.", RuntimeInstanceId.Parse("frost"))
            ]),
            [new KeyValuePair<ContentId, string>(ContentId.Parse("scene"), "clean_save_demo")]);
    }

    private static RuntimeActorSnapshot CreateActor(RuntimeInstanceId instanceId, ContentId entityId, IEnumerable<ContentId> skillIds) =>
        new(
            new RuntimeActorIdentitySnapshot(instanceId, entityId, ContentId.Parse("demon"), entityId.ToString()),
            new RuntimeActorOwnershipSnapshot(ContentId.Parse("host"), ContentId.Parse("player_team")),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Deployed, IsActive: true),
            new RuntimeProgressionSnapshot(5, 0, 0, 0),
            [
                new RuntimeResourceSnapshot(ContentId.Parse("hp"), 50, 75),
                new RuntimeResourceSnapshot(ContentId.Parse("sp"), 20, 30)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(ContentId.Parse("magic"), 8)],
                [new KeyValuePair<ContentId, decimal>(ContentId.Parse("magic"), 8)]),
            new RuntimeSkillStateSnapshot(skillIds, skillIds),
            new RuntimeFormStockSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(ContentId.Parse("hp"), 40)]);

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

    private async ValueTask PublishDiagnosticsAsync(
        IEnumerable<RuntimeSaveValidationDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (RuntimeSaveValidationDiagnostic diagnostic in diagnostics)
        {
            await _eventSink.PublishAsync(
                $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                cancellationToken);
        }
    }

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEntityKind("demon")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", new AcceptAnyParametersValidator())
            .RegisterShopCategory("weapon_shop")
            .RegisterNegotiationPersonality("childlike")
            .RegisterNegotiationDemand("macca")
            .RegisterEncounterEnvironment("thebel")
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
}

internal static class CleanSaveJsonCodec
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(RuntimeSaveGameSnapshot snapshot) =>
        JsonSerializer.Serialize(ToDto(snapshot), Options);

    public static RuntimeSaveGameSnapshot Deserialize(string json)
    {
        HostSaveGameDto dto = JsonSerializer.Deserialize<HostSaveGameDto>(json, Options)
            ?? throw new InvalidOperationException("Save JSON did not contain a save document.");
        return FromDto(dto);
    }

    private static HostSaveGameDto ToDto(RuntimeSaveGameSnapshot snapshot) =>
        new(
            snapshot.ContractVersion,
            snapshot.FrameworkVersion.ToString(),
            snapshot.Actors.Select(ToDto).ToArray(),
            ToDto(snapshot.PartyStock),
            ToDto(snapshot.Inventory),
            ToDto(snapshot.Equipment),
            snapshot.Wallet.Macca,
            ToDto(snapshot.Field),
            ToDto(snapshot.Compendium),
            ToDto(snapshot.Knowledge),
            ToDto(snapshot.Session),
            snapshot.Checkpoints.Entries.Select(ToDto).ToArray(),
            snapshot.HostContext.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value));

    private static RuntimeSaveGameSnapshot FromDto(HostSaveGameDto dto) =>
        new(
            SemanticVersion.Parse(dto.FrameworkVersion),
            dto.Actors.Select(FromDto),
            FromDto(dto.PartyStock),
            FromDto(dto.Inventory),
            FromDto(dto.Equipment),
            new RuntimeWalletSnapshot(dto.Macca),
            FromDto(dto.Field),
            FromDto(dto.Compendium),
            FromDto(dto.Knowledge),
            FromDto(dto.Session),
            new RuntimeCheckpointLogSnapshot(dto.Checkpoints.Select(FromDto)),
            dto.HostContext.Select(pair => new KeyValuePair<ContentId, string>(Id(pair.Key), pair.Value)),
            dto.ContractVersion);

    private static HostActorDto ToDto(RuntimeActorSnapshot actor) =>
        new(
            actor.Identity.InstanceId.ToString(),
            actor.Identity.EntityDefinitionId.ToString(),
            actor.Identity.ActorKindId.ToString(),
            actor.Identity.DisplayName,
            actor.Identity.DisplaySubtitle,
            actor.Ownership.ControllerId.ToString(),
            actor.Ownership.TeamId.ToString(),
            actor.Ownership.OwnerInstanceId?.ToString(),
            actor.Deployment.Deployment.ToString(),
            actor.Deployment.IsActive,
            actor.Deployment.HasSwappedThisTurn,
            actor.Progression.Level,
            actor.Progression.Experience,
            actor.Progression.LifetimeExperience,
            actor.Progression.UnspentStatPoints,
            actor.Resources.Select(resource => new HostResourceDto(resource.ResourceId.ToString(), resource.Current, resource.Maximum)).ToArray(),
            actor.BaseResourceValues.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Stats.BaseStats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Stats.EffectiveStats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Skills.LearnedSkillIds.Select(id => id.ToString()).ToArray(),
            actor.Skills.EquippedSkillIds.Select(id => id.ToString()).ToArray(),
            actor.Forms.ActiveForm is null ? null : ToDto(actor.Forms.ActiveForm),
            actor.Forms.PersonaStock.Select(ToDto).ToArray(),
            actor.Forms.DemonStock.Select(ToDto).ToArray(),
            actor.Equipment.EquippedItemIds.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString()),
            actor.BattleStatus.Ailments.Select(ToDto).ToArray(),
            actor.BattleStatus.Statuses.Select(ToDto).ToArray(),
            actor.BattleStatus.StatStages.Select(stage => new HostStatStageDto(stage.ModifierTrackId.ToString(), stage.Stage, stage.RemainingTurns)).ToArray(),
            actor.BattleStatus.Charges.Select(charge => new HostChargeDto(charge.Kind.ToString(), charge.Multiplier, charge.RemainingTurns)).ToArray(),
            actor.BattleStatus.Shields.Select(shield => new HostShieldDto(shield.Kind.ToString(), shield.RemainingTurns)).ToArray(),
            actor.BattleStatus.Breaks.Select(breakState => new HostBreakDto(breakState.Element.ToString(), breakState.RemainingTurns)).ToArray(),
            actor.BattleStatus.IsGuarding,
            actor.BattleStatus.Analysis.Select(analysis => new HostAnalysisDto(analysis.TargetInstanceId.ToString(), analysis.Layers.Select(layer => layer.ToString()).ToArray())).ToArray(),
            actor.BattleActivations.PassiveActivations.Select(passive => new HostPassiveActivationDto(
                passive.SkillId.ToString(),
                passive.EventId.ToString(),
                passive.TriggerIndex,
                passive.ActivationCount)).ToArray());

    private static RuntimeActorSnapshot FromDto(HostActorDto dto) =>
        new(
            new RuntimeActorIdentitySnapshot(Instance(dto.InstanceId), Id(dto.EntityDefinitionId), Id(dto.ActorKindId), dto.DisplayName, dto.DisplaySubtitle),
            new RuntimeActorOwnershipSnapshot(Id(dto.ControllerId), Id(dto.TeamId), dto.OwnerInstanceId is null ? null : Instance(dto.OwnerInstanceId)),
            new RuntimeActorDeploymentSnapshot(Enum.Parse<RuntimeActorDeployment>(dto.Deployment), dto.IsActive, dto.HasSwappedThisTurn),
            new RuntimeProgressionSnapshot(dto.Level, dto.Experience, dto.LifetimeExperience, dto.UnspentStatPoints),
            dto.Resources.Select(resource => new RuntimeResourceSnapshot(Id(resource.ResourceId), resource.Current, resource.Maximum)),
            new RuntimeStatBlockSnapshot(ToDecimalDictionary(dto.BaseStats), ToDecimalDictionary(dto.EffectiveStats)),
            new RuntimeSkillStateSnapshot(dto.LearnedSkillIds.Select(Id), dto.EquippedSkillIds.Select(Id)),
            new RuntimeFormStockSnapshot(
                dto.ActiveForm is null ? null : FromDto(dto.ActiveForm),
                dto.PersonaStock.Select(FromDto),
                dto.DemonStock.Select(FromDto)),
            new RuntimeEquipmentSnapshot(dto.EquippedItemIds.Select(pair => new KeyValuePair<EquipmentSlot, ContentId>(Enum.Parse<EquipmentSlot>(pair.Key), Id(pair.Value)))),
            new RuntimeBattleStatusSnapshot(
                dto.Ailments.Select(FromDto),
                dto.Statuses.Select(FromDto),
                dto.StatStages.Select(stage => new RuntimeStatStageSnapshot(Id(stage.ModifierTrackId), stage.Stage, stage.RemainingTurns)),
                dto.Charges.Select(charge => new RuntimeChargeSnapshot(Enum.Parse<ChargeKind>(charge.Kind), charge.Multiplier, charge.RemainingTurns)),
                dto.Shields.Select(shield => new RuntimeShieldSnapshot(Enum.Parse<ShieldKind>(shield.Kind), shield.RemainingTurns)),
                dto.Breaks.Select(breakState => new RuntimeBreakSnapshot(Enum.Parse<DamageElement>(breakState.Element), breakState.RemainingTurns)),
                dto.IsGuarding,
                dto.Analysis.Select(analysis => new RuntimeAnalysisSnapshot(
                    Instance(analysis.TargetInstanceId),
                    analysis.Layers.Select(layer => Enum.Parse<AnalysisLayer>(layer))))),
            new RuntimeBattleActivationSnapshot(dto.PassiveActivations.Select(passive => new RuntimePassiveActivationSnapshot(
                Id(passive.SkillId),
                Id(passive.EventId),
                passive.TriggerIndex,
                passive.ActivationCount))),
            ToDecimalDictionary(dto.BaseResourceValues));

    private static HostReferenceDto ToDto(RuntimeActorReferenceSnapshot reference) =>
        new(reference.InstanceId.ToString(), reference.EntityDefinitionId.ToString(), reference.DisplayName);

    private static RuntimeActorReferenceSnapshot FromDto(HostReferenceDto dto) =>
        new(Instance(dto.InstanceId), Id(dto.EntityDefinitionId), dto.DisplayName);

    private static HostTimedStateDto ToDto(RuntimeTimedStateSnapshot timed) =>
        new(timed.Id.ToString(), timed.RemainingTurns, timed.IsRemovable);

    private static RuntimeTimedStateSnapshot FromDto(HostTimedStateDto dto) =>
        new(Id(dto.Id), dto.RemainingTurns, dto.IsRemovable);

    private static HostPartyStockDto ToDto(RuntimePartyStockSnapshot snapshot) =>
        new(
            ToDto(snapshot.Owner),
            snapshot.OwnerLevel,
            snapshot.ActiveParty.Select(ToDto).ToArray(),
            snapshot.ReserveMembers.Select(ToDto).ToArray(),
            snapshot.ActiveForm is null ? null : ToDto(snapshot.ActiveForm),
            snapshot.PersonaStock.Select(ToDto).ToArray(),
            snapshot.DemonStock.Select(ToDto).ToArray(),
            snapshot.MaxActivePartySize);

    private static RuntimePartyStockSnapshot FromDto(HostPartyStockDto dto) =>
        new(
            FromDto(dto.Owner),
            dto.OwnerLevel,
            dto.ActiveParty.Select(FromDto),
            dto.ReserveMembers.Select(FromDto),
            dto.ActiveForm is null ? null : FromDto(dto.ActiveForm),
            dto.PersonaStock.Select(FromDto),
            dto.DemonStock.Select(FromDto),
            dto.MaxActivePartySize);

    private static HostInventoryDto ToDto(RuntimeInventorySnapshot snapshot) =>
        new(
            snapshot.ItemQuantities.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            snapshot.OwnedEquipmentIds.ToDictionary(
                pair => pair.Key.ToString(),
                pair => pair.Value.Select(id => id.ToString()).ToArray()));

    private static RuntimeInventorySnapshot FromDto(HostInventoryDto dto) =>
        new(
            dto.ItemQuantities.Select(pair => new KeyValuePair<ContentId, int>(Id(pair.Key), pair.Value)),
            dto.OwnedEquipmentIds.Select(pair => new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(
                Enum.Parse<EquipmentSlot>(pair.Key),
                pair.Value.Select(Id))));

    private static HostEquipmentDto ToDto(RuntimeEquipmentSnapshot snapshot) =>
        new(snapshot.EquippedItemIds.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString()));

    private static RuntimeEquipmentSnapshot FromDto(HostEquipmentDto dto) =>
        new(dto.EquippedItemIds.Select(pair => new KeyValuePair<EquipmentSlot, ContentId>(
            Enum.Parse<EquipmentSlot>(pair.Key),
            Id(pair.Value))));

    private static HostFieldDto? ToDto(RuntimeFieldSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new HostFieldDto(
                snapshot.Navigation.CurrentLocationId.ToString(),
                snapshot.DungeonProgress is null
                    ? null
                    : new HostDungeonProgressDto(
                        snapshot.DungeonProgress.DungeonId.ToString(),
                        snapshot.DungeonProgress.CurrentFloor,
                        snapshot.DungeonProgress.MaxFloorReached,
                        snapshot.DungeonProgress.UnlockedTerminals.ToArray(),
                        snapshot.DungeonProgress.DefeatedBossIds.Select(id => id.ToString()).ToArray()));

    private static RuntimeFieldSnapshot? FromDto(HostFieldDto? dto) =>
        dto is null
            ? null
            : new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(Id(dto.LocationId)),
                dto.DungeonProgress is null
                    ? null
                    : new RuntimeDungeonProgressSnapshot(
                        Id(dto.DungeonProgress.DungeonId),
                        dto.DungeonProgress.CurrentFloor,
                        dto.DungeonProgress.MaxFloorReached,
                        dto.DungeonProgress.UnlockedTerminals,
                        dto.DungeonProgress.DefeatedBossIds.Select(Id)));

    private static HostCompendiumDto ToDto(CompendiumStateSnapshot snapshot) =>
        new(snapshot.Entries.Select(entry => new HostCompendiumEntryDto(
            entry.SpeciesId.ToString(),
            entry.DisplayName,
            entry.Level,
            entry.Stats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            entry.SkillIds.Select(id => id.ToString()).ToArray(),
            entry.Experience,
            entry.LifetimeExperience)).ToArray());

    private static CompendiumStateSnapshot FromDto(HostCompendiumDto dto) =>
        new(dto.Entries.Select(entry => new CompendiumEntrySnapshot(
            Id(entry.SpeciesId),
            entry.DisplayName,
            entry.Level,
            entry.Stats.Select(pair => new KeyValuePair<ContentId, int>(Id(pair.Key), pair.Value)),
            entry.SkillIds.Select(Id),
            entry.Experience,
            entry.LifetimeExperience)));

    private static HostKnowledgeDto ToDto(RuntimeKnowledgeSnapshot snapshot) =>
        new(
            snapshot.ElementalAffinities.Select(entry => new HostElementalKnowledgeDto(entry.EntityId.ToString(), entry.Element.ToString(), entry.Affinity.ToString())).ToArray(),
            snapshot.AilmentResistances.Select(entry => new HostAilmentKnowledgeDto(entry.EntityId.ToString(), entry.AilmentId.ToString(), entry.Resistance.ToString())).ToArray(),
            snapshot.InstantDeathResistances.Select(entry => new HostInstantDeathKnowledgeDto(entry.EntityId.ToString(), entry.Channel.ToString(), entry.Resistance.ToString())).ToArray());

    private static RuntimeKnowledgeSnapshot FromDto(HostKnowledgeDto dto) =>
        new(
            dto.ElementalAffinities.Select(entry => new RuntimeElementalAffinityKnowledgeSnapshot(Id(entry.EntityId), Enum.Parse<DamageElement>(entry.Element), Enum.Parse<ElementalAffinity>(entry.Affinity))),
            dto.AilmentResistances.Select(entry => new RuntimeAilmentResistanceKnowledgeSnapshot(Id(entry.EntityId), Id(entry.AilmentId), Enum.Parse<ResistanceLevel>(entry.Resistance))),
            dto.InstantDeathResistances.Select(entry => new RuntimeInstantDeathResistanceKnowledgeSnapshot(Id(entry.EntityId), Enum.Parse<InstantDeathChannel>(entry.Channel), Enum.Parse<ResistanceLevel>(entry.Resistance))));

    private static HostSessionDto ToDto(RuntimeSessionProgressSnapshot snapshot) =>
        new(
            snapshot.MoonPhaseId?.ToString(),
            snapshot.ElapsedTicks,
            snapshot.Counters.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            snapshot.Flags.Select(id => id.ToString()).ToArray());

    private static RuntimeSessionProgressSnapshot FromDto(HostSessionDto dto) =>
        new(
            dto.MoonPhaseId is null ? null : Id(dto.MoonPhaseId),
            dto.ElapsedTicks,
            dto.Counters.Select(pair => new KeyValuePair<ContentId, long>(Id(pair.Key), pair.Value)),
            dto.Flags.Select(Id));

    private static HostCheckpointDto ToDto(RuntimeCheckpointEntrySnapshot entry) =>
        new(entry.Sequence, entry.Kind.ToString(), entry.Message, entry.ActorId?.ToString(), entry.ContentId?.ToString());

    private static RuntimeCheckpointEntrySnapshot FromDto(HostCheckpointDto dto) =>
        new(
            dto.Sequence,
            Enum.Parse<RuntimeCheckpointKind>(dto.Kind),
            dto.Message,
            dto.ActorId is null ? null : Instance(dto.ActorId),
            dto.ContentId is null ? null : Id(dto.ContentId));

    private static Dictionary<ContentId, decimal> ToDecimalDictionary(Dictionary<string, decimal> values) =>
        values.ToDictionary(pair => Id(pair.Key), pair => pair.Value);

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static RuntimeInstanceId Instance(string value) => RuntimeInstanceId.Parse(value);

    private sealed record HostSaveGameDto(
        int ContractVersion,
        string FrameworkVersion,
        HostActorDto[] Actors,
        HostPartyStockDto PartyStock,
        HostInventoryDto Inventory,
        HostEquipmentDto Equipment,
        int Macca,
        HostFieldDto? Field,
        HostCompendiumDto Compendium,
        HostKnowledgeDto Knowledge,
        HostSessionDto Session,
        HostCheckpointDto[] Checkpoints,
        Dictionary<string, string> HostContext);

    private sealed record HostActorDto(
        string InstanceId,
        string EntityDefinitionId,
        string ActorKindId,
        string DisplayName,
        string? DisplaySubtitle,
        string ControllerId,
        string TeamId,
        string? OwnerInstanceId,
        string Deployment,
        bool IsActive,
        bool HasSwappedThisTurn,
        int Level,
        long Experience,
        long LifetimeExperience,
        int UnspentStatPoints,
        HostResourceDto[] Resources,
        Dictionary<string, decimal> BaseResourceValues,
        Dictionary<string, decimal> BaseStats,
        Dictionary<string, decimal> EffectiveStats,
        string[] LearnedSkillIds,
        string[] EquippedSkillIds,
        HostReferenceDto? ActiveForm,
        HostReferenceDto[] PersonaStock,
        HostReferenceDto[] DemonStock,
        Dictionary<string, string> EquippedItemIds,
        HostTimedStateDto[] Ailments,
        HostTimedStateDto[] Statuses,
        HostStatStageDto[] StatStages,
        HostChargeDto[] Charges,
        HostShieldDto[] Shields,
        HostBreakDto[] Breaks,
        bool IsGuarding,
        HostAnalysisDto[] Analysis,
        HostPassiveActivationDto[] PassiveActivations);

    private sealed record HostResourceDto(string ResourceId, decimal Current, decimal Maximum);
    private sealed record HostReferenceDto(string InstanceId, string EntityDefinitionId, string DisplayName);
    private sealed record HostTimedStateDto(string Id, int? RemainingTurns, bool IsRemovable);
    private sealed record HostStatStageDto(string ModifierTrackId, int Stage, int? RemainingTurns);
    private sealed record HostChargeDto(string Kind, decimal Multiplier, int? RemainingTurns);
    private sealed record HostShieldDto(string Kind, int? RemainingTurns);
    private sealed record HostBreakDto(string Element, int? RemainingTurns);
    private sealed record HostAnalysisDto(string TargetInstanceId, string[] Layers);
    private sealed record HostPassiveActivationDto(string SkillId, string EventId, int TriggerIndex, int ActivationCount);
    private sealed record HostPartyStockDto(HostReferenceDto Owner, int OwnerLevel, HostReferenceDto[] ActiveParty, HostReferenceDto[] ReserveMembers, HostReferenceDto? ActiveForm, HostReferenceDto[] PersonaStock, HostReferenceDto[] DemonStock, int MaxActivePartySize);
    private sealed record HostInventoryDto(Dictionary<string, int> ItemQuantities, Dictionary<string, string[]> OwnedEquipmentIds);
    private sealed record HostEquipmentDto(Dictionary<string, string> EquippedItemIds);
    private sealed record HostFieldDto(string LocationId, HostDungeonProgressDto? DungeonProgress);
    private sealed record HostDungeonProgressDto(string DungeonId, int CurrentFloor, int MaxFloorReached, int[] UnlockedTerminals, string[] DefeatedBossIds);
    private sealed record HostCompendiumDto(HostCompendiumEntryDto[] Entries);
    private sealed record HostCompendiumEntryDto(string SpeciesId, string DisplayName, int Level, Dictionary<string, int> Stats, string[] SkillIds, long Experience, long LifetimeExperience);
    private sealed record HostKnowledgeDto(HostElementalKnowledgeDto[] ElementalAffinities, HostAilmentKnowledgeDto[] AilmentResistances, HostInstantDeathKnowledgeDto[] InstantDeathResistances);
    private sealed record HostElementalKnowledgeDto(string EntityId, string Element, string Affinity);
    private sealed record HostAilmentKnowledgeDto(string EntityId, string AilmentId, string Resistance);
    private sealed record HostInstantDeathKnowledgeDto(string EntityId, string Channel, string Resistance);
    private sealed record HostSessionDto(string? MoonPhaseId, long ElapsedTicks, Dictionary<string, long> Counters, string[] Flags);
    private sealed record HostCheckpointDto(long Sequence, string Kind, string Message, string? ActorId, string? ContentId);
}
