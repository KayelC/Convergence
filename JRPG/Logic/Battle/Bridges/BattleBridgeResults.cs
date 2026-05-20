using System;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Battle.Bridges
{
    public enum BattleMainMenuAction
    {
        Attack,
        Guard,
        Persona,
        UseSkill,
        Comp,
        Pass,
        UseItem,
        Talk,
        Tactics
    }

    public enum BattleMenuResultKind
    {
        Selected,
        Back
    }

    public sealed record BattleMainMenuResult(BattleMenuResultKind Kind, BattleMainMenuAction? Action = null)
    {
        public static BattleMainMenuResult Back { get; } =
            new BattleMainMenuResult(BattleMenuResultKind.Back);

        public static BattleMainMenuResult Selected(BattleMainMenuAction action)
            => new BattleMainMenuResult(BattleMenuResultKind.Selected, action);
    }

    public enum BattlePersonaActionKind
    {
        SelectedSkill,
        RequestSwap,
        Back
    }

    public sealed record BattlePersonaActionResult(BattlePersonaActionKind Kind, SkillData? SelectedSkill = null)
    {
        public static BattlePersonaActionResult Back { get; } =
            new BattlePersonaActionResult(BattlePersonaActionKind.Back);

        public static BattlePersonaActionResult RequestSwap { get; } =
            new BattlePersonaActionResult(BattlePersonaActionKind.RequestSwap);

        public static BattlePersonaActionResult Skill(SkillData skill)
            => new BattlePersonaActionResult(BattlePersonaActionKind.SelectedSkill, skill);
    }

    public enum BattleCompActionKind
    {
        Back,
        Summon,
        Swap,
        Return,
        Analyze
    }

    public sealed record BattleCompActionResult(
        BattleCompActionKind Kind,
        Combatant? Standby = null,
        Combatant? Active = null)
    {
        public static BattleCompActionResult Back { get; } =
            new BattleCompActionResult(BattleCompActionKind.Back);

        public static BattleCompActionResult Summon(Combatant standby)
            => new BattleCompActionResult(BattleCompActionKind.Summon, Standby: standby);

        public static BattleCompActionResult Swap(Combatant standby, Combatant active)
            => new BattleCompActionResult(BattleCompActionKind.Swap, Standby: standby, Active: active);

        public static BattleCompActionResult Return(Combatant active)
            => new BattleCompActionResult(BattleCompActionKind.Return, Active: active);

        public static BattleCompActionResult Analyze(Combatant target)
            => new BattleCompActionResult(BattleCompActionKind.Analyze, Active: target);
    }

    public enum BattleTacticsAction
    {
        Escape,
        Strategy
    }

    public sealed record BattleTacticsResult(BattleMenuResultKind Kind, BattleTacticsAction? Action = null)
    {
        public static BattleTacticsResult Back { get; } =
            new BattleTacticsResult(BattleMenuResultKind.Back);

        public static BattleTacticsResult Selected(BattleTacticsAction action)
            => new BattleTacticsResult(BattleMenuResultKind.Selected, action);
    }

    public enum BattleSelectionResultKind
    {
        Selected,
        Back,
        Unavailable
    }

    public sealed record BattlePersonaSelectionResult(BattleSelectionResultKind Kind, Persona? Persona = null)
    {
        public static BattlePersonaSelectionResult Back { get; } =
            new BattlePersonaSelectionResult(BattleSelectionResultKind.Back);

        public static BattlePersonaSelectionResult Unavailable { get; } =
            new BattlePersonaSelectionResult(BattleSelectionResultKind.Unavailable);

        public static BattlePersonaSelectionResult Selected(Persona persona)
            => new BattlePersonaSelectionResult(BattleSelectionResultKind.Selected, persona);
    }

    public sealed record BattleTargetSelectionResult(
        BattleSelectionResultKind Kind,
        IReadOnlyList<Combatant> Targets)
    {
        public static BattleTargetSelectionResult Back { get; } =
            new BattleTargetSelectionResult(BattleSelectionResultKind.Back, Array.Empty<Combatant>());

        public static BattleTargetSelectionResult Unavailable { get; } =
            new BattleTargetSelectionResult(BattleSelectionResultKind.Unavailable, Array.Empty<Combatant>());

        public static BattleTargetSelectionResult Selected(IReadOnlyList<Combatant> targets)
            => new BattleTargetSelectionResult(BattleSelectionResultKind.Selected, targets);
    }

    public sealed record BattleStrategyTargetSelectionResult(BattleSelectionResultKind Kind, Combatant? Target = null)
    {
        public static BattleStrategyTargetSelectionResult Back { get; } =
            new BattleStrategyTargetSelectionResult(BattleSelectionResultKind.Back);

        public static BattleStrategyTargetSelectionResult Unavailable { get; } =
            new BattleStrategyTargetSelectionResult(BattleSelectionResultKind.Unavailable);

        public static BattleStrategyTargetSelectionResult Selected(Combatant target)
            => new BattleStrategyTargetSelectionResult(BattleSelectionResultKind.Selected, target);
    }

    public sealed record BattleSkillSelectionResult(BattleSelectionResultKind Kind, SkillData? Skill = null)
    {
        public static BattleSkillSelectionResult Back { get; } =
            new BattleSkillSelectionResult(BattleSelectionResultKind.Back);

        public static BattleSkillSelectionResult Unavailable { get; } =
            new BattleSkillSelectionResult(BattleSelectionResultKind.Unavailable);

        public static BattleSkillSelectionResult Selected(SkillData skill)
            => new BattleSkillSelectionResult(BattleSelectionResultKind.Selected, skill);
    }

    public sealed record BattleItemSelectionResult(BattleSelectionResultKind Kind, ItemData? Item = null)
    {
        public static BattleItemSelectionResult Back { get; } =
            new BattleItemSelectionResult(BattleSelectionResultKind.Back);

        public static BattleItemSelectionResult Unavailable { get; } =
            new BattleItemSelectionResult(BattleSelectionResultKind.Unavailable);

        public static BattleItemSelectionResult Selected(ItemData item)
            => new BattleItemSelectionResult(BattleSelectionResultKind.Selected, item);
    }
}
