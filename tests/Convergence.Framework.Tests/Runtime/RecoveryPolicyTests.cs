using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RecoveryPolicyTests
{
    private static readonly ContentId Credits = Id("test.pack:credits");
    private static readonly ContentId Hp = Id("hp");
    private static readonly ContentId Sp = Id("sp");
    private static readonly ContentId Team = Id("patient_team");

    [Fact]
    public void StandardPolicy_UsesGenericResourceIdsAndTruncatesOnceAfterAggregation()
    {
        ContentId stamina = Id("stamina");
        ContentId focus = Id("focus");
        RuntimeActorState actor = Actor(
            new BattleResourceState(stamina, 3m, 10m),
            new BattleResourceState(focus, 2m, 5m));
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(stamina, 0.25m), Pair(focus, 0.50m)],
            removeAilments: false);

        RecoveryPolicyDecision decision = policy.Plan(new RecoveryPolicyRequest(actor.ToSnapshot()));

        RecoveryTreatmentPlan plan = Assert.IsType<RecoveryTreatmentPlan>(decision.Plan);
        Assert.True(decision.IsSuccess);
        Assert.Equal(3, plan.Cost);
        Assert.Equal([stamina, focus], plan.ResourceIds);
    }

    [Fact]
    public void StandardPolicy_PreservesEstablishedHpSpQuoteAndRejectsOverflow()
    {
        RuntimeActorState patient = Actor(
            new BattleResourceState(Hp, 30m, 100m),
            new BattleResourceState(Sp, 10m, 20m));
        var standard = StandardPolicy();
        RuntimeActorState extreme = Actor(new BattleResourceState(Hp, 0m, decimal.MaxValue));
        var overflowing = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, decimal.MaxValue)],
            removeAilments: false);

        RecoveryPolicyDecision quoted = standard.Plan(new RecoveryPolicyRequest(patient.ToSnapshot()));
        RecoveryPolicyDecision overflow = overflowing.Plan(new RecoveryPolicyRequest(extreme.ToSnapshot()));

        Assert.Equal(120, quoted.Plan!.Cost);
        Assert.False(overflow.IsSuccess);
        Assert.Equal(RecoveryPolicyDiagnosticCode.NumericOverflow, Assert.Single(overflow.Diagnostics).Code);
    }

    [Fact]
    public void Assessment_IsPureAndExecutionReevaluatesCurrentState()
    {
        RuntimeActorState actor = Actor(
            new BattleResourceState(Hp, 90m, 100m),
            new BattleResourceState(Sp, 20m, 20m));
        RuntimeCurrencyLedgerSnapshot ledger = Ledger(100);
        var service = Service(StandardPolicy());

        RecoveryTransactionResult assessment = service.Assess(actor, ledger);
        actor.SetResource(Hp, 60m);
        RecoveryTransactionResult execution = service.Recover(actor, ledger);

        Assert.Equal(RecoveryOperation.Assessment, assessment.Operation);
        Assert.Equal(10, assessment.Cost);
        Assert.Equal(90m, Resource(assessment.BeforeActor, Hp).Current);
        Assert.Equal(100m, Resource(assessment.AfterActor, Hp).Current);
        Assert.Equal(60m, execution.BeforeActor.Resources.Single(resource => resource.ResourceId == Hp).Current);
        Assert.Equal(40, execution.Cost);
        Assert.Equal(100m, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(60, execution.AfterCurrencyLedger.GetRequiredBalance(Credits));
        Assert.Equal(100, ledger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_RollsBackActorAndCurrencyWhenFundsAreInsufficientOrCurrencyIsMissing()
    {
        RuntimeActorState actor = Actor(
            new BattleResourceState(Hp, 30m, 100m),
            new BattleResourceState(Sp, 10m, 20m));
        RuntimeActorSnapshot before = actor.ToSnapshot();
        var service = Service(StandardPolicy());
        RuntimeCurrencyLedgerSnapshot insufficientLedger = Ledger(119);
        RuntimeCurrencyLedgerSnapshot wrongLedger = RuntimeCurrencyLedgerSnapshot.Single(
            Id("test.pack:tokens"),
            500);

        RecoveryTransactionResult insufficient = service.Recover(actor, insufficientLedger);
        RecoveryTransactionResult missing = service.Recover(actor, wrongLedger);

        Assert.Equal(RecoveryTransactionCode.InsufficientCurrency, insufficient.Code);
        Assert.Equal(RecoveryTransactionCode.CurrencyNotFound, missing.Code);
        AssertActorUnchanged(before, actor.ToSnapshot());
        Assert.Same(insufficientLedger, insufficient.AfterCurrencyLedger);
        Assert.Same(wrongLedger, missing.AfterCurrencyLedger);
    }

    [Fact]
    public void Recover_RemovesOnlyAilmentsWhoseProfilesPermitRecoveryEvents()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 100m, 100m));
        AilmentDefinition removable = Ailment(
            "removable",
            new StatusRemovalProfileDefinition([StatusRemovalCause.RecoveryEvent]));
        AilmentDefinition protectedAilment = Ailment(
            "protected",
            StatusRemovalProfiles.Protected);
        actor.ApplyAilment(removable, removable.DefaultLifetime);
        actor.ApplyAilment(protectedAilment, protectedAilment.DefaultLifetime);
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, 1m)],
            removeAilments: true);

        RecoveryTransactionResult result = Service(policy).Recover(actor, Ledger(0));

        Assert.True(result.Applied);
        Assert.False(actor.HasAilment(removable.Id));
        Assert.True(actor.HasAilment(protectedAilment.Id));
        Assert.Equal(0, result.Cost);
        Assert.Equal(0, result.AfterCurrencyLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_ClearsEveryConfiguredTemporaryCategoryThroughCanonicalBoundaries()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 90m, 100m));
        StatusLifetimeDefinition removable = Lifetime(StatusRemovalProfiles.Standard);
        actor.SetGuarding(true);
        IStatModifierPolicyService modifiers = new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(Id("test.pack:recovery_modifiers")));
        StatModifierTransitionResult modifier = modifiers.Apply(new StatModifierApplicationRequest(
            new RuntimeStatModifierStateSnapshot(modifiers.PolicyId),
            Id("attack"),
            1));
        actor.ReplaceStatModifierState(modifiers, modifier.After);
        Assert.True(new SplitChargePolicy().Apply(new ChargeApplicationRequest(
            actor,
            ChargeKind.Physical,
            2m,
            removable)).Applied);
        actor.GrantShield(ShieldKind.Physical, removable);
        actor.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Null, removable);
        actor.BreakAffinity(DamageElement.Ice, removable);
        actor.AddOtherStatus(Id("recovery_mark"), removable);
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, 1m)],
            removeAilments: false,
            Enum.GetValues<RecoveryTemporaryStateKind>());

        RecoveryTransactionResult result = Service(policy).Recover(actor, Ledger(10), modifiers);

        Assert.True(result.Applied);
        Assert.Equal(100m, actor.GetRequiredResource(Hp).Current);
        Assert.False(actor.IsGuarding);
        Assert.Empty(actor.StatStages);
        Assert.Empty(actor.Charges);
        Assert.Empty(actor.Shields);
        Assert.Empty(actor.AffinityOverrides);
        Assert.Empty(actor.AffinityBreaks);
        Assert.Empty(actor.OtherStatuses);
        Assert.Equal(0, result.AfterCurrencyLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_PreservesProtectedTemporaryStateAndReportsNoChange()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 100m, 100m));
        actor.GrantShield(ShieldKind.Magical, Lifetime(StatusRemovalProfiles.Protected));
        actor.AddOtherStatus(Id("protected_mark"), Lifetime(StatusRemovalProfiles.Protected));
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, 1m)],
            removeAilments: false,
            [RecoveryTemporaryStateKind.Shields, RecoveryTemporaryStateKind.OtherStatuses]);

        RecoveryTransactionResult result = Service(policy).Recover(actor, Ledger(50));

        Assert.Equal(RecoveryTransactionCode.NoRecoveryNeeded, result.Code);
        Assert.Single(actor.Shields);
        Assert.Single(actor.OtherStatuses);
        Assert.Equal(50, result.AfterCurrencyLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_ProtectedAilmentAloneReportsNoRecoveryNeededWithoutDebit()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 100m, 100m));
        AilmentDefinition protectedAilment = Ailment(
            "protected",
            StatusRemovalProfiles.Protected);
        actor.ApplyAilment(protectedAilment, protectedAilment.DefaultLifetime);
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, 1m)],
            removeAilments: true);

        RecoveryTransactionResult result = Service(policy).Recover(actor, Ledger(50));

        Assert.Equal(RecoveryTransactionCode.NoRecoveryNeeded, result.Code);
        Assert.True(actor.HasAilment(protectedAilment.Id));
        Assert.Same(result.BeforeCurrencyLedger, result.AfterCurrencyLedger);
        Assert.Equal(50, result.AfterCurrencyLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_RejectsMissingOrMismatchedStatModifierAuthorityAtomically()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 90m, 100m));
        IStatModifierPolicyService owning = new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(Id("test.pack:owning_modifiers")));
        RuntimeStatModifierStateSnapshot applied = owning.Apply(new StatModifierApplicationRequest(
            new RuntimeStatModifierStateSnapshot(owning.PolicyId),
            Id("attack"),
            1)).After;
        actor.ReplaceStatModifierState(owning, applied);
        RuntimeActorSnapshot before = actor.ToSnapshot();
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, 1m)],
            removeAilments: false,
            [RecoveryTemporaryStateKind.StatModifiers]);
        IStatModifierPolicyService mismatched = new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(Id("test.pack:other_modifiers")));

        RecoveryTransactionResult missing = Service(policy).Recover(actor, Ledger(10));
        RecoveryTransactionResult rejected = Service(policy).Recover(actor, Ledger(10), mismatched);

        Assert.Equal(RecoveryTransactionCode.MissingStatModifierPolicy, missing.Code);
        Assert.Equal(RecoveryTransactionCode.StatModifierCleanupRejected, rejected.Code);
        AssertActorUnchanged(before, actor.ToSnapshot());
        Assert.Equal(10, rejected.AfterCurrencyLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_DoesNotRequireStatModifierAuthorityForAnEmptyModifierState()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 90m, 100m));
        IStatModifierPolicyService owning = new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(Id("test.pack:empty_modifiers")));
        actor.ReplaceStatModifierState(
            owning,
            new RuntimeStatModifierStateSnapshot(owning.PolicyId));
        var policy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Hp, 1m)],
            removeAilments: false,
            [RecoveryTemporaryStateKind.StatModifiers]);

        RecoveryTransactionResult result = Service(policy).Recover(actor, Ledger(10));

        Assert.True(result.Applied);
        Assert.Equal(100m, actor.GetRequiredResource(Hp).Current);
        Assert.Empty(actor.StatModifierState!.Tracks);
        Assert.Equal(0, result.AfterCurrencyLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void Recover_MapsMissingResourcesAndContainsFaultingOrNullPolicies()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 50m, 100m));
        var missingPolicy = new StandardHospitalRecoveryPolicy(
            Credits,
            [Pair(Sp, 1m)],
            removeAilments: false);

        RecoveryTransactionResult missing = Service(missingPolicy).Recover(actor, Ledger(100));
        RecoveryTransactionResult faulted = Service(new ThrowingRecoveryPolicy()).Recover(actor, Ledger(100));
        RecoveryTransactionResult invalid = Service(new NullRecoveryPolicy()).Recover(actor, Ledger(100));

        Assert.Equal(RecoveryTransactionCode.MissingResource, missing.Code);
        Assert.Equal(RecoveryTransactionCode.PolicyFaulted, faulted.Code);
        Assert.Equal(RecoveryTransactionCode.InvalidPolicyResult, invalid.Code);
        Assert.Equal(50m, actor.GetRequiredResource(Hp).Current);
        Assert.All([missing, faulted, invalid], result =>
            Assert.Equal(100, result.AfterCurrencyLedger.GetRequiredBalance(Credits)));
    }

    [Fact]
    public void Recover_ContainsExplicitPolicyRejectionAndPropagatesCancellation()
    {
        RuntimeActorState actor = Actor(new BattleResourceState(Hp, 50m, 100m));
        RuntimeActorSnapshot before = actor.ToSnapshot();

        RecoveryTransactionResult rejected = Service(new RejectingRecoveryPolicy())
            .Recover(actor, Ledger(100));
        RuntimeCurrencyLedgerSnapshot cancellationLedger = Ledger(100);

        Assert.Equal(RecoveryTransactionCode.PolicyRejected, rejected.Code);
        Assert.Equal("recovery blocked", Assert.Single(rejected.Diagnostics).Message);
        AssertActorUnchanged(before, actor.ToSnapshot());
        Assert.Equal(100, rejected.AfterCurrencyLedger.GetRequiredBalance(Credits));
        Assert.Throws<OperationCanceledException>(() =>
            Service(new CancelingRecoveryPolicy()).Recover(actor, cancellationLedger));
        AssertActorUnchanged(before, actor.ToSnapshot());
        Assert.Equal(100, cancellationLedger.GetRequiredBalance(Credits));
    }

    [Fact]
    public void RecoveryFactory_RequiresExplicitTypedConfigurationAndSupportsHostFactories()
    {
        RecoveryPolicyFactoryRegistry standard = RecoveryPolicyFactoryRegistry.CreateStandard();
        IReadOnlyDictionary<string, object?> valid = Parameters();

        RecoveryPolicyBindingResult bound = standard.Bind(StandardRecoveryPolicyIds.Hospital, valid);
        RecoveryPolicyBindingResult malformed = standard.Bind(
            StandardRecoveryPolicyIds.Hospital,
            new Dictionary<string, object?> { ["currencyId"] = Credits.ToString() });
        ContentId customId = Id("custom_recovery");
        var custom = RecoveryPolicyFactoryRegistry.CreateStandard(
            [new FixedRecoveryPolicyFactory(customId, new FixedRecoveryPolicy(Credits))]);

        Assert.IsType<StandardHospitalRecoveryPolicy>(bound.RequirePolicy().Policy);
        Assert.Equal(3, malformed.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RecoveryPolicyFactoryDiagnosticCode.MissingParameter));
        Assert.IsType<FixedRecoveryPolicy>(custom.Bind(customId, new Dictionary<string, object?>())
            .RequirePolicy().Policy);
        Assert.Single(RecoveryPolicyFactoryRegistry.CreateStandard().PolicyIds);
    }

    [Fact]
    public void RecoveryFactory_ContainsMalformedHostResultsAndNormalizedDuplicateResources()
    {
        ContentId malformedId = Id("malformed_recovery");
        RecoveryPolicyFactoryRegistry malformed = RecoveryPolicyFactoryRegistry.CreateStandard(
            [new MalformedRecoveryPolicyFactory(malformedId)]);
        RecoveryPolicyBindingResult malformedResult = malformed.Bind(
            malformedId,
            new Dictionary<string, object?>());
        RecoveryPolicyBindingResult duplicateResources = RecoveryPolicyFactoryRegistry.CreateStandard().Bind(
            StandardRecoveryPolicyIds.Hospital,
            new Dictionary<string, object?>
            {
                ["currencyId"] = Credits.ToString(),
                ["resourceCosts"] = new Dictionary<string, object?>
                {
                    ["HP"] = 1m,
                    ["hp"] = 2m
                },
                ["removeAilments"] = false,
                ["temporaryStateKinds"] = Array.Empty<object?>()
            });

        Assert.Equal(
            RecoveryPolicyFactoryDiagnosticCode.PolicyFactoryFailure,
            Assert.Single(malformedResult.Diagnostics).Code);
        Assert.Contains(duplicateResources.Diagnostics, diagnostic =>
            diagnostic.Code == RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue &&
            diagnostic.ParameterName == "resourceCosts.hp");
        Assert.Null(malformedResult.Policy);
        Assert.Null(duplicateResources.Policy);
    }

    private static RecoveryService Service(IRecoveryPolicy policy) =>
        new(new BoundRecoveryPolicy(Id("test_recovery"), policy));

    private static StandardHospitalRecoveryPolicy StandardPolicy() =>
        new(
            Credits,
            [Pair(Hp, 1m), Pair(Sp, 5m)],
            removeAilments: true,
            Enum.GetValues<RecoveryTemporaryStateKind>());

    private static RuntimeActorState Actor(params BattleResourceState[] resources)
    {
        Assert.NotEmpty(resources);
        return new RuntimeActorState(
            RuntimeInstanceId.Parse("recovery_patient"),
            Id("test.pack:recovery_patient"),
            Team,
            resources[0].Id,
            CombatDefenseProfile.Empty,
            resources,
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeActorAffiliationSnapshot(Id("test_controller"), Team));
    }

    private static AilmentDefinition Ailment(
        string id,
        StatusRemovalProfileDefinition removalProfile)
    {
        StatusLifetimeDefinition lifetime = Lifetime(removalProfile);
        return new AilmentDefinition(
            Id(id),
            id,
            "Recovery test ailment.",
            lifetime,
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());
    }

    private static StatusLifetimeDefinition Lifetime(StatusRemovalProfileDefinition profile) =>
        new(new PermanentDurationDefinition(), profile);

    private static KeyValuePair<ContentId, decimal> Pair(ContentId id, decimal cost) =>
        new(id, cost);

    private static RuntimeCurrencyLedgerSnapshot Ledger(int balance) =>
        RuntimeCurrencyLedgerSnapshot.Single(Credits, balance);

    private static RuntimeResourceSnapshot Resource(RuntimeActorSnapshot actor, ContentId id) =>
        actor.Resources.Single(resource => resource.ResourceId == id);

    private static IReadOnlyDictionary<string, object?> Parameters() =>
        new Dictionary<string, object?>
        {
            ["currencyId"] = Credits.ToString(),
            ["resourceCosts"] = new Dictionary<string, object?>
            {
                [Hp.ToString()] = 1m,
                [Sp.ToString()] = 5m
            },
            ["removeAilments"] = true,
            ["temporaryStateKinds"] = new object?[] { "guard", "charges" }
        };

    private static void AssertActorUnchanged(
        RuntimeActorSnapshot expected,
        RuntimeActorSnapshot actual)
    {
        Assert.Equal(
            expected.Resources.Select(resource => (resource.ResourceId, resource.Current, resource.Maximum)),
            actual.Resources.Select(resource => (resource.ResourceId, resource.Current, resource.Maximum)));
        Assert.Equal(expected.BattleStatus.IsGuarding, actual.BattleStatus.IsGuarding);
        Assert.Equal(expected.BattleStatus.Ailments, actual.BattleStatus.Ailments);
        Assert.Equal(expected.BattleStatus.StatModifiers, actual.BattleStatus.StatModifiers);
        Assert.Equal(expected.BattleStatus.Charges, actual.BattleStatus.Charges);
        Assert.Equal(expected.BattleStatus.Shields, actual.BattleStatus.Shields);
        Assert.Equal(expected.BattleStatus.AffinityOverrides, actual.BattleStatus.AffinityOverrides);
        Assert.Equal(expected.BattleStatus.AffinityBreaks, actual.BattleStatus.AffinityBreaks);
        Assert.Equal(expected.BattleStatus.Statuses, actual.BattleStatus.Statuses);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class ThrowingRecoveryPolicy : IRecoveryPolicy
    {
        public RecoveryPolicyDecision Plan(RecoveryPolicyRequest request) =>
            throw new InvalidOperationException("policy fault");
    }

    private sealed class NullRecoveryPolicy : IRecoveryPolicy
    {
        public RecoveryPolicyDecision Plan(RecoveryPolicyRequest request) => null!;
    }

    private sealed class RejectingRecoveryPolicy : IRecoveryPolicy
    {
        public RecoveryPolicyDecision Plan(RecoveryPolicyRequest request) =>
            RecoveryPolicyDecision.Rejected(
                RecoveryPolicyDiagnosticCode.PolicyRejected,
                "recovery blocked");
    }

    private sealed class CancelingRecoveryPolicy : IRecoveryPolicy
    {
        public RecoveryPolicyDecision Plan(RecoveryPolicyRequest request) =>
            throw new OperationCanceledException("recovery canceled");
    }

    private sealed class FixedRecoveryPolicy(ContentId currencyId) : IRecoveryPolicy
    {
        public RecoveryPolicyDecision Plan(RecoveryPolicyRequest request) =>
            RecoveryPolicyDecision.Planned(new RecoveryTreatmentPlan(
                currencyId,
                0,
                [request.Actor.VitalResourceId],
                removeAilments: false));
    }

    private sealed class FixedRecoveryPolicyFactory(
        ContentId policyId,
        IRecoveryPolicy policy) : IRecoveryPolicyFactory
    {
        public ContentId PolicyId => policyId;

        public RecoveryPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            new(new BoundRecoveryPolicy(policyId, policy));
    }

    private sealed class MalformedRecoveryPolicyFactory(ContentId policyId) : IRecoveryPolicyFactory
    {
        public ContentId PolicyId => policyId;

        public RecoveryPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters) =>
            new(policy: null);
    }
}
