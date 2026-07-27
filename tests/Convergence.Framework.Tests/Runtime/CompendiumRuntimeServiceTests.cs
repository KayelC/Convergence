using Convergence.Content;
using Convergence.Catalog;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Fusion;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

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
    public void RecordAcquisition_AddsOncePreservesTheSavedEntryAndLeavesExplicitUpdatesAvailable()
    {
        TestContext context = CreateContext();
        var service = context.CreateService();
        RuntimeActorSnapshot firstActor = context.CreateActor("first_acquisition").State.ToSnapshot();
        CompendiumActorRegistrationResult first = service.RecordAcquisition(
            new CompendiumStateSnapshot(),
            firstActor);
        var laterActor = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                firstActor.Identity.InstanceId,
                firstActor.Identity.EntityDefinitionId,
                firstActor.Identity.ActorKindId,
                "Later Acquired Snapshot",
                firstActor.Identity.DisplaySubtitle),
            firstActor.Affiliation,
            firstActor.EncounterPresence,
            firstActor.Progression,
            firstActor.Resources,
            firstActor.Stats,
            firstActor.Skills,
            firstActor.Equipment,
            firstActor.BattleStatus,
            firstActor.BattleActivations,
            firstActor.BaseResourceValues,
            firstActor.VitalResourceId,
            firstActor.CapabilityIds);

        CompendiumActorRegistrationResult repeated = service.RecordAcquisition(first.After, laterActor);

        Assert.Equal(CompendiumRegistrationCode.Added, first.Code);
        Assert.True(first.Applied);
        Assert.True(first.Accepted);
        Assert.Equal(CompendiumRegistrationCode.AlreadyRegistered, repeated.Code);
        Assert.False(repeated.Applied);
        Assert.True(repeated.Accepted);
        Assert.Same(first.After, repeated.Before);
        Assert.Same(first.After, repeated.After);
        Assert.Same(first.Entry, repeated.Entry);
        Assert.NotEqual("Later Acquired Snapshot", repeated.Entry!.DisplayName);

        CompendiumActorRegistrationResult explicitUpdate = service.RegisterActor(repeated.After, laterActor);

        Assert.Equal(CompendiumRegistrationCode.Updated, explicitUpdate.Code);
        Assert.True(explicitUpdate.Applied);
        Assert.Equal("Later Acquired Snapshot", explicitUpdate.Entry!.DisplayName);
        Assert.Equal("Later Acquired Snapshot", Assert.Single(explicitUpdate.After.Entries).DisplayName);
    }

    [Fact]
    public void RegisterActor_RejectsMissingOrIneligibleCatalogEntitiesWithoutMutation()
    {
        TestContext eligible = CreateContext();
        CatalogBattleActor actor = eligible.CreateActor("owned_ashling");
        var missingService = new CompendiumRuntimeService(
            new EmptyEntityRepository(),
            eligible.Catalog,
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
    public void RegisterActor_RejectsMalformedSkillStateWithoutUpdatingTheCompendium()
    {
        TestContext context = CreateContext();
        RuntimeActorSnapshot source = context.CreateActor("malformed_registration").State.ToSnapshot();
        ContentId missingSkillId = Id("test.pack:missing_skill");
        var malformed = new RuntimeActorSnapshot(
            source.Identity,
            source.Affiliation,
            source.EncounterPresence,
            source.Progression,
            source.Resources,
            source.Stats,
            new RuntimeSkillStateSnapshot(
                [missingSkillId, missingSkillId],
                [missingSkillId, missingSkillId]),
            source.Equipment,
            source.BattleStatus,
            source.BattleActivations,
            source.BaseResourceValues,
            source.VitalResourceId,
            source.CapabilityIds);
        CompendiumStateSnapshot state = new();

        CompendiumActorRegistrationResult result = context.CreateService().RegisterActor(state, malformed);

        Assert.False(result.Applied);
        Assert.Equal(CompendiumRegistrationCode.InvalidEntry, result.Code);
        Assert.Same(state, result.Before);
        Assert.Same(state, result.After);
        Assert.Empty(state.Entries);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.DuplicateLearnedSkill);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.DuplicateEquippedSkill);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.MissingSkill);
    }

    [Fact]
    public void RegisterActor_DefaultIdentityReturnsTypedRejectionBeforeCatalogLookup()
    {
        TestContext context = CreateContext();
        RuntimeActorSnapshot source = context.CreateActor("malformed_identity").State.ToSnapshot();
        var malformed = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                default,
                default,
                source.Identity.ActorKindId,
                source.Identity.DisplayName),
            source.Affiliation,
            source.EncounterPresence,
            source.Progression,
            source.Resources,
            source.Stats,
            source.Skills,
            source.Equipment,
            source.BattleStatus,
            source.BattleActivations,
            source.BaseResourceValues,
            source.VitalResourceId,
            source.CapabilityIds);
        CompendiumStateSnapshot state = new();

        CompendiumActorRegistrationResult result = context.CreateService().RegisterActor(state, malformed);

        Assert.False(result.Applied);
        Assert.Same(state, result.Before);
        Assert.Same(state, result.After);
        CompendiumRuntimeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.InvalidIdentifier, diagnostic.Code);
    }

    [Fact]
    public void Recall_IsAtomicAndRestoresRegisteredProgressionStatsSkillsAndFullResources()
    {
        TestContext context = CreateContext(includePassive: true);
        var service = context.CreateService();
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        RuntimeActorSnapshot sourceSnapshot = source.State.ToSnapshot();
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            sourceSnapshot).After;
        RuntimePartyRosterSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            OwnerActor(party),
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.Companion));

        Assert.True(result.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.Applied, result.Code);
        Assert.Equal(wallet.Balance - result.Cost, result.AfterWallet.Balance);
        RuntimeActorReferenceSnapshot stockEntry = Assert.Single(result.AfterPartyRoster.CompanionRoster);
        Assert.Equal(RuntimeInstanceId.Parse("recalled_ashling"), stockEntry.InstanceId);
        CatalogBattleActor recalled = Assert.IsType<CatalogBattleActor>(result.Actor);
        RuntimeActorSnapshot recalledSnapshot = recalled.State.ToSnapshot();
        Assert.Equal(sourceSnapshot.Progression, recalledSnapshot.Progression);
        Assert.Equal(sourceSnapshot.Stats.BaseStats, recalledSnapshot.Stats.BaseStats);
        Assert.Equal(sourceSnapshot.Skills.LearnedSkillIds, recalledSnapshot.Skills.LearnedSkillIds);
        Assert.Equal(sourceSnapshot.Skills.EquippedSkillIds, recalledSnapshot.Skills.EquippedSkillIds);
        RuntimePassiveSkillStateSnapshot passiveState = Assert.Single(
            recalledSnapshot.BattleActivations.PassiveSkillStates);
        Assert.Equal(Id("test.pack:steady_breath"), passiveState.SkillId);
        Assert.True(passiveState.IsEnabled);
        Assert.Equal(Id("player_controller"), recalledSnapshot.Affiliation.CommandAuthorityId);
        Assert.Equal(Id("player_team"), recalledSnapshot.Affiliation.TeamId);
        Assert.All(recalledSnapshot.Resources, resource => Assert.Equal(resource.Maximum, resource.Current));
        Assert.Empty(recalledSnapshot.BattleStatus.Ailments);
        Assert.Empty(recalledSnapshot.Equipment.EquippedItemIds);
        Assert.Empty(party.CompanionRoster);
        Assert.Equal(10_000, wallet.Balance);
    }

    [Theory]
    [InlineData(true, 10_000, CompendiumRecallTransactionCode.DuplicateOwned)]
    [InlineData(false, 0, CompendiumRecallTransactionCode.InsufficientCurrency)]
    public void Recall_RejectionsPreservePartyAndWallet(
        bool alreadyOwned,
        int credits,
        CompendiumRecallTransactionCode expected)
    {
        TestContext context = CreateContext();
        var service = context.CreateService();
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            source.State.ToSnapshot()).After;
        RuntimePartyRosterSnapshot party = EmptyParty(
            companionRoster: alreadyOwned
                ? [Reference(source)]
                : []);
        RuntimeWalletSnapshot wallet = new(credits);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            OwnerActor(party),
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.Companion));

        Assert.False(result.Applied);
        Assert.Equal(expected, result.Code);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Same(wallet, result.AfterWallet);
        Assert.Null(result.Actor);
    }

    [Fact]
    public void Recall_DefaultIdentifiersReturnTypedRejectionWithoutRepositoryAccess()
    {
        TestContext context = CreateContext();
        var service = context.CreateService();
        RuntimePartyRosterSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            new CompendiumStateSnapshot(),
            party,
            OwnerActor(party),
            wallet,
            default,
            default,
            default,
            default,
            CompendiumRecallRosterKind.Companion));

        Assert.False(result.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.InvalidEntry, result.Code);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.InvalidIdentifier, Assert.Single(result.Diagnostics).Code);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Same(wallet, result.AfterWallet);
    }

    [Fact]
    public void Recall_WithoutPricingPolicyIsUnavailableAndFreePolicyRequiresNoWalletBalance()
    {
        TestContext context = CreateContext();
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        CompendiumStateSnapshot compendium = context.CreateService().RegisterActor(
            new CompendiumStateSnapshot(),
            source.State.ToSnapshot()).After;
        RuntimePartyRosterSnapshot party = EmptyParty();
        RuntimeWalletSnapshot emptyWallet = new(0);
        var registrationOnly = new CompendiumRuntimeService(
            context.Catalog,
            context.Catalog,
            context.ActorFactory,
            new StandardResourceGrowthPolicy());
        var freeRecall = new CompendiumRuntimeService(
            context.Catalog,
            context.Catalog,
            context.ActorFactory,
            new StandardResourceGrowthPolicy(),
            compendium: new CompendiumService(new FixedCompendiumRecallPricingPolicy(0)));

        CompendiumRecallTransactionRequest unavailableRequest = new(
            compendium,
            party,
            OwnerActor(party),
            emptyWallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("unavailable_recall"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.Companion);
        CompendiumRecallTransactionRequest freeRequest = new(
            compendium,
            party,
            OwnerActor(party),
            emptyWallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("free_recall"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.Companion);

        CompendiumRecallTransactionResult unavailable = registrationOnly.Recall(unavailableRequest);
        CompendiumRecallTransactionResult free = freeRecall.Recall(freeRequest);

        Assert.Equal(CompendiumRecallTransactionCode.RecallUnavailable, unavailable.Code);
        Assert.Same(party, unavailable.AfterPartyRoster);
        Assert.Same(emptyWallet, unavailable.AfterWallet);
        Assert.Null(unavailable.Actor);
        Assert.True(free.Applied);
        Assert.Equal(0, free.Cost);
        Assert.Same(emptyWallet, free.AfterWallet);
        Assert.Single(free.AfterPartyRoster.CompanionRoster);
    }

    [Theory]
    [InlineData(CompendiumRecallRosterKind.Companion, PartyReferenceLocation.Owner)]
    [InlineData(CompendiumRecallRosterKind.Companion, PartyReferenceLocation.ActiveParty)]
    [InlineData(CompendiumRecallRosterKind.Companion, PartyReferenceLocation.ReserveParty)]
    [InlineData(CompendiumRecallRosterKind.Companion, PartyReferenceLocation.ActiveHostedEntity)]
    [InlineData(CompendiumRecallRosterKind.Companion, PartyReferenceLocation.HostedEntityRoster)]
    [InlineData(CompendiumRecallRosterKind.Companion, PartyReferenceLocation.CompanionRoster)]
    [InlineData(CompendiumRecallRosterKind.HostedEntity, PartyReferenceLocation.Owner)]
    [InlineData(CompendiumRecallRosterKind.HostedEntity, PartyReferenceLocation.ActiveParty)]
    [InlineData(CompendiumRecallRosterKind.HostedEntity, PartyReferenceLocation.ReserveParty)]
    [InlineData(CompendiumRecallRosterKind.HostedEntity, PartyReferenceLocation.ActiveHostedEntity)]
    [InlineData(CompendiumRecallRosterKind.HostedEntity, PartyReferenceLocation.HostedEntityRoster)]
    [InlineData(CompendiumRecallRosterKind.HostedEntity, PartyReferenceLocation.CompanionRoster)]
    public void Recall_RejectsRuntimeIdsUsedAnywhereInPartyRosterBeforeMutation(
        CompendiumRecallRosterKind destination,
        PartyReferenceLocation collisionLocation)
    {
        TestContext context = CreateContext();
        CompendiumRuntimeService service = context.CreateService();
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            context.CreateActor("registered_ashling").State.ToSnapshot()).After;
        RuntimeInstanceId recalledId = RuntimeInstanceId.Parse("recalled_collision");
        RuntimePartyRosterSnapshot party = PartyWithCollision(collisionLocation, recalledId);
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            OwnerActor(party),
            wallet,
            context.Entity.Id,
            recalledId,
            Id("player_controller"),
            Id("player_team"),
            destination));

        Assert.False(result.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.DuplicateRuntimeInstanceId, result.Code);
        Assert.Same(party, result.BeforePartyRoster);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Same(wallet, result.BeforeWallet);
        Assert.Same(wallet, result.AfterWallet);
        Assert.Null(result.Actor);
        CompendiumRuntimeDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.DuplicateRuntimeInstanceId, diagnostic.Code);
        Assert.Equal(recalledId, diagnostic.InstanceId);
    }

    [Fact]
    public void Recall_UsesTheSelectedStockPolicyAndReportsCapacityBeforeMutation()
    {
        TestContext context = CreateContext();
        var partyTransitions = new PartyRosterTransitionService(new FixedRosterCapacityPolicy(0));
        CompendiumRuntimeService service = context.CreateService(partyTransitions);
        CatalogBattleActor source = context.CreateActor("owned_ashling");
        CompendiumStateSnapshot compendium = service.RegisterActor(
            new CompendiumStateSnapshot(),
            source.State.ToSnapshot()).After;
        RuntimePartyRosterSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult full = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            OwnerActor(party),
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.HostedEntity));

        Assert.Equal(CompendiumRecallTransactionCode.RosterFull, full.Code);
        Assert.Same(party, full.AfterPartyRoster);
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
                    new KeyValuePair<ContentId, int>(Id("strength"), 4),
                    new KeyValuePair<ContentId, int>(Id("magic"), 6),
                    new KeyValuePair<ContentId, int>(Id("vitality"), int.MaxValue),
                    new KeyValuePair<ContentId, int>(Id("agility"), 4),
                    new KeyValuePair<ContentId, int>(Id("luck"), 3)
                ])
        ]);
        RuntimePartyRosterSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(int.MaxValue);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            OwnerActor(party),
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("recalled_ashling"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.Companion));

        Assert.Equal(CompendiumRecallTransactionCode.InvalidRecallCost, result.Code);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Same(wallet, result.AfterWallet);
        Assert.Equal(CompendiumRuntimeDiagnosticCode.InvalidRecallCost, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Recall_RejectsMalformedEntryBeforeActorCreationOrCurrencyMutation()
    {
        TestContext context = CreateContext();
        var actors = new TrackingActorFactory(context.ActorFactory);
        var economy = new TrackingEconomyService();
        var service = new CompendiumRuntimeService(
            context.Catalog,
            context.Catalog,
            actors,
            new StandardResourceGrowthPolicy(),
            economy: economy);
        ContentId learnedSkillId = Id("test.pack:missing_learned_skill");
        ContentId equippedSkillId = Id("test.pack:missing_equipped_skill");
        var compendium = new CompendiumStateSnapshot(
        [
            new CompendiumEntrySnapshot(
                context.Entity.Id,
                context.Entity.DisplayName,
                level: 3,
                stats:
                [
                    new KeyValuePair<ContentId, int>(Id("strength"), -1),
                    new KeyValuePair<ContentId, int>(Id("magic"), 6),
                    new KeyValuePair<ContentId, int>(Id("vitality"), 5),
                    new KeyValuePair<ContentId, int>(Id("agility"), 4),
                    new KeyValuePair<ContentId, int>(Id("forged_stat"), 3)
                ],
                skillIds: [learnedSkillId, learnedSkillId],
                equippedSkillIds: [equippedSkillId, equippedSkillId])
        ]);
        RuntimePartyRosterSnapshot party = EmptyParty();
        RuntimeWalletSnapshot wallet = new(10_000);

        CompendiumRecallTransactionResult result = service.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            party,
            OwnerActor(party),
            wallet,
            context.Entity.Id,
            RuntimeInstanceId.Parse("invalid_recall"),
            Id("player_controller"),
            Id("player_team"),
            CompendiumRecallRosterKind.Companion));

        Assert.False(result.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.InvalidEntry, result.Code);
        Assert.Same(party, result.BeforePartyRoster);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Same(wallet, result.BeforeWallet);
        Assert.Same(wallet, result.AfterWallet);
        Assert.Equal(0, result.Cost);
        Assert.Null(result.Actor);
        Assert.Equal(0, actors.CreateCalls);
        Assert.Equal(0, actors.RestoreCalls);
        Assert.Equal(0, economy.SpendCalls);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.InvalidStatValue);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.UnknownStat);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.MissingStat);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.DuplicateLearnedSkill);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.DuplicateEquippedSkill);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.MissingSkill);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == CompendiumRuntimeDiagnosticCode.EquippedSkillNotLearned);
    }

    [Fact]
    public void FamiliarKnowledgeImport_RevealsTypedCatalogDefensesWithoutMutatingOtherKnowledge()
    {
        TestContext context = CreateContext();
        RuntimeKnowledgeSnapshot playerKnowledge = new();
        RuntimeKnowledgeSnapshot encounterAiKnowledge = new();
        var service = FamiliarKnowledgeService(context);

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
    public void FamiliarKnowledgeImport_RejectsDuplicateCurrentKnowledgeWithoutThrowingOrMutating()
    {
        TestContext context = CreateContext();
        var current = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    context.Entity.Id,
                    DamageElement.Ice,
                    ElementalAffinity.Weak),
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    context.Entity.Id,
                    DamageElement.Ice,
                    ElementalAffinity.Resist)
            ],
            ailmentResistances:
            [
                new RuntimeAilmentResistanceKnowledgeSnapshot(
                    context.Entity.Id,
                    context.Ailment.Id,
                    ResistanceLevel.Normal),
                new RuntimeAilmentResistanceKnowledgeSnapshot(
                    context.Entity.Id,
                    context.Ailment.Id,
                    ResistanceLevel.Resistant)
            ],
            instantDeathResistances:
            [
                new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    context.Entity.Id,
                    InstantDeathChannel.Dark,
                    ResistanceLevel.Normal),
                new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    context.Entity.Id,
                    InstantDeathChannel.Dark,
                    ResistanceLevel.Immune)
            ]);

        FamiliarKnowledgeImportResult result = FamiliarKnowledgeService(context)
            .Import(current, [context.Entity.Id]);

        Assert.False(result.IsSuccess);
        Assert.Same(current, result.Before);
        Assert.Same(current, result.After);
        Assert.Empty(result.ImportedEntityIds);
        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(
                    FamiliarKnowledgeImportDiagnosticCode.DuplicateElementalAffinityKnowledge,
                    diagnostic.Code);
                Assert.Equal(context.Entity.Id, diagnostic.EntityId);
                Assert.Equal(1, diagnostic.Index);
            },
            diagnostic =>
            {
                Assert.Equal(
                    FamiliarKnowledgeImportDiagnosticCode.DuplicateAilmentResistanceKnowledge,
                    diagnostic.Code);
                Assert.Equal(context.Entity.Id, diagnostic.EntityId);
                Assert.Equal(1, diagnostic.Index);
            },
            diagnostic =>
            {
                Assert.Equal(
                    FamiliarKnowledgeImportDiagnosticCode.DuplicateInstantDeathResistanceKnowledge,
                    diagnostic.Code);
                Assert.Equal(context.Entity.Id, diagnostic.EntityId);
                Assert.Equal(1, diagnostic.Index);
            });
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

        FamiliarKnowledgeImportResult result = FamiliarKnowledgeService(context)
            .ImportRegistered(new RuntimeKnowledgeSnapshot(), state);

        Assert.Equal([context.Entity.Id], result.ImportedEntityIds);
        FamiliarKnowledgeImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(FamiliarKnowledgeImportDiagnosticCode.EntityMissing, diagnostic.Code);
        Assert.Equal(Id("missing.pack:entity"), diagnostic.EntityId);
    }

    [Fact]
    public void FamiliarKnowledgeImport_DefaultIdentifiersRemainInsideTypedDiagnostics()
    {
        TestContext context = CreateContext();
        var service = FamiliarKnowledgeService(context);
        var malformedCurrent = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    default,
                    DamageElement.Ice,
                    ElementalAffinity.Weak)
            ]);

        FamiliarKnowledgeImportResult malformedState = service.Import(
            malformedCurrent,
            [context.Entity.Id]);

        Assert.False(malformedState.IsSuccess);
        Assert.Same(malformedCurrent, malformedState.Before);
        Assert.Same(malformedCurrent, malformedState.After);
        Assert.Empty(malformedState.ImportedEntityIds);
        Assert.Equal(
            FamiliarKnowledgeImportDiagnosticCode.InvalidIdentifier,
            Assert.Single(malformedState.Diagnostics).Code);

        FamiliarKnowledgeImportResult malformedRequest = service.Import(
            new RuntimeKnowledgeSnapshot(),
            [default, context.Entity.Id]);

        Assert.False(malformedRequest.IsSuccess);
        Assert.Equal([context.Entity.Id], malformedRequest.ImportedEntityIds);
        FamiliarKnowledgeImportDiagnostic diagnostic = Assert.Single(malformedRequest.Diagnostics);
        Assert.Equal(FamiliarKnowledgeImportDiagnosticCode.InvalidIdentifier, diagnostic.Code);
        Assert.Equal(0, diagnostic.Index);
    }

    [Fact]
    public void FamiliarKnowledgeImport_UsesTheCanonicalTransitionAndPreservesAnalyzeKnowledge()
    {
        TestContext context = CreateContext();
        var current = new RuntimeKnowledgeSnapshot(
            elementalAffinities: null,
            ailmentResistances: null,
            instantDeathResistances: null,
            analyzedDefenses:
            [
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    context.Entity.Id,
                    [BattleAnalysisField.ElementalAffinities])
            ]);

        FamiliarKnowledgeImportResult result = FamiliarKnowledgeService(context).Import(
            current,
            [context.Entity.Id],
            FamiliarKnowledgeImportSource.Acquisition);

        Assert.True(result.IsSuccess);
        RuntimeAnalyzedDefenseKnowledgeSnapshot profile = Assert.Single(result.After.AnalyzedDefenses);
        Assert.Equal(
        [
            BattleAnalysisField.ElementalAffinities,
            BattleAnalysisField.AilmentResistances,
            BattleAnalysisField.InstantDeathResistances
        ],
            profile.DisclosedFields);
        Assert.NotSame(current, result.After);
    }

    [Fact]
    public void FamiliarKnowledgeImport_DisabledPolicyLeavesKnowledgeUnchanged()
    {
        TestContext context = CreateContext();
        var current = new RuntimeKnowledgeSnapshot();
        var service = new FamiliarEntityKnowledgeService(
            context.Catalog,
            new DisabledFamiliarKnowledgeImportPolicy());

        FamiliarKnowledgeImportResult result = service.Import(
            current,
            [context.Entity.Id],
            FamiliarKnowledgeImportSource.Acquisition);

        Assert.True(result.IsSuccess);
        Assert.Same(current, result.After);
        Assert.Empty(result.ImportedEntityIds);
    }

    [Fact]
    public void FamiliarKnowledgeImport_PolicyCanDistinguishAcquisitionFromRegistration()
    {
        TestContext context = CreateContext();
        var policy = new SourceSelectiveFamiliarKnowledgePolicy(
            FamiliarKnowledgeImportSource.Acquisition);
        var service = new FamiliarEntityKnowledgeService(context.Catalog, policy);
        var current = new RuntimeKnowledgeSnapshot();

        FamiliarKnowledgeImportResult registration = service.Import(
            current,
            [context.Entity.Id],
            FamiliarKnowledgeImportSource.CompendiumRegistration);
        FamiliarKnowledgeImportResult acquisition = service.Import(
            current,
            [context.Entity.Id],
            FamiliarKnowledgeImportSource.Acquisition);

        Assert.Same(current, registration.After);
        Assert.Empty(registration.ImportedEntityIds);
        Assert.Equal([context.Entity.Id], acquisition.ImportedEntityIds);
        Assert.Equal(
        [
            FamiliarKnowledgeImportSource.CompendiumRegistration,
            FamiliarKnowledgeImportSource.Acquisition
        ],
            policy.Sources);
    }

    [Fact]
    public void FamiliarKnowledgeImport_InvalidPolicyDecisionRejectsWithoutMutation()
    {
        TestContext context = CreateContext();
        var current = new RuntimeKnowledgeSnapshot();
        var service = new FamiliarEntityKnowledgeService(
            context.Catalog,
            new InvalidFamiliarKnowledgePolicy());

        FamiliarKnowledgeImportResult result = service.Import(
            current,
            [context.Entity.Id],
            FamiliarKnowledgeImportSource.Acquisition);

        Assert.False(result.IsSuccess);
        Assert.Same(current, result.After);
        Assert.Empty(result.ImportedEntityIds);
        Assert.Equal(
            FamiliarKnowledgeImportDiagnosticCode.InvalidPolicyDecision,
            Assert.Single(result.Diagnostics).Code);
    }

    private static FamiliarEntityKnowledgeService FamiliarKnowledgeService(TestContext context) =>
        new(context.Catalog, new StandardFamiliarKnowledgeImportPolicy());

    private static TestContext CreateContext(
        bool compendiumEligible = true,
        bool includePassive = false)
    {
        ContentId entityId = Id("test.pack:ashling");
        ContentId ailmentId = Id("test.pack:poison");
        ContentId passiveId = Id("test.pack:steady_breath");
        SkillDefinition? passive = includePassive
            ? new SkillDefinition(
                passiveId,
                "Steady Breath",
                "Framework Compendium test passive.",
                SkillActivation.Passive,
                null,
                InheritanceGroup.Passive,
                new SkillInheritanceDefinition(true))
            : null;
        var entity = new EntityDefinition(
            entityId,
            "Ashling",
            "Framework Compendium test entity.",
            Id("companion"),
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
            ],
            baseSkillIds: passive is null ? [] : [passive.Id]);
        var ailment = new AilmentDefinition(
            ailmentId,
            "Poison",
            "Test ailment.",
            StandardStatusLifetimes.Persistent,
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());
        var catalog = new GameDataCatalog(
            contentPacks: [],
            skills: passive is null
                ? []
                : [new KeyValuePair<ContentId, SkillDefinition>(passive.Id, passive)],
            entities: [new KeyValuePair<ContentId, EntityDefinition>(entity.Id, entity)],
            races: [],
            ailments: [new KeyValuePair<ContentId, AilmentDefinition>(ailment.Id, ailment)],
            items: []);
        var actorFactory = new CatalogBattleActorFactory(catalog, catalog, new TestInitializationPolicy());
        return new TestContext(entity, ailment, catalog, actorFactory);
    }

    private static RuntimePartyRosterSnapshot EmptyParty(
        IEnumerable<RuntimeActorReferenceSnapshot>? companionRoster = null) =>
        new(
            new RuntimeActorReferenceSnapshot(
                RuntimeInstanceId.Parse("owner"),
                Id("test.pack:owner"),
                "Owner"),
            activeParty:
            [
                new RuntimeActorReferenceSnapshot(
                    RuntimeInstanceId.Parse("owner"),
                    Id("test.pack:owner"),
                    "Owner")
            ],
            companionRoster: companionRoster);

    private static RuntimeActorSnapshot OwnerActor(RuntimePartyRosterSnapshot party) =>
        new(
            new RuntimeActorIdentitySnapshot(
                party.Owner.InstanceId,
                party.Owner.EntityDefinitionId,
                Id("independent_actor"),
                party.Owner.DisplayName),
            new RuntimeActorAffiliationSnapshot(
                Id("player_controller"),
                Id("player_team")),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeProgressionSnapshot(10, 0, 0, 0),
            [new RuntimeResourceSnapshot(Id("hp"), 1, 1)],
            new RuntimeStatBlockSnapshot(),
            new RuntimeSkillStateSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 1)],
            Id("hp"));

    private static RuntimeActorReferenceSnapshot Reference(CatalogBattleActor actor) =>
        new(actor.State.InstanceId, actor.Entity.Id, actor.Entity.DisplayName);

    private static RuntimePartyRosterSnapshot PartyWithCollision(
        PartyReferenceLocation location,
        RuntimeInstanceId collisionId)
    {
        RuntimeActorReferenceSnapshot owner = new(
            RuntimeInstanceId.Parse("owner"),
            Id("test.pack:owner"),
            "Owner");
        RuntimeActorReferenceSnapshot collision = new(
            collisionId,
            Id("test.pack:other_entity"),
            "Other Actor");
        return new RuntimePartyRosterSnapshot(
            location == PartyReferenceLocation.Owner ? collision : owner,
            activeParty: location == PartyReferenceLocation.ActiveParty
                ? [owner, collision]
                : location == PartyReferenceLocation.Owner
                    ? []
                    : [owner],
            reserveMembers: location == PartyReferenceLocation.ReserveParty ? [collision] : [],
            activeHostedEntity: location == PartyReferenceLocation.ActiveHostedEntity ? collision : null,
            hostedEntityRoster: location == PartyReferenceLocation.HostedEntityRoster ? [collision] : [],
            companionRoster: location == PartyReferenceLocation.CompanionRoster ? [collision] : []);
    }

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
                IsDeployed: false,
                Id("player_controller"),
                new RuntimeProgressionSnapshot(Entity.BaseLevel, 4, 9, 2))).RequireActor();

        public CompendiumRuntimeService CreateService(IPartyRosterTransitionService? partyRoster = null) =>
            new(
                Catalog,
                Catalog,
                ActorFactory,
                new StandardResourceGrowthPolicy(),
                compendium: new CompendiumService(new LinearCompendiumRecallPricingPolicy(
                    defaultBasePrice: 2000,
                    levelFactor: 100,
                    statPointFactor: 50,
                    skillFactor: 200)),
                partyRoster: partyRoster);
    }

    public enum PartyReferenceLocation
    {
        Owner,
        ActiveParty,
        ReserveParty,
        ActiveHostedEntity,
        HostedEntityRoster,
        CompanionRoster
    }

    private sealed class SourceSelectiveFamiliarKnowledgePolicy(
        FamiliarKnowledgeImportSource allowedSource) : IFamiliarKnowledgeImportPolicy
    {
        private readonly List<FamiliarKnowledgeImportSource> _sources = [];

        public IReadOnlyList<FamiliarKnowledgeImportSource> Sources => _sources.AsReadOnly();

        public IReadOnlyList<BattleAnalysisField> SelectDefenseFields(
            FamiliarKnowledgeImportPolicyRequest request)
        {
            _sources.Add(request.Source);
            return request.Source == allowedSource
                ? [BattleAnalysisField.ElementalAffinities]
                : [];
        }
    }

    private sealed class InvalidFamiliarKnowledgePolicy : IFamiliarKnowledgeImportPolicy
    {
        public IReadOnlyList<BattleAnalysisField> SelectDefenseFields(
            FamiliarKnowledgeImportPolicyRequest request) =>
            [BattleAnalysisField.CurrentHp];
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

    private sealed class TrackingActorFactory(ICatalogBattleActorFactory inner) : ICatalogBattleActorFactory
    {
        public int CreateCalls { get; private set; }
        public int RestoreCalls { get; private set; }

        public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request)
        {
            CreateCalls++;
            return inner.Create(request);
        }

        public CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request)
        {
            RestoreCalls++;
            return inner.Restore(request);
        }
    }

    private sealed class TrackingEconomyService : IEconomyTransactionService
    {
        private readonly IEconomyTransactionService _inner = new EconomyTransactionService();

        public int SpendCalls { get; private set; }

        public WalletTransactionResult Credit(RuntimeWalletSnapshot snapshot, int amount) =>
            _inner.Credit(snapshot, amount);

        public WalletTransactionResult Debit(RuntimeWalletSnapshot snapshot, int amount)
        {
            SpendCalls++;
            return _inner.Debit(snapshot, amount);
        }
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

    private sealed class FixedRosterCapacityPolicy(int capacity) : IRosterCapacityPolicy
    {
        public int GetCapacity(RuntimeRosterKind rosterKind, int ownerLevel) => capacity;
    }
}
