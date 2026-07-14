using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Effects;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Messaging;
using Xunit;

namespace Convergence.Tests;

public sealed class BattleEffectTests
{
    [Theory]
    [InlineData(40, 60)]
    [InlineData(9999, 100)]
    public void HealEffect_RestoresHpUpToMaximum(int power, int expectedHp)
    {
        var target = CreateCombatant("Target");
        target.CurrentHP = 20;
        var effect = new HealEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            power,
            "Medicine",
            "Restores HP.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(expectedHp, target.CurrentHP);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void HealEffect_ParsesFlatHealingValueFromEffectTextWhenPowerIsZero()
    {
        var target = CreateCombatant("Target");
        target.CurrentHP = 20;
        var effect = new HealEffect();

        effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Dia",
            "Restores HP to one ally. (30)",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(50, target.CurrentHP);
    }

    [Fact]
    public void HealEffect_ReturnsNormalFallbackForEmptyTargets()
    {
        var effect = new HealEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant>(),
            50,
            "Dia",
            "Restores HP.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void SpiritEffect_RestoresSpUpToMaximum()
    {
        var target = CreateCombatant("Target");
        target.CurrentSP = 10;
        var effect = new SpiritEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            9999,
            "Soul Food",
            "Fully restores SP.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(50, target.CurrentSP);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(100, 100)]
    public void ReviveEffect_RestoresDeadTargetHp(int power, int expectedHp)
    {
        var target = CreateCombatant("Target");
        target.CurrentHP = 0;
        var effect = new ReviveEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            power,
            "Recarm",
            power >= 100 ? "Fully revives one ally." : "Revives one ally.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(expectedHp, target.CurrentHP);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void ReviveEffect_ReturnsMissWhenNoTargetsAreDead()
    {
        var target = CreateCombatant("Target");
        var effect = new ReviveEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            40,
            "Recarm",
            "Revives one ally.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(HitType.Miss, Assert.Single(results).Type);
    }

    [Fact]
    public void BuffEffect_AppliesStatChangeAndReturnsNormal()
    {
        var target = CreateCombatant("Target");
        var effect = new BuffEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Tarukaja",
            "Raises physical attack.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(1, target.Buffs["PhysAtk"]);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void BuffEffect_ReturnsNormalFallbackForEmptyTargets()
    {
        var effect = new BuffEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant>(),
            0,
            "Tarukaja",
            "Raises physical attack.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void CureEffect_RemovesMatchingAilment()
    {
        var target = CreateCombatant("Target");
        target.InflictAilment(CreateAilment("Poison"));
        var effect = new CureEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Dis-Poison",
            "Cures Poison.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Null(target.CurrentAilment);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void CureEffect_ReturnsNormalFallbackWhenCureDoesNotMatch()
    {
        var target = CreateCombatant("Target");
        target.InflictAilment(CreateAilment("Panic"));
        var effect = new CureEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Dis-Poison",
            "Cures Poison.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal("Panic", target.CurrentAilment?.Name);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void ChargeEffect_SetsPowerCharge()
    {
        var target = CreateCombatant("Target");
        var effect = new ChargeEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Power Charge",
            "Charges physical power.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.True(target.IsCharged);
        Assert.False(target.IsMindCharged);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void ChargeEffect_SetsMindCharge()
    {
        var target = CreateCombatant("Target");
        var effect = new ChargeEffect();

        effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Mind Charge",
            "Charges magical power.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.False(target.IsCharged);
        Assert.True(target.IsMindCharged);
    }

    [Fact]
    public void BreakEffect_AppliesElementBreakForThreeTurns()
    {
        var target = CreateCombatant("Target");
        var effect = new BreakEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Fire Break",
            "Removes Fire resistance.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(3, target.BrokenAffinities[Element.Fire]);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void BreakEffect_ReturnsNormalWhenElementCannotBeDetermined()
    {
        var target = CreateCombatant("Target");
        var effect = new BreakEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Mystery Break",
            "Unknown break.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Empty(target.BrokenAffinities);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Theory]
    [InlineData("Tetrakarn", true, false)]
    [InlineData("Makarakarn", false, true)]
    public void ShieldEffect_AppliesExpectedShield(string actionName, bool expectedPhys, bool expectedMagic)
    {
        var target = CreateCombatant("Target");
        var effect = new ShieldEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            actionName,
            "Deploys a shield.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(expectedPhys, target.PhysKarnActive);
        Assert.Equal(expectedMagic, target.MagicKarnActive);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void DekajaEffect_ClearsPositiveBuffsOnly()
    {
        var target = CreateCombatant("Target");
        target.Buffs["PhysAtk"] = 2;
        target.Buffs["Defense"] = -2;
        var effect = new DekajaEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Dekaja",
            "Removes stat bonuses.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(0, target.Buffs["PhysAtk"]);
        Assert.Equal(-2, target.Buffs["Defense"]);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void DekundaEffect_ClearsNegativeDebuffsOnly()
    {
        var target = CreateCombatant("Target");
        target.Buffs["PhysAtk"] = 2;
        target.Buffs["Defense"] = -2;
        var effect = new DekundaEffect();

        List<CombatResult> results = effect.Apply(
            CreateCombatant("User"),
            new List<Combatant> { target },
            0,
            "Dekunda",
            "Removes stat penalties.",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.Equal(2, target.Buffs["PhysAtk"]);
        Assert.Equal(0, target.Buffs["Defense"]);
        Assert.Equal(HitType.Normal, Assert.Single(results).Type);
    }

    [Fact]
    public void DamageEffect_NullAffinityReturnsNullAndLearnsAffinity()
    {
        var user = CreateCombatant("User");
        var target = CreateCombatant("Target", Element.Fire, Affinity.Null);
        target.InflictAilment(CreateAilment("Shock"));
        var knowledge = new BattleKnowledge();
        var effect = new DamageEffect(Element.Fire);

        List<CombatResult> results = effect.Apply(
            user,
            new List<Combatant> { target },
            50,
            "Agi",
            "Deals Fire damage. 100%",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            knowledge);

        Assert.Equal(HitType.Null, Assert.Single(results).Type);
        Assert.Equal(Affinity.Null, knowledge.GetKnownAffinity(target.SourceId, Element.Fire));
    }

    [Fact]
    public void DamageEffect_PureSpDrainDamagesSpAndRestoresUserSp()
    {
        var user = CreateCombatant("User");
        var target = CreateCombatant("Target");
        user.CurrentSP = 10;
        target.CurrentSP = 30;
        target.InflictAilment(CreateAilment("Shock"));
        var effect = new DamageEffect(Element.Almighty);

        List<CombatResult> results = effect.Apply(
            user,
            new List<Combatant> { target },
            100,
            "Spirit Drain",
            "Drains SP from one foe. 100%",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        CombatResult result = Assert.Single(results);
        Assert.Equal(HitType.Normal, result.Type);
        Assert.True(result.DamageDealt > 0);
        Assert.Equal(30 - result.DamageDealt, target.CurrentSP);
        Assert.Equal(10 + result.DamageDealt, user.CurrentSP);
    }

    [Fact]
    public void DamageEffect_PhysicalAttackConsumesPowerCharge()
    {
        var user = CreateCombatant("User");
        var target = CreateCombatant("Target");
        user.IsCharged = true;
        var effect = new DamageEffect(Element.Strike);

        effect.Apply(
            user,
            new List<Combatant> { target },
            50,
            "Attack",
            "Deals Strike damage. 100%",
            new RecordingBattleMessenger(),
            new StatusRegistry(),
            new BattleKnowledge());

        Assert.False(user.IsCharged);
    }

    [Fact]
    public void BattleEffectRegistry_MapsKnownAndCleanedKeysToStrategies()
    {
        var registry = new BattleEffectRegistry();

        Assert.IsType<HealEffect>(registry.GetEffect("Healing"));
        Assert.IsType<AilmentEffect>(registry.GetEffect("Ailment Skills"));
        Assert.IsType<DamageEffect>(registry.GetEffect("Fire Skills"));
        Assert.Null(registry.GetEffect("No Such Effect"));
    }

    private static Combatant CreateCombatant(
        string name,
        Element affinityElement = Element.Fire,
        Affinity affinity = Affinity.Normal)
    {
        var combatant = new Combatant(name)
        {
            SourceId = name,
            MaxHP = 100,
            CurrentHP = 100,
            MaxSP = 50,
            CurrentSP = 50,
            ActivePersona = new Persona
            {
                Name = $"{name} Persona",
                AffinityMap = { [affinityElement] = affinity }
            }
        };

        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            combatant.CharacterStats[stat] = 20;
            combatant.ActivePersona.StatModifiers[stat] = 20;
        }

        return combatant;
    }

    private static AilmentData CreateAilment(string name)
    {
        return new AilmentData
        {
            Name = name,
            ActionRestriction = "None",
            DamageDealMult = 1.0,
            DamageTakenMult = 1.0,
            RemovalTriggers = new List<string>(),
            CureKeyword = string.Empty,
            Description = $"{name} test ailment."
        };
    }

    private sealed class RecordingBattleMessenger : IBattleMessenger
    {
        public event EventHandler<BattleMessageArgs>? OnMessagePublished;

        public List<string> Messages { get; } = new List<string>();

        public void Publish(
            string message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            Combatant? analysisTarget = null,
            bool clearScreen = false)
        {
            Messages.Add(message);
            OnMessagePublished?.Invoke(this, new BattleMessageArgs(message, color, delay, waitForInput, analysisTarget, clearScreen));
        }
    }
}
