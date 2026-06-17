using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Logic.Field.State;

namespace JRPGPrototype.Logic.Fusion.Bridges
{
    /// <summary>
    /// UI authority for the Cathedral of Shadows.
    /// Handles ritual presentation, deterministic skill inheritance, 
    /// and Compendium visualization.
    /// </summary>
    public class CathedralUIBridge
    {
        private readonly IGameIO _io;
        private readonly FieldUIState _uiState;
        private readonly CompendiumRegistry _compendium;

        public CathedralUIBridge(IGameIO io, FieldUIState uiState, CompendiumRegistry compendium)
        {
            _io = io;
            _uiState = uiState;
            _compendium = compendium;
        }

        #region Navigation and Ritual Selection

        /// <summary>
        /// Renders the main Cathedral service list.
        /// The visible labels stay paired with typed actions so conditional entries, such as Full Moon
        /// sacrificial fusion, do not force the conductor to compare against display strings.
        /// </summary>
        public FusionMainMenuResult ShowCathedralMainMenu(int moonPhase)
            => ShowCathedralMainMenuDetailed(moonPhase).LegacyResult;

        internal CathedralMainMenuPresentationResult ShowCathedralMainMenuDetailed(int moonPhase)
        {
            _io.Clear();
            string phaseName = MoonPhaseSystem.GetPhaseName();
            string header = $"=== CATHEDRAL OF SHADOWS === [LUNAR PHASE: {phaseName}]\n" +
                            "\"Welcome to the Cathedral of Shadows where Demons Gather.\"\n";

            List<string> options = new List<string> { "Binary Fusion" };
            List<FusionMainMenuAction> actions = new List<FusionMainMenuAction> { FusionMainMenuAction.BinaryFusion };

            // Phase 8 is the current Full Moon gate for sacrifice; the option is absent at all other phases.
            if (moonPhase == 8)
            {
                options.Add("Sacrificial Fusion");
                actions.Add(FusionMainMenuAction.SacrificialFusion);
            }

            options.Add("Browse Compendium");
            actions.Add(FusionMainMenuAction.BrowseCompendium);

            options.Add("Register Demon");
            actions.Add(FusionMainMenuAction.RegisterDemon);

            options.Add("Back");

            int choice = _io.RenderMenu(header, options, 0);

            if (choice == -1 || choice == options.Count - 1)
            {
                return new CathedralMainMenuPresentationResult(
                    FusionPresentationResultKind.Back,
                    FusionMainMenuResult.Back,
                    options,
                    actions,
                    choice);
            }

            return new CathedralMainMenuPresentationResult(
                FusionPresentationResultKind.Selected,
                FusionMainMenuResult.Selected(actions[choice]),
                options,
                actions,
                choice);
        }

        #endregion

        #region Ritual Participant Selection

        /// <summary>
        /// Renders a participant picker for demons, personas, or transient fusion candidates.
        /// Exclusions are compared by object identity so the exact instance already chosen for this
        /// ritual cannot be selected again.
        /// </summary>
        public RitualParticipantSelectionResult<T> SelectRitualParticipant<T>(
            List<T> pool,
            string prompt,
            List<T> exclusions,
            IReadOnlyDictionary<T, string>? disabledReasons = null) where T : class
            => SelectRitualParticipantDetailed(pool, prompt, exclusions, disabledReasons).LegacyResult;

        internal RitualParticipantPresentationResult<T> SelectRitualParticipantDetailed<T>(
            List<T> pool,
            string prompt,
            List<T> exclusions,
            IReadOnlyDictionary<T, string>? disabledReasons = null) where T : class
        {
            var validChoices = pool.Where(x => !exclusions.Contains(x)).ToList();

            if (!validChoices.Any())
            {
                // Unavailable means the caller has no legal next step from this pool; it is distinct
                // from the player pressing Cancel on a populated list.
                _io.WriteLine("No further candidates available for this ritual.", ConsoleColor.Red);
                _io.Wait(800);
                return new RitualParticipantPresentationResult<T>(
                    FusionPresentationResultKind.Unavailable,
                    RitualParticipantSelectionResult<T>.Unavailable,
                    prompt,
                    Array.Empty<string>(),
                    Array.Empty<bool>(),
                    -1);
            }

            List<string> labels = new List<string>();
            List<bool> disabledList = new List<bool>();
            foreach (var item in validChoices)
            {
                bool isDisabled = disabledReasons != null && disabledReasons.ContainsKey(item);
                string disabledSuffix = isDisabled ? $" ({disabledReasons![item]})" : "";

                if (item is Combatant c)
                {
                    string race = c.ActivePersona?.Race ?? "Unknown";
                    string rank = c.ActivePersona?.Rank > 0 ? $"(Rk.{c.ActivePersona.Rank})" : "";
                    labels.Add($"{c.Name,-15} (Lv.{c.Level}) {race} {rank}{disabledSuffix}");
                }
                else if (item is Persona p)
                {
                    string rank = p.Rank > 0 ? $"(Rk.{p.Rank})" : "";
                    labels.Add($"{p.Name,-15} (Lv.{p.Level}) {p.Race} {rank}{disabledSuffix}");
                }

                disabledList.Add(isDisabled);
            }
            labels.Add("Cancel");
            disabledList.Add(false);

            int choice = _io.RenderMenu(prompt, labels, 0, disabledList);

            if (choice == -1 || choice == labels.Count - 1)
            {
                return new RitualParticipantPresentationResult<T>(
                    FusionPresentationResultKind.Canceled,
                    RitualParticipantSelectionResult<T>.Canceled,
                    prompt,
                    labels,
                    disabledList,
                    choice);
            }

            return new RitualParticipantPresentationResult<T>(
                FusionPresentationResultKind.Selected,
                RitualParticipantSelectionResult<T>.Selected(validChoices[choice]),
                prompt,
                labels,
                disabledList,
                choice);
        }

        #endregion

        #region Skill Selection

        /// <summary>
        /// Deterministic Skill Selection.
        /// Allows the player to manually select exactly which skills pass to the child.
        /// "Grays Out" skills that the target already possesses (Already Known) or skills that are parent-locked (Exclusive).
        /// Allows confirming 0 skills to avoid soft-locks when all are unavailable. 
        /// </summary>
        public SkillInheritanceSelectionResult SelectInheritedSkills(List<string> pool, int maxSlots, List<string> inherentSkills, List<string> exclusivePool)
            => SelectInheritedSkillsDetailed(pool, maxSlots, inherentSkills, exclusivePool).LegacyResult;

        internal SkillInheritancePresentationResult SelectInheritedSkillsDetailed(
            List<string> pool,
            int maxSlots,
            List<string> inherentSkills,
            List<string> exclusivePool,
            IReadOnlyList<FusionInheritanceEntry>? frameworkEntries = null)
        {
            List<string> selected = new List<string>();
            List<SkillInheritanceRowPresentation> rows = new List<SkillInheritanceRowPresentation>();
            int selectedIndex = -1;

            while (selected.Count < maxSlots)
            {
                _io.Clear();
                string header = $"=== SKILL INHERITANCE ===\nChoose skills to pass down to the new creation.\n" +
                                $"Selected: {selected.Count} / {maxSlots} slots filled.\n";

                List<string> labels = new List<string>();
                List<bool> disabledList = new List<bool>();
                rows = BuildSkillInheritanceRows(pool, selected, inherentSkills, exclusivePool, frameworkEntries).ToList();

                foreach (var row in rows)
                {
                    labels.Add(row.Label);

                    // The menu prevents duplicate picks and rule-illegal inheritance, while still
                    // letting the confirmation path accept zero inherited skills when all picks are blocked.
                    disabledList.Add(!row.IsSelectable);
                }

                // Confirm and Abort are separate choices so "inherit nothing" is a valid ritual decision.
                labels.Add("Confirm Selection");
                disabledList.Add(false);

                labels.Add("Abort Fusion");
                disabledList.Add(false);

                // Highlight text is bridge-owned presentation data; the conductor only receives final skill names.
                int choice = _io.RenderMenu(header, labels, 0, disabledList, (idx) =>
                {
                    if (idx >= 0 && idx < pool.Count)
                    {
                        if (Database.Skills.TryGetValue(pool[idx], out var data))
                            _io.WriteLine($"Skill Detail: {data.Effect}", ConsoleColor.Cyan);
                    }
                });

                selectedIndex = choice;

                if (choice == -1)
                {
                    return new SkillInheritancePresentationResult(
                        FusionPresentationResultKind.Canceled,
                        SkillInheritanceSelectionResult.Aborted,
                        rows,
                        frameworkEntries ?? [],
                        maxSlots,
                        selectedIndex);
                }

                // Abort is distinct from confirming an empty skill list.
                if (choice == labels.Count - 1)
                {
                    return new SkillInheritancePresentationResult(
                        FusionPresentationResultKind.Canceled,
                        SkillInheritanceSelectionResult.Aborted,
                        rows,
                        frameworkEntries ?? [],
                        maxSlots,
                        selectedIndex);
                }

                // Confirm may return an empty list, which is different from Abort.
                if (choice == labels.Count - 2)
                {
                    break;
                }

                selected.Add(pool[choice]);
            }

            return new SkillInheritancePresentationResult(
                FusionPresentationResultKind.Confirmed,
                SkillInheritanceSelectionResult.Confirmed(selected),
                rows,
                frameworkEntries ?? [],
                maxSlots,
                selectedIndex);
        }

        private static IEnumerable<SkillInheritanceRowPresentation> BuildSkillInheritanceRows(
            IReadOnlyList<string> pool,
            IReadOnlyList<string> selected,
            IReadOnlyList<string> inherentSkills,
            IReadOnlyList<string> exclusivePool,
            IReadOnlyList<FusionInheritanceEntry>? frameworkEntries)
        {
            Dictionary<string, FusionInheritanceEntry> frameworkByName =
                (frameworkEntries ?? [])
                .GroupBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var skillName in pool)
            {
                bool isPicked = selected.Contains(skillName);
                bool isAlreadyKnown = inherentSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase);
                bool isExclusive = exclusivePool.Contains(skillName, StringComparer.OrdinalIgnoreCase);

                string prefix = isPicked ? "[X]" : ((isAlreadyKnown || isExclusive) ? "[-]" : "[ ]");
                string label = $"{prefix} {skillName}";

                if (isAlreadyKnown)
                {
                    label += " (Already Known)";
                }
                else if (isExclusive)
                {
                    label += " (Exclusive)";
                }

                string reasonCode = "allowed";
                if (frameworkByName.TryGetValue(skillName, out FusionInheritanceEntry? frameworkEntry))
                {
                    reasonCode = frameworkEntry.ReasonCode;
                }
                else if (isPicked)
                {
                    reasonCode = "selected";
                }
                else if (isAlreadyKnown)
                {
                    reasonCode = "already_known";
                }
                else if (isExclusive)
                {
                    reasonCode = "owner_exclusive";
                }

                yield return new SkillInheritanceRowPresentation(
                    skillName,
                    label,
                    isPicked,
                    isAlreadyKnown,
                    isExclusive,
                    IsSelectable: !isPicked && !isAlreadyKnown && !isExclusive,
                    reasonCode);
            }
        }

        #endregion

        #region Ritual Presentation

        /// <summary>
        /// Final confirmation screen for the staged fusion result.
        /// It previews create, rank mutation, and stat-boost operations with enough detail for the
        /// player to decide whether to commit, revisit inheritance, or abandon the staged ritual.
        /// </summary>
        /// <param name="stagedDemon">The Combatant representing the FINAL state of the demon AFTER fusion.</param>
        /// <param name="originalParent">The original parent demon (for Rank/Stat boost "Before" comparison).</param>
        /// <param name="inheritedSkills">Skills to be inherited (only relevant for CreateNewDemon/RankUp/Down).</param>
        /// <param name="playerLevel">Current player level for level check.</param>
        /// <param name="operationType">The type of fusion operation.</param>
        public RitualConfirmationResult ConfirmRitual(Combatant stagedDemon, Combatant? originalParent, List<string> inheritedSkills, int playerLevel, FusionOperationType operationType)
            => ConfirmRitualDetailed(stagedDemon, originalParent, inheritedSkills, playerLevel, operationType).LegacyResult;

        internal RitualConfirmationPresentationResult ConfirmRitualDetailed(Combatant stagedDemon, Combatant? originalParent, List<string> inheritedSkills, int playerLevel, FusionOperationType operationType)
        {
            // Authority is checked against the result's natural template level.
            // Sacrificial XP can create a breakthrough after this point, but it does not let the
            // player create a base demon whose template is already above their level.
            int baseTemplateLevel = 0;
            if (Database.Personas.TryGetValue(stagedDemon.SourceId.ToLower(), out var template))
            {
                baseTemplateLevel = template.Level;
            }

            // Forbidden returns before the confirmation menu is shown; the player never gets a
            // "Commence Ritual" option for a base result they are not allowed to create.
            if (baseTemplateLevel > playerLevel)
            {
                _io.Clear();
                _io.WriteLine("=== RITUAL FORBIDDEN ===", ConsoleColor.Red);
                _io.WriteLine($"The resulting being, {stagedDemon.Name} (Lv.{baseTemplateLevel}) exceeds your authority.");
                _io.WriteLine($"Your current level: {playerLevel}", ConsoleColor.Gray);
                _io.WriteLine("\nThe spirits refuse to stabilize.", ConsoleColor.Red);
                _io.Wait(2000);
                return new RitualConfirmationPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    RitualConfirmationResult.Forbidden,
                    stagedDemon,
                    originalParent,
                    inheritedSkills,
                    playerLevel,
                    baseTemplateLevel,
                    operationType,
                    -1);
            }

            List<string> options = new List<string> { "Commence Ritual", "Wait", "Cancel Fusion" };

            // The menu callback redraws the staged preview below every highlighted option, keeping
            // all comparison data in the same screen as the final decision.
            int choice = _io.RenderMenu("Is this creation acceptable?", options, 0, null, (idx) =>
            {
                _io.WriteLine("\n--- PROJECTED RESULT ---", ConsoleColor.Yellow);

                // A breakthrough is allowed only after the base authority gate succeeds; this callout
                // explains why the preview can show a final level above the player's current level.
                if (stagedDemon.Level > playerLevel)
                {
                    _io.WriteLine($"!!! BREAKTHROUGH !!!", ConsoleColor.DarkYellow);
                    _io.WriteLine($"Sacrificial energy has pushed this soul beyond your standard limits!", ConsoleColor.Green);
                }

                switch (operationType)
                {
                    case FusionOperationType.CreateNewDemon:
                        _io.WriteLine($"Form  : {stagedDemon.Name}", ConsoleColor.Yellow);
                        _io.WriteLine($"Race  : {stagedDemon.ActivePersona.Race}", ConsoleColor.Yellow);
                        _io.WriteLine($"Rank  : {stagedDemon.ActivePersona.Rank}", ConsoleColor.Yellow);
                        _io.WriteLine($"Level : {stagedDemon.Level}", stagedDemon.Level > playerLevel ? ConsoleColor.Green : ConsoleColor.Yellow);
                        break;

                    case FusionOperationType.RankUpParent:
                    case FusionOperationType.RankDownParent:
                    case FusionOperationType.StatBoostFusion:
                        _io.WriteLine($"Result: {stagedDemon.Name} (Lv.{stagedDemon.Level})", stagedDemon.Level > playerLevel ? ConsoleColor.Green : ConsoleColor.Yellow);
                        _io.WriteLine("------------------------");
                        _io.WriteLine("Stat Changes:", ConsoleColor.Yellow);
                        if (originalParent != null)
                        {
                            foreach (StatType st in Enum.GetValues(typeof(StatType)))
                            {
                                int originalVal = originalParent.GetStat(st);
                                int stagedVal = stagedDemon.GetStat(st);
                                if (stagedVal != originalVal)
                                {
                                    _io.WriteLine($"  {st}: {originalVal} -> {stagedVal} ({(stagedVal > originalVal ? "+" : "")}{stagedVal - originalVal})", ConsoleColor.Green);
                                }
                                else
                                {
                                    _io.WriteLine($"  {st}: {originalVal}", ConsoleColor.DarkGray);
                                }
                            }
                        }
                        break;
                }

                _io.WriteLine("------------------------");

                // Show inherent skills first so inherited picks read as additions, not replacements.
                var baseSkills = stagedDemon.ActivePersona.SkillSet;
                if (baseSkills.Any())
                {
                    _io.WriteLine("Inherent Base Skills:", ConsoleColor.Cyan);
                    foreach (var s in baseSkills)
                    {
                        _io.WriteLine($"  * {s}", ConsoleColor.Cyan);
                    }
                }

                // Inherited skills are the exact list the mutator will attempt to apply if confirmed.
                if (inheritedSkills != null && inheritedSkills.Any())
                {
                    _io.WriteLine("Inherited Skills:", ConsoleColor.Green);
                    foreach (var s in inheritedSkills)
                    {
                        _io.WriteLine($"  + {s}", ConsoleColor.Green);
                    }
                }

                _io.WriteLine("------------------------");
            });

            RitualConfirmationResult legacy = choice switch
            {
                0 => RitualConfirmationResult.Commence,
                1 => RitualConfirmationResult.Wait,
                _ => RitualConfirmationResult.Cancel
            };

            FusionPresentationResultKind kind = legacy.Kind switch
            {
                RitualConfirmationKind.Commence => FusionPresentationResultKind.Confirmed,
                RitualConfirmationKind.Wait => FusionPresentationResultKind.Back,
                RitualConfirmationKind.Cancel => FusionPresentationResultKind.Canceled,
                _ => FusionPresentationResultKind.Rejected
            };

            return new RitualConfirmationPresentationResult(
                kind,
                legacy,
                stagedDemon,
                originalParent,
                inheritedSkills,
                playerLevel,
                baseTemplateLevel,
                operationType,
                choice);
        }

        /// <summary>
        /// Orchestrates the visual sequence of the fusion ritual.
        /// Handles the atmospheric delays and accident feedback.
        /// </summary>
        public void DisplayRitualSequence(bool isAccident)
            => DisplayRitualSequenceDetailed(isAccident);

        internal RitualSequencePresentationResult DisplayRitualSequenceDetailed(bool isAccident)
        {
            List<FusionPresentationEvent> events = new List<FusionPresentationEvent>();

            _io.Clear();
            var circle = new FusionPresentationEvent(FusionPresentationResultKind.Shown, "The sacrificial circle glows with a cold, blue light...");
            events.Add(circle);
            _io.WriteLine(circle.Message!);
            _io.Wait(1200);
            var data = new FusionPresentationEvent(FusionPresentationResultKind.Shown, "The participants are reduced to pure spiritual data...");
            events.Add(data);
            _io.WriteLine(data.Message!);
            _io.Wait(1200);
            var merge = new FusionPresentationEvent(FusionPresentationResultKind.Shown, "The streams of energy collide and begin to merge...");
            events.Add(merge);
            _io.WriteLine(merge.Message!);
            _io.Wait(1200);

            // Fusion accident is a surprise, revealed only after confirmation and animation
            if (isAccident)
            {
                var warning = new FusionPresentationEvent(
                    FusionPresentationResultKind.Shown,
                    "!!! WARNING: LUNAR INTERFERENCE DETECTED !!!",
                    ConsoleColor.Red);
                var unstable = new FusionPresentationEvent(
                    FusionPresentationResultKind.Shown,
                    "The fusion process has become unstable!",
                    ConsoleColor.Red,
                    DelayMilliseconds: 2000);
                events.Add(warning);
                events.Add(unstable);
                _io.WriteLine(warning.Message!, warning.Color!.Value);
                _io.WriteLine(unstable.Message!, unstable.Color!.Value);
                _io.Wait(2000);
            }

            return new RitualSequencePresentationResult(isAccident, events);
        }

        #endregion

        #region Compendium UI

        /// <summary>
        /// Renders the current Compendium snapshots and returns the chosen recall entry.
        /// Empty registry is Unavailable; Back means the player left a populated recall list.
        /// </summary>
        public CompendiumRecallResult ShowCompendiumRecallMenu()
            => ShowCompendiumRecallMenuDetailed().LegacyResult;

        internal CompendiumRecallPresentationResult ShowCompendiumRecallMenuDetailed()
        {
            var entries = _compendium.GetAllRegisteredDemons();

            if (!entries.Any())
            {
                _io.WriteLine("The Compendium is empty. You must register a demon first.", ConsoleColor.Gray);
                _io.Wait(1000);
                return new CompendiumRecallPresentationResult(
                    FusionPresentationResultKind.Unavailable,
                    CompendiumRecallResult.Unavailable,
                    Array.Empty<string>(),
                    -1);
            }

            string header = "=== DEMONIC COMPENDIUM ===\nRecall the data of a previously registered demon.\n";
            List<string> labels = new List<string>();

            foreach (var entry in entries)
            {
                int cost = _compendium.CalculateRecallCost(entry.SourceId);
                labels.Add($"{entry.Name,-15} (Lv.{entry.Level}) {entry.ActivePersona?.Race} (Rk.{entry.ActivePersona?.Rank}) | {cost} M");
            }
            labels.Add("Back");

            int choice = _io.RenderMenu(header, labels, 0);
            if (choice == -1 || choice == labels.Count - 1)
            {
                return new CompendiumRecallPresentationResult(
                    FusionPresentationResultKind.Back,
                    CompendiumRecallResult.Back,
                    labels,
                    choice);
            }

            return new CompendiumRecallPresentationResult(
                FusionPresentationResultKind.Selected,
                CompendiumRecallResult.Selected(entries[choice]),
                labels,
                choice);
        }

        /// <summary>
        /// Prompts the Operator to choose a demon snapshot source for Compendium registration.
        /// Unavailable means the Operator owns no registerable demons; Canceled means they left a populated picker.
        /// </summary>
        public CompendiumRegistrationSelectionResult SelectDemonToRegister(List<Combatant> party)
            => SelectDemonToRegisterDetailed(party).LegacyResult;

        internal CompendiumRegistrationSelectionPresentationResult SelectDemonToRegisterDetailed(List<Combatant> party)
        {
            var demonsOnly = party.Where(c => c.Class == ClassType.Demon).ToList();

            if (!demonsOnly.Any())
            {
                _io.WriteLine("You have no demons in your party to register.", ConsoleColor.Red);
                _io.Wait(800);
                return new CompendiumRegistrationSelectionPresentationResult(
                    FusionPresentationResultKind.Unavailable,
                    CompendiumRegistrationSelectionResult.Unavailable,
                    Array.Empty<string>(),
                    -1);
            }

            string header = "=== REGISTER DEMON ===\nSelect a demon to overwrite its current snapshot in the registry.\n";
            List<string> labels = demonsOnly.Select(d => $"{d.Name,-15} (Lv.{d.Level}) {d.ActivePersona?.Race} (Rk.{d.ActivePersona?.Rank})").ToList();
            labels.Add("Cancel");

            int choice = _io.RenderMenu(header, labels, 0);
            if (choice == -1 || choice == labels.Count - 1)
            {
                return new CompendiumRegistrationSelectionPresentationResult(
                    FusionPresentationResultKind.Canceled,
                    CompendiumRegistrationSelectionResult.Canceled,
                    labels,
                    choice);
            }

            return new CompendiumRegistrationSelectionPresentationResult(
                FusionPresentationResultKind.Selected,
                CompendiumRegistrationSelectionResult.Selected(demonsOnly[choice]),
                labels,
                choice);
        }

        #endregion
    }
}
