using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Knowledge;

public sealed class BattleAnalysisTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Poison = ContentId.Parse("poison");
    private static readonly ContentId Strength = ContentId.Parse("strength");

    [Fact]
    public void StandardPolicyDisclosesTypedFullAnalysisData()
    {
        RuntimeActorState actor = Actor("observer");
        RuntimeActorState target = Actor(
            "target",
            defense: new CombatDefenseProfile(
                [KeyValuePair.Create(DamageElement.Ice, ElementalAffinity.Weak)],
                [KeyValuePair.Create(Poison, ResistanceLevel.Immune)],
                [KeyValuePair.Create(InstantDeathChannel.Dark, ResistanceLevel.Resistant)]),
            skillIds: [ContentId.Parse("target_skill")],
            stats: [KeyValuePair.Create(Strength, 12m)]);
        target.SetResource(Hp, 64m);
        target.SetResource(Sp, 17m);

        BattleAnalysisResult result = new BattleAnalysisService().Analyze(
            new BattleAnalysisRequest(actor, target, [AnalysisLayer.Full], Sp));

        Assert.Equal(Enum.GetValues<BattleAnalysisField>(), result.Disclosures.Select(value => value.Field));
        Assert.All(result.Disclosures, value =>
            Assert.Equal(BattleAnalysisDisclosureStatus.Disclosed, value.Status));
        Assert.Equal(64m, result.Data.CurrentHp);
        Assert.Equal(17m, result.Data.CurrentSp);
        Assert.Equal(12m, result.Data.CoreStats[Strength]);
        Assert.Equal(ContentId.Parse("target_skill"), Assert.Single(result.Data.SkillIds));
        Assert.Equal(ElementalAffinity.Weak, result.Data.ElementalAffinities[DamageElement.Ice]);
        Assert.Equal(ElementalAffinity.Normal, result.Data.ElementalAffinities[DamageElement.Fire]);
        Assert.Equal(ResistanceLevel.Immune, result.Data.AilmentResistances[Poison]);
        Assert.Equal(
            ResistanceLevel.Resistant,
            result.Data.InstantDeathResistances[InstantDeathChannel.Dark]);
    }

    [Fact]
    public void RestrictedPolicyReturnsTypedUnknownFieldsWithoutLeakingData()
    {
        RuntimeActorState actor = Actor("observer");
        RuntimeActorState target = Actor(
            "restricted",
            defense: new CombatDefenseProfile(
                [KeyValuePair.Create(DamageElement.Ice, ElementalAffinity.Weak)],
                [KeyValuePair.Create(Poison, ResistanceLevel.Immune)]),
            skillIds: [ContentId.Parse("secret_skill")],
            stats: [KeyValuePair.Create(Strength, 99m)]);
        var service = new BattleAnalysisService(new RestrictedBattleAnalysisDisclosurePolicy());

        BattleAnalysisResult result = service.Analyze(
            new BattleAnalysisRequest(actor, target, [AnalysisLayer.Full], Sp));

        Assert.All(result.Disclosures, value =>
            Assert.Equal(BattleAnalysisDisclosureStatus.Unknown, value.Status));
        Assert.Empty(result.DisclosedFields);
        Assert.Null(result.Data.CurrentHp);
        Assert.Null(result.Data.CurrentSp);
        Assert.Empty(result.Data.CoreStats);
        Assert.Empty(result.Data.SkillIds);
        Assert.Empty(result.Data.ElementalAffinities);
        Assert.Empty(result.Data.AilmentResistances);
        Assert.Empty(result.Data.InstantDeathResistances);
    }

    [Fact]
    public void RestrictedPolicyCanHideOnlySelectedFields()
    {
        var service = new BattleAnalysisService(
            new RestrictedBattleAnalysisDisclosurePolicy(
                [BattleAnalysisField.ElementalAffinities]));

        BattleAnalysisResult result = service.Analyze(new BattleAnalysisRequest(
            Actor("observer"),
            Actor("target", stats: [KeyValuePair.Create(Strength, 8m)]),
            [AnalysisLayer.Stats, AnalysisLayer.Affinities],
            Sp));

        Assert.Equal(
            BattleAnalysisDisclosureStatus.Unknown,
            Assert.Single(result.Disclosures, value =>
                value.Field == BattleAnalysisField.ElementalAffinities).Status);
        Assert.Equal(8m, result.Data.CoreStats[Strength]);
        Assert.Empty(result.Data.ElementalAffinities);
    }

    [Fact]
    public void MissingSpIsUnavailableRatherThanUnknown()
    {
        RuntimeActorState target = Actor("target", includeSp: false);

        BattleAnalysisResult result = new BattleAnalysisService().Analyze(
            new BattleAnalysisRequest(Actor("observer"), target, [AnalysisLayer.Stats], Sp));

        Assert.Equal(
            BattleAnalysisDisclosureStatus.Unavailable,
            Assert.Single(result.Disclosures, value => value.Field == BattleAnalysisField.CurrentSp).Status);
        Assert.Null(result.Data.CurrentSp);
        Assert.Equal(BattleAnalysisDisclosureStatus.Disclosed,
            Assert.Single(result.Disclosures, value => value.Field == BattleAnalysisField.CurrentHp).Status);
    }

    [Fact]
    public void AnalyzeTransitionPersistsExactDefensesAndEncounterOnlyFields()
    {
        RuntimeActorState target = Actor(
            "target",
            defense: new CombatDefenseProfile(
                [KeyValuePair.Create(DamageElement.Ice, ElementalAffinity.Weak)],
                [KeyValuePair.Create(Poison, ResistanceLevel.Immune)],
                [KeyValuePair.Create(InstantDeathChannel.Light, ResistanceLevel.Resistant)]));
        BattleAnalysisResult analysis = new BattleAnalysisService().Analyze(new BattleAnalysisRequest(
            Actor("observer"),
            target,
            [AnalysisLayer.Full],
            Sp));
        var service = new BattleAnalysisKnowledgeTransitionService();

        BattleAnalysisKnowledgeTransitionResult transition = service.Apply(
            new RuntimeKnowledgeSnapshot(),
            RuntimeEncounterKnowledgeSnapshot.Empty,
            analysis);

        Assert.True(transition.Applied);
        var view = new BattleKnowledgeView(transition.PersistentAfter, transition.EncounterAfter);
        Assert.True(view.TryGetElementalAffinity(
            target.InstanceId,
            target.EntityId,
            DamageElement.Ice,
            out ElementalAffinity ice,
            out BattleKnowledgeFactSource iceSource,
            out _));
        Assert.Equal(ElementalAffinity.Weak, ice);
        Assert.Equal(BattleKnowledgeFactSource.Encounter, iceSource);
        Assert.Contains(
            transition.PersistentAfter.ElementalAffinities,
            entry => entry.EntityId == target.EntityId &&
                     entry.Element == DamageElement.Ice &&
                     entry.Affinity == ElementalAffinity.Weak);
        Assert.True(view.TryGetElementalAffinity(
            target.InstanceId,
            target.EntityId,
            DamageElement.Fire,
            out ElementalAffinity fire,
            out _,
            out _));
        Assert.Equal(ElementalAffinity.Normal, fire);
        Assert.True(view.TryGetAilmentResistance(
            target.InstanceId,
            target.EntityId,
            ContentId.Parse("unlisted_ailment"),
            out ResistanceLevel unlisted,
            out _,
            out _));
        Assert.Equal(ResistanceLevel.Normal, unlisted);
        Assert.True(view.IsAnalysisDisclosed(
            target.InstanceId,
            target.EntityId,
            BattleAnalysisField.CurrentHp));
        Assert.DoesNotContain(
            transition.PersistentAfter.AnalyzedDefenses.SelectMany(entry => entry.DisclosedFields),
            field => field == BattleAnalysisField.CurrentHp);
    }

    [Fact]
    public void UnknownAnalysisFieldsMutateNeitherKnowledgeScope()
    {
        BattleAnalysisResult analysis = new BattleAnalysisService(
            new RestrictedBattleAnalysisDisclosurePolicy()).Analyze(new BattleAnalysisRequest(
                Actor("observer"),
                Actor("target"),
                [AnalysisLayer.Full],
                Sp));
        var persistent = new RuntimeKnowledgeSnapshot();
        RuntimeEncounterKnowledgeSnapshot encounter = RuntimeEncounterKnowledgeSnapshot.Empty;

        BattleAnalysisKnowledgeTransitionResult transition =
            new BattleAnalysisKnowledgeTransitionService().Apply(persistent, encounter, analysis);

        Assert.Equal(BattleKnowledgeTransitionStatus.Unchanged, transition.Status);
        Assert.Same(persistent, transition.PersistentAfter);
        Assert.Same(encounter, transition.EncounterAfter);
    }

    [Fact]
    public void AnalyzeIdentityConflictWithObservedKnowledgeRejectsAtomically()
    {
        BattleAnalysisResult analysis = new BattleAnalysisService().Analyze(new BattleAnalysisRequest(
            Actor("observer"),
            Actor("target"),
            [AnalysisLayer.Affinities],
            Sp));
        var encounter = new RuntimeEncounterKnowledgeSnapshot(
            [new EncounterElementalKnowledgeEntry(
                analysis.TargetId,
                ContentId.Parse("different_entity"),
                DamageElement.Fire,
                ElementalAffinity.Weak)]);
        var persistent = new RuntimeKnowledgeSnapshot();

        BattleAnalysisKnowledgeTransitionResult transition =
            new BattleAnalysisKnowledgeTransitionService().Apply(persistent, encounter, analysis);

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, transition.Status);
        Assert.Same(persistent, transition.PersistentAfter);
        Assert.Same(encounter, transition.EncounterAfter);
        Assert.Equal(
            BattleAnalysisKnowledgeDiagnosticCode.TargetIdentityConflict,
            Assert.Single(transition.Diagnostics).Code);
    }

    [Fact]
    public void RepeatedAnalyzeUnionsDisclosedFieldsWithoutDuplicates()
    {
        RuntimeActorState actor = Actor("observer");
        RuntimeActorState target = Actor("target");
        var analyzer = new BattleAnalysisService();
        var transitions = new BattleAnalysisKnowledgeTransitionService();
        BattleAnalysisResult stats = analyzer.Analyze(
            new BattleAnalysisRequest(actor, target, [AnalysisLayer.Stats], Sp));
        BattleAnalysisKnowledgeTransitionResult first = transitions.Apply(
            new RuntimeKnowledgeSnapshot(),
            RuntimeEncounterKnowledgeSnapshot.Empty,
            stats);
        BattleAnalysisResult skills = analyzer.Analyze(
            new BattleAnalysisRequest(actor, target, [AnalysisLayer.Skills], Sp));

        BattleAnalysisKnowledgeTransitionResult second = transitions.Apply(
            first.PersistentAfter,
            first.EncounterAfter,
            skills);

        EncounterAnalysisKnowledgeEntry entry = Assert.Single(second.EncounterAfter.Analysis);
        Assert.Equal(
            [
                BattleAnalysisField.CurrentHp,
                BattleAnalysisField.CurrentSp,
                BattleAnalysisField.CoreStats,
                BattleAnalysisField.Skills
            ],
            entry.DisclosedFields);
    }

    [Fact]
    public void MalformedPolicyCannotOmitRequestedFields()
    {
        var service = new BattleAnalysisService(new IncompletePolicy());

        Assert.Throws<InvalidOperationException>(() => service.Analyze(
            new BattleAnalysisRequest(Actor("observer"), Actor("target"), [AnalysisLayer.Stats], Sp)));
    }

    private static RuntimeActorState Actor(
        string id,
        CombatDefenseProfile? defense = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? stats = null,
        bool includeSp = true)
    {
        List<BattleResourceState> resources = [new(Hp, 100m, 100m)];
        if (includeSp)
        {
            resources.Add(new BattleResourceState(Sp, 50m, 50m));
        }

        return new RuntimeActorState(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            ContentId.Parse("test_team"),
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            resources,
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_authority"), ContentId.Parse("test_team")),
            stats,
            skillIds: skillIds);
    }

    private sealed class IncompletePolicy : IBattleAnalysisDisclosurePolicy
    {
        public IReadOnlyList<BattleAnalysisFieldDisclosure> Resolve(
            BattleAnalysisDisclosurePolicyRequest request) => [];
    }
}
