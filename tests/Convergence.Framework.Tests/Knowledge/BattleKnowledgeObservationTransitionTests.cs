using Convergence.Content;
using Convergence.Execution;
using Convergence.Knowledge;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Knowledge;

public sealed class BattleKnowledgeObservationTransitionTests
{
    private static readonly ContentId Action = ContentId.Parse("test_action");
    private static readonly RuntimeInstanceId Actor = RuntimeInstanceId.Parse("observer");
    private static readonly RuntimeInstanceId Target = RuntimeInstanceId.Parse("target");
    private static readonly ContentId Entity = ContentId.Parse("target_entity");
    private static readonly ContentId Poison = ContentId.Parse("poison");

    [Fact]
    public void ContactWithoutTemporaryDefenseUpdatesEncounterAndPersistentKnowledge()
    {
        var before = EmptyPersistent();
        BattleKnowledgeObservation observation = Elemental(
            true,
            ElementalAffinity.Weak,
            ElementalAffinity.Weak);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [observation],
            BattleKnowledgePersistenceScope.EncounterAndPersistent,
            before);

        Assert.True(result.Applied);
        Assert.Same(before, result.PersistentBefore);
        Assert.Equal(ElementalAffinity.Weak, Assert.Single(result.PersistentAfter.ElementalAffinities).Affinity);
        EncounterElementalKnowledgeEntry encounter = Assert.Single(result.EncounterAfter.Elemental);
        Assert.Equal(Target, encounter.TargetInstanceId);
        Assert.Equal(Entity, encounter.TargetEntityId);
        Assert.Equal(ElementalAffinity.Weak, encounter.Affinity);
        Assert.Equal(BattleDefenseInfluence.None, encounter.TemporaryInfluences);
        Assert.Same(observation, Assert.Single(result.AcceptedObservations));
    }

    [Fact]
    public void MissDoesNotRevealAnyDefenseFact()
    {
        BattleKnowledgeObservationTransitionResult result = Apply(
            [Elemental(false, ElementalAffinity.Weak, ElementalAffinity.Weak)],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(BattleKnowledgeTransitionStatus.Unchanged, result.Status);
        Assert.True(result.PersistentAfter.ElementalAffinities.Count == 0);
        Assert.True(result.EncounterAfter.IsEmpty);
        Assert.Empty(result.AcceptedObservations);
    }

    [Fact]
    public void TemporaryAffinityObservationUpdatesEncounterOnlyAndOverridesPersistentLookup()
    {
        var persistent = new RuntimeKnowledgeSnapshot(
            [new RuntimeElementalAffinityKnowledgeSnapshot(Entity, DamageElement.Fire, ElementalAffinity.Weak)]);
        BattleKnowledgeObservation observation = Elemental(
            true,
            ElementalAffinity.Weak,
            ElementalAffinity.Repel,
            BattleDefenseInfluence.Shield);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [observation],
            BattleKnowledgePersistenceScope.EncounterAndPersistent,
            persistent);

        Assert.Equal(ElementalAffinity.Weak, Assert.Single(result.PersistentAfter.ElementalAffinities).Affinity);
        EncounterElementalKnowledgeEntry encounter = Assert.Single(result.EncounterAfter.Elemental);
        Assert.Equal(ElementalAffinity.Repel, encounter.Affinity);
        Assert.Equal(BattleDefenseInfluence.Shield, encounter.TemporaryInfluences);
        var view = new BattleKnowledgeView(result.PersistentAfter, result.EncounterAfter);
        Assert.True(view.TryGetElementalAffinity(
            Target,
            Entity,
            DamageElement.Fire,
            out ElementalAffinity affinity,
            out BattleKnowledgeFactSource source,
            out BattleDefenseInfluence influences));
        Assert.Equal(ElementalAffinity.Repel, affinity);
        Assert.Equal(BattleKnowledgeFactSource.Encounter, source);
        Assert.Equal(BattleDefenseInfluence.Shield, influences);
        Assert.Throws<InvalidOperationException>(() => view.TryGetElementalAffinity(
            Target,
            ContentId.Parse("wrong_entity"),
            DamageElement.Fire,
            out _,
            out _,
            out _));
    }

    [Fact]
    public void AilmentSuccessAndRandomMissRevealNoTierWhileImmunityDoes()
    {
        BattleKnowledgeObservation applied = BattleKnowledgeObservation.Ailment(
            Action, Actor, Target, Entity, 0, Poison,
            BattleAilmentApplicationStatus.Applied,
            ResistanceLevel.Vulnerable,
            ResistanceLevel.Vulnerable);
        BattleKnowledgeObservation missed = BattleKnowledgeObservation.Ailment(
            Action, Actor, Target, Entity, 1, Poison,
            BattleAilmentApplicationStatus.Missed,
            ResistanceLevel.Resistant,
            ResistanceLevel.Resistant);
        BattleKnowledgeObservation immune = BattleKnowledgeObservation.Ailment(
            Action, Actor, Target, Entity, 2, Poison,
            BattleAilmentApplicationStatus.Immune,
            ResistanceLevel.Immune,
            ResistanceLevel.Immune);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [applied, missed, immune],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(ResistanceLevel.Immune, Assert.Single(result.EncounterAfter.Ailments).Resistance);
        Assert.Equal(ResistanceLevel.Immune, Assert.Single(result.PersistentAfter.AilmentResistances).Resistance);
        Assert.Equal([immune], result.AcceptedObservations);
    }

    [Fact]
    public void TemporaryAilmentImmunityCannotOverwritePersistentSpeciesKnowledge()
    {
        var persistent = new RuntimeKnowledgeSnapshot(
            ailmentResistances:
            [
                new RuntimeAilmentResistanceKnowledgeSnapshot(
                    Entity,
                    Poison,
                    ResistanceLevel.Normal)
            ]);
        BattleKnowledgeObservation observation = BattleKnowledgeObservation.Ailment(
            Action, Actor, Target, Entity, 0, Poison,
            BattleAilmentApplicationStatus.Immune,
            ResistanceLevel.Normal,
            ResistanceLevel.Immune,
            BattleDefenseInfluence.PassiveModifier);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [observation],
            BattleKnowledgePersistenceScope.EncounterAndPersistent,
            persistent);

        Assert.Equal(ResistanceLevel.Normal, Assert.Single(result.PersistentAfter.AilmentResistances).Resistance);
        EncounterAilmentKnowledgeEntry encounter = Assert.Single(result.EncounterAfter.Ailments);
        Assert.Equal(ResistanceLevel.Immune, encounter.Resistance);
        Assert.Equal(BattleDefenseInfluence.PassiveModifier, encounter.TemporaryInfluences);
    }

    [Fact]
    public void InstantDeathLearnsOnlyExplicitCheckedImmunity()
    {
        BattleKnowledgeObservation bypassed = BattleKnowledgeObservation.InstantDeath(
            Action, Actor, Target, Entity, 0,
            channel: null,
            resistanceBypassed: true,
            defeated: true,
            authoredResistance: null,
            effectiveResistance: null);
        BattleKnowledgeObservation failedRoll = BattleKnowledgeObservation.InstantDeath(
            Action, Actor, Target, Entity, 1,
            InstantDeathChannel.Light,
            resistanceBypassed: false,
            defeated: false,
            authoredResistance: ResistanceLevel.Resistant,
            effectiveResistance: ResistanceLevel.Resistant);
        BattleKnowledgeObservation immune = BattleKnowledgeObservation.InstantDeath(
            Action, Actor, Target, Entity, 2,
            InstantDeathChannel.Dark,
            resistanceBypassed: false,
            defeated: false,
            authoredResistance: ResistanceLevel.Immune,
            effectiveResistance: ResistanceLevel.Immune,
            resistanceBlockConfirmed: true);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [bypassed, failedRoll, immune],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(InstantDeathChannel.Dark, Assert.Single(result.EncounterAfter.InstantDeath).Channel);
        Assert.Equal(InstantDeathChannel.Dark, Assert.Single(result.PersistentAfter.InstantDeathResistances).Channel);
        Assert.Equal([immune], result.AcceptedObservations);
    }

    [Fact]
    public void InstantDeathEvidence_RequiresACompleteCheckedOrBypassedResistanceTuple()
    {
        var partialCheckedTuples = new[]
        {
            new InstantDeathTuple(InstantDeathChannel.Light, null, null),
            new InstantDeathTuple(null, ResistanceLevel.Normal, null),
            new InstantDeathTuple(null, null, ResistanceLevel.Normal),
            new InstantDeathTuple(InstantDeathChannel.Light, ResistanceLevel.Normal, null),
            new InstantDeathTuple(InstantDeathChannel.Light, null, ResistanceLevel.Normal),
            new InstantDeathTuple(null, ResistanceLevel.Normal, ResistanceLevel.Normal)
        };

        foreach (InstantDeathTuple tuple in partialCheckedTuples)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                BattleKnowledgeObservation.InstantDeath(
                    Action,
                    Actor,
                    Target,
                    Entity,
                    0,
                    tuple.Channel,
                    resistanceBypassed: false,
                    defeated: false,
                    tuple.AuthoredResistance,
                    tuple.EffectiveResistance));
            Assert.Equal("resistanceBypassed", exception.ParamName);
        }

        BattleKnowledgeObservation bypassed = BattleKnowledgeObservation.InstantDeath(
            Action,
            Actor,
            Target,
            Entity,
            0,
            channel: null,
            resistanceBypassed: true,
            defeated: true,
            authoredResistance: null,
            effectiveResistance: null);
        BattleKnowledgeObservation checkedResistance = BattleKnowledgeObservation.InstantDeath(
            Action,
            Actor,
            Target,
            Entity,
            1,
            InstantDeathChannel.Dark,
            resistanceBypassed: false,
            defeated: false,
            ResistanceLevel.Resistant,
            ResistanceLevel.Resistant);

        Assert.True(bypassed.ResistanceBypassed);
        Assert.False(checkedResistance.ResistanceBypassed);
        Assert.Equal(InstantDeathChannel.Dark, checkedResistance.InstantDeathChannel);
    }

    [Fact]
    public void RepeatedFactsAreDeduplicatedWithLastObservationWinning()
    {
        BattleKnowledgeObservation first = Elemental(true, ElementalAffinity.Weak, ElementalAffinity.Weak);
        BattleKnowledgeObservation second = Elemental(true, ElementalAffinity.Resist, ElementalAffinity.Resist);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [first, second],
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

        Assert.Equal(ElementalAffinity.Resist, Assert.Single(result.EncounterAfter.Elemental).Affinity);
        Assert.Equal(ElementalAffinity.Resist, Assert.Single(result.PersistentAfter.ElementalAffinities).Affinity);
        Assert.Equal([first, second], result.AcceptedObservations);
    }

    [Fact]
    public void RuntimeIdentityConflictRejectsAtomically()
    {
        var before = new RuntimeEncounterKnowledgeSnapshot(
            [new EncounterElementalKnowledgeEntry(Target, Entity, DamageElement.Fire, ElementalAffinity.Weak)]);
        BattleKnowledgeObservation conflicting = BattleKnowledgeObservation.Elemental(
            Action,
            Actor,
            Target,
            ContentId.Parse("different_entity"),
            0,
            DamageElement.Ice,
            true,
            ElementalAffinity.Normal,
            ElementalAffinity.Normal);

        BattleKnowledgeObservationTransitionResult result = Apply(
            [conflicting],
            BattleKnowledgePersistenceScope.EncounterAndPersistent,
            encounter: before);

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(result.PersistentBefore, result.PersistentAfter);
        Assert.Same(before, result.EncounterAfter);
        Assert.Equal(
            BattleKnowledgeObservationDiagnosticCode.TargetIdentityConflict,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void BattleEndCleanupDiscardsEncounterKnowledgeOnly()
    {
        var before = new RuntimeEncounterKnowledgeSnapshot(
            [new EncounterElementalKnowledgeEntry(Target, Entity, DamageElement.Fire, ElementalAffinity.Weak)],
            analysis:
            [
                new EncounterAnalysisKnowledgeEntry(
                    Target,
                    Entity,
                    [BattleAnalysisField.CurrentHp, BattleAnalysisField.Skills])
            ]);
        var service = new BattleKnowledgeObservationTransitionService();

        var view = new EncounterBattleKnowledgeView(before);
        Assert.True(view.IsAnalysisDisclosed(Target, Entity, BattleAnalysisField.CurrentHp));
        Assert.False(view.IsAnalysisDisclosed(Target, Entity, BattleAnalysisField.CurrentSp));

        BattleKnowledgeEncounterCleanupResult result = service.ClearEncounter(before);

        Assert.Equal(BattleKnowledgeTransitionStatus.Applied, result.Status);
        Assert.Same(before, result.Before);
        Assert.Same(RuntimeEncounterKnowledgeSnapshot.Empty, result.After);
        Assert.True(service.ClearEncounter(result.After).After.IsEmpty);
    }

    [Fact]
    public void EncounterSnapshotIsImmutableAndRejectsDuplicateOrConflictingTargets()
    {
        var source = new List<EncounterElementalKnowledgeEntry>
        {
            new(Target, Entity, DamageElement.Fire, ElementalAffinity.Weak)
        };
        var snapshot = new RuntimeEncounterKnowledgeSnapshot(source);
        source.Clear();

        Assert.Single(snapshot.Elemental);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EncounterElementalKnowledgeEntry>)snapshot.Elemental).Clear());
        Assert.Throws<ArgumentException>(() => new RuntimeEncounterKnowledgeSnapshot(
        [
            new(Target, Entity, DamageElement.Fire, ElementalAffinity.Weak),
            new(Target, Entity, DamageElement.Fire, ElementalAffinity.Resist)
        ]));
        Assert.Throws<ArgumentException>(() => new RuntimeEncounterKnowledgeSnapshot(
            [new(Target, Entity, DamageElement.Fire, ElementalAffinity.Weak)],
            [new(Target, ContentId.Parse("other_entity"), Poison, ResistanceLevel.Immune)]));
        Assert.Throws<ArgumentException>(() => new EncounterAnalysisKnowledgeEntry(
            Target,
            Entity,
            [BattleAnalysisField.Skills, BattleAnalysisField.Skills]));
    }

    private static BattleKnowledgeObservation Elemental(
        bool contacted,
        ElementalAffinity authored,
        ElementalAffinity effective,
        BattleDefenseInfluence influences = BattleDefenseInfluence.None) =>
        BattleKnowledgeObservation.Elemental(
            Action,
            Actor,
            Target,
            Entity,
            0,
            DamageElement.Fire,
            contacted,
            authored,
            effective,
            influences);

    private sealed record InstantDeathTuple(
        InstantDeathChannel? Channel,
        ResistanceLevel? AuthoredResistance,
        ResistanceLevel? EffectiveResistance);

    private static BattleKnowledgeObservationTransitionResult Apply(
        IEnumerable<BattleKnowledgeObservation> observations,
        BattleKnowledgePersistenceScope scope,
        RuntimeKnowledgeSnapshot? persistent = null,
        RuntimeEncounterKnowledgeSnapshot? encounter = null)
    {
        RuntimeKnowledgeSnapshot before = persistent ?? EmptyPersistent();
        return new BattleKnowledgeObservationTransitionService().Apply(
            new BattleKnowledgeObservationTransitionRequest(
                before,
                encounter ?? RuntimeEncounterKnowledgeSnapshot.Empty,
                observations,
                scope));
    }

    private static RuntimeKnowledgeSnapshot EmptyPersistent() => new();
}
