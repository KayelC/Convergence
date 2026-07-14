using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using static JRPGPrototype.Logic.Fusion.Bridges.FusionPresentationSnapshots;

namespace JRPGPrototype.Logic.Fusion.Bridges
{
    public enum FusionPresentationResultKind
    {
        Selected,
        Back,
        Canceled,
        Unavailable,
        Confirmed,
        Rejected,
        Applied,
        Shown,
        Suppressed,
        HostOwned
    }

    public sealed record FusionPresentationEvent(
        FusionPresentationResultKind Kind,
        string? Message = null,
        ConsoleColor? Color = null,
        int DelayMilliseconds = 0,
        bool WaitForInput = false);

    public sealed record CathedralMainMenuPresentationResult
    {
        public CathedralMainMenuPresentationResult(
            FusionPresentationResultKind kind,
            FusionMainMenuResult legacyResult,
            IEnumerable<string> options,
            IEnumerable<FusionMainMenuAction> actions,
            int selectedIndex)
        {
            Kind = kind;
            LegacyResult = legacyResult;
            Options = Snapshot(options);
            Actions = Snapshot(actions);
            SelectedIndex = selectedIndex;
        }

        public FusionPresentationResultKind Kind { get; }
        public FusionMainMenuResult LegacyResult { get; }
        public IReadOnlyList<string> Options { get; }
        public IReadOnlyList<FusionMainMenuAction> Actions { get; }
        public int SelectedIndex { get; }
    }

    public sealed record RitualParticipantPresentationResult<T>
        where T : class
    {
        public RitualParticipantPresentationResult(
            FusionPresentationResultKind kind,
            RitualParticipantSelectionResult<T> legacyResult,
            string prompt,
            IEnumerable<string> labels,
            IEnumerable<bool> disabledOptions,
            int selectedIndex)
        {
            Kind = kind;
            LegacyResult = legacyResult;
            Prompt = prompt;
            Labels = Snapshot(labels);
            DisabledOptions = Snapshot(disabledOptions);
            SelectedIndex = selectedIndex;
        }

        public FusionPresentationResultKind Kind { get; }
        public RitualParticipantSelectionResult<T> LegacyResult { get; }
        public string Prompt { get; }
        public IReadOnlyList<string> Labels { get; }
        public IReadOnlyList<bool> DisabledOptions { get; }
        public int SelectedIndex { get; }
    }

    public sealed record SkillInheritanceRowPresentation(
        string SkillName,
        string Label,
        bool IsSelected,
        bool IsAlreadyKnown,
        bool IsExclusive,
        bool IsSelectable,
        string ReasonCode);

    public sealed record SkillInheritancePresentationResult
    {
        public SkillInheritancePresentationResult(
            FusionPresentationResultKind kind,
            SkillInheritanceSelectionResult legacyResult,
            IEnumerable<SkillInheritanceRowPresentation> rows,
            IEnumerable<FusionInheritanceEntry> frameworkEntries,
            int maximumSlots,
            int selectedIndex)
        {
            Kind = kind;
            LegacyResult = legacyResult;
            Rows = Snapshot(rows);
            FrameworkEntries = Snapshot(frameworkEntries);
            MaximumSlots = maximumSlots;
            SelectedIndex = selectedIndex;
        }

        public FusionPresentationResultKind Kind { get; }
        public SkillInheritanceSelectionResult LegacyResult { get; }
        public IReadOnlyList<SkillInheritanceRowPresentation> Rows { get; }
        public IReadOnlyList<FusionInheritanceEntry> FrameworkEntries { get; }
        public int MaximumSlots { get; }
        public int SelectedIndex { get; }
    }

    public sealed record RitualConfirmationPresentationResult
    {
        public RitualConfirmationPresentationResult(
            FusionPresentationResultKind kind,
            RitualConfirmationResult legacyResult,
            Combatant stagedDemon,
            Combatant? originalParent,
            IEnumerable<string> inheritedSkills,
            int playerLevel,
            int baseTemplateLevel,
            FusionOperationType operationType,
            int selectedIndex)
        {
            Kind = kind;
            LegacyResult = legacyResult;
            StagedDemon = stagedDemon;
            OriginalParent = originalParent;
            InheritedSkills = Snapshot(inheritedSkills);
            PlayerLevel = playerLevel;
            BaseTemplateLevel = baseTemplateLevel;
            OperationType = operationType;
            SelectedIndex = selectedIndex;
        }

        public FusionPresentationResultKind Kind { get; }
        public RitualConfirmationResult LegacyResult { get; }
        public Combatant StagedDemon { get; }
        public Combatant? OriginalParent { get; }
        public IReadOnlyList<string> InheritedSkills { get; }
        public int PlayerLevel { get; }
        public int BaseTemplateLevel { get; }
        public FusionOperationType OperationType { get; }
        public int SelectedIndex { get; }
    }

    public sealed record RitualSequencePresentationResult(
        bool IsAccident,
        IReadOnlyList<FusionPresentationEvent> Events);

    public sealed record FusionTransactionPresentationResult
    {
        public FusionTransactionPresentationResult(
            FusionPresentationResultKind kind,
            FusionOperationType operationType,
            string resultId,
            IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null,
            IReadOnlyList<object>? consumedParticipants = null)
        {
            Kind = kind;
            OperationType = operationType;
            ResultId = resultId;
            Diagnostics = Snapshot(diagnostics);
            ConsumedParticipants = Snapshot(consumedParticipants);
        }

        public FusionPresentationResultKind Kind { get; }
        public FusionOperationType OperationType { get; }
        public string ResultId { get; }
        public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }
        public IReadOnlyList<object> ConsumedParticipants { get; }
        public bool Applied => Kind == FusionPresentationResultKind.Applied;
    }

    public sealed record CompendiumRecallPresentationResult
    {
        public CompendiumRecallPresentationResult(
            FusionPresentationResultKind kind,
            CompendiumRecallResult legacyResult,
            IEnumerable<string> labels,
            int selectedIndex,
            CompendiumRecallAssessment? assessment = null)
        {
            Kind = kind;
            LegacyResult = legacyResult;
            Labels = Snapshot(labels);
            SelectedIndex = selectedIndex;
            Assessment = assessment;
        }

        public FusionPresentationResultKind Kind { get; }
        public CompendiumRecallResult LegacyResult { get; }
        public IReadOnlyList<string> Labels { get; }
        public int SelectedIndex { get; }
        public CompendiumRecallAssessment? Assessment { get; }
    }

    public sealed record CompendiumRegistrationSelectionPresentationResult
    {
        public CompendiumRegistrationSelectionPresentationResult(
            FusionPresentationResultKind kind,
            CompendiumRegistrationSelectionResult legacyResult,
            IEnumerable<string> labels,
            int selectedIndex)
        {
            Kind = kind;
            LegacyResult = legacyResult;
            Labels = Snapshot(labels);
            SelectedIndex = selectedIndex;
        }

        public FusionPresentationResultKind Kind { get; }
        public CompendiumRegistrationSelectionResult LegacyResult { get; }
        public IReadOnlyList<string> Labels { get; }
        public int SelectedIndex { get; }
    }

    public sealed record CompendiumRegistrationPresentationResult
    {
        public CompendiumRegistrationPresentationResult(
            FusionPresentationResultKind kind,
            Combatant? source,
            CompendiumRegistrationResult? result,
            FusionPresentationEvent? presentationEvent)
        {
            Kind = kind;
            Source = source;
            Result = result;
            Event = presentationEvent;
        }

        public FusionPresentationResultKind Kind { get; }
        public Combatant? Source { get; }
        public CompendiumRegistrationResult? Result { get; }
        public FusionPresentationEvent? Event { get; }
    }

    public sealed record CompendiumRecallTransactionPresentationResult
    {
        public CompendiumRecallTransactionPresentationResult(
            FusionPresentationResultKind kind,
            Combatant? snapshot,
            int cost,
            CompendiumRecallAssessment? assessment,
            FusionPresentationEvent? presentationEvent)
        {
            Kind = kind;
            Snapshot = snapshot;
            Cost = cost;
            Assessment = assessment;
            Event = presentationEvent;
        }

        public FusionPresentationResultKind Kind { get; }
        public Combatant? Snapshot { get; }
        public int Cost { get; }
        public CompendiumRecallAssessment? Assessment { get; }
        public FusionPresentationEvent? Event { get; }
        public bool Applied => Kind == FusionPresentationResultKind.Applied;
    }

    internal static class FusionPresentationSnapshots
    {
        public static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? values) =>
            new ReadOnlyCollection<T>(values?.ToArray() ?? []);
    }
}
