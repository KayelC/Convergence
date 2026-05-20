using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data.Definitions
{
    public enum SkillKind
    {
        Damage,
        Healing,
        Revive,
        Ailment,
        BuffDebuff,
        Charge,
        Break,
        Shield,
        Passive,
        Special
    }

    public enum SkillCostResource
    {
        None,
        SP,
        HP
    }

    public enum SkillTargeting
    {
        Self,
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        DeadAlly,
        AllDeadAllies
    }

    public enum RecoveryResource
    {
        HP,
        SP
    }

    public enum RecoveryAmountKind
    {
        Flat,
        Percent,
        Full
    }

    public enum ChargeKind
    {
        Physical,
        Magical
    }

    public enum ShieldKind
    {
        Physical,
        Magical
    }

    public enum StatModifierTrack
    {
        PhysAtk,
        MagAtk,
        Defense,
        Agility
    }

    public sealed record SkillDefinition(
        string Id,
        string DisplayName,
        string Description,
        SkillKind Kind,
        SkillCostDefinition Cost,
        SkillTargeting Targeting,
        SkillInheritanceDefinition Inheritance,
        SkillEffectPayload Payload);

    public sealed record SkillCostDefinition(
        SkillCostResource Resource,
        int Amount,
        bool IsPercent)
    {
        public static SkillCostDefinition None { get; } =
            new SkillCostDefinition(SkillCostResource.None, 0, false);
    }

    public sealed record SkillInheritanceDefinition(
        bool IsInheritable,
        string? Family,
        int? Rank,
        bool IsExclusive);

    public abstract record SkillEffectPayload;

    public sealed record DamageSkillPayload(
        Element Element,
        int Power,
        int Accuracy,
        int? CriticalChance,
        bool DrainsHp,
        bool DrainsSp,
        bool IsInstantKill,
        SecondaryAilmentDefinition? SecondaryAilment) : SkillEffectPayload;

    public sealed record SecondaryAilmentDefinition(
        string AilmentId,
        int Chance);

    public sealed record HealingSkillPayload(
        RecoveryResource Resource,
        RecoveryAmountKind AmountKind,
        int Amount) : SkillEffectPayload;

    public sealed record ReviveSkillPayload(
        RecoveryAmountKind AmountKind,
        int Amount) : SkillEffectPayload;

    public sealed record AilmentSkillPayload(
        string AilmentId,
        int Chance) : SkillEffectPayload;

    public sealed record BuffDebuffSkillPayload(
        IReadOnlyList<StatModifierTrack> Tracks,
        int StageDelta) : SkillEffectPayload;

    public sealed record ChargeSkillPayload(
        ChargeKind Kind,
        double Multiplier) : SkillEffectPayload;

    public sealed record BreakSkillPayload(
        Element Element,
        int Duration) : SkillEffectPayload;

    public sealed record ShieldSkillPayload(
        ShieldKind Kind) : SkillEffectPayload;

    public sealed record PassiveSkillPayload(
        string PassiveKind) : SkillEffectPayload;

    public sealed record SpecialSkillPayload(
        string SpecialKind) : SkillEffectPayload;
}
