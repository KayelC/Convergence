using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using Xunit;

namespace Convergence.Tests;

public sealed class CombatVocabularyTests
{
    public static IEnumerable<object[]> DamageElementCases()
    {
        yield return [DamageElement.Physical, ElementalAffinity.Weak];
        yield return [DamageElement.Fire, ElementalAffinity.Resist];
        yield return [DamageElement.Ice, ElementalAffinity.Null];
        yield return [DamageElement.Electric, ElementalAffinity.Repel];
        yield return [DamageElement.Wind, ElementalAffinity.Absorb];
        yield return [DamageElement.Light, ElementalAffinity.Weak];
        yield return [DamageElement.Dark, ElementalAffinity.Resist];
        yield return [DamageElement.Almighty, ElementalAffinity.Normal];
    }

    public static IEnumerable<object[]> AffinityCases()
    {
        foreach (ElementalAffinity affinity in Enum.GetValues<ElementalAffinity>())
        {
            yield return [affinity];
        }
    }

    public static IEnumerable<object[]> LegacyElementCases()
    {
        yield return [Element.Slash, true, DamageElement.Physical];
        yield return [Element.Strike, true, DamageElement.Physical];
        yield return [Element.Pierce, true, DamageElement.Physical];
        yield return [Element.Fire, true, DamageElement.Fire];
        yield return [Element.Ice, true, DamageElement.Ice];
        yield return [Element.Elec, true, DamageElement.Electric];
        yield return [Element.Wind, true, DamageElement.Wind];
        yield return [Element.Light, true, DamageElement.Light];
        yield return [Element.Dark, true, DamageElement.Dark];
        yield return [Element.Almighty, true, DamageElement.Almighty];
        yield return [Element.Earth, false, default(DamageElement)];
        yield return [Element.Mind, false, default(DamageElement)];
        yield return [Element.Nerve, false, default(DamageElement)];
        yield return [Element.Curse, false, default(DamageElement)];
        yield return [Element.None, false, default(DamageElement)];
    }

    [Theory]
    [MemberData(nameof(DamageElementCases))]
    public void DefenseProfile_ResolvesAllDamageElements(
        DamageElement element,
        ElementalAffinity expected)
    {
        var profile = new CombatDefenseProfile(
        [
            new(DamageElement.Physical, ElementalAffinity.Weak),
            new(DamageElement.Fire, ElementalAffinity.Resist),
            new(DamageElement.Ice, ElementalAffinity.Null),
            new(DamageElement.Electric, ElementalAffinity.Repel),
            new(DamageElement.Wind, ElementalAffinity.Absorb),
            new(DamageElement.Light, ElementalAffinity.Weak),
            new(DamageElement.Dark, ElementalAffinity.Resist),
            new(DamageElement.Almighty, ElementalAffinity.Absorb)
        ]);

        Assert.Equal(expected, profile.GetElementalAffinity(element));
    }

    [Theory]
    [MemberData(nameof(AffinityCases))]
    public void ElementalAffinityResolver_PreservesEachAffinityOutcome(ElementalAffinity affinity)
    {
        var profile = new CombatDefenseProfile([new(DamageElement.Fire, affinity)]);

        ElementalAffinity result = ElementalAffinityResolver.Resolve(profile, DamageElement.Fire);

        Assert.Equal(affinity, result);
    }

    [Fact]
    public void ElementalAffinityResolver_UsesStrongestPassiveReplacement()
    {
        var profile = new CombatDefenseProfile(
            [new(DamageElement.Ice, ElementalAffinity.Weak)]);

        ElementalAffinity result = ElementalAffinityResolver.Resolve(
            profile,
            DamageElement.Ice,
            [ElementalAffinity.Normal, ElementalAffinity.Null, ElementalAffinity.Absorb]);

        Assert.Equal(ElementalAffinity.Absorb, result);
    }

    [Fact]
    public void ElementalAffinityResolver_ShieldPrecedesBreakAndAffinity()
    {
        var profile = new CombatDefenseProfile(
            [new(DamageElement.Physical, ElementalAffinity.Absorb)]);

        ElementalAffinity result = ElementalAffinityResolver.Resolve(
            profile,
            DamageElement.Physical,
            activeShields: [ShieldKind.Physical],
            isBroken: true);

        Assert.Equal(ElementalAffinity.Repel, result);
    }

    [Fact]
    public void ElementalAffinityResolver_BreakNormalizesWithoutMatchingShield()
    {
        var profile = new CombatDefenseProfile(
            [new(DamageElement.Fire, ElementalAffinity.Absorb)]);

        ElementalAffinity result = ElementalAffinityResolver.Resolve(
            profile,
            DamageElement.Fire,
            activeShields: [ShieldKind.Physical],
            isBroken: true);

        Assert.Equal(ElementalAffinity.Normal, result);
    }

    [Fact]
    public void ElementalAffinityResolver_AlmightyIgnoresEveryOverride()
    {
        var profile = new CombatDefenseProfile(
            [new(DamageElement.Almighty, ElementalAffinity.Absorb)]);

        ElementalAffinity result = ElementalAffinityResolver.Resolve(
            profile,
            DamageElement.Almighty,
            [ElementalAffinity.Absorb],
            [ShieldKind.Magical],
            isBroken: true);

        Assert.Equal(ElementalAffinity.Normal, result);
    }

    [Theory]
    [InlineData(InstantDeathChannel.Light, ResistanceLevel.Vulnerable)]
    [InlineData(InstantDeathChannel.Dark, ResistanceLevel.Immune)]
    public void InstantDeathResistanceResolver_UsesRequestedChannel(
        InstantDeathChannel channel,
        ResistanceLevel expected)
    {
        var profile = new CombatDefenseProfile(
            instantDeathResistances:
            [
                new(InstantDeathChannel.Light, ResistanceLevel.Vulnerable),
                new(InstantDeathChannel.Dark, ResistanceLevel.Immune)
            ]);

        InstantDeathResistanceResolution result = InstantDeathResistanceResolver.Resolve(
            profile,
            new ChannelInstantDeathResistanceCheckDefinition(channel));

        Assert.Equal(InstantDeathResistanceMode.Channel, result.Mode);
        Assert.Equal(channel, result.Channel);
        Assert.Equal(expected, result.Resistance);
        Assert.False(result.BypassesResistance);
    }

    [Fact]
    public void InstantDeathResistanceResolver_MissingChannelDefaultsToNormal()
    {
        InstantDeathResistanceResolution result = InstantDeathResistanceResolver.Resolve(
            CombatDefenseProfile.Empty,
            new ChannelInstantDeathResistanceCheckDefinition(InstantDeathChannel.Light));

        Assert.Equal(ResistanceLevel.Normal, result.Resistance);
    }

    [Fact]
    public void InstantDeathResistanceResolver_EternalRestBypassesChannels()
    {
        var profile = new CombatDefenseProfile(
            instantDeathResistances:
            [new(InstantDeathChannel.Dark, ResistanceLevel.Immune)]);

        InstantDeathResistanceResolution result = InstantDeathResistanceResolver.Resolve(
            profile,
            new NoInstantDeathResistanceCheckDefinition());

        Assert.True(result.BypassesResistance);
        Assert.Equal(InstantDeathResistanceMode.None, result.Mode);
        Assert.Null(result.Channel);
        Assert.Null(result.Resistance);
    }

    [Fact]
    public void AilmentResistanceResolver_UsesAilmentIdOnly()
    {
        ContentId poison = ContentId.Parse("poison");
        var profile = new CombatDefenseProfile(
            ailmentResistances: [new(poison, ResistanceLevel.Resistant)]);

        Assert.Equal(ResistanceLevel.Resistant, AilmentResistanceResolver.Resolve(profile, poison));
        Assert.Equal(
            ResistanceLevel.Normal,
            AilmentResistanceResolver.Resolve(profile, ContentId.Parse("sleep")));
    }

    [Fact]
    public void CombatDefenseProfile_DefensivelyCopiesCollections()
    {
        var elemental = new Dictionary<DamageElement, ElementalAffinity>
        {
            [DamageElement.Fire] = ElementalAffinity.Weak
        };
        var ailments = new Dictionary<ContentId, ResistanceLevel>
        {
            [ContentId.Parse("poison")] = ResistanceLevel.Resistant
        };
        var instantDeath = new Dictionary<InstantDeathChannel, ResistanceLevel>
        {
            [InstantDeathChannel.Light] = ResistanceLevel.Immune
        };
        var profile = new CombatDefenseProfile(elemental, ailments, instantDeath);

        elemental[DamageElement.Fire] = ElementalAffinity.Absorb;
        ailments[ContentId.Parse("poison")] = ResistanceLevel.Vulnerable;
        instantDeath[InstantDeathChannel.Light] = ResistanceLevel.Vulnerable;

        Assert.Equal(ElementalAffinity.Weak, profile.GetElementalAffinity(DamageElement.Fire));
        Assert.Equal(ResistanceLevel.Resistant, profile.GetAilmentResistance(ContentId.Parse("poison")));
        Assert.Equal(ResistanceLevel.Immune, profile.GetInstantDeathResistance(InstantDeathChannel.Light));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<DamageElement, ElementalAffinity>)profile.ElementalAffinities)
                .Add(DamageElement.Ice, ElementalAffinity.Weak));
    }

    [Fact]
    public void Persona_CleanDefenseProfileIsIndependentFromLegacyAffinities()
    {
        var persona = new Persona();
        persona.AffinityMap[Element.Fire] = Affinity.Weak;

        Assert.Equal(Affinity.Weak, persona.GetAffinity(Element.Fire));
        Assert.Equal(
            ElementalAffinity.Normal,
            persona.CombatDefenseProfile.GetElementalAffinity(DamageElement.Fire));
    }

    [Theory]
    [MemberData(nameof(LegacyElementCases))]
    public void LegacyAdapter_MapsOnlySupportedDamageElements(
        Element legacyElement,
        bool expectedSuccess,
        DamageElement expectedElement)
    {
        bool success = LegacyCombatVocabularyAdapter.TryToDamageElement(legacyElement, out DamageElement element);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedElement, element);
    }

    [Theory]
    [MemberData(nameof(AffinityCases))]
    public void LegacyAdapter_RoundTripsAffinityValues(ElementalAffinity affinity)
    {
        Affinity legacy = LegacyCombatVocabularyAdapter.ToLegacyAffinity(affinity);

        Assert.Equal(affinity, LegacyCombatVocabularyAdapter.ToElementalAffinity(legacy));
    }

    [Theory]
    [InlineData("Slash")]
    [InlineData("Pierce")]
    [InlineData("Strike")]
    [InlineData("Wind")]
    [InlineData("Electric")]
    public void Combatant_BasicAttackIsPhysicalWhileWeaponTypeRemainsMetadata(string weaponType)
    {
        var combatant = new Combatant("User")
        {
            EquippedWeapon = new WeaponData
            {
                Id = "weapon",
                Name = "Test Weapon",
                Type = weaponType
            }
        };

        Assert.Equal(DamageElement.Physical, combatant.BasicAttackElement);
        Assert.Equal(weaponType, combatant.EquippedWeapon.Type);
    }

    [Fact]
    public void LegacyPersona_StillKeepsSlashAndPierceAffinitiesSeparate()
    {
        var persona = new Persona();
        persona.AffinityMap[Element.Slash] = Affinity.Weak;
        persona.AffinityMap[Element.Pierce] = Affinity.Null;

        Assert.Equal(Affinity.Weak, persona.GetAffinity(Element.Slash));
        Assert.Equal(Affinity.Null, persona.GetAffinity(Element.Pierce));
    }

    [Fact]
    public void BattleKnowledge_KeepsDefenseChannelsSeparate()
    {
        ContentId entity = ContentId.Parse("sample_demon");
        ContentId fireAilment = ContentId.Parse("fire");
        var knowledge = new BattleKnowledge();

        knowledge.LearnElementalAffinity(entity, DamageElement.Fire, ElementalAffinity.Weak);
        knowledge.LearnAilmentResistance(entity, fireAilment, ResistanceLevel.Immune);
        knowledge.LearnInstantDeathResistance(entity, InstantDeathChannel.Light, ResistanceLevel.Resistant);

        Assert.True(knowledge.ElementalAffinities.TryGet(entity, DamageElement.Fire, out ElementalAffinity affinity));
        Assert.True(knowledge.AilmentResistances.TryGet(entity, fireAilment, out ResistanceLevel ailment));
        Assert.True(knowledge.InstantDeathResistances.TryGet(
            entity,
            InstantDeathChannel.Light,
            out ResistanceLevel instantDeath));
        Assert.Equal(ElementalAffinity.Weak, affinity);
        Assert.Equal(ResistanceLevel.Immune, ailment);
        Assert.Equal(ResistanceLevel.Resistant, instantDeath);
    }

    [Fact]
    public void ElementalKnowledge_IgnoresAlmightyAndReturnsImmutableSnapshots()
    {
        ContentId entity = ContentId.Parse("sample_demon");
        var knowledge = new ElementalAffinityKnowledge();
        knowledge.Learn(entity, DamageElement.Fire, ElementalAffinity.Weak);
        IReadOnlyDictionary<ElementalAffinityKnowledgeKey, ElementalAffinity> snapshot = knowledge.Snapshot();

        knowledge.Learn(entity, DamageElement.Fire, ElementalAffinity.Absorb);
        knowledge.Learn(entity, DamageElement.Almighty, ElementalAffinity.Weak);

        Assert.Equal(
            ElementalAffinity.Weak,
            snapshot[new ElementalAffinityKnowledgeKey(entity, DamageElement.Fire)]);
        Assert.False(knowledge.HasDiscovery(entity, DamageElement.Almighty));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ElementalAffinityKnowledgeKey, ElementalAffinity>)snapshot)
                .Add(new(entity, DamageElement.Ice), ElementalAffinity.Weak));
    }
}
