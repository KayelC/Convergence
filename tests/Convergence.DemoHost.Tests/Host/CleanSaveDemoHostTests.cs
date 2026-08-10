using System.Text.Json;
using System.Text.Json.Nodes;
using Convergence.DemoHost.Tests.TestSupport;
using Convergence.Content;
using Convergence.DemoHost;
using Convergence.Execution;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.DemoHost.Tests.Host;

public sealed class CleanSaveDemoHostTests
{
    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesRuntimeSaveFamilies()
    {
        RuntimeSaveGameSnapshot snapshot = CleanSaveTestFixture.CreateSaveSnapshot();

        string json = CleanSaveJsonCodec.Serialize(snapshot);
        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(json);

        Assert.Contains("\"contractVersion\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"ownerLevel\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Equipment\":", json, StringComparison.Ordinal);
        Assert.Contains("\"OwnedEquipmentInstances\"", json, StringComparison.Ordinal);
        Assert.Equal(RuntimeSaveGameSnapshot.CurrentContractVersion, restored.ContractVersion);
        Assert.Equal(snapshot.ContractVersion, restored.ContractVersion);
        Assert.Equal(snapshot.FrameworkVersion, restored.FrameworkVersion);
        Assert.Equal(snapshot.Actors.Select(actor => actor.Identity.InstanceId), restored.Actors.Select(actor => actor.Identity.InstanceId));
        Assert.Equal(
            snapshot.Actors.Select(actor => actor.CombatProfileIdentity),
            restored.Actors.Select(actor => actor.CombatProfileIdentity));
        Assert.Equal(snapshot.Actors[0].CapabilityIds, restored.Actors[0].CapabilityIds);
        Assert.Equal(
            snapshot.Actors[0].BattleActivations.PassiveSkillStates,
            restored.Actors[0].BattleActivations.PassiveSkillStates);
        Assert.Equal(snapshot.PartyRoster.ActiveParty.Select(actor => actor.InstanceId), restored.PartyRoster.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            snapshot.Inventory.ItemQuantities.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)),
            restored.Inventory.ItemQuantities.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)));
        Assert.Equal(
            snapshot.Inventory.OwnedEquipmentInstances
                .SelectMany(pair => pair.Value)
                .OrderBy(instance => instance.InstanceId.ToString()),
            restored.Inventory.OwnedEquipmentInstances
                .SelectMany(pair => pair.Value)
                .OrderBy(instance => instance.InstanceId.ToString()));
        Assert.Equal(
            snapshot.Actors[0].Equipment.EquippedInstanceIds,
            restored.Actors[0].Equipment.EquippedInstanceIds);
        Assert.Equal(snapshot.Wallet.Balance, restored.Wallet.Balance);
        Assert.Equal(
            snapshot.Field!.DungeonTraversal!.CurrentNodeId,
            restored.Field!.DungeonTraversal!.CurrentNodeId);
        Assert.Equal(snapshot.Compendium.Entries.Select(entry => entry.SpeciesId), restored.Compendium.Entries.Select(entry => entry.SpeciesId));
        Assert.Equal(
            snapshot.Compendium.Entries.Select(entry => entry.UnspentStatPoints),
            restored.Compendium.Entries.Select(entry => entry.UnspentStatPoints));
        Assert.Equal(
            snapshot.Compendium.Entries.SelectMany(entry => entry.EquippedSkillIds),
            restored.Compendium.Entries.SelectMany(entry => entry.EquippedSkillIds));
        Assert.Equal(snapshot.Knowledge.ElementalAffinities.Select(entry => entry.EntityId), restored.Knowledge.ElementalAffinities.Select(entry => entry.EntityId));
        Assert.Equal(
            snapshot.Session.Counters.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)),
            restored.Session.Counters.OrderBy(pair => pair.Key.ToString()).Select(pair => KeyValuePair.Create(pair.Key.ToString(), pair.Value)));
        Assert.Equal(snapshot.Checkpoints.Entries.Select(entry => entry.Sequence), restored.Checkpoints.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesAnalyzedDefenseProfilesAndKnownNormalSemantics()
    {
        ContentId entityId = ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo");
        var knowledge = new RuntimeKnowledgeSnapshot(
            elementalAffinities: null,
            ailmentResistances: null,
            instantDeathResistances: null,
            analyzedDefenses:
            [
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    entityId,
                    [
                        BattleAnalysisField.ElementalAffinities,
                        BattleAnalysisField.AilmentResistances,
                        BattleAnalysisField.InstantDeathResistances
                    ])
            ]);
        RuntimeSaveGameSnapshot snapshot = CleanSaveTestFixture.CreateSaveSnapshot(knowledge: knowledge);

        string json = CleanSaveJsonCodec.Serialize(snapshot);
        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(json);

        RuntimeAnalyzedDefenseKnowledgeSnapshot profile =
            Assert.Single(restored.Knowledge.AnalyzedDefenses);
        Assert.Equal(entityId, profile.EntityId);
        Assert.Equal(
            [
                BattleAnalysisField.ElementalAffinities,
                BattleAnalysisField.AilmentResistances,
                BattleAnalysisField.InstantDeathResistances
            ],
            profile.DisclosedFields);
        var view = new PersistentBattleKnowledgeView(restored.Knowledge);
        Assert.True(view.TryGetElementalAffinity(
            entityId,
            DamageElement.Fire,
            out ElementalAffinity affinity));
        Assert.Equal(ElementalAffinity.Normal, affinity);
    }

    [Fact]
    public void HostOwnedJsonWithoutAnalyzedProfiles_RemainsReadableAsEmptyKnowledge()
    {
        JsonObject root = JsonNode.Parse(CleanSaveJsonCodec.Serialize(
            CleanSaveTestFixture.CreateSaveSnapshot()))?.AsObject()
            ?? throw new InvalidOperationException("Expected a host save JSON object.");
        JsonObject knowledge = root["Knowledge"]?.AsObject()
            ?? throw new InvalidOperationException("Expected host-owned knowledge data.");
        Assert.True(knowledge.Remove("AnalyzedDefenses"));

        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(root.ToJsonString());

        Assert.Empty(restored.Knowledge.AnalyzedDefenses);
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesPendingSkillChoicesAndRevision()
    {
        RuntimeActorSnapshot original = CleanSaveTestFixture.CreateActor(
            RuntimeInstanceId.Parse("frost"),
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"));
        ContentId pendingSkillId =
            ContentId.Parse("convergence.clean_battle_demo:ember_bolt_demo");
        var actor = new RuntimeActorSnapshot(
            original.Identity,
            original.Affiliation,
            original.EncounterPresence,
            original.Progression,
            original.Resources,
            original.Stats,
            new RuntimeSkillStateSnapshot(
                original.Skills.LearnedSkillIds,
                original.Skills.EquippedSkillIds,
                [
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(17),
                        5,
                        pendingSkillId)
                ],
                revision: 9),
            original.Equipment,
            original.BattleStatus,
            original.BattleActivations,
            original.BaseResourceValues,
            original.VitalResourceId,
            original.CapabilityIds);
        RuntimeSaveGameSnapshot snapshot =
            CleanSaveTestFixture.CreateSaveSnapshot(actors: [actor]);

        RuntimeSaveGameSnapshot restored =
            CleanSaveJsonCodec.Deserialize(CleanSaveJsonCodec.Serialize(snapshot));

        RuntimeSkillStateSnapshot skills = Assert.Single(restored.Actors).Skills;
        Assert.Equal(9, skills.Revision);
        RuntimePendingSkillChoiceSnapshot choice = Assert.Single(skills.PendingChoices);
        Assert.Equal(new RuntimeSkillChoiceToken(17), choice.Token);
        Assert.Equal(5, choice.UnlockLevel);
        Assert.Equal(pendingSkillId, choice.SkillId);
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesCanonicalActorBattleStateAndDurationKinds()
    {
        ContentId passiveSkillId =
            ContentId.Parse("convergence.skill_system_redesign_sample:ice_boost_sample");
        RuntimeActorSnapshot original = CleanSaveTestFixture.CreateActor(
            RuntimeInstanceId.Parse("frost"),
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
            learnedSkills:
            [
                ContentId.Parse("convergence.clean_battle_demo:frost_lance_demo"),
                passiveSkillId
            ]);
        var actor = new RuntimeActorSnapshot(
            original.Identity,
            original.Affiliation,
            original.EncounterPresence,
            original.Progression,
            original.Resources,
            original.Stats,
            original.Skills,
            original.Equipment,
            new RuntimeBattleStatusSnapshot(
                statuses:
                [
                    new RuntimeTimedStateSnapshot(
                        ContentId.Parse("focused"),
                        StandardStatusLifetimes.Persistent)
                ],
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    ContentId.Parse("test.pack:timed_contribution"),
                    [
                        new RuntimeStatModifierTrackSnapshot(
                            ContentId.Parse("attack"),
                            2,
                            [
                                new RuntimeStatModifierContributionSnapshot(
                                    1,
                                    2,
                                    new TurnDurationDefinition(
                                        3,
                                        ContentId.Parse("owner_turn_end"),
                                        false),
                                    new StatModifierLifecycleBoundary(
                                        ContentId.Parse("owner_turn_end"),
                                        4))
                            ])
                    ]),
                chargeState: new RuntimeChargeStateSnapshot(
                    StandardChargePolicyIds.Split,
                    [new RuntimeChargeSnapshot(
                        ChargeKind.Magical,
                        2.5m,
                        StandardStatusLifetimes.DeploymentTransient)]),
                affinityOverrides:
                [
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Ice,
                        ElementalAffinity.Resist,
                        StandardStatusLifetimes.Encounter(new BattleDurationDefinition()))
                ],
                affinityBreaks:
                [
                    new RuntimeAffinityBreakSnapshot(
                        DamageElement.Fire,
                        StandardStatusLifetimes.Encounter(
                            new TurnDurationDefinition(
                                2,
                                ContentId.Parse("owner_turn_end"),
                                true)))
                ],
                isGuarding: true),
            new RuntimeBattleActivationSnapshot(
                passiveActivations:
                [
                    new RuntimePassiveActivationSnapshot(
                        passiveSkillId,
                        ContentId.Parse("owner_turn_end"),
                        triggerIndex: 0,
                        activationCount: 1,
                        targetInstanceId: RuntimeInstanceId.Parse("frost"))
                ],
                passiveSkillStates:
                [
                    new RuntimePassiveSkillStateSnapshot(
                        passiveSkillId,
                        IsEnabled: false)
                ]),
            original.BaseResourceValues,
            original.VitalResourceId,
            [ContentId.Parse("analyze"), ContentId.Parse("swap_hosted_entity")]);
        RuntimeSaveGameSnapshot snapshot = CleanSaveTestFixture.CreateSaveSnapshot(actors: [actor]);

        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(CleanSaveJsonCodec.Serialize(snapshot));
        RuntimeActorSnapshot restoredActor = Assert.Single(restored.Actors);

        Assert.Equal(original.VitalResourceId, restoredActor.VitalResourceId);
        RuntimeTimedStateSnapshot restoredStatus = Assert.Single(restoredActor.BattleStatus.Statuses);
        Assert.IsType<PermanentDurationDefinition>(restoredStatus.Duration);
        Assert.True(restoredStatus.Lifetime.Allows(StatusRemovalCause.DispelEffect));
        Assert.False(restoredStatus.Lifetime.Allows(StatusRemovalCause.BattleEnd));
        RuntimeStatModifierStateSnapshot modifiers = Assert.IsType<RuntimeStatModifierStateSnapshot>(
            restoredActor.BattleStatus.StatModifiers);
        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(
            Assert.Single(modifiers.Tracks).Contributions);
        Assert.IsType<TurnDurationDefinition>(contribution.Duration);
        Assert.Equal(4, contribution.LastLifecycleBoundary?.Sequence);
        RuntimeChargeStateSnapshot charges = Assert.IsType<RuntimeChargeStateSnapshot>(
            restoredActor.BattleStatus.ChargeState);
        Assert.Equal(StandardChargePolicyIds.Split, charges.PolicyId);
        RuntimeChargeSnapshot restoredCharge = Assert.Single(charges.Charges);
        Assert.Equal(2.5m, restoredCharge.Multiplier);
        Assert.True(restoredCharge.Lifetime.Allows(StatusRemovalCause.DeploymentSwap));
        RuntimeAffinityBreakSnapshot restoredBreak = Assert.Single(restoredActor.BattleStatus.AffinityBreaks);
        Assert.IsType<TurnDurationDefinition>(restoredBreak.Duration);
        Assert.Equal(DamageElement.Fire, restoredBreak.Element);
        Assert.False(restoredBreak.Lifetime.Allows(StatusRemovalCause.DeploymentSwap));
        Assert.IsType<BattleDurationDefinition>(Assert.Single(restoredActor.BattleStatus.AffinityOverrides).Duration);
        Assert.True(restoredActor.BattleStatus.IsGuarding);
        Assert.Equal(
            [ContentId.Parse("analyze"), ContentId.Parse("swap_hosted_entity")],
            restoredActor.CapabilityIds);
        Assert.False(Assert.Single(restoredActor.BattleActivations.PassiveSkillStates).IsEnabled);
        RuntimePassiveActivationSnapshot passiveActivation =
            Assert.Single(restoredActor.BattleActivations.PassiveActivations);
        Assert.Equal(RuntimeInstanceId.Parse("frost"), passiveActivation.TargetInstanceId);
    }

    [Fact]
    public void CleanSaveDemo_ValidatesSerializesRestoresAndExitsWithoutInput()
    {
        using var output = new StringWriter();
        int exitCode = new CleanSaveDemoHost(output, Path.Combine(AppContext.BaseDirectory, "Content")).Run();

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains(
            $"[save] Created runtime save snapshot v{RuntimeSaveGameSnapshot.CurrentContractVersion}",
            text,
            StringComparison.Ordinal);
        Assert.Contains("[serialize] Host-owned JSON round-trip completed", text, StringComparison.Ordinal);
        Assert.Contains("[validate] Restored snapshot validated with 0 diagnostic(s).", text, StringComparison.Ordinal);
        Assert.Contains("[restore] Restored 2 actor(s), 1 item stack(s), dungeon node convergence.catalog_surface_sample:floor_5.", text, StringComparison.Ordinal);
        Assert.Contains("[outcome] Clean save demo completed successfully.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsMalformedHostOwnedJson()
    {
        Assert.Throws<JsonException>(() => CleanSaveJsonCodec.Deserialize("{"));
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsPreProfileIdentitySaveBeforeActorDecoding()
    {
        JsonObject root = JsonNode.Parse(CleanSaveJsonCodec.Serialize(
            CleanSaveTestFixture.CreateSaveSnapshot()))?.AsObject()
            ?? throw new InvalidOperationException("Expected a host save JSON object.");
        root["ContractVersion"] = RuntimeSaveGameSnapshot.CurrentContractVersion - 1;
        JsonObject actor = root["Actors"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected an actor record.");
        actor.Remove("CombatProfileSourceActorInstanceId");
        actor.Remove("CombatProfileSourceEntityDefinitionId");
        actor.Remove("CombatProfileRevision");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CleanSaveJsonCodec.Deserialize(root.ToJsonString()));

        Assert.Contains("unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            RuntimeSaveGameSnapshot.CurrentContractVersion.ToString(),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsRetainedChargesWithoutPolicyIdentity()
    {
        JsonObject root = JsonNode.Parse(CleanSaveJsonCodec.Serialize(
            CleanSaveTestFixture.CreateSaveSnapshot()))?.AsObject()
            ?? throw new InvalidOperationException("Save JSON did not produce an object.");
        JsonObject actor = root["Actors"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Save JSON did not contain an actor.");
        actor["ChargePolicyId"] = null;
        actor["Charges"] = new JsonArray(
            new JsonObject
            {
                ["Kind"] = ChargeKind.Physical.ToString(),
                ["Multiplier"] = 2m,
                ["Duration"] = null
            });

        Assert.Throws<JsonException>(() => CleanSaveJsonCodec.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void HostOwnedJsonCorruption_ReportsDuplicateCompendiumSkillsAndNegativeStats()
    {
        string json = CleanSaveJsonCodec.Serialize(CleanSaveTestFixture.CreateSaveSnapshot());
        JsonObject root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("Expected a host save JSON object.");
        JsonObject entry = root["Compendium"]?["Entries"]?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected a Compendium entry.");
        JsonArray learnedSkills = entry["SkillIds"]?.AsArray()
            ?? throw new InvalidOperationException("Expected learned skills.");
        JsonArray equippedSkills = entry["EquippedSkillIds"]?.AsArray()
            ?? throw new InvalidOperationException("Expected equipped skills.");
        JsonObject stats = entry["Stats"]?.AsObject()
            ?? throw new InvalidOperationException("Expected Compendium stats.");
        learnedSkills.Add(learnedSkills[0]?.GetValue<string>());
        equippedSkills.Add(equippedSkills[0]?.GetValue<string>());
        stats["strength"] = -1;

        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(root.ToJsonString());
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            restored,
            CleanSaveTestFixture.LoadCatalog());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicateCompendiumLearnedSkill &&
            diagnostic.Path == "$.compendium.entries[0].skillIds[1]");
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicateCompendiumEquippedSkill &&
            diagnostic.Path == "$.compendium.entries[0].equippedSkillIds[1]");
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.InvalidCompendiumStatValue &&
            diagnostic.Path == "$.compendium.entries[0].stats['strength']");
    }

    [Fact]
    public void HostOwnedJsonCorruption_ReportsUnknownAndMissingCompendiumStats()
    {
        string json = CleanSaveJsonCodec.Serialize(CleanSaveTestFixture.CreateSaveSnapshot());
        JsonObject root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("Expected a host save JSON object.");
        JsonObject stats = root["Compendium"]?["Entries"]?[0]?["Stats"]?.AsObject()
            ?? throw new InvalidOperationException("Expected Compendium stats.");
        Assert.True(stats.Remove("luck"));
        stats["forged_stat"] = 4;

        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(root.ToJsonString());
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            restored,
            CleanSaveTestFixture.LoadCatalog());

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.UnknownCompendiumStat &&
            diagnostic.ContentId == ContentId.Parse("forged_stat"));
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.MissingCompendiumStat &&
            diagnostic.ContentId == ContentId.Parse("luck"));
    }

    [Fact]
    public void HostOwnedJsonCorruption_ReportsDuplicateKnowledgeKeysInEveryChannel()
    {
        ContentId entityId = ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo");
        ContentId ailmentId = ContentId.Parse("convergence.shared_effects_demo:poison_demo");
        RuntimeSaveGameSnapshot snapshot = CleanSaveTestFixture.CreateSaveSnapshot(
            knowledge: new RuntimeKnowledgeSnapshot(
                elementalAffinities:
                [
                    new RuntimeElementalAffinityKnowledgeSnapshot(
                        entityId,
                        DamageElement.Ice,
                        ElementalAffinity.Weak)
                ],
                ailmentResistances:
                [
                    new RuntimeAilmentResistanceKnowledgeSnapshot(
                        entityId,
                        ailmentId,
                        ResistanceLevel.Normal)
                ],
                instantDeathResistances:
                [
                    new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                        entityId,
                        InstantDeathChannel.Light,
                        ResistanceLevel.Normal)
                ]));
        JsonObject root = JsonNode.Parse(CleanSaveJsonCodec.Serialize(snapshot))?.AsObject()
            ?? throw new InvalidOperationException("Expected a host save JSON object.");
        JsonObject knowledge = root["Knowledge"]?.AsObject()
            ?? throw new InvalidOperationException("Expected host-owned knowledge data.");
        foreach (string collectionName in new[]
                 {
                     "ElementalAffinities",
                     "AilmentResistances",
                     "InstantDeathResistances"
                 })
        {
            JsonArray entries = knowledge[collectionName]?.AsArray()
                ?? throw new InvalidOperationException($"Expected knowledge collection '{collectionName}'.");
            entries.Add(entries[0]?.DeepClone());
        }

        RuntimeSaveGameSnapshot restored = CleanSaveJsonCodec.Deserialize(root.ToJsonString());
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            restored,
            CleanSaveTestFixture.LoadCatalog());

        Assert.Equal(2, restored.Knowledge.ElementalAffinities.Count);
        Assert.Equal(2, restored.Knowledge.AilmentResistances.Count);
        Assert.Equal(2, restored.Knowledge.InstantDeathResistances.Count);
        Assert.Collection(
            validation.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateElementalAffinityKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.elementalAffinities[1]", diagnostic.Path);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateAilmentResistanceKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.ailmentResistances[1]", diagnostic.Path);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateInstantDeathResistanceKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.instantDeathResistances[1]", diagnostic.Path);
            });
    }

    [Fact]
    public void HostOwnedJsonCorruption_RejectsStoredIntrinsicElementDuringDecoding()
    {
        JsonObject root = JsonNode.Parse(CleanSaveJsonCodec.Serialize(
            CleanSaveTestFixture.CreateSaveSnapshot()))?.AsObject()
            ?? throw new InvalidOperationException("Expected a host save JSON object.");
        JsonArray elemental = root["Knowledge"]?["ElementalAffinities"]?.AsArray()
            ?? throw new InvalidOperationException("Expected elemental knowledge data.");
        JsonObject entry = elemental[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected an elemental knowledge entry.");
        entry["Element"] = DamageElement.Almighty.ToString();
        entry["Affinity"] = ElementalAffinity.Weak.ToString();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CleanSaveJsonCodec.Deserialize(root.ToJsonString()));

        Assert.Contains("cannot be stored", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesSaveRecordMetadata()
    {
        RuntimeSaveRecord record = new(
            RuntimeSaveKind.Suspend,
            CleanSaveTestFixture.CreateSaveSnapshot(),
            new RuntimeSaveContextSnapshot(ContentId.Parse("dungeon_menu")),
            sequence: 42);

        string json = CleanSaveJsonCodec.SerializeRecord(record);
        RuntimeSaveRecord restored = CleanSaveJsonCodec.DeserializeRecord(json);

        Assert.Contains("\"kind\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"contentPacks\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RuntimeSaveKind.Suspend, restored.Kind);
        Assert.Equal(ContentId.Parse("dungeon_menu"), restored.Context.ContextId);
        Assert.False(restored.Context.HasPendingHostAction);
        Assert.Equal(42, restored.Sequence);
        Assert.Equal(record.Snapshot.ContractVersion, restored.Snapshot.ContractVersion);
        Assert.Equal(record.Snapshot.ContentPacks, restored.Snapshot.ContentPacks);
        Assert.Equal(record.Snapshot.Wallet.Balance, restored.Snapshot.Wallet.Balance);
    }

    [Fact]
    public void CleanSaveJsonCodec_RejectsMalformedHostOwnedSaveRecordJson()
    {
        Assert.Throws<JsonException>(() => CleanSaveJsonCodec.DeserializeRecord("{"));
    }

    [Fact]
    public void HostOwnedJsonRoundTrip_PreservesOptionalNavigationAndDungeonState()
    {
        RuntimeSaveGameSnapshot noField = CleanSaveTestFixture.CreateSaveSnapshot(
            includeDefaultField: false);
        RuntimeSaveGameSnapshot navigationOnly = CleanSaveTestFixture.CreateSaveSnapshot(
            field: new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(ContentId.Parse("host_owned_location"))));

        RuntimeSaveGameSnapshot restoredNoField = CleanSaveJsonCodec.Deserialize(
            CleanSaveJsonCodec.Serialize(noField));
        RuntimeSaveGameSnapshot restoredNavigationOnly = CleanSaveJsonCodec.Deserialize(
            CleanSaveJsonCodec.Serialize(navigationOnly));

        Assert.Null(restoredNoField.Field);
        Assert.Equal(
            ContentId.Parse("host_owned_location"),
            restoredNavigationOnly.Field!.Navigation.CurrentLocationId);
        Assert.Null(restoredNavigationOnly.Field.DungeonTraversal);
    }

}
