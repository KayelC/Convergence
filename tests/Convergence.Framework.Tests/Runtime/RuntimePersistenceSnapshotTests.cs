using System.Reflection;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Fusion;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimePersistenceSnapshotTests
{
    private static readonly ContentId PersistentModifierRuleset =
        Id("convergence.catalog_surface_sample:persistent_staged_modifiers_sample");
    private static readonly ContentId TimedContributionModifierRuleset =
        Id("convergence.catalog_surface_sample:timed_contribution_modifiers_sample");

    [Fact]
    public void RuntimeSaveSnapshot_ValidatesRepresentativeCleanSessionAndRestoresActors()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot();

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        RuntimeSaveGameSnapshot valid = result.RequireValidSnapshot();
        RuntimeActorSnapshot restored = RuntimeActorState.Restore(
            valid.Actors[0],
            CombatDefenseProfile.Empty,
            [catalog.Skills[Id("convergence.skill_system_redesign_sample:ice_boost_sample")]])
            .ToSnapshot();
        Assert.Equal(Id("convergence.clean_battle_demo:frost_duelist_demo"), restored.Identity.EntityDefinitionId);
        Assert.Equal(Id("convergence.shared_effects_demo:medicine_demo"), valid.Inventory.ItemQuantities.Keys.Single());
        Assert.Equal(
            Id("convergence.catalog_surface_sample:sample_depths"),
            valid.Field!.DungeonTraversal!.DungeonId);
        Assert.Equal(2, valid.Checkpoints.Entries.Count);
    }

    [Fact]
    public void PersistenceSnapshots_RejectNullCollectionEntriesBeforeValidation()
    {
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();

        Assert.Throws<ArgumentException>(() => Copy(
            baseline,
            contentPacks: new ContentPackIdentity[] { null! }));
        Assert.Throws<ArgumentException>(() => Copy(
            baseline,
            actors: new RuntimeActorSnapshot[] { null! }));
        RuntimeActorReferenceSnapshot owner = Reference(baseline.Actors[0]);
        Assert.Throws<ArgumentException>(() => new RuntimePartyRosterSnapshot(
            owner,
            hostedEntityRoster: new RuntimeActorReferenceSnapshot[] { null! }));
        Assert.Throws<ArgumentException>(() => new RuntimeKnowledgeSnapshot(
            elementalAffinities: new RuntimeElementalAffinityKnowledgeSnapshot[] { null! }));
        Assert.Throws<ArgumentException>(() => new RuntimeCheckpointLogSnapshot(
            new RuntimeCheckpointEntrySnapshot[] { null! }));
        Assert.Throws<ArgumentException>(() => new CompendiumStateSnapshot(
            new CompendiumEntrySnapshot[] { null! }));
    }

    [Fact]
    public void PersistenceSnapshots_RejectInvalidPackIdentityAndNullDictionaryValues()
    {
        SemanticVersion version = SemanticVersion.Parse("0.8.0");
        Assert.Throws<ArgumentException>(() => new ContentPackIdentity(null!, version));
        Assert.Throws<ArgumentException>(() => new ContentPackIdentity(" ", version));

        var identity = new ContentPackIdentity("test.pack", version);
        Assert.Throws<ArgumentException>(() => identity with { Id = null! });

        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        Assert.Throws<ArgumentException>(() => new RuntimeSaveGameSnapshot(
            baseline.FrameworkVersion,
            baseline.ContentPacks,
            baseline.Actors,
            baseline.PartyRoster,
            baseline.Inventory,
            baseline.Equipment,
            baseline.Wallet,
            baseline.Field,
            baseline.Compendium,
            baseline.Knowledge,
            baseline.Session,
            baseline.Checkpoints,
            [new KeyValuePair<ContentId, string>(Id("scene"), null!)],
            baseline.ContractVersion));
    }

    [Fact]
    public void RuntimeSaveValidator_ApprovedActorsRestoreThroughCatalogFactory()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(CreateSaveSnapshot(), catalog);
        var factory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Diagnostics.Select(item => item.Message)));
        foreach (RuntimeActorSnapshot actor in validation.RequireValidSnapshot().Actors)
        {
            CatalogBattleActorCreationResult restored = factory.Restore(ActorRestore(actor));

            Assert.True(
                restored.IsSuccess,
                string.Join(Environment.NewLine, restored.Diagnostics.Select(item => item.Message)));
            RuntimeActorSnapshot roundTrip = restored.RequireActor().State.ToSnapshot();
            Assert.Equal(actor.Identity, roundTrip.Identity);
            Assert.Equal(actor.Affiliation, roundTrip.Affiliation);
            Assert.Equal(actor.Progression, roundTrip.Progression);
            Assert.All(roundTrip.Resources, resource =>
                Assert.InRange(resource.Current, 0m, resource.Maximum));
            Assert.Equal(StandardProgressionIds.CoreStats.Count, roundTrip.Stats.EffectiveStats.Count);
            Assert.Equal(actor.Skills.LearnedSkillIds, roundTrip.Skills.LearnedSkillIds);
            Assert.Equal(actor.Skills.EquippedSkillIds, roundTrip.Skills.EquippedSkillIds);
        }
    }

    [Fact]
    public void RuntimeSessionRestoreService_RestoresHostedEntityBeforeVesselAndReturnsSnapshotOrder()
    {
        ContentId pendingSkillId =
            Id("convergence.clean_battle_demo:frost_lance_demo");
        GameDataCatalog catalog = WithEntitySkillUnlock(
            LoadCatalog(),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            new SkillUnlockDefinition(5, pendingSkillId));
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot savedEmber = baseline.Actors.Single(actor =>
            actor.Identity.InstanceId == RuntimeInstanceId.Parse("ember"));
        RuntimeActorSnapshot savedVessel = baseline.Actors.Single(actor =>
            actor.Identity.InstanceId == RuntimeInstanceId.Parse("frost"));
        ContentId emberBolt =
            Id("convergence.clean_battle_demo:ember_bolt_demo");
        ContentId iceBoost =
            Id("convergence.skill_system_redesign_sample:ice_boost_sample");
        RuntimeActorSnapshot ember = CopyActor(
            savedEmber,
            stats: new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 12)],
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 12)]),
            skills: new RuntimeSkillStateSnapshot(
                [emberBolt, iceBoost],
                [emberBolt, iceBoost],
                [new RuntimePendingSkillChoiceSnapshot(
                    new RuntimeSkillChoiceToken(41),
                    5,
                    pendingSkillId)],
                revision: 7),
            battleActivations: new RuntimeBattleActivationSnapshot(
                passiveSkillStates:
                [
                    new RuntimePassiveSkillStateSnapshot(
                        iceBoost,
                        IsEnabled: true)
                ]));
        RuntimeActorSnapshot vessel = CopyActor(
            savedVessel,
            stats: new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 2)],
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 2)]),
            skills: new RuntimeSkillStateSnapshot(
                [pendingSkillId, iceBoost],
                [pendingSkillId, iceBoost],
                revision: 2),
            battleActivations: new RuntimeBattleActivationSnapshot(
                passiveSkillStates:
                [
                    new RuntimePassiveSkillStateSnapshot(
                        iceBoost,
                        IsEnabled: false)
                ]),
            combatProfileIdentity: new RuntimeCombatProfileIdentitySnapshot(
                savedEmber.Identity.InstanceId,
                savedEmber.Identity.EntityDefinitionId,
                revision: 9));
        RuntimeSaveGameSnapshot snapshot = Copy(baseline, actors: [vessel, ember]);
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        var profiles = new DelegateActorRestoreProfileResolver(request =>
            request.Actor.Identity.InstanceId == vessel.Identity.InstanceId
                ? new RuntimeActorRestoreProfile(
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution)
                : ActorProfile());
        var service = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(),
            factory,
            profiles);

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        Assert.Equal([ember.Identity.InstanceId, vessel.Identity.InstanceId], factory.RestoreOrder);
        RuntimeRestoredSession session = result.RequireSession();
        Assert.Equal(
            [vessel.Identity.InstanceId, ember.Identity.InstanceId],
            session.Actors.Select(actor => actor.State.InstanceId));
        Assert.Same(session.Actors[0], session.ActorsByInstanceId[vessel.Identity.InstanceId]);
        CatalogBattleActor restoredVessel =
            session.ActorsByInstanceId[vessel.Identity.InstanceId];
        CatalogBattleActor restoredHostedEntity =
            session.ActorsByInstanceId[ember.Identity.InstanceId];
        Assert.Equal(
            restoredHostedEntity.State.Stats,
            restoredVessel.State.Stats);
        Assert.Equal(
            restoredHostedEntity.State.Skills.LearnedSkillIds,
            restoredVessel.State.Skills.LearnedSkillIds);
        Assert.Equal(
            restoredHostedEntity.State.Skills.EquippedSkillIds,
            restoredVessel.State.Skills.EquippedSkillIds);
        Assert.Equal(7, restoredHostedEntity.State.Skills.Revision);
        Assert.Single(restoredHostedEntity.State.Skills.PendingChoices);
        Assert.Equal(7, restoredVessel.State.Skills.Revision);
        Assert.Empty(restoredVessel.State.Skills.PendingChoices);
        Assert.Equal(vessel.CombatProfileIdentity, restoredVessel.State.CombatProfileIdentity);
        Assert.False(Assert.Single(
            restoredVessel.State.ToSnapshot()
                .BattleActivations.PassiveSkillStates).IsEnabled);
        RuntimeActorSnapshot normalizedVessel = session.Snapshot.Actors.Single(actor =>
            actor.Identity.InstanceId == vessel.Identity.InstanceId);
        RuntimeActorSnapshot normalizedHostedEntity =
            session.Snapshot.Actors.Single(actor =>
                actor.Identity.InstanceId == ember.Identity.InstanceId);
        Assert.Equal(
            restoredVessel.State.Skills.LearnedSkillIds,
            normalizedVessel.Skills.LearnedSkillIds);
        Assert.Equal(
            restoredVessel.State.Stats.OrderBy(pair => pair.Key.ToString()),
            normalizedVessel.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString()));
        Assert.Equal(
            restoredHostedEntity.State.Skills.PendingChoices,
            normalizedHostedEntity.Skills.PendingChoices);
        Assert.Equal(
            restoredHostedEntity.State.Skills.Revision,
            normalizedHostedEntity.Skills.Revision);
        Assert.Equal(vessel.CombatProfileIdentity, normalizedVessel.CombatProfileIdentity);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CatalogBattleActor>)session.Actors).Add(session.Actors[0]));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<RuntimeInstanceId, CatalogBattleActor>)session.ActorsByInstanceId)
            .Add(RuntimeInstanceId.Parse("late_actor"), session.Actors[0]));
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMissingAndMismatchedCombatProfileSources()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        RuntimeActorSnapshot ember = baseline.Actors[1];
        RuntimeActorSnapshot missing = CopyActor(
            frost,
            combatProfileIdentity: new RuntimeCombatProfileIdentitySnapshot(
                RuntimeInstanceId.Parse("missing_profile_source"),
                ember.Identity.EntityDefinitionId,
                revision: 1));

        RuntimeSaveValidationResult missingResult = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [missing, ember]),
            catalog);

        Assert.Contains(
            missingResult.Diagnostics,
            diagnostic =>
                diagnostic.Code == RuntimeSaveValidationCode.CombatProfileSourceMissing &&
                diagnostic.Path == "$.actors[0].combatProfileIdentity.sourceActorInstanceId");

        RuntimeActorSnapshot mismatched = CopyActor(
            frost,
            combatProfileIdentity: new RuntimeCombatProfileIdentitySnapshot(
                ember.Identity.InstanceId,
                frost.Identity.EntityDefinitionId,
                revision: 1));
        RuntimeSaveValidationResult mismatchResult = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [mismatched, ember]),
            catalog);

        Assert.Contains(
            mismatchResult.Diagnostics,
            diagnostic =>
                diagnostic.Code == RuntimeSaveValidationCode.CombatProfileSourceEntityMismatch &&
                diagnostic.Path == "$.actors[0].combatProfileIdentity.sourceEntityDefinitionId");
    }

    [Fact]
    public void RuntimeSessionRestoreService_RejectsActiveHostedEntityProfileForNonOwner()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        RuntimeActorSnapshot ember = baseline.Actors[1];
        RuntimeSaveGameSnapshot snapshot = Copy(baseline, actors: [frost, ember]);
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        var service = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(),
            factory,
            new DelegateActorRestoreProfileResolver(request => new RuntimeActorRestoreProfile(
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution)));

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSessionRestoreDiagnosticCode.ActorProfileResolutionFailed &&
            diagnostic.ActorId == ember.Identity.InstanceId);
        Assert.Empty(factory.RestoreOrder);
    }

    [Fact]
    public void RuntimeSessionRestoreService_RejectsMissingHostedEntityDependencyWithoutPartialRestore()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeInstanceId vesselId = baseline.Actors[0].Identity.InstanceId;
        RuntimeActorReferenceSnapshot missing = new(
            RuntimeInstanceId.Parse("missing_hosted_entity"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            "Missing Hosted Entity");
        RuntimeSaveGameSnapshot snapshot = Copy(
            baseline,
            partyRoster: baseline.PartyRoster.With(
                activeHostedEntity: missing,
                replaceActiveHostedEntity: true,
                hostedEntityRoster: [missing]));
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        var service = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(),
            factory,
            new DelegateActorRestoreProfileResolver(request =>
                request.Actor.Identity.InstanceId == vesselId
                    ? new RuntimeActorRestoreProfile(
                        RuntimeStatSourceKind.ActiveHostedEntity,
                        MissingHostedEntityBehavior.RejectStatResolution)
                    : ActorProfile()));

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSessionRestoreDiagnosticCode.SaveValidationRejected &&
            diagnostic.SaveValidationCode == RuntimeSaveValidationCode.MissingActorReference);
        Assert.Null(result.Session);
    }

    [Fact]
    public void RuntimeSessionRestoreService_ExposesNoPartialSessionWhenAnActorRestoreFails()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot();
        RuntimeInstanceId failingId = snapshot.Actors[1].Identity.InstanceId;
        var factory = new RecordingActorFactory(
            new CatalogBattleActorFactory(catalog, catalog, new RestoreOnlyInitializationPolicy(), catalog),
            failingId);
        var service = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(),
            factory,
            new DelegateActorRestoreProfileResolver(_ => ActorProfile()));

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Equal(snapshot.Actors.Select(actor => actor.Identity.InstanceId), factory.RestoreOrder);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSessionRestoreDiagnosticCode.ActorRestoreFailed &&
            diagnostic.ActorId == failingId);
    }

    [Fact]
    public void RuntimeSessionRestoreService_UsesExplicitMigrationSeamAndRejectsMissingPath()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot oldSnapshot = CreateSaveSnapshot(contractVersion: 8);
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        var profiles = new DelegateActorRestoreProfileResolver(_ => ActorProfile());

        RuntimeSessionRestoreResult missingPath = new RuntimeSessionRestoreService(
                new RuntimeSaveValidator(),
                factory,
                profiles)
            .Restore(oldSnapshot, catalog);
        Assert.False(missingPath.IsSuccess);
        Assert.Equal(RuntimeSessionRestoreDiagnosticCode.MigrationRejected, Assert.Single(missingPath.Diagnostics).Code);
        Assert.Empty(factory.RestoreOrder);

        RuntimeSaveGameSnapshot migratedSnapshot = CreateSaveSnapshot();
        RuntimeSessionRestoreResult migrated = new RuntimeSessionRestoreService(
                new RuntimeSaveValidator(),
                factory,
                profiles,
                new RuntimeSaveMigrationService(
                    [new FixedMigrationStep(8, RuntimeSaveGameSnapshot.CurrentContractVersion, migratedSnapshot)]))
            .Restore(oldSnapshot, catalog);

        Assert.True(migrated.IsSuccess, string.Join(Environment.NewLine, migrated.Diagnostics.Select(item => item.Message)));
        Assert.Equal(
            RuntimeSaveGameSnapshot.CurrentContractVersion,
            migrated.RequireSession().Snapshot.ContractVersion);
    }

    [Fact]
    public void RetainedChargeState_RequiresMatchingPolicyForValidationAndAggregateRestore()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot chargedActor = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(
                chargeState: new RuntimeChargeStateSnapshot(
                    StandardChargePolicyIds.Split,
                    [new RuntimeChargeSnapshot(
                        ChargeKind.Physical,
                        2m,
                        StandardStatusLifetimes.DeploymentTransient)])));
        RuntimeSaveGameSnapshot snapshot = Copy(
            baseline,
            actors: [chargedActor, baseline.Actors[1]]);
        ChargePolicyRegistry policies = ChargePolicyRegistry.CreateStandard();

        RuntimeSaveValidationResult missingResolver = new RuntimeSaveValidator()
            .Validate(snapshot, catalog);
        Assert.Contains(missingResolver.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorChargePolicyResolverMissing &&
            diagnostic.Path == "$.actors[0].battleStatus.chargeState.policyId");

        RuntimeActorSnapshot incompatibleActor = CopyActor(
            chargedActor,
            battleStatus: new RuntimeBattleStatusSnapshot(
                chargeState: new RuntimeChargeStateSnapshot(
                    StandardChargePolicyIds.Split,
                    [new RuntimeChargeSnapshot(
                        ChargeKind.General,
                        2m,
                        StandardStatusLifetimes.DeploymentTransient)])));
        RuntimeSaveValidationResult incompatible = new RuntimeSaveValidator(chargePolicies: policies)
            .Validate(Copy(baseline, actors: [incompatibleActor, baseline.Actors[1]]), catalog);
        Assert.Contains(incompatible.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorChargeStateInvalid &&
            diagnostic.ChargePolicyCode == ChargePolicyDiagnosticCode.UnsupportedChargeKind);

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog);
        var profiles = new DelegateActorRestoreProfileResolver(_ => ActorProfile());
        RuntimeSaveValidator validator = new(chargePolicies: policies);

        RuntimeSessionRestoreResult unresolved = new RuntimeSessionRestoreService(
                validator,
                actorFactory,
                profiles)
            .Restore(snapshot, catalog);
        Assert.False(unresolved.IsSuccess);
        Assert.Contains(unresolved.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSessionRestoreDiagnosticCode.ChargePolicyResolutionFailed &&
            diagnostic.ActorId == chargedActor.Identity.InstanceId);

        RuntimeSessionRestoreResult restored = new RuntimeSessionRestoreService(
                validator,
                actorFactory,
                profiles,
                chargePolicies: policies)
            .Restore(snapshot, catalog);
        Assert.True(restored.IsSuccess, string.Join(
            Environment.NewLine,
            restored.Diagnostics.Select(diagnostic => diagnostic.Message)));
        CatalogBattleActor restoredActor = restored.RequireSession().ActorsByInstanceId[
            chargedActor.Identity.InstanceId];
        Assert.Equal(StandardChargePolicyIds.Split, restoredActor.State.ChargePolicyId);
        Assert.Equal(2m, restoredActor.State.Charges[ChargeKind.Physical].Multiplier);

        RuntimeActorSnapshot disabledActor = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(
                chargeState: new RuntimeChargeStateSnapshot(StandardChargePolicyIds.Disabled)));
        RuntimeSaveGameSnapshot disabledSnapshot = Copy(
            baseline,
            actors: [disabledActor, baseline.Actors[1]]);
        RuntimeSaveValidationResult disabledValidation = validator.Validate(disabledSnapshot, catalog);
        Assert.True(disabledValidation.IsValid, string.Join(
            Environment.NewLine,
            disabledValidation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        RuntimeSessionRestoreResult disabledRestore = new RuntimeSessionRestoreService(
                validator,
                actorFactory,
                profiles,
                chargePolicies: policies)
            .Restore(disabledSnapshot, catalog);
        Assert.True(disabledRestore.IsSuccess, string.Join(
            Environment.NewLine,
            disabledRestore.Diagnostics.Select(diagnostic => diagnostic.Message)));
        CatalogBattleActor disabledRestoredActor = disabledRestore.RequireSession().ActorsByInstanceId[
            disabledActor.Identity.InstanceId];
        Assert.Equal(StandardChargePolicyIds.Disabled, disabledRestoredActor.State.ChargePolicyId);
        Assert.Empty(disabledRestoredActor.State.Charges);
    }

    [Fact]
    public void RuntimeSaveValidator_AcceptsCatalogBackedPendingSkillChoice()
    {
        ContentId entityId =
            Id("convergence.clean_battle_demo:frost_duelist_demo");
        ContentId skillId =
            Id("convergence.clean_battle_demo:regenerate_demo");
        GameDataCatalog catalog = WithEntitySkillUnlock(
            LoadCatalog(),
            entityId,
            new SkillUnlockDefinition(5, skillId));
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        RuntimeActorSnapshot pending = CopyActor(
            actor,
            skills: new RuntimeSkillStateSnapshot(
                actor.Skills.LearnedSkillIds,
                actor.Skills.EquippedSkillIds,
                [new RuntimePendingSkillChoiceSnapshot(
                    new RuntimeSkillChoiceToken(5),
                    5,
                    skillId)],
                revision: 3));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [pending, baseline.Actors[1]]),
            catalog);

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsPendingSkillChoiceIntegrityViolations()
    {
        ContentId entityId =
            Id("convergence.clean_battle_demo:frost_duelist_demo");
        ContentId pendingSkillId =
            Id("convergence.clean_battle_demo:regenerate_demo");
        ContentId secondPendingSkillId =
            Id("convergence.shared_effects_demo:field_recovery_demo");
        GameDataCatalog catalog = WithEntitySkillUnlock(
            LoadCatalog(),
            entityId,
            new SkillUnlockDefinition(5, pendingSkillId),
            new SkillUnlockDefinition(5, secondPendingSkillId));
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        RuntimeActorSnapshot malformed = CopyActor(
            actor,
            skills: new RuntimeSkillStateSnapshot(
                actor.Skills.LearnedSkillIds.Append(pendingSkillId),
                actor.Skills.EquippedSkillIds,
                [
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(1),
                        5,
                        pendingSkillId),
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(1),
                        5,
                        secondPendingSkillId),
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(2),
                        5,
                        secondPendingSkillId)
                ]));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            result,
            RuntimeSaveValidationCode.DuplicateActorPendingSkillChoiceToken,
            "$.actors[0].skills.pendingChoices[1]");
        AssertDiagnostic(
            result,
            RuntimeSaveValidationCode.DuplicateActorPendingSkill,
            "$.actors[0].skills.pendingChoices[2]");
        AssertDiagnostic(
            result,
            RuntimeSaveValidationCode.ActorPendingSkillAlreadyLearned,
            "$.actors[0].skills.pendingChoices[0].skillId");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsPendingSkillCatalogAndUnlockMismatches()
    {
        ContentId entityId =
            Id("convergence.clean_battle_demo:frost_duelist_demo");
        ContentId authoredSkillId =
            Id("convergence.clean_battle_demo:regenerate_demo");
        ContentId wrongSkillId =
            Id("convergence.shared_effects_demo:field_recovery_demo");
        ContentId missingSkillId =
            Id("missing.pack:missing_skill");
        GameDataCatalog catalog = WithEntitySkillUnlock(
            LoadCatalog(),
            entityId,
            new SkillUnlockDefinition(6, authoredSkillId));
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        RuntimeActorSnapshot malformed = CopyActor(
            actor,
            skills: new RuntimeSkillStateSnapshot(
                actor.Skills.LearnedSkillIds,
                actor.Skills.EquippedSkillIds,
                [
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(1),
                        5,
                        wrongSkillId),
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(2),
                        5,
                        missingSkillId),
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(3),
                        6,
                        authoredSkillId)
                ]));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            result,
            RuntimeSaveValidationCode.ActorPendingSkillUnlockMismatch,
            "$.actors[0].skills.pendingChoices[0]");
        AssertDiagnostic(
            result,
            RuntimeSaveValidationCode.MissingCatalogSkill,
            "$.actors[0].skills.pendingChoices[1].skillId");
        AssertDiagnostic(
            result,
            RuntimeSaveValidationCode.ActorPendingSkillLevelUnavailable,
            "$.actors[0].skills.pendingChoices[2].unlockLevel");
    }

    [Fact]
    public void RuntimeSaveValidator_AndActorRestoreShareStructuralIntegrityRules()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        ContentId iceBoost = Id("convergence.skill_system_redesign_sample:ice_boost_sample");
        ContentId poison = Id("convergence.shared_effects_demo:poison_demo");
        StatusLifetimeDefinition duration =
            EncounterLifetime(new TurnDurationDefinition(2, Id("owner_turn_end"), false));
        var activation = new RuntimePassiveActivationSnapshot(
            iceBoost,
            Id("owner_turn_end"),
            triggerIndex: 0,
            activationCount: 1);
        RuntimeActorSnapshot malformed = CopyActor(
            frost,
            resources: [frost.Resources[0], frost.Resources[0], frost.Resources[1]],
            skills: new RuntimeSkillStateSnapshot(
                [frost.Skills.LearnedSkillIds[0], iceBoost, frost.Skills.LearnedSkillIds[0]],
                [iceBoost, iceBoost]),
            battleStatus: new RuntimeBattleStatusSnapshot(
                ailments:
                [
                    new RuntimeTimedStateSnapshot(poison, duration),
                    new RuntimeTimedStateSnapshot(poison, duration)
                ],
                statuses:
                [
                    new RuntimeTimedStateSnapshot(Id("sealed"), duration),
                    new RuntimeTimedStateSnapshot(Id("sealed"), duration)
                ],
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    PersistentModifierRuleset,
                    [
                        new RuntimeStatModifierTrackSnapshot(
                            Id("attack"),
                            1,
                            [new RuntimeStatModifierContributionSnapshot(1, 1)]),
                        new RuntimeStatModifierTrackSnapshot(
                            Id("attack"),
                            1,
                            [new RuntimeStatModifierContributionSnapshot(2, 1)])
                    ]),
                chargeState: new RuntimeChargeStateSnapshot(
                    StandardChargePolicyIds.Split,
                    [
                        new RuntimeChargeSnapshot(ChargeKind.Physical, 2m, duration),
                        new RuntimeChargeSnapshot(ChargeKind.Physical, 2m, duration)
                    ]),
                shields:
                [
                    new RuntimeShieldSnapshot(ShieldKind.Physical, duration),
                    new RuntimeShieldSnapshot(ShieldKind.Physical, duration)
                ],
                affinityBreaks:
                [
                    new RuntimeAffinityBreakSnapshot(DamageElement.Fire, duration),
                    new RuntimeAffinityBreakSnapshot(DamageElement.Fire, duration),
                    new RuntimeAffinityBreakSnapshot(DamageElement.Almighty, duration)
                ],
                affinityOverrides:
                [
                    new RuntimeAffinityOverrideSnapshot(DamageElement.Fire, ElementalAffinity.Resist, duration),
                    new RuntimeAffinityOverrideSnapshot(DamageElement.Fire, ElementalAffinity.Resist, duration)
                ]),
            battleActivations: new RuntimeBattleActivationSnapshot(
                [activation, activation],
                [
                    new RuntimePassiveSkillStateSnapshot(iceBoost, true),
                    new RuntimePassiveSkillStateSnapshot(iceBoost, false)
                ]),
            capabilityIds: [Id("analyze"), Id("analyze")]);
        RuntimeSaveValidationResult validation = CreateModifierAwareValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        Assert.False(validation.IsValid);
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorResource, "$.actors[0].resources[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorLearnedSkill, "$.actors[0].skills.learnedSkillIds[2]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorEquippedSkill, "$.actors[0].skills.equippedSkillIds[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorCapability, "$.actors[0].capabilityIds[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorAilment, "$.actors[0].battleStatus.ailments[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorStatus, "$.actors[0].battleStatus.statuses[1]");
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorStatModifierStateInvalid &&
            diagnostic.StatModifierCode == StatModifierDiagnosticCode.DuplicateModifierTrack);
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorCharge, "$.actors[0].battleStatus.charges[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorShield, "$.actors[0].battleStatus.shields[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorAffinityBreak, "$.actors[0].battleStatus.affinityBreaks[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.InvalidActorAffinityBreakElement, "$.actors[0].battleStatus.affinityBreaks[2].element");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicateActorAffinityOverride, "$.actors[0].battleStatus.affinityOverrides[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicatePassiveSkillState, "$.actors[0].battleActivations.passiveSkillStates[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicatePassiveActivation, "$.actors[0].battleActivations.passiveActivations[1]");

        ArgumentException directRestore = Assert.Throws<ArgumentException>(() => RuntimeActorState.Restore(
            malformed,
            CombatDefenseProfile.Empty,
            [catalog.Skills[iceBoost]],
            [catalog.Ailments[poison]]));
        Assert.Contains("$.resources[1]", directRestore.Message, StringComparison.Ordinal);

        CatalogBattleActorCreationResult catalogRestore = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog).Restore(ActorRestore(malformed));
        Assert.False(catalogRestore.IsSuccess);
        Assert.Contains(catalogRestore.Diagnostics, item => item.Code == CatalogBattleActorDiagnosticCode.SnapshotInvalid);
    }

    [Fact]
    public void RuntimeSaveValidator_AndActorRestoreRejectPolicyIncompatibleModifierState()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        ContentId attack = Id("attack");
        RuntimeActorSnapshot malformed = CopyActor(
            actor,
            battleStatus: new RuntimeBattleStatusSnapshot(
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    PersistentModifierRuleset,
                    [
                        new RuntimeStatModifierTrackSnapshot(
                            attack,
                            5,
                            [new RuntimeStatModifierContributionSnapshot(1, 5)])
                    ])));

        RuntimeSaveValidationResult validation = CreateModifierAwareValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorStatModifierStateInvalid,
            "$.actors[0].battleStatus.statModifiers.tracks[0]");

        SkillDefinition[] passives = malformed.Skills.EquippedSkillIds
            .Select(skillId => catalog.Skills[skillId])
            .Where(skill => skill.Activation == SkillActivation.Passive)
            .ToArray();
        ArgumentException restore = Assert.Throws<ArgumentException>(() => RuntimeActorState.Restore(
            malformed,
            CombatDefenseProfile.Empty,
            passives,
            statModifierPolicy: ModifierPolicy(malformed)));
        Assert.Contains("incompatible", restore.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeSaveValidator_RequiresExplicitPolicyBindingForRetainedModifiers()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = WithTimedContributions(baseline.Actors[0]);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [actor, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorStatModifierPolicyResolverMissing,
            "$.actors[0].battleStatus.statModifiers.policyId");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsUnboundRetainedModifierPolicy()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    Id("missing.pack:missing_modifier_policy"))));

        RuntimeSaveValidationResult validation = CreateModifierAwareValidator().Validate(
            Copy(baseline, actors: [actor, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorStatModifierPolicyBindingRejected,
            "$.actors[0].battleStatus.statModifiers.policyId");
    }

    [Fact]
    public void RuntimeSessionRestoreService_PreservesIndependentModifierTimersAndBoundaries()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = WithTimedContributions(baseline.Actors[0]);
        RuntimeSaveGameSnapshot snapshot = Copy(
            baseline,
            actors: [actor, baseline.Actors[1]]);
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        IRuntimeRulesetBindingResolver rulesets = CreateRulesetBindings();
        var service = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(rulesetBindings: rulesets),
            factory,
            new DelegateActorRestoreProfileResolver(_ => ActorProfile()),
            rulesetBindings: rulesets);

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        RuntimeActorSnapshot restored = result.RequireSession().Snapshot.Actors[0];
        RuntimeStatModifierStateSnapshot state = Assert.IsType<RuntimeStatModifierStateSnapshot>(
            restored.BattleStatus.StatModifiers);
        Assert.Equal(TimedContributionModifierRuleset, state.PolicyId);
        RuntimeStatModifierTrackSnapshot track = Assert.Single(state.Tracks);
        Assert.Equal(Id("attack"), track.ModifierTrackId);
        Assert.Equal(2, track.ResolvedStage);
        Assert.Collection(
            track.Contributions,
            contribution => AssertContribution(contribution, 2, 1, 2, 7),
            contribution => AssertContribution(contribution, 5, 1, 3, 8));
    }

    [Fact]
    public void RuntimeSessionRestoreService_RejectsMalformedModifierStateBeforeActorConstruction()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    TimedContributionModifierRuleset,
                    [
                        new RuntimeStatModifierTrackSnapshot(
                            Id("attack"),
                            2,
                            [
                                new RuntimeStatModifierContributionSnapshot(
                                    1,
                                    1,
                                    new TurnDurationDefinition(2, Id("owner_turn_end"), false)),
                                new RuntimeStatModifierContributionSnapshot(
                                    1,
                                    1,
                                    new TurnDurationDefinition(3, Id("owner_turn_end"), false))
                            ])
                    ])));
        RuntimeSaveGameSnapshot snapshot = Copy(
            baseline,
            actors: [actor, baseline.Actors[1]]);
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        IRuntimeRulesetBindingResolver rulesets = CreateRulesetBindings();
        var service = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(rulesetBindings: rulesets),
            factory,
            new DelegateActorRestoreProfileResolver(_ => ActorProfile()),
            rulesetBindings: rulesets);

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Empty(factory.RestoreOrder);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSessionRestoreDiagnosticCode.SaveValidationRejected &&
            diagnostic.SaveValidationCode == RuntimeSaveValidationCode.ActorStatModifierStateInvalid);
    }

    [Fact]
    public void RuntimeSessionRestoreService_RevalidatesStateAgainstRestorePolicyBeforeConstruction()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = WithTimedContributions(baseline.Actors[0]);
        RuntimeSaveGameSnapshot snapshot = Copy(
            baseline,
            actors: [actor, baseline.Actors[1]]);
        var factory = new RecordingActorFactory(new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog));
        IRuntimeRulesetBindingResolver restoreRulesets = new RuntimeRulesetBindingResolver(
            new RuntimeRulesetPolicyFactoryRegistry(
                statModifier: [new NarrowTimedContributionPolicyFactory()]));
        var service = new RuntimeSessionRestoreService(
            CreateModifierAwareValidator(),
            factory,
            new DelegateActorRestoreProfileResolver(_ => ActorProfile()),
            rulesetBindings: restoreRulesets);

        RuntimeSessionRestoreResult result = service.Restore(snapshot, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Empty(factory.RestoreOrder);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSessionRestoreDiagnosticCode.StatModifierPolicyResolutionFailed &&
            diagnostic.ActorId == actor.Identity.InstanceId);
    }

    [Theory]
    [InlineData("convergence.catalog_surface_sample:persistent_staged_modifiers_sample", false)]
    [InlineData("convergence.catalog_surface_sample:timed_exclusive_modifiers_sample", true)]
    [InlineData("convergence.catalog_surface_sample:timed_contribution_modifiers_sample", true)]
    public void RuntimeSessionRestoreService_RoundTripsEverySuppliedModifierPolicy(
        string rulesetId,
        bool retainsDuration)
    {
        GameDataCatalog catalog = LoadCatalog();
        IRuntimeRulesetBindingResolver rulesets = CreateRulesetBindings();
        IStatModifierPolicyService policy = rulesets.BindStatModifierPolicy(
            catalog,
            Id(rulesetId)).RequireService();
        RuntimeStatModifierStateSnapshot state = policy.Apply(
            new StatModifierApplicationRequest(
                new RuntimeStatModifierStateSnapshot(policy.PolicyId),
                Id("attack"),
                1,
                new TurnDurationDefinition(3, Id("owner_turn_end"), true),
                activeLifecycleBoundary: new StatModifierLifecycleBoundary(
                    Id("owner_turn_end"),
                    1))).After;
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(statModifiers: state));
        RuntimeSaveGameSnapshot snapshot = Copy(
            baseline,
            actors: [actor, baseline.Actors[1]]);
        var factory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog);
        var restore = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(rulesetBindings: rulesets),
            factory,
            new DelegateActorRestoreProfileResolver(_ => ActorProfile()),
            rulesetBindings: rulesets);

        RuntimeSessionRestoreResult result = restore.Restore(snapshot, catalog);

        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        RuntimeStatModifierStateSnapshot restored = Assert.IsType<RuntimeStatModifierStateSnapshot>(
            result.RequireSession().Snapshot.Actors[0].BattleStatus.StatModifiers);
        Assert.Equal(policy.PolicyId, restored.PolicyId);
        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(
            Assert.Single(restored.Tracks).Contributions);
        Assert.Equal(1, contribution.StageDelta);
        Assert.Equal(1, Assert.Single(restored.Tracks).ResolvedStage);
        if (retainsDuration)
        {
            Assert.Equal(
                new TurnDurationDefinition(3, Id("owner_turn_end"), true),
                contribution.Duration);
            Assert.Equal(1, contribution.LastLifecycleBoundary?.Sequence);
        }
        else
        {
            Assert.Null(contribution.Duration);
            Assert.Null(contribution.LastLifecycleBoundary);
        }
    }

    [Fact]
    public void RuntimeSaveValidator_AndActorRestoreAcceptEveryRetainedDurationKind()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        RuntimeActorSnapshot retained = CopyActor(
            actor,
            battleStatus: new RuntimeBattleStatusSnapshot(
                statuses:
                [
                    new RuntimeTimedStateSnapshot(
                        Id("turn_state"),
                        FieldLifetime(new TurnDurationDefinition(2, Id("owner_turn_end"), false))),
                    new RuntimeTimedStateSnapshot(
                        Id("phase_state"),
                        EncounterLifetime(new PhaseDurationDefinition(Id("player_phase")))),
                    new RuntimeTimedStateSnapshot(
                        Id("battle_state"),
                        EncounterLifetime(new BattleDurationDefinition())),
                    new RuntimeTimedStateSnapshot(
                        Id("permanent_state"),
                        StandardStatusLifetimes.Persistent)
                ]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [retained, baseline.Actors[1]]),
            catalog);
        RuntimeActorState restored = RuntimeActorState.Restore(
            retained,
            CombatDefenseProfile.Empty,
            retained.Skills.EquippedSkillIds
                .Select(skillId => catalog.Skills[skillId])
                .Where(skill => skill.Activation == SkillActivation.Passive));

        Assert.True(
            validation.IsValid,
            string.Join(Environment.NewLine, validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Collection(
            restored.ToSnapshot().BattleStatus.Statuses.OrderBy(status => status.Id.ToString()),
            status => Assert.IsType<BattleDurationDefinition>(status.Duration),
            status => Assert.IsType<PermanentDurationDefinition>(status.Duration),
            status => Assert.IsType<PhaseDurationDefinition>(status.Duration),
            status => Assert.IsType<TurnDurationDefinition>(status.Duration));
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMalformedDurationInEveryTimedStateCollection()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        ContentId poison = Id("convergence.shared_effects_demo:poison_demo");
        RuntimeActorSnapshot malformed = CopyActor(
            actor,
            battleStatus: new RuntimeBattleStatusSnapshot(
                ailments:
                [
                    new RuntimeTimedStateSnapshot(
                        poison,
                        FieldLifetime(new TurnDurationDefinition(0, Id("owner_turn_end"), false)))
                ],
                statuses:
                [
                    new RuntimeTimedStateSnapshot(
                        Id("instant_state"),
                        EncounterLifetime(new InstantDurationDefinition()))
                ],
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    TimedContributionModifierRuleset,
                    [
                        new RuntimeStatModifierTrackSnapshot(
                            Id("attack"),
                            1,
                            [
                                new RuntimeStatModifierContributionSnapshot(
                                    1,
                                    1,
                                    new TurnDurationDefinition(1, default, false))
                            ])
                    ]),
                chargeState: new RuntimeChargeStateSnapshot(
                    StandardChargePolicyIds.Split,
                    [
                        new RuntimeChargeSnapshot(
                            ChargeKind.Physical,
                            2m,
                            DeploymentLifetime(new PhaseDurationDefinition(default)))
                    ]),
                shields:
                [
                    new RuntimeShieldSnapshot(
                        ShieldKind.Magical,
                        DeploymentLifetime(new TurnDurationDefinition(-1, Id("unregistered_event"), false)))
                ],
                affinityBreaks:
                [
                    new RuntimeAffinityBreakSnapshot(
                        DamageElement.Ice,
                        EncounterLifetime(new InstantDurationDefinition()))
                ],
                affinityOverrides:
                [
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Fire,
                        ElementalAffinity.Resist,
                        EncounterLifetime(new PhaseDurationDefinition(Id("unregistered_phase"))))
                ]));

        RuntimeSaveValidationResult validation = CreateModifierAwareValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        Assert.False(validation.IsValid);
        Assert.Equal(
        [
            (RuntimeSaveValidationCode.ActorTurnDurationValueOutOfRange,
                "$.actors[0].battleStatus.ailments[0].duration.value"),
            (RuntimeSaveValidationCode.ActorRetainedDurationKindInvalid,
                "$.actors[0].battleStatus.statuses[0].duration.kind"),
            (RuntimeSaveValidationCode.ActorTurnDurationTickEventIdInvalid,
                "$.actors[0].battleStatus.statModifiers.tracks[0].contributions[0].duration.tickEventId"),
            (RuntimeSaveValidationCode.ActorPhaseDurationPhaseIdInvalid,
                "$.actors[0].battleStatus.charges[0].duration.phaseId"),
            (RuntimeSaveValidationCode.ActorTurnDurationValueOutOfRange,
                "$.actors[0].battleStatus.shields[0].duration.value"),
            (RuntimeSaveValidationCode.ActorTurnDurationTickEventIdInvalid,
                "$.actors[0].battleStatus.shields[0].duration.tickEventId"),
            (RuntimeSaveValidationCode.ActorRetainedDurationKindInvalid,
                "$.actors[0].battleStatus.affinityBreaks[0].duration.kind"),
            (RuntimeSaveValidationCode.ActorPhaseDurationPhaseIdInvalid,
                "$.actors[0].battleStatus.affinityOverrides[0].duration.phaseId")
        ],
            validation.Diagnostics
                .Where(diagnostic => diagnostic.Code is
                    RuntimeSaveValidationCode.ActorRetainedDurationKindInvalid or
                    RuntimeSaveValidationCode.ActorTurnDurationValueOutOfRange or
                    RuntimeSaveValidationCode.ActorTurnDurationTickEventIdInvalid or
                    RuntimeSaveValidationCode.ActorPhaseDurationPhaseIdInvalid)
                .Select(diagnostic => (diagnostic.Code, diagnostic.Path)));
        Assert.Contains(
            validation.Diagnostics,
            diagnostic =>
                diagnostic.Path == "$.actors[0].battleStatus.shields[0].duration.tickEventId" &&
                diagnostic.Message.Contains("not registered", StringComparison.Ordinal));
        Assert.Contains(
            validation.Diagnostics,
            diagnostic =>
                diagnostic.Path == "$.actors[0].battleStatus.affinityOverrides[0].duration.phaseId" &&
                diagnostic.Message.Contains("not registered", StringComparison.Ordinal));

        SkillDefinition[] passives = malformed.Skills.EquippedSkillIds
            .Select(skillId => catalog.Skills[skillId])
            .Where(skill => skill.Activation == SkillActivation.Passive)
            .ToArray();
        RuntimeActorSnapshot unregisteredOnly = CopyActor(
            actor,
            battleStatus: new RuntimeBattleStatusSnapshot(
                statuses:
                [
                    new RuntimeTimedStateSnapshot(
                        Id("unknown_tick_state"),
                        EncounterLifetime(new TurnDurationDefinition(1, Id("unregistered_event"), false)))
                ]));
        ArgumentException directRestore = Assert.Throws<ArgumentException>(() => RuntimeActorState.Restore(
            unregisteredOnly,
            CombatDefenseProfile.Empty,
            passives,
            registeredEventIds: catalog.RegisteredEventIds,
            registeredPhaseIds: catalog.RegisteredPhaseIds));
        Assert.Contains(
            "$.battleStatus.statuses[0].duration.tickEventId",
            directRestore.Message,
            StringComparison.Ordinal);

        CatalogBattleActorCreationResult catalogRestore = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog).Restore(ActorRestore(unregisteredOnly));
        CatalogBattleActorDiagnostic restoreDiagnostic = Assert.Single(
            catalogRestore.Diagnostics,
            diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.SnapshotInvalid);
        Assert.Contains(
            "$.battleStatus.statuses[0].duration.tickEventId",
            restoreDiagnostic.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSaveValidator_AggregatesActorNumericDomainErrorsBeforeRestore()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        RuntimeActorSnapshot malformed = CopyActor(
            actor,
            stats: new RuntimeStatBlockSnapshot(
            [
                new KeyValuePair<ContentId, decimal>(Id("strength"), -1m),
                new KeyValuePair<ContentId, decimal>(
                    Id("magic"),
                    RuntimeActorNumericDomain.MaximumStatValue + 1m)
            ],
            [
                new KeyValuePair<ContentId, decimal>(Id("vitality"), -0.5m),
                new KeyValuePair<ContentId, decimal>(
                    Id("luck"),
                    RuntimeActorNumericDomain.MaximumStatValue + 0.5m)
            ]),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(Id("hp"), -1m),
                new KeyValuePair<ContentId, decimal>(Id("sp"), -2m)
            ]);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        RuntimeSaveValidationDiagnostic[] numericDiagnostics = validation.Diagnostics
            .Where(diagnostic => diagnostic.Code is
                RuntimeSaveValidationCode.ActorBaseStatOutOfRange or
                RuntimeSaveValidationCode.ActorEffectiveStatOutOfRange or
                RuntimeSaveValidationCode.ActorBaseResourceValueOutOfRange)
            .ToArray();
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorBaseStatOutOfRange,
            "$.actors[0].stats.baseStats.strength");
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorBaseStatOutOfRange,
            "$.actors[0].stats.baseStats.magic");
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorEffectiveStatOutOfRange,
            "$.actors[0].stats.effectiveStats.vitality");
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorEffectiveStatOutOfRange,
            "$.actors[0].stats.effectiveStats.luck");
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorBaseResourceValueOutOfRange,
            "$.actors[0].baseResourceValues.hp");
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ActorBaseResourceValueOutOfRange,
            "$.actors[0].baseResourceValues.sp");
        Assert.Collection(
            numericDiagnostics,
            diagnostic => Assert.Equal("$.actors[0].stats.baseStats.strength", diagnostic.Path),
            diagnostic => Assert.Equal("$.actors[0].stats.baseStats.magic", diagnostic.Path),
            diagnostic => Assert.Equal("$.actors[0].stats.effectiveStats.vitality", diagnostic.Path),
            diagnostic => Assert.Equal("$.actors[0].stats.effectiveStats.luck", diagnostic.Path),
            diagnostic => Assert.Equal("$.actors[0].baseResourceValues.hp", diagnostic.Path),
            diagnostic => Assert.Equal("$.actors[0].baseResourceValues.sp", diagnostic.Path));
        Assert.Contains(numericDiagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorBaseStatOutOfRange &&
            diagnostic.ContentId == Id("strength"));
        Assert.Contains(numericDiagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorEffectiveStatOutOfRange &&
            diagnostic.ContentId == Id("luck"));
        Assert.Contains(numericDiagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorBaseResourceValueOutOfRange &&
            diagnostic.ContentId == Id("hp"));
        Assert.Throws<RuntimeSaveValidationException>(() => validation.RequireValidSnapshot());

        ArgumentException directRestore = Assert.Throws<ArgumentException>(() =>
            RuntimeActorState.Restore(malformed, CombatDefenseProfile.Empty));
        Assert.Contains("$.stats.baseStats.strength", directRestore.Message, StringComparison.Ordinal);

        CatalogBattleActorCreationResult catalogRestore = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new RestoreOnlyInitializationPolicy(),
            catalog).Restore(ActorRestore(malformed));
        Assert.False(catalogRestore.IsSuccess);
        Assert.Contains(
            catalogRestore.Diagnostics,
            diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.SnapshotInvalid);
    }

    [Fact]
    public void ValidatedNumericBoundaries_RestoreAndRemainSafeForStandardPolicies()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot actor = baseline.Actors[0];
        KeyValuePair<ContentId, decimal>[] boundaryStats =
        [
            new(StandardProgressionIds.Strength, RuntimeActorNumericDomain.MaximumStatValue),
            new(StandardProgressionIds.Magic, RuntimeActorNumericDomain.MaximumStatValue),
            new(StandardProgressionIds.Vitality, RuntimeActorNumericDomain.MaximumStatValue),
            new(StandardProgressionIds.Luck, RuntimeActorNumericDomain.MaximumStatValue)
        ];
        RuntimeActorSnapshot boundaryActor = CopyActor(
            actor,
            stats: new RuntimeStatBlockSnapshot(boundaryStats, boundaryStats),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, decimal.MaxValue),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, decimal.MaxValue)
            ]);
        RuntimeSaveGameSnapshot candidate = Copy(
            baseline,
            actors: [boundaryActor, baseline.Actors[1]]);

        RuntimeSaveGameSnapshot validated = new RuntimeSaveValidator()
            .Validate(candidate, catalog)
            .RequireValidSnapshot();
        RuntimeActorState restored = RuntimeActorState.Restore(
            validated.Actors[0],
            CombatDefenseProfile.Empty,
            [catalog.Skills[Id("convergence.skill_system_redesign_sample:ice_boost_sample")]]);

        StatResolutionResult resolved = new StandardStatResolutionPolicy().Resolve(
            new StatResolutionRequest(
                RuntimeStatSourceKind.Actor,
                StandardProgressionIds.Strength,
                restored.BaseStats));
        ResourceRecalculationResult resources = new StandardResourceGrowthPolicy().Recalculate(
            new ResourceRecalculationRequest(
                restored.ToSnapshot().Resources,
                restored.BaseResourceValues,
                restored.Stats));

        Assert.Equal(40, resolved.CappedValue);
        Assert.Equal(40, resolved.FinalValue);
        Assert.Equal(666m, resources.GetRequired(StandardProgressionIds.Hp).Maximum);
        Assert.Equal(333m, resources.GetRequired(StandardProgressionIds.Sp).Maximum);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsUnloadedPassivesSkillShapeAndActorKindMismatch()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        ContentId frostLance = Id("convergence.clean_battle_demo:frost_lance_demo");
        ContentId emberBolt = Id("convergence.clean_battle_demo:ember_bolt_demo");
        ContentId iceBoost = Id("convergence.skill_system_redesign_sample:ice_boost_sample");
        RuntimeActorSnapshot malformed = CopyActor(
            frost,
            identity: new RuntimeActorIdentitySnapshot(
                frost.Identity.InstanceId,
                frost.Identity.EntityDefinitionId,
                Id("independent_actor"),
                frost.Identity.DisplayName),
            skills: new RuntimeSkillStateSnapshot(
                [frostLance, iceBoost],
                [frostLance, emberBolt]),
            battleActivations: new RuntimeBattleActivationSnapshot(
                [new RuntimePassiveActivationSnapshot(iceBoost, Id("owner_turn_end"), 0, 1)],
                [new RuntimePassiveSkillStateSnapshot(iceBoost, true)]));
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        Assert.False(validation.IsValid);
        AssertDiagnostic(validation, RuntimeSaveValidationCode.ActorKindMismatch, "$.actors[0].identity.actorKindId");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.ActorEquippedSkillNotLearned, "$.actors[0].skills.equippedSkillIds[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.PassiveStateSkillNotLoaded, "$.actors[0].battleActivations.passiveSkillStates[0]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.PassiveActivationSkillNotLoaded, "$.actors[0].battleActivations.passiveActivations[0]");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsActivationForPassiveWithoutAuthoredTriggers()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        ContentId iceBoost = Id("convergence.skill_system_redesign_sample:ice_boost_sample");
        RuntimeActorSnapshot malformed = CopyActor(
            frost,
            battleActivations: new RuntimeBattleActivationSnapshot(
                [new RuntimePassiveActivationSnapshot(
                    iceBoost,
                    Id("owner_turn_end"),
                    triggerIndex: 0,
                    activationCount: 1)],
                [new RuntimePassiveSkillStateSnapshot(iceBoost, IsEnabled: true)]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.PassiveActivationTriggerIndexInvalid,
            "$.actors[0].battleActivations.passiveActivations[0].triggerIndex");
        ArgumentException directRestore = Assert.Throws<ArgumentException>(() =>
            RuntimeActorState.Restore(
                malformed,
                CombatDefenseProfile.Empty,
                [catalog.Skills[iceBoost]]));
        Assert.Contains(
            "$.battleActivations.passiveActivations[0].triggerIndex",
            directRestore.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsPassiveActivationWithWrongIndexOrEvent()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot ember = baseline.Actors[1];
        ContentId regenerate = Id("convergence.clean_battle_demo:regenerate_demo");
        RuntimeActorSnapshot malformed = WithRegeneratePassive(
            ember,
            [
                new RuntimePassiveActivationSnapshot(
                    regenerate,
                    Id("owner_turn_end"),
                    triggerIndex: 1,
                    activationCount: 1),
                new RuntimePassiveActivationSnapshot(
                    regenerate,
                    Id("battle_start"),
                    triggerIndex: 0,
                    activationCount: 1)
            ]);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [baseline.Actors[0], malformed]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.PassiveActivationTriggerIndexInvalid,
            "$.actors[1].battleActivations.passiveActivations[0].triggerIndex");
        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.PassiveActivationEventMismatch,
            "$.actors[1].battleActivations.passiveActivations[1].eventId");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMissingEquippedPassiveState()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot ember = baseline.Actors[1];
        ContentId emberBolt = Id("convergence.clean_battle_demo:ember_bolt_demo");
        ContentId regenerate = Id("convergence.clean_battle_demo:regenerate_demo");
        RuntimeActorSnapshot malformed = CopyActor(
            ember,
            skills: new RuntimeSkillStateSnapshot(
                [emberBolt, regenerate],
                [emberBolt, regenerate]),
            battleActivations: new RuntimeBattleActivationSnapshot());

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [baseline.Actors[0], malformed]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.MissingPassiveSkillState,
            "$.actors[1].battleActivations.passiveSkillStates");
        ArgumentException directRestore = Assert.Throws<ArgumentException>(() =>
            RuntimeActorState.Restore(
                malformed,
                CombatDefenseProfile.Empty,
                [catalog.Skills[regenerate]]));
        Assert.Contains(
            "$.battleActivations.passiveSkillStates",
            directRestore.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeSaveValidator_RoundTripsExactEquippedPassiveState(bool isEnabled)
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot ember = baseline.Actors[1];
        ContentId emberBolt = Id("convergence.clean_battle_demo:ember_bolt_demo");
        ContentId regenerate = Id("convergence.clean_battle_demo:regenerate_demo");
        RuntimeActorSnapshot candidate = CopyActor(
            ember,
            skills: new RuntimeSkillStateSnapshot(
                [emberBolt, regenerate],
                [emberBolt, regenerate]),
            battleActivations: new RuntimeBattleActivationSnapshot(
                passiveSkillStates:
                [
                    new RuntimePassiveSkillStateSnapshot(regenerate, isEnabled)
                ]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [baseline.Actors[0], candidate]),
            catalog);
        Assert.True(
            validation.IsValid,
            string.Join(
                " | ",
                validation.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}:{diagnostic.Path}:{diagnostic.Message}")));
        RuntimeActorState restored = RuntimeActorState.Restore(
            validation.RequireValidSnapshot().Actors[1],
            CombatDefenseProfile.Empty,
            [catalog.Skills[regenerate]]);

        BattlePassiveEntry entry = Assert.Single(restored.Passives.Entries);
        Assert.Equal(regenerate, entry.Skill.Id);
        Assert.Equal(isEnabled, entry.IsEnabled);
        RuntimePassiveSkillStateSnapshot captured = Assert.Single(
            restored.ToSnapshot().BattleActivations.PassiveSkillStates);
        Assert.Equal(isEnabled, captured.IsEnabled);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMutuallyExclusiveRestoredAilments()
    {
        ContentId groupId = Id("convergence.shared_effects_demo:major_ailment");
        AilmentDefinition first = RestoreAilment("conflicting_ailment_one", groupId);
        AilmentDefinition second = RestoreAilment("conflicting_ailment_two", groupId);
        GameDataCatalog catalog = WithAilments(LoadCatalog(), first, second);
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot malformed = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(
                ailments:
                [
                    new RuntimeTimedStateSnapshot(first.Id, StandardStatusLifetimes.Persistent),
                    new RuntimeTimedStateSnapshot(second.Id, StandardStatusLifetimes.Persistent)
                ]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.ConflictingActorAilmentExclusivityGroup,
            "$.actors[0].battleStatus.ailments[1]");
        ArgumentException directRestore = Assert.Throws<ArgumentException>(() =>
            RuntimeActorState.Restore(
                malformed,
                CombatDefenseProfile.Empty,
                [catalog.Skills[Id("convergence.skill_system_redesign_sample:ice_boost_sample")]],
                [first, second]));
        Assert.Contains(
            "$.battleStatus.ailments[1]",
            directRestore.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSaveValidator_AllowsIndependentRestoredAilments()
    {
        AilmentDefinition first = RestoreAilment("independent_ailment_one");
        AilmentDefinition second = RestoreAilment("independent_ailment_two");
        GameDataCatalog catalog = WithAilments(LoadCatalog(), first, second);
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot candidate = CopyActor(
            baseline.Actors[0],
            battleStatus: new RuntimeBattleStatusSnapshot(
                ailments:
                [
                    new RuntimeTimedStateSnapshot(first.Id, StandardStatusLifetimes.Persistent),
                    new RuntimeTimedStateSnapshot(second.Id, StandardStatusLifetimes.Persistent)
                ]));

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [candidate, baseline.Actors[1]]),
            catalog);
        RuntimeActorState restored = RuntimeActorState.Restore(
            validation.RequireValidSnapshot().Actors[0],
            CombatDefenseProfile.Empty,
            [catalog.Skills[Id("convergence.skill_system_redesign_sample:ice_boost_sample")]],
            [first, second]);

        Assert.Equal([first.Id, second.Id], restored.Ailments.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeSaveValidator_RoundTripsDefinitionCoherentPassiveActivation(
        bool perTarget)
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot ember = baseline.Actors[1];
        ContentId regenerate = Id("convergence.clean_battle_demo:regenerate_demo");
        RuntimeInstanceId? targetInstanceId = perTarget
            ? baseline.Actors[0].Identity.InstanceId
            : null;
        RuntimeActorSnapshot candidate = WithRegeneratePassive(
            ember,
            [
                new RuntimePassiveActivationSnapshot(
                    regenerate,
                    Id("owner_turn_end"),
                    triggerIndex: 0,
                    activationCount: 2,
                    targetInstanceId: targetInstanceId)
            ]);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
            Copy(baseline, actors: [baseline.Actors[0], candidate]),
            catalog);
        RuntimeActorState restored = RuntimeActorState.Restore(
            validation.RequireValidSnapshot().Actors[1],
            CombatDefenseProfile.Empty,
            [catalog.Skills[regenerate]]);

        RuntimePassiveActivationSnapshot activation = Assert.Single(
            restored.ToSnapshot().BattleActivations.PassiveActivations);
        Assert.Equal(regenerate, activation.SkillId);
        Assert.Equal(Id("owner_turn_end"), activation.EventId);
        Assert.Equal(0, activation.TriggerIndex);
        Assert.Equal(2, activation.ActivationCount);
        Assert.Equal(targetInstanceId, activation.TargetInstanceId);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMissingPerTargetPassiveActivationActor()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        ContentId passiveSkill = Id("convergence.skill_system_redesign_sample:ice_boost_sample");
        RuntimeActorSnapshot malformed = CopyActor(
            frost,
            battleActivations: new RuntimeBattleActivationSnapshot(
                [new RuntimePassiveActivationSnapshot(
                    passiveSkill,
                    Id("owner_turn_end"),
                    triggerIndex: 0,
                    activationCount: 1,
                    targetInstanceId: RuntimeInstanceId.Parse("missing_passive_target"))],
                [new RuntimePassiveSkillStateSnapshot(passiveSkill, true)]));

        RuntimeSaveValidationResult validation = CreateModifierAwareValidator().Validate(
            Copy(baseline, actors: [malformed, baseline.Actors[1]]),
            catalog);

        AssertDiagnostic(
            validation,
            RuntimeSaveValidationCode.MissingActorReference,
            "$.actors[0].battleActivations.passiveActivations[0].targetInstanceId");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMalformedCanonicalRosterAndEquipmentOwnership()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot frost = baseline.Actors[0];
        RuntimeActorSnapshot ember = baseline.Actors[1];
        ContentId shortsword = Id("convergence.catalog_surface_sample:shortsword_sample");
        RuntimeActorReferenceSnapshot wrongEmberReference = new(
            ember.Identity.InstanceId,
            frost.Identity.EntityDefinitionId,
            ember.Identity.DisplayName);
        RuntimeActorSnapshot malformedFrost = CopyActor(
            frost,
            equipment: new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Armor, shortsword)
            ]));
        RuntimeActorSnapshot malformedEmber = CopyActor(
            ember,
            equipment: new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Weapon, shortsword)
            ]));
        var missingHostedEntity = new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("missing_hosted_entity"),
            frost.Identity.EntityDefinitionId,
            "Missing Hosted Entity");
        var malformedPartyRoster = new RuntimePartyRosterSnapshot(
            Reference(frost),
            activeParty: [Reference(frost)],
            activeHostedEntity: missingHostedEntity,
            hostedEntityRoster: [wrongEmberReference, wrongEmberReference],
            companionRoster: [Reference(frost)]);
        RuntimeSaveValidationResult validation = CreateModifierAwareValidator().Validate(
            Copy(
                baseline,
                actors: [malformedFrost, malformedEmber],
                partyRoster: malformedPartyRoster),
            catalog);

        Assert.False(validation.IsValid);
        AssertDiagnostic(validation, RuntimeSaveValidationCode.ActiveHostedEntityNotOwned, "$.partyRoster.activeHostedEntity");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.MissingActiveHostedEntityReference, "$.partyRoster.activeHostedEntity");
        Assert.Contains(validation.Diagnostics, item =>
            item.Code == RuntimeSaveValidationCode.ActorReferenceEntityMismatch &&
            item.Path == "$.partyRoster.hostedEntityRoster[0].entityDefinitionId");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.DuplicatePartyRosterReference, "$.partyRoster.hostedEntityRoster[1]");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.EquipmentSlotMismatch, "$.actors[0].equipment.equippedItemIds.armor");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.EquippedEquipmentNotOwned, "$.actors[0].equipment.equippedItemIds.armor");
        AssertDiagnostic(validation, RuntimeSaveValidationCode.EquipmentAssignedToMultipleActors, "$.actors[1].equipment.equippedItemIds.weapon");
    }

    [Fact]
    public void RuntimeSaveSnapshot_DefensivelyCopiesCollectionsAndCheckpointOrder()
    {
        GameDataCatalog catalog = LoadCatalog();
        List<RuntimeActorSnapshot> actors = [CreateActor(RuntimeInstanceId.Parse("frost"), Id("convergence.clean_battle_demo:frost_duelist_demo"))];
        List<KeyValuePair<ContentId, string>> hostContext = [new(Id("scene"), "/root/Frost")];
        List<RuntimeCheckpointEntrySnapshot> checkpoints =
        [
            new(0, RuntimeCheckpointKind.SaveCreated, "Save created.", RuntimeInstanceId.Parse("frost"))
        ];
        RuntimeActorReferenceSnapshot frostRef = Reference(actors[0]);

        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors,
            hostContext,
            checkpoints,
            new RuntimePartyRosterSnapshot(frostRef, activeParty: [frostRef]));
        actors.Add(CreateActor(RuntimeInstanceId.Parse("ember"), Id("convergence.clean_battle_demo:ember_duelist_demo")));
        hostContext.Add(new KeyValuePair<ContentId, string>(Id("late"), "mutation"));
        checkpoints.Add(new RuntimeCheckpointEntrySnapshot(1, RuntimeCheckpointKind.HostAction, "Late mutation."));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(result.IsValid);
        Assert.Single(snapshot.Actors);
        Assert.Single(snapshot.HostContext);
        Assert.Single(snapshot.Checkpoints.Entries);
        Assert.Equal(0, snapshot.Checkpoints.Entries[0].Sequence);
    }

    [Fact]
    public void RuntimeSaveValidator_AggregatesGraphAndCatalogDiagnostics()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("missing.pack:missing_entity"),
            learnedSkills: [Id("missing.pack:missing_skill")],
            ailments: [new RuntimeTimedStateSnapshot(
                Id("missing.pack:missing_ailment"),
                FieldLifetime(new TurnDurationDefinition(1, Id("owner_turn_end"), false)))]);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [frost, frost],
            partyRoster: new RuntimePartyRosterSnapshot(
                new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("ghost_owner"), Id("convergence.clean_battle_demo:frost_duelist_demo"), "Ghost Owner"),
                activeParty: [new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("ghost"), Id("convergence.clean_battle_demo:frost_duelist_demo"), "Ghost")],
                activeHostedEntity: new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("missing_hosted_entity"), Id("convergence.clean_battle_demo:frost_duelist_demo"), "Missing"),
                hostedEntityRoster: [],
                companionRoster: []),
            inventory: new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(Id("missing.pack:missing_item"), 1)],
                [new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(EquipmentSlot.Weapon, [Id("missing.pack:missing_equipment")])]),
            equipment: new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Armor, Id("missing.pack:missing_armor"))
            ]),
            field: new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(Id("missing_location")),
                new RuntimeDungeonTraversalSnapshot(
                    Id("missing.pack:missing_dungeon"),
                    Id("missing_node"))),
            compendium: new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(Id("missing.pack:missing_species"), "Missing", 1, skillIds: [Id("missing.pack:missing_skill")])
            ]),
            knowledge: new RuntimeKnowledgeSnapshot(
                elementalAffinities: [new RuntimeElementalAffinityKnowledgeSnapshot(Id("missing.pack:missing_target"), DamageElement.Fire, ElementalAffinity.Weak)],
                ailmentResistances: [new RuntimeAilmentResistanceKnowledgeSnapshot(Id("missing.pack:missing_target"), Id("missing.pack:missing_ailment"), ResistanceLevel.Resistant)]),
            checkpoints:
            [
                new(2, RuntimeCheckpointKind.SaveCreated, "Second."),
                new(1, RuntimeCheckpointKind.ActorRestored, "Out of order.", RuntimeInstanceId.Parse("ghost"))
            ]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.DuplicateActorInstanceId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingActorReference);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingActiveHostedEntityReference);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogEntity);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogSkill);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogItem);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogEquipment);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogDungeon);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCatalogAilment);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.MissingCompendiumEntity);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.KnowledgeTargetMissing);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == RuntimeSaveValidationCode.InvalidCheckpoint);
        Assert.Throws<RuntimeSaveValidationException>(() => result.RequireValidSnapshot());
    }

    [Fact]
    public void RuntimeSaveValidator_AggregatesDefaultIdentifiersBeforeRestoreOrLookup()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot baseline = CreateSaveSnapshot();
        RuntimeActorSnapshot invalidActor = CopyActor(
            baseline.Actors[0],
            identity: new RuntimeActorIdentitySnapshot(default, default, default, "Invalid Actor"));
        var invalidReference = new RuntimeActorReferenceSnapshot(default, default, "Invalid Reference");
        var snapshot = new RuntimeSaveGameSnapshot(
            baseline.FrameworkVersion,
            baseline.ContentPacks,
            [invalidActor],
            new RuntimePartyRosterSnapshot(invalidReference, activeParty: [invalidReference]),
            new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(default, 1)],
                [
                    new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(
                        EquipmentSlot.Weapon,
                        [default])
                ]),
            new RuntimeEquipmentSnapshot(
            [
                new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Weapon, default)
            ]),
            baseline.Wallet,
            new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(default),
                new RuntimeDungeonTraversalSnapshot(default, default)),
            new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    default,
                    "Invalid Entry",
                    1,
                    [new KeyValuePair<ContentId, int>(default, 1)],
                    [default])
            ]),
            new RuntimeKnowledgeSnapshot(
                ailmentResistances:
                [
                    new RuntimeAilmentResistanceKnowledgeSnapshot(default, default, ResistanceLevel.Normal)
                ]),
            new RuntimeSessionProgressSnapshot(
                (ContentId?)default(ContentId),
                counters: [new KeyValuePair<ContentId, long>(default, 1)],
                flags: [default]),
            new RuntimeCheckpointLogSnapshot(
            [
                new RuntimeCheckpointEntrySnapshot(
                    0,
                    RuntimeCheckpointKind.HostAction,
                    "Invalid identifiers.",
                    (RuntimeInstanceId?)default(RuntimeInstanceId),
                    (ContentId?)default(ContentId))
            ]),
            [new KeyValuePair<ContentId, string>(default, "invalid")]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidRuntimeInstanceId,
            "$.actors[0].identity.instanceId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.actors[0].identity.entityDefinitionId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.actors[0].identity.actorKindId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidRuntimeInstanceId,
            "$.partyRoster.owner.instanceId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.inventory.itemQuantities.keys[0]");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.field.navigation.currentLocationId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.compendium.entries[0].entityId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.knowledge.ailmentResistances[0].ailmentId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.session.moonPhaseId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidRuntimeInstanceId,
            "$.checkpoints.entries[0].actorId");
        AssertDiagnostic(result, RuntimeSaveValidationCode.InvalidContentId,
            "$.hostContext.keys[0]");
        Assert.Throws<RuntimeSaveValidationException>(() => result.RequireValidSnapshot());
        Assert.Throws<ArgumentException>(() => RuntimeActorState.Restore(
            invalidActor,
            CombatDefenseProfile.Empty));
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsDuplicateKnowledgeKeysWithIndexedPaths()
    {
        ContentId entityId = Id("convergence.clean_battle_demo:ember_duelist_demo");
        ContentId ailmentId = Id("convergence.shared_effects_demo:poison_demo");
        var knowledge = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    entityId,
                    DamageElement.Ice,
                    ElementalAffinity.Weak),
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    entityId,
                    DamageElement.Ice,
                    ElementalAffinity.Resist)
            ],
            ailmentResistances:
            [
                new RuntimeAilmentResistanceKnowledgeSnapshot(
                    entityId,
                    ailmentId,
                    ResistanceLevel.Normal),
                new RuntimeAilmentResistanceKnowledgeSnapshot(
                    entityId,
                    ailmentId,
                    ResistanceLevel.Immune)
            ],
            instantDeathResistances:
            [
                new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    entityId,
                    InstantDeathChannel.Light,
                    ResistanceLevel.Normal),
                new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    entityId,
                    InstantDeathChannel.Light,
                    ResistanceLevel.Resistant)
            ],
            analyzedDefenses:
            [
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    entityId,
                    [BattleAnalysisField.ElementalAffinities]),
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    entityId,
                    [BattleAnalysisField.AilmentResistances])
            ]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(
            CreateSaveSnapshot(knowledge: knowledge),
            LoadCatalog());

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateElementalAffinityKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.elementalAffinities[1]", diagnostic.Path);
                Assert.Equal(entityId, diagnostic.ContentId);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateAilmentResistanceKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.ailmentResistances[1]", diagnostic.Path);
                Assert.Equal(entityId, diagnostic.ContentId);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateInstantDeathResistanceKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.instantDeathResistances[1]", diagnostic.Path);
                Assert.Equal(entityId, diagnostic.ContentId);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateAnalyzedDefenseKnowledge, diagnostic.Code);
                Assert.Equal("$.knowledge.analyzedDefenses[1]", diagnostic.Path);
                Assert.Equal(entityId, diagnostic.ContentId);
            });
        Assert.Throws<RuntimeSaveValidationException>(() => result.RequireValidSnapshot());
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsClonedIntrinsicElementKnowledgeWithStableDiagnostic()
    {
        ContentId entityId = Id("convergence.clean_battle_demo:ember_duelist_demo");
        var valid = new RuntimeElementalAffinityKnowledgeSnapshot(
            entityId,
            DamageElement.Ice,
            ElementalAffinity.Weak);
        RuntimeElementalAffinityKnowledgeSnapshot malformed = CloneWithProperty(
            valid,
            nameof(RuntimeElementalAffinityKnowledgeSnapshot.Element),
            DamageElement.Almighty);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            knowledge: new RuntimeKnowledgeSnapshot(elementalAffinities: [malformed]));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, LoadCatalog());

        Assert.False(result.IsValid);
        RuntimeSaveValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuntimeSaveValidationCode.IntrinsicElementKnowledgeNotStorable, diagnostic.Code);
        Assert.Equal("$.knowledge.elementalAffinities[0].element", diagnostic.Path);
        Assert.Equal(entityId, diagnostic.ContentId);
        Assert.Throws<RuntimeSaveValidationException>(() => result.RequireValidSnapshot());
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMissingAnalyzedDefenseTarget()
    {
        ContentId missing = Id("missing.pack:unknown_entity");
        var knowledge = new RuntimeKnowledgeSnapshot(
            elementalAffinities: null,
            ailmentResistances: null,
            instantDeathResistances: null,
            analyzedDefenses:
            [
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    missing,
                    [BattleAnalysisField.ElementalAffinities])
            ]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(
            CreateSaveSnapshot(knowledge: knowledge),
            LoadCatalog());

        RuntimeSaveValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuntimeSaveValidationCode.KnowledgeTargetMissing, diagnostic.Code);
        Assert.Equal("$.knowledge.analyzedDefenses", diagnostic.Path);
        Assert.Equal(missing, diagnostic.ContentId);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsPartyRosterStructuralInvariantViolations()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorSnapshot ward = CreateActor(
            RuntimeInstanceId.Parse("ward"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);
        RuntimeActorReferenceSnapshot wardRef = Reference(ward);
        RuntimePartyRosterSnapshot invalidParty = new(
            frostRef,
            activeParty: [frostRef, emberRef, frostRef],
            reserveMembers: [emberRef],
            activeHostedEntity: frostRef,
            hostedEntityRoster: [frostRef, emberRef, emberRef],
            companionRoster: [frostRef, emberRef, wardRef, wardRef],
            maxActivePartySize: 2);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [frost, ember, ward],
            partyRoster: invalidParty);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator(new FixedRosterCapacityPolicy(3))
            .Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActivePartyCapacityExceeded &&
            diagnostic.Path == "$.partyRoster.activeParty");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.CompanionRosterCapacityExceeded &&
            diagnostic.Path == "$.partyRoster.companionRoster");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyRosterReference &&
            diagnostic.Path == "$.partyRoster.activeParty[2]" &&
            diagnostic.InstanceId == frostRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyRosterReference &&
            diagnostic.Path == "$.partyRoster.reserveMembers[0]" &&
            diagnostic.InstanceId == emberRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyRosterReference &&
            diagnostic.Path == "$.partyRoster.hostedEntityRoster[2]" &&
            diagnostic.InstanceId == emberRef.InstanceId);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicatePartyRosterReference &&
            diagnostic.Path == "$.partyRoster.companionRoster[3]" &&
            diagnostic.InstanceId == wardRef.InstanceId);
    }

    [Fact]
    public void RuntimeSaveValidator_UsesItsSelectedMoveListCapacityPolicy()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot();

        RuntimeSaveValidationResult result = new RuntimeSaveValidator(
            moveListCapacityPolicy: new SharedRuntimeMoveListCapacityPolicy(1))
            .Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ActorMoveListCapacityRejected &&
            diagnostic.Path == "$.actors[0].skills.equippedSkillIds");
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsDuplicateCompendiumEntitiesAndUnlearnedEquippedSkills()
    {
        ContentId entityId = Id("convergence.clean_battle_demo:frost_duelist_demo");
        ContentId learnedSkillId = Id("convergence.clean_battle_demo:frost_lance_demo");
        ContentId unlearnedSkillId = Id("convergence.clean_battle_demo:ember_bolt_demo");
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            compendium: new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    entityId,
                    "Frost Duelist",
                    5,
                    skillIds: [learnedSkillId],
                    equippedSkillIds: [learnedSkillId]),
                new CompendiumEntrySnapshot(
                    entityId,
                    "Frost Duelist Duplicate",
                    5,
                    skillIds: [learnedSkillId],
                    equippedSkillIds: [unlearnedSkillId])
            ]));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, LoadCatalog());

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicateCompendiumEntity &&
            diagnostic.Path == "$.compendium.entries[1].entityId");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.CompendiumEquippedSkillNotLearned &&
            diagnostic.ContentId == unlearnedSkillId);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsDuplicateSkillsAndMalformedCompendiumStatOverrides()
    {
        ContentId entityId = Id("convergence.clean_battle_demo:frost_duelist_demo");
        ContentId skillId = Id("convergence.clean_battle_demo:frost_lance_demo");
        ContentId unknownStatId = Id("forged_stat");
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            compendium: new CompendiumStateSnapshot(
            [
                new CompendiumEntrySnapshot(
                    entityId,
                    "Frost Duelist",
                    5,
                    stats:
                    [
                        new KeyValuePair<ContentId, int>(Id("strength"), -1),
                        new KeyValuePair<ContentId, int>(Id("magic"), 8),
                        new KeyValuePair<ContentId, int>(Id("vitality"), 5),
                        new KeyValuePair<ContentId, int>(Id("agility"), 6),
                        new KeyValuePair<ContentId, int>(unknownStatId, 4)
                    ],
                    skillIds: [skillId, skillId],
                    equippedSkillIds: [skillId, skillId])
            ]));

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, LoadCatalog());

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.InvalidCompendiumStatValue, diagnostic.Code);
                Assert.Equal(Id("strength"), diagnostic.ContentId);
                Assert.Equal("$.compendium.entries[0].stats['strength']", diagnostic.Path);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.UnknownCompendiumStat, diagnostic.Code);
                Assert.Equal(unknownStatId, diagnostic.ContentId);
                Assert.Equal("$.compendium.entries[0].stats['forged_stat']", diagnostic.Path);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.MissingCompendiumStat, diagnostic.Code);
                Assert.Equal(Id("luck"), diagnostic.ContentId);
                Assert.Equal("$.compendium.entries[0].stats", diagnostic.Path);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateCompendiumLearnedSkill, diagnostic.Code);
                Assert.Equal(skillId, diagnostic.ContentId);
                Assert.Equal("$.compendium.entries[0].skillIds[1]", diagnostic.Path);
            },
            diagnostic =>
            {
                Assert.Equal(RuntimeSaveValidationCode.DuplicateCompendiumEquippedSkill, diagnostic.Code);
                Assert.Equal(skillId, diagnostic.ContentId);
                Assert.Equal("$.compendium.entries[0].equippedSkillIds[1]", diagnostic.Path);
            });
    }

    [Fact]
    public void RuntimeSaveValidator_AllowsIntentionalActiveCompanionOwnedStockOverlap()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot();

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Contains(
            snapshot.PartyRoster.CompanionRoster,
            actor => actor.InstanceId == snapshot.PartyRoster.ActiveParty[0].InstanceId);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsIllegalCrossRoleReuseButAllowsOwnerAndActiveCompanionOverlap()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot owner = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot activeCompanion = CreateActor(
            RuntimeInstanceId.Parse("active_companion"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"));
        RuntimeActorSnapshot reserve = CreateActor(
            RuntimeInstanceId.Parse("reserve"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot activeHostedEntity = CreateActor(
            RuntimeInstanceId.Parse("active_hosted_entity"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"));
        RuntimeActorSnapshot hostedEntity = CreateActor(
            RuntimeInstanceId.Parse("hosted_entity_roster_entry"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorReferenceSnapshot ownerRef = Reference(owner);
        RuntimeActorReferenceSnapshot activeCompanionRef = Reference(activeCompanion);
        RuntimeActorReferenceSnapshot reserveRef = Reference(reserve);
        RuntimeActorReferenceSnapshot activeHostedEntityRef = Reference(activeHostedEntity);
        RuntimeActorReferenceSnapshot hostedEntityRef = Reference(hostedEntity);
        RuntimeActorSnapshot[] actors = [owner, activeCompanion, reserve, activeHostedEntity, hostedEntity];
        RuntimePartyRosterSnapshot validParty = new(
            ownerRef,
            activeParty: [ownerRef, activeCompanionRef],
            reserveMembers: [reserveRef],
            activeHostedEntity: activeHostedEntityRef,
            hostedEntityRoster: [activeHostedEntityRef, hostedEntityRef],
            companionRoster: [activeCompanionRef]);

        RuntimeSaveValidationResult valid = new RuntimeSaveValidator().Validate(
            CreateSaveSnapshot(actors: actors, partyRoster: validParty),
            catalog);

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Diagnostics.Select(diagnostic => diagnostic.Message)));

        RuntimePartyRosterSnapshot invalidParty = new(
            ownerRef,
            activeParty: [ownerRef, activeCompanionRef],
            reserveMembers: [reserveRef],
            activeHostedEntity: ownerRef,
            hostedEntityRoster: [ownerRef, activeCompanionRef, reserveRef],
            companionRoster: [activeCompanionRef, reserveRef]);
        RuntimeSaveValidationResult invalid = new RuntimeSaveValidator().Validate(
            CreateSaveSnapshot(actors: actors, partyRoster: invalidParty),
            catalog);

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.PartyRosterIdentityCollision &&
            diagnostic.Path == "$.partyRoster.activeHostedEntity" &&
            diagnostic.InstanceId == ownerRef.InstanceId);
        Assert.Contains(invalid.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.PartyRosterIdentityCollision &&
            diagnostic.Path == "$.partyRoster.hostedEntityRoster[1]" &&
            diagnostic.InstanceId == activeCompanionRef.InstanceId);
        Assert.Contains(invalid.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.PartyRosterIdentityCollision &&
            diagnostic.Path == "$.partyRoster.hostedEntityRoster[2]" &&
            diagnostic.InstanceId == reserveRef.InstanceId);
        Assert.Contains(invalid.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.PartyRosterIdentityCollision &&
            diagnostic.Path == "$.partyRoster.companionRoster[1]" &&
            diagnostic.InstanceId == reserveRef.InstanceId);
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsEntityMismatchForEveryPartyRosterReferenceRole()
    {
        GameDataCatalog catalog = LoadCatalog();
        ContentId actualEntityId = Id("convergence.clean_battle_demo:frost_duelist_demo");
        ContentId claimedEntityId = Id("convergence.clean_battle_demo:ember_duelist_demo");
        RuntimeActorSnapshot[] actors =
        [
            CreateActor(RuntimeInstanceId.Parse("frost"), actualEntityId),
            CreateActor(RuntimeInstanceId.Parse("active"), actualEntityId),
            CreateActor(RuntimeInstanceId.Parse("reserve"), actualEntityId),
            CreateActor(RuntimeInstanceId.Parse("active_hosted_entity"), actualEntityId),
            CreateActor(RuntimeInstanceId.Parse("hosted_entity_roster_entry"), actualEntityId),
            CreateActor(RuntimeInstanceId.Parse("companion"), actualEntityId)
        ];
        RuntimeActorReferenceSnapshot[] mismatches = actors
            .Select(actor => new RuntimeActorReferenceSnapshot(
                actor.Identity.InstanceId,
                claimedEntityId,
                actor.Identity.DisplayName))
            .ToArray();
        RuntimePartyRosterSnapshot party = new(
            mismatches[0],
            activeParty: [mismatches[1]],
            reserveMembers: [mismatches[2]],
            activeHostedEntity: mismatches[3],
            hostedEntityRoster: [mismatches[3], mismatches[4]],
            companionRoster: [mismatches[5]]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(
            CreateSaveSnapshot(actors: actors, partyRoster: party),
            catalog);

        RuntimeSaveValidationDiagnostic[] mismatchedReferences = result.Diagnostics
            .Where(diagnostic => diagnostic.Code == RuntimeSaveValidationCode.ActorReferenceEntityMismatch)
            .ToArray();
        Assert.Equal(7, mismatchedReferences.Length);
        string[] expectedPaths =
        [
            "$.partyRoster.owner.entityDefinitionId",
            "$.partyRoster.activeParty[0].entityDefinitionId",
            "$.partyRoster.reserveMembers[0].entityDefinitionId",
            "$.partyRoster.activeHostedEntity.entityDefinitionId",
            "$.partyRoster.hostedEntityRoster[0].entityDefinitionId",
            "$.partyRoster.hostedEntityRoster[1].entityDefinitionId",
            "$.partyRoster.companionRoster[0].entityDefinitionId"
        ];
        Assert.Equal(
            expectedPaths.Order(),
            mismatchedReferences.Select(diagnostic => diagnostic.Path).Order().ToArray());
        Assert.All(mismatchedReferences, diagnostic => Assert.Equal(claimedEntityId, diagnostic.ContentId));
    }

    [Fact]
    public void RuntimeSaveValidator_UsesOnlyExplicitRosterCapacityPolicy()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot frost = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot ember = CreateActor(
            RuntimeInstanceId.Parse("ember"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorSnapshot ward = CreateActor(
            RuntimeInstanceId.Parse("ward"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot veil = CreateActor(
            RuntimeInstanceId.Parse("veil"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorReferenceSnapshot frostRef = Reference(frost);
        RuntimeActorReferenceSnapshot emberRef = Reference(ember);
        RuntimeActorReferenceSnapshot wardRef = Reference(ward);
        RuntimeActorReferenceSnapshot veilRef = Reference(veil);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [frost, ember, ward, veil],
            partyRoster: new RuntimePartyRosterSnapshot(
                frostRef,
                activeParty: [frostRef],
                companionRoster: [frostRef, emberRef, wardRef, veilRef]));

        RuntimeSaveValidationResult defaultResult = new RuntimeSaveValidator().Validate(snapshot, catalog);
        RuntimeSaveValidationResult constrainedResult = new RuntimeSaveValidator(new FixedRosterCapacityPolicy(3))
            .Validate(snapshot, catalog);
        RuntimeSaveValidationResult permissiveResult = new RuntimeSaveValidator(new FixedRosterCapacityPolicy(4))
            .Validate(snapshot, catalog);

        Assert.True(defaultResult.IsValid);
        Assert.Contains(constrainedResult.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.CompanionRosterCapacityExceeded);
        Assert.True(permissiveResult.IsValid, string.Join(Environment.NewLine, permissiveResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [Fact]
    public void RuntimeSaveValidator_DerivesRosterCapacityFromTheSavedOwnerActor()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot owner = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot first = CreateActor(
            RuntimeInstanceId.Parse("first"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorSnapshot second = CreateActor(
            RuntimeInstanceId.Parse("second"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot third = CreateActor(
            RuntimeInstanceId.Parse("third"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimePartyRosterSnapshot party = new(
            Reference(owner),
            activeParty: [Reference(owner)],
            companionRoster:
            [
                Reference(first),
                Reference(second),
                Reference(third),
                Reference(owner)
            ]);
        var policy = new TieredRosterCapacityPolicy(
        [
            new RosterCapacityTier(RuntimeRosterKind.Companion, 1, 3),
            new RosterCapacityTier(RuntimeRosterKind.Companion, 10, 5)
        ]);
        RuntimeSaveGameSnapshot levelFive = CreateSaveSnapshot(
            actors: [owner, first, second, third],
            partyRoster: party);
        RuntimeActorSnapshot grownOwner = CopyActor(
            owner,
            progression: new RuntimeProgressionSnapshot(10, 0, 0, 0));
        RuntimeSaveGameSnapshot levelTen = CreateSaveSnapshot(
            actors: [grownOwner, first, second, third],
            partyRoster: party);

        RuntimeSaveValidationResult beforeThreshold =
            new RuntimeSaveValidator(policy).Validate(levelFive, catalog);
        RuntimeSaveValidationResult afterThreshold =
            new RuntimeSaveValidator(policy).Validate(levelTen, catalog);

        Assert.Contains(beforeThreshold.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.CompanionRosterCapacityExceeded);
        Assert.True(
            afterThreshold.IsValid,
            string.Join(
                Environment.NewLine,
                afterThreshold.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [Fact]
    public void RuntimeSaveValidator_UsesExplicitCapacityForHostedEntityRoster()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorSnapshot owner = CreateActor(
            RuntimeInstanceId.Parse("frost"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot first = CreateActor(
            RuntimeInstanceId.Parse("hosted_entity_1"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot second = CreateActor(
            RuntimeInstanceId.Parse("hosted_entity_2"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorSnapshot third = CreateActor(
            RuntimeInstanceId.Parse("hosted_entity_3"),
            Id("convergence.clean_battle_demo:frost_duelist_demo"));
        RuntimeActorSnapshot fourth = CreateActor(
            RuntimeInstanceId.Parse("hosted_entity_4"),
            Id("convergence.clean_battle_demo:ember_duelist_demo"),
            learnedSkills: [Id("convergence.clean_battle_demo:ember_bolt_demo")]);
        RuntimeActorReferenceSnapshot ownerRef = Reference(owner);
        RuntimePartyRosterSnapshot party = new(
            ownerRef,
            activeParty: [ownerRef],
            hostedEntityRoster: [Reference(first), Reference(second), Reference(third), Reference(fourth)]);
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            actors: [owner, first, second, third, fourth],
            partyRoster: party);

        RuntimeSaveValidationResult defaultResult = new RuntimeSaveValidator().Validate(snapshot, catalog);
        RuntimeSaveValidationResult constrainedResult = new RuntimeSaveValidator(new FixedRosterCapacityPolicy(3))
            .Validate(snapshot, catalog);
        RuntimeSaveValidationResult permissiveResult = new RuntimeSaveValidator(new FixedRosterCapacityPolicy(4))
            .Validate(snapshot, catalog);

        RuntimeSaveValidationDiagnostic diagnostic = Assert.Single(
            constrainedResult.Diagnostics,
            candidate => candidate.Code == RuntimeSaveValidationCode.HostedEntityRosterCapacityExceeded);
        Assert.Equal("$.partyRoster.hostedEntityRoster", diagnostic.Path);
        Assert.Contains("4 entries", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("capacity of 3", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            constrainedResult.Diagnostics,
            candidate => candidate.Code == RuntimeSaveValidationCode.CompanionRosterCapacityExceeded);
        Assert.True(defaultResult.IsValid);
        Assert.True(
            permissiveResult.IsValid,
            string.Join(Environment.NewLine, permissiveResult.Diagnostics.Select(candidate => candidate.Message)));
    }

    [Fact]
    public void RuntimeSaveValidator_RejectsMissingDuplicateAndVersionMismatchedContentPacks()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot snapshot = Copy(
            CreateSaveSnapshot(),
            contentPacks:
            [
                new ContentPackIdentity("convergence.skill_system_redesign_sample", SemanticVersion.Parse("0.8.0")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("9.9.9")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("0.8.0")),
                new ContentPackIdentity("missing.pack", SemanticVersion.Parse("0.8.0"))
            ]);

        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.ContentPackVersionMismatch &&
            diagnostic.Path == "$.contentPacks[1].version");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.DuplicateContentPack &&
            diagnostic.Path == "$.contentPacks[2].id");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.MissingContentPack &&
            diagnostic.Path == "$.contentPacks[3].id");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSaveValidationCode.MissingContentPack &&
            diagnostic.Path == "$.contentPacks");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void RuntimeSaveValidator_RejectsUnsupportedContractVersion(int unsupportedVersion)
    {
        RuntimeSaveGameSnapshot snapshot = CreateSaveSnapshot(
            contractVersion: unsupportedVersion);
        RuntimeSaveValidationResult result = new RuntimeSaveValidator().Validate(snapshot, LoadCatalog());

        RuntimeSaveValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuntimeSaveValidationCode.ContractVersionUnsupported, diagnostic.Code);
        Assert.Equal("$.contractVersion", diagnostic.Path);
    }

    [Fact]
    public void RuntimeSaveSnapshot_AllowsNavigationAndDungeonModulesToBeOmittedIndependently()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeSaveGameSnapshot noField = CreateSaveSnapshot(includeDefaultField: false);
        RuntimeSaveGameSnapshot navigationOnly = CreateSaveSnapshot(
            field: new RuntimeFieldSnapshot(new RuntimeNavigationSnapshot(Id("host_owned_location"))));

        RuntimeSaveValidationResult noFieldResult = new RuntimeSaveValidator().Validate(noField, catalog);
        RuntimeSaveValidationResult navigationOnlyResult =
            new RuntimeSaveValidator().Validate(navigationOnly, catalog);

        Assert.True(noFieldResult.IsValid);
        Assert.Null(noField.Field);
        Assert.True(navigationOnlyResult.IsValid);
        Assert.Equal(Id("host_owned_location"), navigationOnly.Field!.Navigation.CurrentLocationId);
        Assert.Null(navigationOnly.Field.DungeonTraversal);
    }

    [Fact]
    public void RuntimeSavePolicy_AllowsManualAndSuspendOnlyInRegisteredStableContexts()
    {
        var service = new RuntimeSavePolicyService(new RuntimeSavePolicyOptions(
            manualAllowedContextIds: [Id("field_menu"), Id("dungeon_menu")],
            suspendAllowedContextIds: [Id("field_menu"), Id("dungeon_menu")]));

        RuntimeSavePolicyAssessment manual = service.AssessSave(
            RuntimeSaveKind.Manual,
            new RuntimeSaveContextSnapshot(Id("field_menu")));
        RuntimeSavePolicyAssessment suspend = service.AssessSave(
            RuntimeSaveKind.Suspend,
            new RuntimeSaveContextSnapshot(Id("dungeon_menu")));
        RuntimeSavePolicyAssessment battle = service.AssessSave(
            RuntimeSaveKind.Manual,
            new RuntimeSaveContextSnapshot(Id("battle")));
        RuntimeSavePolicyAssessment pending = service.AssessSave(
            RuntimeSaveKind.Suspend,
            new RuntimeSaveContextSnapshot(Id("field_menu"), hasPendingHostAction: true));

        Assert.True(manual.IsAllowed);
        Assert.True(suspend.IsAllowed);
        Assert.False(battle.IsAllowed);
        Assert.Contains(battle.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.ContextNotAllowed &&
            diagnostic.ContextId == Id("battle"));
        Assert.False(pending.IsAllowed);
        Assert.Contains(pending.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.PendingHostAction);
    }

    [Fact]
    public void RuntimeSavePolicy_AssessesLoadRecordsAndSuspendConsumption()
    {
        var service = new RuntimeSavePolicyService(new RuntimeSavePolicyOptions(
            manualAllowedContextIds: [Id("field_menu")],
            suspendAllowedContextIds: [Id("field_menu")]));
        var context = new RuntimeSaveContextSnapshot(Id("field_menu"));
        RuntimeSaveRecord manual = new(RuntimeSaveKind.Manual, CreateSaveSnapshot(), context, sequence: 3);
        RuntimeSaveRecord suspend = new(RuntimeSaveKind.Suspend, CreateSaveSnapshot(), context, sequence: 4);

        RuntimeSavePolicyAssessment missing = service.AssessLoad(null, RuntimeSaveKind.Manual, context);
        RuntimeSavePolicyAssessment mismatch = service.AssessLoad(manual, RuntimeSaveKind.Suspend, context);
        RuntimeSavePolicyAssessment suspendLoad = service.AssessLoad(suspend, RuntimeSaveKind.Suspend, context);
        RuntimeSavePolicyAssessment manualLoad = service.AssessLoad(manual, RuntimeSaveKind.Manual, context);
        RuntimeSavePolicyAssessment savedContextMismatch = service.AssessLoad(
            new RuntimeSaveRecord(
                RuntimeSaveKind.Manual,
                CreateSaveSnapshot(),
                new RuntimeSaveContextSnapshot(Id("battle"))),
            RuntimeSaveKind.Manual,
            context);
        RuntimeSavePolicyAssessment savedPending = service.AssessLoad(
            new RuntimeSaveRecord(
                RuntimeSaveKind.Manual,
                CreateSaveSnapshot(),
                new RuntimeSaveContextSnapshot(Id("field_menu"), hasPendingHostAction: true)),
            RuntimeSaveKind.Manual,
            context);

        Assert.False(missing.IsAllowed);
        Assert.Contains(missing.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.MissingSaveRecord);
        Assert.False(mismatch.IsAllowed);
        Assert.Contains(mismatch.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.SaveKindMismatch);
        Assert.True(suspendLoad.IsAllowed);
        Assert.True(suspendLoad.ConsumeAfterSuccessfulRestore);
        Assert.True(manualLoad.IsAllowed);
        Assert.False(manualLoad.ConsumeAfterSuccessfulRestore);
        Assert.False(savedContextMismatch.IsAllowed);
        Assert.Contains(savedContextMismatch.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.SavedContextNotAllowed &&
            diagnostic.ContextId == Id("battle"));
        Assert.False(savedPending.IsAllowed);
        Assert.Contains(savedPending.Diagnostics, diagnostic =>
            diagnostic.Code == RuntimeSavePolicyDiagnosticCode.SavedContextPendingHostAction);
    }

    [Fact]
    public void RuntimeSavePolicy_DefensivelyCopiesOptionsAndRecordsMetadata()
    {
        List<ContentId> manualContexts = [Id("field_menu")];
        List<ContentId> suspendContexts = [Id("dungeon_menu")];
        RuntimeSavePolicyOptions options = new(manualContexts, suspendContexts);
        RuntimeSaveRecord record = new(
            RuntimeSaveKind.Manual,
            CreateSaveSnapshot(),
            new RuntimeSaveContextSnapshot(Id("field_menu")),
            sequence: 7);
        manualContexts.Add(Id("battle"));
        suspendContexts.Clear();

        Assert.Equal([Id("field_menu")], options.ManualAllowedContextIds);
        Assert.Equal([Id("dungeon_menu")], options.SuspendAllowedContextIds);
        Assert.Equal(RuntimeSaveKind.Manual, record.Kind);
        Assert.Equal(Id("field_menu"), record.Context.ContextId);
        Assert.Equal(7, record.Sequence);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeSaveRecord(
                RuntimeSaveKind.Manual,
                CreateSaveSnapshot(),
                new RuntimeSaveContextSnapshot(Id("field_menu")),
                sequence: -1));
    }

    [Fact]
    public void RuntimePersistenceContracts_ExposeNoHostSerializerOrLegacyTypes()
    {
        Type[] runtimeTypes = typeof(RuntimeSaveGameSnapshot).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "Convergence.Runtime")
            .ToArray();

        string[] forbidden =
        [
            "System.Console",
            "System.IO",
            "System.Text.Json",
            "Newtonsoft",
            "Godot"
        ];

        foreach (Type type in runtimeTypes)
        {
            AssertAllowed(type, forbidden);
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                switch (member)
                {
                    case MethodInfo method:
                        AssertAllowed(method.ReturnType, forbidden);
                        foreach (ParameterInfo parameter in method.GetParameters()) AssertAllowed(parameter.ParameterType, forbidden);
                        break;
                    case PropertyInfo property:
                        AssertAllowed(property.PropertyType, forbidden);
                        break;
                    case FieldInfo field:
                        AssertAllowed(field.FieldType, forbidden);
                        break;
                }
            }
        }
    }

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
            ],
            passiveSkillStates:
            [
                new RuntimePassiveSkillStateSnapshot(
                    Id("convergence.skill_system_redesign_sample:ice_boost_sample"),
                    IsEnabled: true)
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
                new ContentPackIdentity("convergence.skill_system_redesign_sample", SemanticVersion.Parse("0.8.0")),
                new ContentPackIdentity("convergence.clean_battle_demo", SemanticVersion.Parse("0.8.0")),
                new ContentPackIdentity("convergence.shared_effects_demo", SemanticVersion.Parse("0.8.0")),
                new ContentPackIdentity("convergence.catalog_surface_sample", SemanticVersion.Parse("0.8.0"))
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
                Id("new_moon"),
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

    private static RuntimeSaveGameSnapshot Copy(
        RuntimeSaveGameSnapshot snapshot,
        IEnumerable<ContentPackIdentity>? contentPacks = null,
        IEnumerable<RuntimeActorSnapshot>? actors = null,
        RuntimePartyRosterSnapshot? partyRoster = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeEquipmentSnapshot? equipment = null) =>
        new(
            snapshot.FrameworkVersion,
            contentPacks ?? snapshot.ContentPacks,
            actors ?? snapshot.Actors,
            partyRoster ?? snapshot.PartyRoster,
            inventory ?? snapshot.Inventory,
            equipment ?? snapshot.Equipment,
            snapshot.Wallet,
            snapshot.Field,
            snapshot.Compendium,
            snapshot.Knowledge,
            snapshot.Session,
            snapshot.Checkpoints,
            snapshot.HostContext,
            snapshot.ContractVersion);

    private static RuntimeActorSnapshot CopyActor(
        RuntimeActorSnapshot snapshot,
        RuntimeActorIdentitySnapshot? identity = null,
        RuntimeProgressionSnapshot? progression = null,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        RuntimeStatBlockSnapshot? stats = null,
        RuntimeSkillStateSnapshot? skills = null,
        RuntimeEquipmentSnapshot? equipment = null,
        RuntimeBattleStatusSnapshot? battleStatus = null,
        RuntimeBattleActivationSnapshot? battleActivations = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null,
        IEnumerable<ContentId>? capabilityIds = null,
        RuntimeCombatProfileIdentitySnapshot? combatProfileIdentity = null) =>
        new(
            identity ?? snapshot.Identity,
            snapshot.Affiliation,
            snapshot.EncounterPresence,
            progression ?? snapshot.Progression,
            resources ?? snapshot.Resources,
            stats ?? snapshot.Stats,
            skills ?? snapshot.Skills,
            equipment ?? snapshot.Equipment,
            battleStatus ?? snapshot.BattleStatus,
            battleActivations ?? snapshot.BattleActivations,
            baseResourceValues ?? snapshot.BaseResourceValues,
            snapshot.VitalResourceId,
            capabilityIds ?? snapshot.CapabilityIds,
            combatProfileIdentity ?? snapshot.CombatProfileIdentity);

    private static RuntimeActorSnapshot WithRegeneratePassive(
        RuntimeActorSnapshot actor,
        IEnumerable<RuntimePassiveActivationSnapshot> activations)
    {
        ContentId emberBolt =
            Id("convergence.clean_battle_demo:ember_bolt_demo");
        ContentId regenerate =
            Id("convergence.clean_battle_demo:regenerate_demo");
        return CopyActor(
            actor,
            skills: new RuntimeSkillStateSnapshot(
                [emberBolt, regenerate],
                [emberBolt, regenerate]),
            battleActivations: new RuntimeBattleActivationSnapshot(
                activations,
                [new RuntimePassiveSkillStateSnapshot(regenerate, IsEnabled: true)]));
    }

    private static GameDataCatalog WithEntitySkillUnlock(
        GameDataCatalog catalog,
        ContentId entityId,
        params SkillUnlockDefinition[] skillUnlocks)
    {
        EntityDefinition source = catalog.Entities[entityId];
        var replacement = new EntityDefinition(
            source.Id,
            source.DisplayName,
            source.Description,
            source.EntityKindId,
            source.RaceId,
            source.Rank,
            source.BaseLevel,
            source.Capabilities,
            source.InheritanceRules,
            source.Stats,
            source.ElementalAffinities,
            source.AilmentResistances,
            source.InstantDeathResistances,
            source.BaseSkillIds,
            skillUnlocks);
        return new GameDataCatalog(
            catalog.ContentPacks,
            catalog.Skills,
            catalog.Entities.Select(pair => pair.Key == entityId
                ? new KeyValuePair<ContentId, EntityDefinition>(pair.Key, replacement)
                : pair),
            catalog.Races,
            catalog.Ailments,
            catalog.Items,
            catalog.Equipment,
            catalog.Shops,
            catalog.Negotiations,
            catalog.Encounters,
            catalog.Dungeons,
            catalog.FusionRecipes,
            catalog.Rulesets,
            Registrations());
    }

    private static GameDataCatalog WithAilments(
        GameDataCatalog catalog,
        params AilmentDefinition[] ailments) =>
        new(
            catalog.ContentPacks,
            catalog.Skills,
            catalog.Entities,
            catalog.Races,
            catalog.Ailments.Concat(ailments.Select(ailment =>
                new KeyValuePair<ContentId, AilmentDefinition>(ailment.Id, ailment))),
            catalog.Items,
            catalog.Equipment,
            catalog.Shops,
            catalog.Negotiations,
            catalog.Encounters,
            catalog.Dungeons,
            catalog.FusionRecipes,
            catalog.Rulesets,
            Registrations());

    private static AilmentDefinition RestoreAilment(
        string localId,
        ContentId? exclusivityGroupId = null) =>
        new(
            Id($"convergence.shared_effects_demo:{localId}"),
            localId,
            "Restore validation test ailment.",
            StandardStatusLifetimes.Persistent,
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition(),
            exclusivityGroupId: exclusivityGroupId);

    internal static RuntimeActorSnapshot CreateActor(
        RuntimeInstanceId instanceId,
        ContentId entityId,
        IEnumerable<ContentId>? learnedSkills = null,
        IEnumerable<RuntimeTimedStateSnapshot>? ailments = null,
        IEnumerable<RuntimePassiveSkillStateSnapshot>? passiveSkillStates = null) =>
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
            new RuntimeSkillStateSnapshot(learnedSkills ?? [Id("convergence.clean_battle_demo:frost_lance_demo")], learnedSkills ?? [Id("convergence.clean_battle_demo:frost_lance_demo")]),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(ailments: ailments),
            new RuntimeBattleActivationSnapshot(passiveSkillStates: passiveSkillStates),
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 40)],
            Id("hp"));

    internal static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

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

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(error => $"{error.Code} {error.JsonPath}: {error.Message}")));
        return result.RequireCatalog();
    }

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
            .RegisterMoonPhase("new_moon")
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

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static RuntimeActorRestoreProfile ActorProfile() =>
        new(RuntimeStatSourceKind.Actor, MissingHostedEntityBehavior.UseActorBaseStats);

    private static CatalogBattleActorRestoreRequest ActorRestore(RuntimeActorSnapshot snapshot) =>
        new(
            snapshot,
            RuntimeStatSourceKind.Actor,
            MissingHostedEntityBehavior.UseActorBaseStats,
            statModifierPolicy: ModifierPolicy(snapshot),
            chargePolicy: ChargePolicy(snapshot));

    private static RuntimeSaveValidator CreateModifierAwareValidator() =>
        new(
            rulesetBindings: CreateRulesetBindings(),
            chargePolicies: ChargePolicyRegistry.CreateStandard());

    private static IChargePolicyService? ChargePolicy(RuntimeActorSnapshot snapshot)
    {
        RuntimeChargeStateSnapshot? state = snapshot.BattleStatus.ChargeState;
        if (state is null)
        {
            return null;
        }

        ChargePolicyRegistry registry = ChargePolicyRegistry.CreateStandard();
        return registry.TryResolve(state.PolicyId, out IChargePolicyService? service)
            ? service
            : null;
    }

    private static IRuntimeRulesetBindingResolver CreateRulesetBindings() =>
        new RuntimeRulesetBindingResolver(RuntimeRulesetPolicyFactoryRegistry.CreateStandard());

    private static RuntimeActorSnapshot WithTimedContributions(RuntimeActorSnapshot actor) =>
        CopyActor(
            actor,
            battleStatus: new RuntimeBattleStatusSnapshot(
                statModifiers: new RuntimeStatModifierStateSnapshot(
                    TimedContributionModifierRuleset,
                    [
                        new RuntimeStatModifierTrackSnapshot(
                            Id("attack"),
                            2,
                            [
                                new RuntimeStatModifierContributionSnapshot(
                                    5,
                                    1,
                                    new TurnDurationDefinition(3, Id("owner_turn_end"), false),
                                    new StatModifierLifecycleBoundary(Id("owner_turn_end"), 8)),
                                new RuntimeStatModifierContributionSnapshot(
                                    2,
                                    1,
                                    new TurnDurationDefinition(2, Id("owner_turn_end"), false),
                                    new StatModifierLifecycleBoundary(Id("owner_turn_end"), 7))
                            ])
                    ])));

    private static void AssertContribution(
        RuntimeStatModifierContributionSnapshot contribution,
        long sequence,
        int stageDelta,
        int duration,
        long boundarySequence)
    {
        Assert.Equal(sequence, contribution.Sequence);
        Assert.Equal(stageDelta, contribution.StageDelta);
        TurnDurationDefinition retainedDuration = Assert.IsType<TurnDurationDefinition>(
            contribution.Duration);
        Assert.Equal(duration, retainedDuration.Value);
        Assert.Equal(Id("owner_turn_end"), retainedDuration.TickEventId);
        StatModifierLifecycleBoundary boundary = Assert.IsType<StatModifierLifecycleBoundary>(
            contribution.LastLifecycleBoundary);
        Assert.Equal(Id("owner_turn_end"), boundary.EventId);
        Assert.Equal(boundarySequence, boundary.Sequence);
    }

    private static IStatModifierPolicyService? ModifierPolicy(RuntimeActorSnapshot snapshot)
    {
        if (snapshot.BattleStatus.StatModifiers is not RuntimeStatModifierStateSnapshot state)
        {
            return null;
        }

        return state.PolicyId switch
        {
            var id when id == PersistentModifierRuleset =>
                new StatModifierPolicyService(new PersistentStagedStatModifierPolicy(id)),
            var id when id == TimedContributionModifierRuleset =>
                new StatModifierPolicyService(new TimedContributionStatModifierPolicy(id)),
            _ => null
        };
    }

    private static void AssertDiagnostic(
        RuntimeSaveValidationResult result,
        RuntimeSaveValidationCode code,
        string path) =>
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code && diagnostic.Path == path);

    private static TSnapshot CloneWithProperty<TSnapshot, TValue>(
        TSnapshot source,
        string propertyName,
        TValue value)
        where TSnapshot : class
    {
        MethodInfo memberwiseClone = typeof(object).GetMethod(
            "MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var clone = (TSnapshot)memberwiseClone.Invoke(source, null)!;
        FieldInfo field = typeof(TSnapshot).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Snapshot property '{typeof(TSnapshot).Name}.{propertyName}' has no backing field.");
        field.SetValue(clone, value);
        return clone;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }

    private static void AssertAllowed(Type type, IReadOnlyList<string> forbidden)
    {
        foreach (Type candidate in Expand(type))
        {
            string identity = candidate.FullName ?? candidate.Name;
            Assert.DoesNotContain(forbidden, fragment => identity.Contains(fragment, StringComparison.Ordinal));
        }
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is Type element)
        {
            foreach (Type nested in Expand(element)) yield return nested;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Expand(argument)) yield return nested;
        }
    }

    private sealed class EmptyParameterValidator : IContentParameterValidator
    {
        public static EmptyParameterValidator Instance { get; } = new();

        public IReadOnlyList<ContentParameterValidationIssue> Validate(IReadOnlyDictionary<string, object?> parameters) => [];
    }

    private sealed class NarrowTimedContributionPolicyFactory :
        IRuntimeStatModifierRulesetPolicyFactory
    {
        public ContentId PolicyId => StandardRulesetPolicyIds.TimedContributionStatModifier;

        public RulesetBindingResult<IStatModifierPolicyService> Create(RulesetDefinition definition) =>
            new(new StatModifierPolicyService(
                new TimedContributionStatModifierPolicy(definition.Id, -1, 1)));
    }

    private sealed class FixedRosterCapacityPolicy(int capacity) : IRosterCapacityPolicy
    {
        public int GetCapacity(RuntimeRosterKind rosterKind, int ownerLevel) => capacity;
    }

    private sealed class DelegateActorRestoreProfileResolver(
        Func<RuntimeActorRestoreProfileRequest, RuntimeActorRestoreProfile> resolve)
        : IRuntimeActorRestoreProfileResolver
    {
        public RuntimeActorRestoreProfile Resolve(RuntimeActorRestoreProfileRequest request) => resolve(request);
    }

    private sealed class RecordingActorFactory(
        ICatalogBattleActorFactory inner,
        RuntimeInstanceId? throwingActorId = null) : ICatalogBattleActorFactory
    {
        private readonly List<RuntimeInstanceId> _restoreOrder = [];

        public IReadOnlyList<RuntimeInstanceId> RestoreOrder => _restoreOrder.ToArray();

        public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request) =>
            inner.Create(request);

        public CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request)
        {
            RuntimeInstanceId actorId = request.Snapshot.Identity.InstanceId;
            _restoreOrder.Add(actorId);
            if (throwingActorId is RuntimeInstanceId rejected && actorId == rejected)
            {
                throw new InvalidOperationException("Deliberate actor restore failure.");
            }

            return inner.Restore(request);
        }
    }

    private sealed class FixedMigrationStep(
        int sourceVersion,
        int targetVersion,
        RuntimeSaveGameSnapshot migratedSnapshot) : IRuntimeSaveMigrationStep
    {
        public int SourceContractVersion => sourceVersion;
        public int TargetContractVersion => targetVersion;

        public RuntimeSaveGameSnapshot Migrate(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog) =>
            migratedSnapshot;
    }

    private sealed class RestoreOnlyInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            new(
                Id("hp"),
                [new BattleResourceState(Id("hp"), 1, 1)]);
    }
}
