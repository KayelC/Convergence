using System.Reflection;
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
    public void PublicSurface_ExposesNoDisconnectedMutableKnowledgeStores()
    {
        System.Reflection.Assembly assembly = typeof(RuntimeKnowledgeSnapshot).Assembly;

        Assert.Null(assembly.GetType("Convergence.Knowledge.ElementalAffinityKnowledge"));
        Assert.Null(assembly.GetType("Convergence.Knowledge.AilmentResistanceKnowledge"));
        Assert.Null(assembly.GetType("Convergence.Knowledge.InstantDeathResistanceKnowledge"));
    }

    [Fact]
    public void PersistentView_DoesNotTreatInvalidQueryKeysAsKnownNormalProfiles()
    {
        var snapshot = new RuntimeKnowledgeSnapshot(
            elementalAffinities: null,
            ailmentResistances: null,
            instantDeathResistances: null,
            analyzedDefenses:
            [
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    EntityId,
                    [
                        BattleAnalysisField.ElementalAffinities,
                        BattleAnalysisField.AilmentResistances,
                        BattleAnalysisField.InstantDeathResistances
                    ])
            ]);
        var view = new PersistentBattleKnowledgeView(snapshot);

        Assert.Throws<ArgumentException>(() =>
            view.TryGetElementalAffinity(default, DamageElement.Fire, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            view.TryGetElementalAffinity(EntityId, (DamageElement)999, out _));
        Assert.Throws<ArgumentException>(() =>
            view.TryGetAilmentResistance(EntityId, default, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            view.TryGetInstantDeathResistance(EntityId, (InstantDeathChannel)999, out _));
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
    public void TransitionAndView_RejectEveryMalformedPersistentKnowledgeEnumAtTheirBoundary()
    {
        var before = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Ice,
                    ElementalAffinity.Weak)
            ]);
        var validElemental = new RuntimeElementalAffinityKnowledgeSnapshot(
            EntityId,
            DamageElement.Fire,
            ElementalAffinity.Normal);
        var validAilment = new RuntimeAilmentResistanceKnowledgeSnapshot(
            EntityId,
            AilmentId,
            ResistanceLevel.Normal);
        var validInstantDeath = new RuntimeInstantDeathResistanceKnowledgeSnapshot(
            EntityId,
            InstantDeathChannel.Light,
            ResistanceLevel.Normal);
        var validAnalyzedDefense = new RuntimeAnalyzedDefenseKnowledgeSnapshot(
            EntityId,
            [BattleAnalysisField.ElementalAffinities]);
        RuntimeAnalyzedDefenseKnowledgeSnapshot undefinedAnalyzedDefense = CloneWithProperty(
            validAnalyzedDefense,
            nameof(RuntimeAnalyzedDefenseKnowledgeSnapshot.DisclosedFields),
            (IReadOnlyList<BattleAnalysisField>)Array.AsReadOnly(
                [Undefined<BattleAnalysisField>()]));
        RuntimeAnalyzedDefenseKnowledgeSnapshot nonDefenseAnalysis = CloneWithProperty(
            validAnalyzedDefense,
            nameof(RuntimeAnalyzedDefenseKnowledgeSnapshot.DisclosedFields),
            (IReadOnlyList<BattleAnalysisField>)Array.AsReadOnly(
                [BattleAnalysisField.CurrentHp]));
        var cases = new[]
        {
            new MalformedKnowledgeCase(
                new RuntimeKnowledgeSnapshot(elementalAffinities:
                    [validElemental with { Element = Undefined<DamageElement>() }]),
                BattleKnowledgeTransitionDiagnosticCode.UndefinedEnumValue,
                "elementalAffinities[0].element"),
            new MalformedKnowledgeCase(
                new RuntimeKnowledgeSnapshot(elementalAffinities:
                    [validElemental with { Affinity = Undefined<ElementalAffinity>() }]),
                BattleKnowledgeTransitionDiagnosticCode.UndefinedEnumValue,
                "elementalAffinities[0].affinity"),
            new MalformedKnowledgeCase(
                new RuntimeKnowledgeSnapshot(ailmentResistances:
                    [validAilment with { Resistance = Undefined<ResistanceLevel>() }]),
                BattleKnowledgeTransitionDiagnosticCode.UndefinedEnumValue,
                "ailmentResistances[0].resistance"),
            new MalformedKnowledgeCase(
                new RuntimeKnowledgeSnapshot(instantDeathResistances:
                    [validInstantDeath with { Channel = Undefined<InstantDeathChannel>() }]),
                BattleKnowledgeTransitionDiagnosticCode.UndefinedEnumValue,
                "instantDeathResistances[0].channel"),
            new MalformedKnowledgeCase(
                new RuntimeKnowledgeSnapshot(instantDeathResistances:
                    [validInstantDeath with { Resistance = Undefined<ResistanceLevel>() }]),
                BattleKnowledgeTransitionDiagnosticCode.UndefinedEnumValue,
                "instantDeathResistances[0].resistance"),
            new MalformedKnowledgeCase(
                KnowledgeWithAnalyzedDefense(undefinedAnalyzedDefense),
                BattleKnowledgeTransitionDiagnosticCode.UndefinedEnumValue,
                "analyzedDefenses[0].disclosedFields[0]"),
            new MalformedKnowledgeCase(
                KnowledgeWithAnalyzedDefense(nonDefenseAnalysis),
                BattleKnowledgeTransitionDiagnosticCode.InvalidAnalyzedDefenseField,
                "analyzedDefenses[0].disclosedFields[0]")
        };
        var service = new PersistentBattleKnowledgeTransitionService();

        foreach (MalformedKnowledgeCase testCase in cases)
        {
            BattleKnowledgeTransitionResult result = service.Apply(
                new BattleKnowledgeTransitionRequest(before, testCase.Snapshot));

            Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
            Assert.Same(before, result.Before);
            Assert.Same(before, result.After);
            Assert.Empty(result.AppliedDiscoveries.ElementalAffinities);
            Assert.Contains(result.Diagnostics, issue =>
                issue.Code == testCase.Code &&
                issue.Path == "$.discoveries." + testCase.RelativePath);

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new PersistentBattleKnowledgeView(testCase.Snapshot));
            Assert.Contains("$.before." + testCase.RelativePath, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PersistentKnowledge_RejectsStoredAlmightyEvenWhenConstructionWasBypassed()
    {
        var before = new RuntimeKnowledgeSnapshot(
            elementalAffinities:
            [
                new RuntimeElementalAffinityKnowledgeSnapshot(
                    EntityId,
                    DamageElement.Fire,
                    ElementalAffinity.Weak)
            ]);
        var valid = new RuntimeElementalAffinityKnowledgeSnapshot(
            EntityId,
            DamageElement.Ice,
            ElementalAffinity.Weak);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeElementalAffinityKnowledgeSnapshot(
                EntityId,
                DamageElement.Almighty,
                ElementalAffinity.Normal));
        RuntimeElementalAffinityKnowledgeSnapshot malformed =
            valid with { Element = DamageElement.Almighty };
        var discoveries = new RuntimeKnowledgeSnapshot(elementalAffinities: [malformed]);

        BattleKnowledgeTransitionResult result = new PersistentBattleKnowledgeTransitionService().Apply(
            new BattleKnowledgeTransitionRequest(before, discoveries));

        Assert.Equal(BattleKnowledgeTransitionStatus.Rejected, result.Status);
        Assert.Same(before, result.After);
        BattleKnowledgeTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            BattleKnowledgeTransitionDiagnosticCode.IntrinsicElementKnowledgeNotStorable,
            diagnostic.Code);
        Assert.Equal("$.discoveries.elementalAffinities[0].element", diagnostic.Path);
        Assert.Throws<ArgumentException>(() => new PersistentBattleKnowledgeView(discoveries));
    }

    [Fact]
    public void PersistentView_ReportsAnalyzedAlmightyAsIntrinsicNormalWithoutStoringIt()
    {
        var snapshot = new RuntimeKnowledgeSnapshot(
            elementalAffinities: null,
            ailmentResistances: null,
            instantDeathResistances: null,
            analyzedDefenses:
            [
                new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    EntityId,
                    [BattleAnalysisField.ElementalAffinities])
            ]);
        var view = new PersistentBattleKnowledgeView(snapshot);

        Assert.True(view.TryGetElementalAffinity(
            EntityId,
            DamageElement.Almighty,
            out ElementalAffinity affinity));
        Assert.Equal(ElementalAffinity.Normal, affinity);
        Assert.Empty(snapshot.ElementalAffinities);
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

    private static RuntimeKnowledgeSnapshot KnowledgeWithAnalyzedDefense(
        RuntimeAnalyzedDefenseKnowledgeSnapshot analyzedDefense) =>
        new(
            elementalAffinities: null,
            ailmentResistances: null,
            instantDeathResistances: null,
            analyzedDefenses: [analyzedDefense]);

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

    private static TEnum Undefined<TEnum>()
        where TEnum : struct, Enum =>
        (TEnum)Enum.ToObject(typeof(TEnum), 999);

    private sealed record MalformedKnowledgeCase(
        RuntimeKnowledgeSnapshot Snapshot,
        BattleKnowledgeTransitionDiagnosticCode Code,
        string RelativePath);
}
