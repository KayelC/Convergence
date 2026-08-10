using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Knowledge;

public sealed class BattleKnowledgeExecutionTransitionTests
{
    private static readonly ContentId Action = ContentId.Parse("test_action");
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly RuntimeInstanceId Observer = RuntimeInstanceId.Parse("observer");
    private static readonly RuntimeInstanceId Target = RuntimeInstanceId.Parse("target");
    private static readonly ContentId TargetEntity = ContentId.Parse("target_entity");
    private static readonly RuntimeCombatProfileIdentitySnapshot TargetProfile = new(Target, TargetEntity);

    [Fact]
    public void ActorAndSaveContractsExposeNoEncounterAnalysisAuthority()
    {
        Assert.Null(typeof(RuntimeActorState).GetMethod("Reveal"));
        Assert.Null(typeof(RuntimeActorState).GetMethod("GetAnalysis"));
        Assert.Null(typeof(RuntimeBattleStatusSnapshot).GetProperty("Analysis"));
        Assert.Null(typeof(RuntimeBattleStatusSnapshot).Assembly.GetType(
            "Convergence.Runtime.RuntimeAnalysisSnapshot"));
        Assert.Equal(17, RuntimeSaveGameSnapshot.CurrentContractVersion);
    }

    [Fact]
    public void AppliesObservationAndAnalysisThroughOneAtomicTransition()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleAnalysisResult analysis = AnalyzeStats();
        EffectExecutionResult[] effects =
        [
            EffectWithObservation(0, Elemental(TargetProfile, 0)),
            new EffectExecutionResult(
                1,
                Target,
                EffectExecutionOutcome.Success)
            {
                Analysis = analysis
            }
        ];

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            effects,
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.True(result.Applied);
        Assert.Same(persistent, result.PersistentBefore);
        Assert.Same(encounter, result.EncounterBefore);
        Assert.Equal(ElementalAffinity.Weak, Assert.Single(result.PersistentAfter.ElementalAffinities).Affinity);
        Assert.Equal(ElementalAffinity.Weak, Assert.Single(result.EncounterAfter.Elemental).Affinity);
        Assert.Equal(
            [
                BattleAnalysisField.CurrentHp,
                BattleAnalysisField.CurrentSp,
                BattleAnalysisField.CoreStats
            ],
            Assert.Single(result.EncounterAfter.Analysis).DisclosedFields);
        Assert.Same(effects[0].KnowledgeObservations[0], Assert.Single(result.AcceptedObservations));
        Assert.Same(analysis, Assert.Single(result.ProcessedAnalyses).Analysis);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void EncounterOnlyScopeNeverPromotesObservedOrAnalyzedDefenseFacts()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        BattleAnalysisResult analysis = AnalyzeAffinities();
        EffectExecutionResult[] effects =
        [
            EffectWithObservation(0, Elemental(TargetProfile, 0)),
            new EffectExecutionResult(1, Target, EffectExecutionOutcome.Success)
            {
                Analysis = analysis
            }
        ];

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            RuntimeEncounterKnowledgeSnapshot.Empty,
            effects,
            BattleKnowledgePersistenceScope.EncounterOnly);

        Assert.True(result.Applied);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.NotEmpty(result.EncounterAfter.Elemental);
        Assert.Contains(
            BattleAnalysisField.ElementalAffinities,
            Assert.Single(result.EncounterAfter.Analysis).DisclosedFields);
    }

    [Fact]
    public void LaterTargetProfileMismatchRejectsTheWholeExecutionBatch()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        EffectExecutionResult[] effects =
        [
            EffectWithObservation(0, Elemental(TargetProfile, 0)),
            EffectWithObservation(1, Elemental(
                new RuntimeCombatProfileIdentitySnapshot(Target, TargetEntity, revision: 1),
                1))
        ];

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            effects,
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.Same(encounter, result.EncounterAfter);
        Assert.Empty(result.AcceptedObservations);
        Assert.Empty(result.ProcessedAnalyses);
        BattleKnowledgeExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(BattleKnowledgeExecutionDiagnosticCode.ObservationTargetProfileMismatch, diagnostic.Code);
        Assert.Equal(1, diagnostic.EffectIndex);
    }

    [Fact]
    public void CurrentExecutionAuthorityInvalidatesStaleTargetProfileBeforeSelectionStateContinues()
    {
        var staleProfile = new RuntimeCombatProfileIdentitySnapshot(Target, TargetEntity, revision: 3);
        var encounter = new RuntimeEncounterKnowledgeSnapshot(
            [new EncounterElementalKnowledgeEntry(
                Target,
                staleProfile,
                DamageElement.Ice,
                ElementalAffinity.Weak)],
            analysis:
            [
                new EncounterAnalysisKnowledgeEntry(
                    Target,
                    staleProfile,
                    [BattleAnalysisField.Skills])
            ]);

        BattleKnowledgeExecutionTransitionResult result = Apply(
            new RuntimeKnowledgeSnapshot(),
            encounter,
            effects: [],
            BattleKnowledgePersistenceScope.EncounterOnly);

        Assert.True(result.Applied);
        Assert.True(result.EncounterAfter.IsEmpty);
        Assert.Same(encounter, result.EncounterBefore);
    }

    [Fact]
    public void ResultCollectionsAreImmutableSnapshots()
    {
        var source = new List<EffectExecutionResult>
        {
            EffectWithObservation(0, Elemental(TargetProfile, 0))
        };
        var request = new BattleKnowledgeExecutionTransitionRequest(
            new RuntimeKnowledgeSnapshot(),
            RuntimeEncounterKnowledgeSnapshot.Empty,
            Authority(),
            source,
            BattleKnowledgePersistenceScope.EncounterOnly);
        source.Clear();

        BattleKnowledgeExecutionTransitionResult result =
            new BattleKnowledgeExecutionTransitionService().Apply(request);

        Assert.Single(request.Effects);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleKnowledgeObservation>)result.AcceptedObservations).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<BattleKnowledgeExecutionDiagnostic>)result.Diagnostics).Add(
                new BattleKnowledgeExecutionDiagnostic(
                    BattleKnowledgeExecutionDiagnosticCode.AnalysisRejected,
                    0,
                    "rejected")));
    }

    [Fact]
    public void ExecutionAuthorityDefensivelyCopiesAndProtectsTargetProfileMappings()
    {
        var targetProfiles = new List<KeyValuePair<RuntimeInstanceId, RuntimeCombatProfileIdentitySnapshot>>
        {
            KeyValuePair.Create(Target, TargetProfile)
        };

        var authority = new BattleKnowledgeExecutionAuthority(
            Action,
            Observer,
            targetProfiles);
        targetProfiles.Clear();

        Assert.Equal(TargetProfile, authority.TargetProfiles[Target]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<RuntimeInstanceId, RuntimeCombatProfileIdentitySnapshot>)authority.TargetProfiles).Add(
                RuntimeInstanceId.Parse("other_target"),
                new RuntimeCombatProfileIdentitySnapshot(
                    RuntimeInstanceId.Parse("other_source"),
                    ContentId.Parse("other_entity"))));
    }

    [Fact]
    public void ExecutionAuthorityRejectsInvalidOrDuplicateIdentityMappings()
    {
        Assert.Throws<ArgumentException>(() =>
            new BattleKnowledgeExecutionAuthority(default, Observer, []));
        Assert.Throws<ArgumentException>(() =>
            new BattleKnowledgeExecutionAuthority(Action, default, []));
        Assert.Throws<ArgumentException>(() =>
            new BattleKnowledgeExecutionAuthority(
                Action,
                Observer,
                [KeyValuePair.Create(default(RuntimeInstanceId), TargetProfile)]));
        Assert.Throws<ArgumentException>(() =>
            new BattleKnowledgeExecutionAuthority(
                Action,
                Observer,
                [KeyValuePair.Create(Target, (RuntimeCombatProfileIdentitySnapshot)null!)]));
        Assert.Throws<ArgumentException>(() =>
            new BattleKnowledgeExecutionAuthority(
                Action,
                Observer,
                [
                    KeyValuePair.Create(Target, TargetProfile),
                    KeyValuePair.Create(
                        Target,
                        new RuntimeCombatProfileIdentitySnapshot(
                            RuntimeInstanceId.Parse("other_source"),
                            ContentId.Parse("other_entity")))
                ]));
    }

    [Fact]
    public void RejectsObservationWhoseEffectIndexDoesNotMatchItsExecutionResult()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        EffectExecutionResult effect = EffectWithObservation(
            0,
            Elemental(TargetProfile, effectIndex: 1));

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [effect],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.Same(encounter, result.EncounterAfter);
        BattleKnowledgeExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            BattleKnowledgeExecutionDiagnosticCode.ObservationEffectIndexMismatch,
            diagnostic.Code);
        Assert.Equal(0, diagnostic.EffectIndex);
        Assert.Contains("does not match", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsObservationWhoseTargetDoesNotMatchItsExecutionResult()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleKnowledgeObservation mismatched = BattleKnowledgeObservation.Elemental(
            Action,
            Observer,
            RuntimeInstanceId.Parse("different_target"),
            TargetProfile,
            effectIndex: 0,
            DamageElement.Ice,
            contacted: true,
            ElementalAffinity.Weak,
            ElementalAffinity.Weak);

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [EffectWithObservation(0, mismatched)],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.Same(encounter, result.EncounterAfter);
        BattleKnowledgeExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(BattleKnowledgeExecutionDiagnosticCode.ObservationTargetMismatch, diagnostic.Code);
        Assert.Equal(0, diagnostic.EffectIndex);
    }

    [Fact]
    public void RejectsObservationWhoseSourceActionDoesNotMatchExecutionAuthority()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleKnowledgeObservation mismatched = Elemental(
            TargetProfile,
            effectIndex: 0,
            sourceActionId: ContentId.Parse("different_action"));

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [EffectWithObservation(0, mismatched)],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        AssertRejectedWithoutMutation(
            result,
            persistent,
            encounter,
            BattleKnowledgeExecutionDiagnosticCode.ObservationSourceActionMismatch);
    }

    [Fact]
    public void RejectsObservationWhoseActorDoesNotMatchExecutionAuthority()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleKnowledgeObservation mismatched = Elemental(
            TargetProfile,
            effectIndex: 0,
            actorId: RuntimeInstanceId.Parse("different_observer"));

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [EffectWithObservation(0, mismatched)],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        AssertRejectedWithoutMutation(
            result,
            persistent,
            encounter,
            BattleKnowledgeExecutionDiagnosticCode.ObservationActorMismatch);
    }

    [Fact]
    public void RejectsObservationWhenExecutionAuthorityDoesNotBindItsTargetProfile()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        var authority = new BattleKnowledgeExecutionAuthority(Action, Observer, []);

        BattleKnowledgeExecutionTransitionResult result =
            new BattleKnowledgeExecutionTransitionService().Apply(
                new BattleKnowledgeExecutionTransitionRequest(
                    persistent,
                    encounter,
                    authority,
                    [EffectWithObservation(0, Elemental(TargetProfile, 0))],
                    BattleKnowledgePersistenceScope.EncounterAndPersistent));

        BattleKnowledgeExecutionDiagnostic diagnostic = AssertRejectedWithoutMutation(
            result,
            persistent,
            encounter,
            BattleKnowledgeExecutionDiagnosticCode.ObservationTargetProfileMismatch);
        Assert.Contains("<unbound>", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnalysisWhoseTargetDoesNotMatchItsExecutionResult()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleAnalysisResult analysis = AnalyzeStats();
        var effect = new EffectExecutionResult(
            0,
            RuntimeInstanceId.Parse("different_target"),
            EffectExecutionOutcome.Success)
        {
            Analysis = analysis
        };

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [effect],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.Same(encounter, result.EncounterAfter);
        BattleKnowledgeExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(BattleKnowledgeExecutionDiagnosticCode.AnalysisTargetMismatch, diagnostic.Code);
        Assert.Equal(0, diagnostic.EffectIndex);
    }

    [Fact]
    public void RejectsAnalysisWhoseActorDoesNotMatchExecutionAuthority()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleAnalysisResult analysis = new BattleAnalysisService().Analyze(
            new BattleAnalysisRequest(
                Actor("different_observer"),
                Actor("target"),
                [AnalysisLayer.Stats],
                Sp));
        var effect = new EffectExecutionResult(0, Target, EffectExecutionOutcome.Success)
        {
            Analysis = analysis
        };

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [effect],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        AssertRejectedWithoutMutation(
            result,
            persistent,
            encounter,
            BattleKnowledgeExecutionDiagnosticCode.AnalysisActorMismatch);
    }

    [Fact]
    public void RejectsAnalysisWhoseProfileDoesNotMatchExecutionAuthority()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleAnalysisResult analysis = new BattleAnalysisService().Analyze(
            new BattleAnalysisRequest(
                Actor("observer"),
                Actor(
                    "target",
                    entityId: ContentId.Parse("different_target_entity")),
                [AnalysisLayer.Stats],
                Sp));
        var effect = new EffectExecutionResult(0, Target, EffectExecutionOutcome.Success)
        {
            Analysis = analysis
        };

        BattleKnowledgeExecutionTransitionResult result = Apply(
            persistent,
            encounter,
            [effect],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        AssertRejectedWithoutMutation(
            result,
            persistent,
            encounter,
            BattleKnowledgeExecutionDiagnosticCode.AnalysisTargetProfileMismatch);
    }

    [Fact]
    public void ProvenancePreflightRejectsTheWholeBatchBeforeAnyLowerTransitionRuns()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        var observations = new RecordingObservationService();
        var service = new BattleKnowledgeExecutionTransitionService(observations);
        EffectExecutionResult[] effects =
        [
            EffectWithObservation(0, Elemental(TargetProfile, 0)),
            EffectWithObservation(
                1,
                Elemental(
                    TargetProfile,
                    effectIndex: 1,
                    sourceActionId: ContentId.Parse("different_action")))
        ];

        BattleKnowledgeExecutionTransitionResult result = service.Apply(
            new BattleKnowledgeExecutionTransitionRequest(
                persistent,
                encounter,
                Authority(),
                effects,
                BattleKnowledgePersistenceScope.EncounterAndPersistent));

        AssertRejectedWithoutMutation(
            result,
            persistent,
            encounter,
            BattleKnowledgeExecutionDiagnosticCode.ObservationSourceActionMismatch);
        Assert.Equal(0, observations.ApplyCalls);
    }

    [Fact]
    public void RejectedDependencyWithoutDiagnosticsProducesTypedFallbackDiagnostic()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        var service = new BattleKnowledgeExecutionTransitionService(
            new DiagnosticFreeRejectingObservationService());

        BattleKnowledgeExecutionTransitionResult result = service.Apply(
            new BattleKnowledgeExecutionTransitionRequest(
                persistent,
                encounter,
                Authority(),
                [EffectWithObservation(0, Elemental(TargetProfile, 0))],
                BattleKnowledgePersistenceScope.EncounterOnly));

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.Same(encounter, result.EncounterAfter);
        BattleKnowledgeExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(BattleKnowledgeExecutionDiagnosticCode.ObservationRejected, diagnostic.Code);
        Assert.Contains("without diagnostics", diagnostic.Message, StringComparison.Ordinal);
    }

    private static BattleKnowledgeExecutionTransitionResult Apply(
        RuntimeKnowledgeSnapshot persistent,
        RuntimeEncounterKnowledgeSnapshot encounter,
        IEnumerable<EffectExecutionResult> effects,
        BattleKnowledgePersistenceScope scope) =>
        new BattleKnowledgeExecutionTransitionService().Apply(
            new BattleKnowledgeExecutionTransitionRequest(
                persistent,
                encounter,
                Authority(),
                effects,
                scope));

    private static BattleKnowledgeExecutionAuthority Authority() =>
        new(
            Action,
            Observer,
            [KeyValuePair.Create(Target, TargetProfile)]);

    private static EffectExecutionResult EffectWithObservation(
        int effectIndex,
        BattleKnowledgeObservation observation) =>
        new(effectIndex, Target, EffectExecutionOutcome.Success)
        {
            KnowledgeObservations = [observation]
        };

    private static BattleKnowledgeObservation Elemental(
        RuntimeCombatProfileIdentitySnapshot targetProfile,
        int effectIndex,
        ContentId? sourceActionId = null,
        RuntimeInstanceId? actorId = null) =>
        BattleKnowledgeObservation.Elemental(
            sourceActionId ?? Action,
            actorId ?? Observer,
            Target,
            targetProfile,
            effectIndex,
            DamageElement.Ice,
            contacted: true,
            ElementalAffinity.Weak,
            ElementalAffinity.Weak);

    private static BattleAnalysisResult AnalyzeStats() =>
        new BattleAnalysisService().Analyze(new BattleAnalysisRequest(
            Actor("observer"),
            Actor("target"),
            [AnalysisLayer.Stats],
            Sp));

    private static BattleAnalysisResult AnalyzeAffinities() =>
        new BattleAnalysisService().Analyze(new BattleAnalysisRequest(
            Actor("observer"),
            Actor("target", new CombatDefenseProfile(
                [KeyValuePair.Create(DamageElement.Ice, ElementalAffinity.Weak)])),
            [AnalysisLayer.Affinities],
            Sp));

    private static RuntimeActorState Actor(
        string id,
        CombatDefenseProfile? defense = null,
        ContentId? entityId = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            entityId ?? ContentId.Parse($"{id}_entity"),
            ContentId.Parse("test_team"),
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, 100m, 100m), new BattleResourceState(Sp, 50m, 50m)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(
                ContentId.Parse("test_authority"),
                ContentId.Parse("test_team")));

    private static BattleKnowledgeExecutionDiagnostic AssertRejectedWithoutMutation(
        BattleKnowledgeExecutionTransitionResult result,
        RuntimeKnowledgeSnapshot persistent,
        RuntimeEncounterKnowledgeSnapshot encounter,
        BattleKnowledgeExecutionDiagnosticCode expectedCode)
    {
        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(persistent, result.PersistentAfter);
        Assert.Same(encounter, result.EncounterAfter);
        Assert.Empty(result.AcceptedObservations);
        Assert.Empty(result.ProcessedAnalyses);
        BattleKnowledgeExecutionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        return diagnostic;
    }

    private sealed class RecordingObservationService
        : IBattleKnowledgeObservationTransitionService
    {
        public int ApplyCalls { get; private set; }

        public BattleKnowledgeObservationTransitionResult Apply(
            BattleKnowledgeObservationTransitionRequest request)
        {
            ApplyCalls++;
            return new BattleKnowledgeObservationTransitionService().Apply(request);
        }

        public BattleKnowledgeEncounterCleanupResult ClearEncounter(
            RuntimeEncounterKnowledgeSnapshot before) =>
            new BattleKnowledgeObservationTransitionService().ClearEncounter(before);
    }

    private sealed class DiagnosticFreeRejectingObservationService
        : IBattleKnowledgeObservationTransitionService
    {
        public BattleKnowledgeObservationTransitionResult Apply(
            BattleKnowledgeObservationTransitionRequest request) =>
            new(
                BattleKnowledgeTransitionStatus.Rejected,
                request.PersistentBefore,
                request.PersistentBefore,
                request.EncounterBefore,
                request.EncounterBefore);

        public BattleKnowledgeEncounterCleanupResult ClearEncounter(
            RuntimeEncounterKnowledgeSnapshot before) =>
            new(
                before.IsEmpty
                    ? BattleKnowledgeTransitionStatus.Unchanged
                    : BattleKnowledgeTransitionStatus.Applied,
                before,
                RuntimeEncounterKnowledgeSnapshot.Empty);
    }
}
