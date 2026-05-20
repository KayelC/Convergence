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
}
