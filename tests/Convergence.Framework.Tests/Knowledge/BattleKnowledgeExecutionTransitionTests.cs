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

    [Fact]
    public void AppliesObservationAndAnalysisThroughOneAtomicTransition()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        BattleAnalysisResult analysis = AnalyzeStats();
        EffectExecutionResult[] effects =
        [
            EffectWithObservation(0, Elemental(TargetEntity, 0)),
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
            EffectWithObservation(0, Elemental(TargetEntity, 0)),
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
    public void LaterTargetEntityMismatchRejectsTheWholeExecutionBatch()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        EffectExecutionResult[] effects =
        [
            EffectWithObservation(0, Elemental(TargetEntity, 0)),
            EffectWithObservation(1, Elemental(ContentId.Parse("different_entity"), 1))
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
        Assert.Equal(BattleKnowledgeExecutionDiagnosticCode.ObservationTargetEntityMismatch, diagnostic.Code);
        Assert.Equal(1, diagnostic.EffectIndex);
    }

    [Fact]
    public void ResultCollectionsAreImmutableSnapshots()
    {
        var source = new List<EffectExecutionResult>
        {
            EffectWithObservation(0, Elemental(TargetEntity, 0))
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
    public void RejectsObservationWhoseEffectIndexDoesNotMatchItsExecutionResult()
    {
        RuntimeKnowledgeSnapshot persistent = new();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;
        EffectExecutionResult effect = EffectWithObservation(
            0,
            Elemental(TargetEntity, effectIndex: 1));

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
            TargetEntity,
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
                [EffectWithObservation(0, Elemental(TargetEntity, 0))],
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
            [KeyValuePair.Create(Target, TargetEntity)]);

    private static EffectExecutionResult EffectWithObservation(
        int effectIndex,
        BattleKnowledgeObservation observation) =>
        new(effectIndex, Target, EffectExecutionOutcome.Success)
        {
            KnowledgeObservations = [observation]
        };

    private static BattleKnowledgeObservation Elemental(ContentId entityId, int effectIndex) =>
        BattleKnowledgeObservation.Elemental(
            Action,
            Observer,
            Target,
            entityId,
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

    private static RuntimeActorState Actor(string id, CombatDefenseProfile? defense = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            ContentId.Parse("test_team"),
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, 100m, 100m), new BattleResourceState(Sp, 50m, 50m)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(
                ContentId.Parse("test_authority"),
                ContentId.Parse("test_team")));

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
