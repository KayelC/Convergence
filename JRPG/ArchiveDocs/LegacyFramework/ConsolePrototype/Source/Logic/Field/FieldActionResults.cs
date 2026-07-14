using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Field
{
    public enum FieldSelectionResultKind
    {
        Back,
        Unavailable,
        Selected
    }

    public enum FieldUseExecutionReason
    {
        None,
        ItemUnavailable,
        NoEffect,
        FullHp,
        FullSp,
        InsufficientSp,
        DungeonExitRequested,
        UnsupportedFieldUse
    }

    public sealed record FieldItemSelectionResult(FieldSelectionResultKind Kind, ItemData? Item = null)
    {
        public static FieldItemSelectionResult Back { get; } =
            new(FieldSelectionResultKind.Back);

        public static FieldItemSelectionResult Unavailable { get; } =
            new(FieldSelectionResultKind.Unavailable);

        public static FieldItemSelectionResult Selected(ItemData item) =>
            new(FieldSelectionResultKind.Selected, item);
    }

    public sealed record FieldSkillPerformerSelectionResult(FieldSelectionResultKind Kind, Combatant? Performer = null)
    {
        public static FieldSkillPerformerSelectionResult Back { get; } =
            new(FieldSelectionResultKind.Back);

        public static FieldSkillPerformerSelectionResult Unavailable { get; } =
            new(FieldSelectionResultKind.Unavailable);

        public static FieldSkillPerformerSelectionResult Selected(Combatant performer) =>
            new(FieldSelectionResultKind.Selected, performer);
    }

    public sealed record FieldSkillSelectionResult(FieldSelectionResultKind Kind, SkillData? Skill = null)
    {
        public static FieldSkillSelectionResult Back { get; } =
            new(FieldSelectionResultKind.Back);

        public static FieldSkillSelectionResult Unavailable { get; } =
            new(FieldSelectionResultKind.Unavailable);

        public static FieldSkillSelectionResult Selected(SkillData skill) =>
            new(FieldSelectionResultKind.Selected, skill);
    }

    public sealed record FieldTargetSelectionResult(FieldSelectionResultKind Kind, Combatant? Target = null)
    {
        public static FieldTargetSelectionResult Back { get; } =
            new(FieldSelectionResultKind.Back);

        public static FieldTargetSelectionResult Selected(Combatant target) =>
            new(FieldSelectionResultKind.Selected, target);
    }

    public sealed record FieldUseAssessment(
        bool CanExecute,
        ItemUsageResult LegacyResult,
        FieldUseExecutionReason Reason,
        bool ConsumeItemOnSuccess)
    {
        public static FieldUseAssessment CanApply(bool consumeItem = false) =>
            new(true, ItemUsageResult.Applied, FieldUseExecutionReason.None, consumeItem);

        public static FieldUseAssessment RequestDungeonExit =>
            new(true, ItemUsageResult.RequestDungeonExit, FieldUseExecutionReason.DungeonExitRequested, true);

        public static FieldUseAssessment Failed(FieldUseExecutionReason reason) =>
            new(false, ItemUsageResult.Failed, reason, false);
    }

    public sealed record FieldUsePresentationEvent(
        string? Message,
        ConsoleColor Color,
        int Delay,
        bool WaitForInput,
        bool ClearScreen);

    public sealed record FieldUseExecutionResult
    {
        public FieldUseExecutionResult(
            ItemUsageResult legacyResult,
            bool applied,
            bool consumeItem,
            FieldUseExecutionReason reason,
            IEnumerable<FieldUsePresentationEvent>? presentationEvents = null)
        {
            LegacyResult = legacyResult;
            Applied = applied;
            ConsumeItem = consumeItem;
            Reason = reason;
            PresentationEvents = new ReadOnlyCollection<FieldUsePresentationEvent>(
                new List<FieldUsePresentationEvent>(presentationEvents ?? Array.Empty<FieldUsePresentationEvent>()));
        }

        public ItemUsageResult LegacyResult { get; }
        public bool Applied { get; }
        public bool ConsumeItem { get; }
        public FieldUseExecutionReason Reason { get; }
        public IReadOnlyList<FieldUsePresentationEvent> PresentationEvents { get; }
    }
}
