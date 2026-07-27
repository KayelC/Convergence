using Convergence.Content;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class PersistentBattleKnowledgeTests
{
    private static readonly ContentId EntityId = ContentId.Parse("knowledge.tests:watcher");
    private static readonly ContentId AilmentId = ContentId.Parse("knowledge.tests:poison");

    [Fact]
    public void MutableStores_RejectInvalidIdentifiersAtTheLearningBoundary()
    {
        Assert.Throws<ArgumentException>(() =>
            new ElementalAffinityKnowledge().Learn(default, DamageElement.Fire, ElementalAffinity.Weak));
        Assert.Throws<ArgumentException>(() =>
            new AilmentResistanceKnowledge().Learn(default, AilmentId, ResistanceLevel.Normal));
        Assert.Throws<ArgumentException>(() =>
            new AilmentResistanceKnowledge().Learn(EntityId, default, ResistanceLevel.Normal));
        Assert.Throws<ArgumentException>(() =>
            new InstantDeathResistanceKnowledge().Learn(
                default,
                InstantDeathChannel.Light,
                ResistanceLevel.Normal));
    }

    [Fact]
    public void Apply_AtomicallyAddsAndReplacesAllTypedKnowledgeDomains()
    {
        var before = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Fire,
                    ElementalAffinity.Normal)
            ]);
        var discoveries = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Fire,
                    ElementalAffinity.Resist)
            ],
            ailmentResistances:
            [
                new RuntimeAilmentResistanceKnowledgeSnapshot(
                    EntityId,
                    AilmentId,
                    ResistanceLevel.Immune)
            ],
            instantDeathResistances:
            [
                new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    EntityId,
                    InstantDeathChannel.Dark,
                    ResistanceLevel.Resistant)
            ]);

        BattleKnowledgeTransitionResult result = new PersistentBattleKnowledgeTransitionService().Apply(
            new BattleKnowledgeTransitionRequest(before, discoveries));

        Assert.Equal(BattleKnowledgeTransitionStatus.Applied, result.Status);
        Assert.Same(before, result.Before);
        Assert.NotSame(before, result.After);
        Assert.Empty(result.Diagnostics);
        Assert.Single(result.AppliedDiscoveries.ElementalAffinities);
        Assert.Single(result.AppliedDiscoveries.AilmentResistances);
        Assert.Single(result.AppliedDiscoveries.InstantDeathResistances);

        var view = new PersistentBattleKnowledgeView(result.After);
        Assert.True(view.TryGetElementalAffinity(EntityId, DamageElement.Fire, out ElementalAffinity affinity));
        Assert.Equal(ElementalAffinity.Resist, affinity);
        Assert.True(view.TryGetAilmentResistance(EntityId, AilmentId, out ResistanceLevel ailment));
        Assert.Equal(ResistanceLevel.Immune, ailment);
        Assert.True(view.TryGetInstantDeathResistance(
            EntityId,
            InstantDeathChannel.Dark,
            out ResistanceLevel instantDeath));
        Assert.Equal(ResistanceLevel.Resistant, instantDeath);
    }

    [Fact]
    public void Apply_RejectsMalformedOrDuplicateInputWithoutChangingBefore()
    {
        var before = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Ice,
                    ElementalAffinity.Weak)
            ]);
        var malformed = new RuntimeKnowledgeSnapshot(
            ailmentResistances:
            [
                new RuntimeAilmentResistanceKnowledgeSnapshot(default, default, ResistanceLevel.Normal),
                new RuntimeAilmentResistanceKnowledgeSnapshot(default, default, ResistanceLevel.Resistant)
            ]);

        BattleKnowledgeTransitionResult result = new PersistentBattleKnowledgeTransitionService().Apply(
            new BattleKnowledgeTransitionRequest(before, malformed));

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(before, result.Before);
        Assert.Same(before, result.After);
        Assert.Empty(result.AppliedDiscoveries.ElementalAffinities);
        Assert.Contains(result.Diagnostics, issue =>
            issue.Code == BattleKnowledgeTransitionDiagnosticCode.InvalidEntityId);
        Assert.Contains(result.Diagnostics, issue =>
            issue.Code == BattleKnowledgeTransitionDiagnosticCode.InvalidAilmentId);
        Assert.Contains(result.Diagnostics, issue =>
            issue.Code == BattleKnowledgeTransitionDiagnosticCode.DuplicateDiscoveryEntry);
    }

    [Fact]
    public void Apply_UnchangedResultPreservesOriginalSnapshotAndIgnoresAlmighty()
    {
        var before = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Fire,
                    ElementalAffinity.Weak)
            ]);
        var discoveries = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Fire,
                    ElementalAffinity.Weak),
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Almighty,
                    ElementalAffinity.Normal)
            ]);

        BattleKnowledgeTransitionResult result = new PersistentBattleKnowledgeTransitionService().Apply(
            new BattleKnowledgeTransitionRequest(before, discoveries));

        Assert.Equal(BattleKnowledgeTransitionStatus.Unchanged, result.Status);
        Assert.Same(before, result.After);
        Assert.Empty(result.AppliedDiscoveries.ElementalAffinities);
    }

    [Fact]
    public void ResultCollectionsAndViewsAreImmutableSnapshots()
    {
        var discoveries = new List<RuntimeElementalAffinityKnowledgeSnapshot>
        {
            new(EntityId, DamageElement.Ice, ElementalAffinity.Weak)
        };
        var discoverySnapshot = new RuntimeKnowledgeSnapshot(elementalAffinities: discoveries);
        BattleKnowledgeTransitionResult result = new PersistentBattleKnowledgeTransitionService().Apply(
            new BattleKnowledgeTransitionRequest(new RuntimeKnowledgeSnapshot(), discoverySnapshot));

        discoveries.Clear();
        Assert.Single(result.After.ElementalAffinities);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeElementalAffinityKnowledgeSnapshot>)result.After.ElementalAffinities).Add(
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Fire,
                    ElementalAffinity.Resist)));

        var mutableDiagnostics = Assert.IsAssignableFrom<IList<BattleKnowledgeTransitionDiagnostic>>(
            result.Diagnostics);
        Assert.Throws<NotSupportedException>(() => mutableDiagnostics.Add(
            new BattleKnowledgeTransitionDiagnostic(
                BattleKnowledgeTransitionDiagnosticCode.InvalidEntityId,
                "invalid",
                "$.x")));
    }

    [Fact]
    public void PublicTransitionResults_RejectUndefinedEnumsAndMalformedDiagnostics()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleKnowledgeTransitionResult(
            (BattleKnowledgeTransitionStatus)999,
            new RuntimeKnowledgeSnapshot(),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeKnowledgeSnapshot()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleKnowledgeTransitionDiagnostic(
            (BattleKnowledgeTransitionDiagnosticCode)999,
            "invalid",
            "$.x"));
        Assert.Throws<ArgumentException>(() => new BattleKnowledgeTransitionDiagnostic(
            BattleKnowledgeTransitionDiagnosticCode.InvalidEntityId,
            " ",
            "$.x"));
    }
}
