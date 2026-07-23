using System.Text.Json;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Hosting;
using Convergence.Fusion;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.DemoHost;

internal sealed class CleanSaveDemoHost
{
    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;

    public CleanSaveDemoHost(TextWriter output, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Content")),
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
                        "reference/skill-system-redesign/skill_system_redesign.manifest.sample.json",
                        [
                            "skill_system_redesign.races.sample.json",
                            "skill_system_redesign.skills.sample.json",
                            "skill_system_redesign.entities.sample.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "demos/clean-battle/clean_battle_demo.manifest.json",
                        [
                            "clean_battle_demo.races.json",
                            "clean_battle_demo.skills.json",
                            "clean_battle_demo.entities.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "demos/shared-effects/shared_effects_demo.manifest.json",
                        [
                            "shared_effects_demo.ailments.json",
                            "shared_effects_demo.skills.json",
                            "shared_effects_demo.entities.json",
                            "shared_effects_demo.items.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "reference/catalog-surface/catalog_surface_sample.manifest.json",
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
        var rulesetResolver = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard());
        RuntimeSaveGameSnapshot snapshot = BuildDemoSnapshot();
        ChargePolicyRegistry chargePolicies = ChargePolicyRegistry.CreateStandard();
        RuntimeSaveValidator validator = new(
            rulesetBindings: rulesetResolver,
            chargePolicies: chargePolicies);
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

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new DemoBattleActorInitializationPolicy(),
            catalog);
        RuntimeSessionRestoreResult aggregate = new RuntimeSessionRestoreService(
                validator,
                actorFactory,
                ActorStatRestoreProfileResolver.Instance,
                rulesetBindings: rulesetResolver,
                chargePolicies: chargePolicies)
            .Restore(restored, catalog);
        if (!aggregate.IsSuccess)
        {
            foreach (RuntimeSessionRestoreDiagnostic diagnostic in aggregate.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken);
            }
            return 4;
        }

        RuntimeRestoredSession restoredSession = aggregate.RequireSession();
        restored = restoredSession.Snapshot;
        CatalogBattleActor[] restoredActors = restoredSession.Actors.ToArray();

        await _eventSink.PublishAsync(
            $"001 [save] Created runtime save snapshot v{restored.ContractVersion} with {restoredActors.Length} actor(s).",
            cancellationToken);
        await _eventSink.PublishAsync(
            $"002 [serialize] Host-owned JSON round-trip completed with {json.Length} character(s).",
            cancellationToken);
        await _eventSink.PublishAsync(
            $"003 [validate] Restored snapshot validated with {aggregate.Diagnostics.Count} diagnostic(s).",
            cancellationToken);
        string fieldSummary = restored.Field?.DungeonTraversal is RuntimeDungeonTraversalSnapshot dungeonTraversal
            ? $"dungeon node {dungeonTraversal.CurrentNodeId}"
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
            [
                new ContentPackIdentity("convergence.skill_system_redesign_sample", SemanticVersion.Parse("0.7.0")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("0.7.0")),
                new ContentPackIdentity("convergence.shared_effects_demo", SemanticVersion.Parse("0.7.0")),
                new ContentPackIdentity("convergence.catalog_surface_sample", SemanticVersion.Parse("0.7.0"))
            ],
            [frost, ember],
            new RuntimePartyRosterSnapshot(
                frostRef,
                activeParty: [frostRef],
                activeHostedEntity: emberRef,
                hostedEntityRoster: [emberRef],
                companionRoster: [frostRef]),
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
                new RuntimeNavigationSnapshot(ContentId.Parse("convergence.catalog_surface_sample:sample_depths_floor_5")),
                new RuntimeDungeonTraversalSnapshot(
                    ContentId.Parse("convergence.catalog_surface_sample:sample_depths"),
                    ContentId.Parse("convergence.catalog_surface_sample:floor_5"),
                    visitedNodeIds:
                    [
                        ContentId.Parse("convergence.catalog_surface_sample:floor_1"),
                        ContentId.Parse("convergence.catalog_surface_sample:floor_5")
                    ],
                    unlockedCheckpointIds:
                    [
                        ContentId.Parse("convergence.catalog_surface_sample:terminal_1"),
                        ContentId.Parse("convergence.catalog_surface_sample:terminal_5")
                    ],
                    defeatedBossIds: [ContentId.Parse("convergence.catalog_surface_sample:entry_block_training_sample")])),
            new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
                    "Frost Duelist",
                    5,
                    [
                        new KeyValuePair<ContentId, int>(ContentId.Parse("strength"), 4),
                        new KeyValuePair<ContentId, int>(ContentId.Parse("magic"), 8),
                        new KeyValuePair<ContentId, int>(ContentId.Parse("vitality"), 5),
                        new KeyValuePair<ContentId, int>(ContentId.Parse("agility"), 6),
                        new KeyValuePair<ContentId, int>(ContentId.Parse("luck"), 4)
                    ],
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
                moonPhaseId: null,
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
            new RuntimeActorIdentitySnapshot(instanceId, entityId, ContentId.Parse("companion"), entityId.ToString()),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("host"), ContentId.Parse("player_team")),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeProgressionSnapshot(5, 0, 0, 0),
            [
                new RuntimeResourceSnapshot(ContentId.Parse("hp"), 50, 75),
                new RuntimeResourceSnapshot(ContentId.Parse("sp"), 20, 30)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(ContentId.Parse("magic"), 8)],
                [new KeyValuePair<ContentId, decimal>(ContentId.Parse("magic"), 8)]),
            new RuntimeSkillStateSnapshot(skillIds, skillIds),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(ContentId.Parse("hp"), 40)],
            ContentId.Parse("hp"));

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
            .RegisterEntityKind("companion")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", new AcceptAnyParametersValidator())
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
}

internal sealed class ActorStatRestoreProfileResolver : IRuntimeActorRestoreProfileResolver
{
    public static ActorStatRestoreProfileResolver Instance { get; } = new();

    public RuntimeActorRestoreProfile Resolve(RuntimeActorRestoreProfileRequest request) =>
        new(RuntimeStatSourceKind.Actor, MissingHostedEntityBehavior.UseActorBaseStats);
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

    public static string SerializeRecord(RuntimeSaveRecord record) =>
        JsonSerializer.Serialize(ToDto(record), Options);

    public static RuntimeSaveRecord DeserializeRecord(string json)
    {
        HostSaveRecordDto dto = JsonSerializer.Deserialize<HostSaveRecordDto>(json, Options)
            ?? throw new InvalidOperationException("Save JSON did not contain a save record.");
        return FromDto(dto);
    }

    private static HostSaveRecordDto ToDto(RuntimeSaveRecord record) =>
        new(
            record.Kind.ToString(),
            record.Context.ContextId.ToString(),
            record.Context.HasPendingHostAction,
            record.Sequence,
            ToDto(record.Snapshot));

    private static RuntimeSaveRecord FromDto(HostSaveRecordDto dto) =>
        new(
            Enum.Parse<RuntimeSaveKind>(dto.Kind),
            FromDto(dto.Snapshot),
            new RuntimeSaveContextSnapshot(Id(dto.ContextId), dto.ContextHasPendingHostAction),
            dto.Sequence);

    private static HostSaveGameDto ToDto(RuntimeSaveGameSnapshot snapshot) =>
        new(
            snapshot.ContractVersion,
            snapshot.FrameworkVersion.ToString(),
            snapshot.ContentPacks
                .Select(pack => new HostContentPackDto(pack.Id, pack.Version.ToString()))
                .ToArray(),
            snapshot.Actors.Select(ToDto).ToArray(),
            ToDto(snapshot.PartyRoster),
            ToDto(snapshot.Inventory),
            ToDto(snapshot.Equipment),
            snapshot.Wallet.Balance,
            ToDto(snapshot.Field),
            ToDto(snapshot.Compendium),
            ToDto(snapshot.Knowledge),
            ToDto(snapshot.Session),
            snapshot.Checkpoints.Entries.Select(ToDto).ToArray(),
            snapshot.HostContext.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value));

    private static RuntimeSaveGameSnapshot FromDto(HostSaveGameDto dto) =>
        new(
            SemanticVersion.Parse(dto.FrameworkVersion),
            (dto.ContentPacks ?? [])
                .Select(pack => new ContentPackIdentity(pack.Id, SemanticVersion.Parse(pack.Version))),
            dto.Actors.Select(FromDto),
            FromDto(dto.PartyRoster),
            FromDto(dto.Inventory),
            FromDto(dto.Equipment),
            new RuntimeWalletSnapshot(dto.Credits),
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
            actor.Affiliation.CommandAuthorityId.ToString(),
            actor.Affiliation.TeamId.ToString(),
            actor.EncounterPresence.IsDeployed,
            actor.EncounterPresence.HasSwappedThisTurn,
            actor.Progression.Level,
            actor.Progression.Experience,
            actor.Progression.LifetimeExperience,
            actor.Progression.UnspentStatPoints,
            actor.VitalResourceId.ToString(),
            actor.Resources.Select(resource => new HostResourceDto(resource.ResourceId.ToString(), resource.Current, resource.Maximum)).ToArray(),
            actor.BaseResourceValues.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Stats.BaseStats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Stats.EffectiveStats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Skills.LearnedSkillIds.Select(id => id.ToString()).ToArray(),
            actor.Skills.EquippedSkillIds.Select(id => id.ToString()).ToArray(),
            actor.Skills.Revision,
            actor.Skills.PendingChoices.Select(choice => new HostPendingSkillChoiceDto(
                choice.Token.Value,
                choice.UnlockLevel,
                choice.SkillId.ToString())).ToArray(),
            actor.CapabilityIds.Select(id => id.ToString()).ToArray(),
            actor.Equipment.EquippedItemIds.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString()),
            actor.BattleStatus.Ailments.Select(ToDto).ToArray(),
            actor.BattleStatus.Statuses.Select(ToDto).ToArray(),
            ToDto(actor.BattleStatus.StatModifiers),
            actor.BattleStatus.ChargeState?.PolicyId.ToString(),
            actor.BattleStatus.Charges.Select(charge => new HostChargeDto(
                charge.Kind.ToString(), charge.Multiplier, ToLifetimeDto(charge.Lifetime))).ToArray(),
            actor.BattleStatus.Shields.Select(shield => new HostShieldDto(
                shield.Kind.ToString(), ToLifetimeDto(shield.Lifetime))).ToArray(),
            actor.BattleStatus.AffinityBreaks.Select(affinityBreak => new HostAffinityBreakDto(
                affinityBreak.Element.ToString(),
                ToLifetimeDto(affinityBreak.Lifetime))).ToArray(),
            actor.BattleStatus.AffinityOverrides.Select(affinity => new HostAffinityOverrideDto(
                affinity.Element.ToString(), affinity.Affinity.ToString(), ToLifetimeDto(affinity.Lifetime))).ToArray(),
            actor.BattleStatus.IsGuarding,
            actor.BattleStatus.Analysis.Select(analysis => new HostAnalysisDto(analysis.TargetInstanceId.ToString(), analysis.Layers.Select(layer => layer.ToString()).ToArray())).ToArray(),
            actor.BattleActivations.PassiveSkillStates.Select(passive => new HostPassiveSkillStateDto(
                passive.SkillId.ToString(),
                passive.IsEnabled)).ToArray(),
            actor.BattleActivations.PassiveActivations.Select(passive => new HostPassiveActivationDto(
                passive.SkillId.ToString(),
                passive.EventId.ToString(),
                passive.TriggerIndex,
                passive.ActivationCount,
                passive.TargetInstanceId?.ToString())).ToArray());

    private static RuntimeActorSnapshot FromDto(HostActorDto dto) =>
        new(
            new RuntimeActorIdentitySnapshot(Instance(dto.InstanceId), Id(dto.EntityDefinitionId), Id(dto.ActorKindId), dto.DisplayName, dto.DisplaySubtitle),
            new RuntimeActorAffiliationSnapshot(Id(dto.CommandAuthorityId), Id(dto.TeamId)),
            new RuntimeEncounterPresenceSnapshot(dto.IsDeployed, dto.HasSwappedThisTurn),
            new RuntimeProgressionSnapshot(dto.Level, dto.Experience, dto.LifetimeExperience, dto.UnspentStatPoints),
            dto.Resources.Select(resource => new RuntimeResourceSnapshot(Id(resource.ResourceId), resource.Current, resource.Maximum)),
            new RuntimeStatBlockSnapshot(ToDecimalDictionary(dto.BaseStats), ToDecimalDictionary(dto.EffectiveStats)),
            new RuntimeSkillStateSnapshot(
                dto.LearnedSkillIds.Select(Id),
                dto.EquippedSkillIds.Select(Id),
                (dto.PendingSkillChoices ?? []).Select(choice =>
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(choice.Token),
                        choice.UnlockLevel,
                        Id(choice.SkillId))),
                dto.SkillRevision),
            new RuntimeEquipmentSnapshot(dto.EquippedItemIds.Select(pair => new KeyValuePair<EquipmentSlot, ContentId>(Enum.Parse<EquipmentSlot>(pair.Key), Id(pair.Value)))),
            new RuntimeBattleStatusSnapshot(
                dto.Ailments.Select(FromDto),
                dto.Statuses.Select(FromDto),
                FromDto(dto.StatModifiers),
                FromDto(dto.ChargePolicyId, dto.Charges),
                dto.Shields.Select(shield => new RuntimeShieldSnapshot(
                    Enum.Parse<ShieldKind>(shield.Kind), FromLifetimeDto(shield.Lifetime))),
                dto.AffinityOverrides.Select(affinity => new RuntimeAffinityOverrideSnapshot(
                    Enum.Parse<DamageElement>(affinity.Element),
                    Enum.Parse<ElementalAffinity>(affinity.Affinity),
                    FromLifetimeDto(affinity.Lifetime))),
                dto.IsGuarding,
                dto.Analysis.Select(analysis => new RuntimeAnalysisSnapshot(
                    Instance(analysis.TargetInstanceId),
                    analysis.Layers.Select(layer => Enum.Parse<AnalysisLayer>(layer)))),
                (dto.AffinityBreaks ?? []).Select(affinityBreak => new RuntimeAffinityBreakSnapshot(
                    Enum.Parse<DamageElement>(affinityBreak.Element),
                    FromLifetimeDto(affinityBreak.Lifetime)))),
            new RuntimeBattleActivationSnapshot(
                (dto.PassiveActivations ?? []).Select(passive => new RuntimePassiveActivationSnapshot(
                    Id(passive.SkillId),
                    Id(passive.EventId),
                    passive.TriggerIndex,
                    passive.ActivationCount,
                    passive.TargetInstanceId is null ? null : Instance(passive.TargetInstanceId))),
                (dto.PassiveSkillStates ?? []).Select(passive => new RuntimePassiveSkillStateSnapshot(
                    Id(passive.SkillId),
                    passive.IsEnabled))),
            ToDecimalDictionary(dto.BaseResourceValues),
            Id(dto.VitalResourceId),
            (dto.CapabilityIds ?? []).Select(Id));

    private static HostReferenceDto ToDto(RuntimeActorReferenceSnapshot reference) =>
        new(reference.InstanceId.ToString(), reference.EntityDefinitionId.ToString(), reference.DisplayName);

    private static RuntimeActorReferenceSnapshot FromDto(HostReferenceDto dto) =>
        new(Instance(dto.InstanceId), Id(dto.EntityDefinitionId), dto.DisplayName);

    private static HostTimedStateDto ToDto(RuntimeTimedStateSnapshot timed) =>
        new(timed.Id.ToString(), ToLifetimeDto(timed.Lifetime));

    private static RuntimeTimedStateSnapshot FromDto(HostTimedStateDto dto) =>
        new(Id(dto.Id), FromLifetimeDto(dto.Lifetime));

    private static HostStatModifierStateDto? ToDto(RuntimeStatModifierStateSnapshot? state) =>
        state is null
            ? null
            : new HostStatModifierStateDto(
                state.PolicyId.ToString(),
                state.Tracks.Select(track => new HostStatModifierTrackDto(
                    track.ModifierTrackId.ToString(),
                    track.ResolvedStage,
                    track.Contributions.Select(contribution => new HostStatModifierContributionDto(
                        contribution.Sequence,
                        contribution.StageDelta,
                        ToDto(contribution.Duration),
                        contribution.LastLifecycleBoundary is null
                            ? null
                            : new HostStatModifierBoundaryDto(
                                contribution.LastLifecycleBoundary.EventId.ToString(),
                                contribution.LastLifecycleBoundary.Sequence))).ToArray())).ToArray());

    private static RuntimeStatModifierStateSnapshot? FromDto(HostStatModifierStateDto? state) =>
        state is null
            ? null
            : new RuntimeStatModifierStateSnapshot(
                Id(state.PolicyId),
                state.Tracks.Select(track => new RuntimeStatModifierTrackSnapshot(
                    Id(track.ModifierTrackId),
                    track.ResolvedStage,
                    track.Contributions.Select(contribution =>
                        new RuntimeStatModifierContributionSnapshot(
                            contribution.Sequence,
                            contribution.StageDelta,
                            FromDto(contribution.Duration),
                            contribution.LastLifecycleBoundary is null
                                ? null
                                : new StatModifierLifecycleBoundary(
                                    Id(contribution.LastLifecycleBoundary.EventId),
                                    contribution.LastLifecycleBoundary.Sequence))))));

    private static RuntimeChargeStateSnapshot? FromDto(
        string? policyId,
        IReadOnlyList<HostChargeDto> charges)
    {
        ArgumentNullException.ThrowIfNull(charges);
        if (policyId is null)
        {
            if (charges.Count > 0)
            {
                throw new JsonException("Retained charges require a charge-policy ID.");
            }

            return null;
        }

        return new RuntimeChargeStateSnapshot(
            Id(policyId),
            charges.Select(charge => new RuntimeChargeSnapshot(
                Enum.Parse<ChargeKind>(charge.Kind),
                charge.Multiplier,
                FromLifetimeDto(charge.Lifetime))));
    }

    private static HostStatusLifetimeDto ToLifetimeDto(StatusLifetimeDefinition lifetime) =>
        new(
            ToDto(lifetime.Expiration)!,
            lifetime.RemovalProfile.AllowedCauses.Select(cause => cause.ToString()).ToArray());

    private static StatusLifetimeDefinition FromLifetimeDto(HostStatusLifetimeDto lifetime) =>
        new(
            FromDto(lifetime.Expiration) ??
                throw new InvalidOperationException("Status lifetime expiration is required."),
            new StatusRemovalProfileDefinition(
                lifetime.AllowedRemovalCauses.Select(Enum.Parse<StatusRemovalCause>)));

    private static HostDurationDto? ToDto(DurationDefinition? duration) => duration switch
    {
        null => null,
        InstantDurationDefinition => new HostDurationDto(DurationKind.Instant.ToString()),
        TurnDurationDefinition turns => new HostDurationDto(
            DurationKind.Turns.ToString(),
            turns.Value,
            turns.TickEventId.ToString(),
            turns.SuspendWhileReserve),
        PhaseDurationDefinition phase => new HostDurationDto(
            DurationKind.Phase.ToString(),
            PhaseId: phase.PhaseId.ToString()),
        BattleDurationDefinition => new HostDurationDto(DurationKind.Battle.ToString()),
        PermanentDurationDefinition => new HostDurationDto(DurationKind.Permanent.ToString()),
        _ => throw new InvalidOperationException($"Unsupported duration type '{duration.GetType().Name}'.")
    };

    private static DurationDefinition? FromDto(HostDurationDto? duration) => duration is null
        ? null
        : Enum.Parse<DurationKind>(duration.Kind) switch
        {
            DurationKind.Instant => new InstantDurationDefinition(),
            DurationKind.Turns => new TurnDurationDefinition(
                duration.Value ?? throw new InvalidOperationException("Turn duration value is required."),
                Id(duration.TickEventId ?? throw new InvalidOperationException("Turn duration tick event is required.")),
                duration.SuspendWhileReserve ?? false),
            DurationKind.Phase => new PhaseDurationDefinition(
                Id(duration.PhaseId ?? throw new InvalidOperationException("Phase duration ID is required."))),
            DurationKind.Battle => new BattleDurationDefinition(),
            DurationKind.Permanent => new PermanentDurationDefinition(),
            _ => throw new InvalidOperationException($"Unsupported duration kind '{duration.Kind}'.")
        };

    private static HostPartyRosterDto ToDto(RuntimePartyRosterSnapshot snapshot) =>
        new(
            ToDto(snapshot.Owner),
            snapshot.ActiveParty.Select(ToDto).ToArray(),
            snapshot.ReserveMembers.Select(ToDto).ToArray(),
            snapshot.ActiveHostedEntity is null ? null : ToDto(snapshot.ActiveHostedEntity),
            snapshot.HostedEntityRoster.Select(ToDto).ToArray(),
            snapshot.CompanionRoster.Select(ToDto).ToArray(),
            snapshot.MaxActivePartySize);

    private static RuntimePartyRosterSnapshot FromDto(HostPartyRosterDto dto) =>
        new(
            FromDto(dto.Owner),
            dto.ActiveParty.Select(FromDto),
            dto.ReserveMembers.Select(FromDto),
            dto.ActiveHostedEntity is null ? null : FromDto(dto.ActiveHostedEntity),
            dto.HostedEntityRoster.Select(FromDto),
            dto.CompanionRoster.Select(FromDto),
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
                snapshot.DungeonTraversal is null
                    ? null
                    : new HostDungeonTraversalDto(
                        snapshot.DungeonTraversal.DungeonId.ToString(),
                        snapshot.DungeonTraversal.CurrentNodeId.ToString(),
                        snapshot.DungeonTraversal.VisitedNodeIds.Select(id => id.ToString()).ToArray(),
                        snapshot.DungeonTraversal.UnlockedCheckpointIds.Select(id => id.ToString()).ToArray(),
                        snapshot.DungeonTraversal.DefeatedBossIds.Select(id => id.ToString()).ToArray()));

    private static RuntimeFieldSnapshot? FromDto(HostFieldDto? dto) =>
        dto is null
            ? null
            : new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(Id(dto.LocationId)),
                dto.DungeonTraversal is null
                    ? null
                    : new RuntimeDungeonTraversalSnapshot(
                        Id(dto.DungeonTraversal.DungeonId),
                        Id(dto.DungeonTraversal.CurrentNodeId),
                        dto.DungeonTraversal.VisitedNodeIds.Select(Id),
                        dto.DungeonTraversal.UnlockedCheckpointIds.Select(Id),
                        dto.DungeonTraversal.DefeatedBossIds.Select(Id)));

    private static HostCompendiumDto ToDto(CompendiumStateSnapshot snapshot) =>
        new(snapshot.Entries.Select(entry => new HostCompendiumEntryDto(
            entry.SpeciesId.ToString(),
            entry.DisplayName,
            entry.Level,
            entry.Stats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            entry.SkillIds.Select(id => id.ToString()).ToArray(),
            entry.Experience,
            entry.LifetimeExperience,
            entry.UnspentStatPoints,
            entry.EquippedSkillIds.Select(id => id.ToString()).ToArray())).ToArray());

    private static CompendiumStateSnapshot FromDto(HostCompendiumDto dto) =>
        new(dto.Entries.Select(entry => new CompendiumEntrySnapshot(
            Id(entry.SpeciesId),
            entry.DisplayName,
            entry.Level,
            entry.Stats.Select(pair => new KeyValuePair<ContentId, int>(Id(pair.Key), pair.Value)),
            entry.SkillIds.Select(Id),
            entry.Experience,
            entry.LifetimeExperience,
            entry.UnspentStatPoints,
            (entry.EquippedSkillIds ?? entry.SkillIds).Select(Id))));

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

    private sealed record HostSaveRecordDto(
        string Kind,
        string ContextId,
        bool ContextHasPendingHostAction,
        long Sequence,
        HostSaveGameDto Snapshot);

    private sealed record HostSaveGameDto(
        int ContractVersion,
        string FrameworkVersion,
        HostContentPackDto[]? ContentPacks,
        HostActorDto[] Actors,
        HostPartyRosterDto PartyRoster,
        HostInventoryDto Inventory,
        HostEquipmentDto Equipment,
        int Credits,
        HostFieldDto? Field,
        HostCompendiumDto Compendium,
        HostKnowledgeDto Knowledge,
        HostSessionDto Session,
        HostCheckpointDto[] Checkpoints,
        Dictionary<string, string> HostContext);

    private sealed record HostContentPackDto(string Id, string Version);

    private sealed record HostActorDto(
        string InstanceId,
        string EntityDefinitionId,
        string ActorKindId,
        string DisplayName,
        string? DisplaySubtitle,
        string CommandAuthorityId,
        string TeamId,
        bool IsDeployed,
        bool HasSwappedThisTurn,
        int Level,
        long Experience,
        long LifetimeExperience,
        int UnspentStatPoints,
        string VitalResourceId,
        HostResourceDto[] Resources,
        Dictionary<string, decimal> BaseResourceValues,
        Dictionary<string, decimal> BaseStats,
        Dictionary<string, decimal> EffectiveStats,
        string[] LearnedSkillIds,
        string[] EquippedSkillIds,
        long SkillRevision,
        HostPendingSkillChoiceDto[]? PendingSkillChoices,
        string[]? CapabilityIds,
        Dictionary<string, string> EquippedItemIds,
        HostTimedStateDto[] Ailments,
        HostTimedStateDto[] Statuses,
        HostStatModifierStateDto? StatModifiers,
        string? ChargePolicyId,
        HostChargeDto[] Charges,
        HostShieldDto[] Shields,
        HostAffinityBreakDto[]? AffinityBreaks,
        HostAffinityOverrideDto[] AffinityOverrides,
        bool IsGuarding,
        HostAnalysisDto[] Analysis,
        HostPassiveSkillStateDto[]? PassiveSkillStates,
        HostPassiveActivationDto[]? PassiveActivations);

    private sealed record HostResourceDto(string ResourceId, decimal Current, decimal Maximum);
    private sealed record HostPendingSkillChoiceDto(long Token, int UnlockLevel, string SkillId);
    private sealed record HostReferenceDto(string InstanceId, string EntityDefinitionId, string DisplayName);
    private sealed record HostTimedStateDto(string Id, HostStatusLifetimeDto Lifetime);
    private sealed record HostStatModifierStateDto(
        string PolicyId,
        HostStatModifierTrackDto[] Tracks);
    private sealed record HostStatModifierTrackDto(
        string ModifierTrackId,
        int ResolvedStage,
        HostStatModifierContributionDto[] Contributions);
    private sealed record HostStatModifierContributionDto(
        long Sequence,
        int StageDelta,
        HostDurationDto? Duration,
        HostStatModifierBoundaryDto? LastLifecycleBoundary);
    private sealed record HostStatModifierBoundaryDto(string EventId, long Sequence);
    private sealed record HostChargeDto(string Kind, decimal Multiplier, HostStatusLifetimeDto Lifetime);
    private sealed record HostShieldDto(string Kind, HostStatusLifetimeDto Lifetime);
    private sealed record HostAffinityBreakDto(string Element, HostStatusLifetimeDto Lifetime);
    private sealed record HostAffinityOverrideDto(string Element, string Affinity, HostStatusLifetimeDto Lifetime);
    private sealed record HostStatusLifetimeDto(
        HostDurationDto Expiration,
        string[] AllowedRemovalCauses);
    private sealed record HostDurationDto(
        string Kind,
        int? Value = null,
        string? TickEventId = null,
        bool? SuspendWhileReserve = null,
        string? PhaseId = null);
    private sealed record HostAnalysisDto(string TargetInstanceId, string[] Layers);
    private sealed record HostPassiveSkillStateDto(string SkillId, bool IsEnabled);
    private sealed record HostPassiveActivationDto(
        string SkillId,
        string EventId,
        int TriggerIndex,
        int ActivationCount,
        string? TargetInstanceId);
    private sealed record HostPartyRosterDto(HostReferenceDto Owner, HostReferenceDto[] ActiveParty, HostReferenceDto[] ReserveMembers, HostReferenceDto? ActiveHostedEntity, HostReferenceDto[] HostedEntityRoster, HostReferenceDto[] CompanionRoster, int MaxActivePartySize);
    private sealed record HostInventoryDto(Dictionary<string, int> ItemQuantities, Dictionary<string, string[]> OwnedEquipmentIds);
    private sealed record HostEquipmentDto(Dictionary<string, string> EquippedItemIds);
    private sealed record HostFieldDto(string LocationId, HostDungeonTraversalDto? DungeonTraversal);
    private sealed record HostDungeonTraversalDto(string DungeonId, string CurrentNodeId, string[] VisitedNodeIds, string[] UnlockedCheckpointIds, string[] DefeatedBossIds);
    private sealed record HostCompendiumDto(HostCompendiumEntryDto[] Entries);
    private sealed record HostCompendiumEntryDto(
        string SpeciesId,
        string DisplayName,
        int Level,
        Dictionary<string, int> Stats,
        string[] SkillIds,
        long Experience,
        long LifetimeExperience,
        int UnspentStatPoints,
        string[]? EquippedSkillIds);
    private sealed record HostKnowledgeDto(HostElementalKnowledgeDto[] ElementalAffinities, HostAilmentKnowledgeDto[] AilmentResistances, HostInstantDeathKnowledgeDto[] InstantDeathResistances);
    private sealed record HostElementalKnowledgeDto(string EntityId, string Element, string Affinity);
    private sealed record HostAilmentKnowledgeDto(string EntityId, string AilmentId, string Resistance);
    private sealed record HostInstantDeathKnowledgeDto(string EntityId, string Channel, string Resistance);
    private sealed record HostSessionDto(string? MoonPhaseId, long ElapsedTicks, Dictionary<string, long> Counters, string[] Flags);
    private sealed record HostCheckpointDto(long Sequence, string Kind, string Message, string? ActorId, string? ContentId);
}
