using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;

namespace JRPGPrototype.Logic.Battle.Bridges
{
    // Helper class to handle the seamless return from the integrated Persona menu.
    public class PersonaMenuResult
    {
        public SkillData? SelectedSkill { get; set; }
        public bool RequestSwap { get; set; }
        public bool Cancelled { get; set; }
    }

    /// <summary>
    /// The UI Flow Orchestrator for the Battle Sub-System.
    /// Manages menu navigation, target selection, and renders battle context to IGameIO.
    /// Decoupled from execution logic to facilitate future GUI porting.
    /// </summary>
    public class InteractionBridge
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly InventoryManager _inv;
        private readonly List<Combatant> _enemies;
        private readonly PressTurnEngine _turnEngine;
        private readonly BattleKnowledge _knowledge;
        private readonly StatusRegistry _statusRegistry;

        private static int _mainMenuIndex = 0;
        private static int _skillMenuIndex = 0;
        private static int _itemMenuIndex = 0;

        public InteractionBridge(IGameIO io, PartyManager party, InventoryManager inventory, List<Combatant> enemies, PressTurnEngine turnEngine, BattleKnowledge knowledge)
        {
            _io = io;
            _party = party;
            _inv = inventory;
            _enemies = enemies;
            _turnEngine = turnEngine;
            _knowledge = knowledge;
            _statusRegistry = new StatusRegistry(); // Initialized to provide redundancy checks
        }

        // Public access for the Conductor to force a HUD update during AI/DOT sequences.
        public void ForceRefreshHUD()
        {
            _io.Clear();
            _io.WriteLine(GetBattleContext(null));
        }

        public string ShowMainMenu(Combatant actor)
        {
            string context = GetBattleContext(actor);
            List<string> options = new List<string> { "Attack", "Guard" };

            // Class-Based Menu Augmentation
            if (actor.Class == ClassType.PersonaUser || actor.Class == ClassType.WildCard)
            {
                options.Add("Persona");
                options.Add("Talk"); // WildCards can negotiate
            }
            else if (actor.Class == ClassType.Operator)
            {
                options.Add("Command");
                options.Add("COMP");
                options.Add("Talk"); // Operators can negotiate
            }
            else
            {
                options.Add("Skill");
            }

            bool isHumanoid = actor.Class != ClassType.Demon;

            if (isHumanoid)
            {
                options.Add("Item");
                options.Add("Tactics");
            }

            options.Add("Pass");

            bool isPanicked = actor.CurrentAilment != null && actor.CurrentAilment.Name == "Panic";
            bool isBound = actor.CurrentAilment != null && actor.CurrentAilment.ActionRestriction == "LimitedAction";

            List<bool> disabledStates = new List<bool>();

            foreach (var opt in options)
            {
                bool isDisabled = false;
                if (isPanicked && (opt == "Persona" || opt == "Skill" || opt == "Command"
                    || opt == "COMP" || opt == "Item" || opt == "Talk"))
                {
                    isDisabled = true;
                }

                if (isBound && (opt == "Persona" || opt == "Skill" || opt == "Command" || opt == "COMP" || opt == "Item" || opt == "Talk"))
                {
                    isDisabled = true;
                }

                disabledStates.Add(isDisabled);
            }

            // Normal menu: Status Inspect is FALSE
            int choice = _io.RenderMenu($"{context}\nCommand: {actor.Name}", options, _mainMenuIndex, disabledStates, null, false);
            if (choice == -1) return "Cancel";

            _mainMenuIndex = choice;
            return options[choice];
        }

        /// <summary>
        /// Provides the seamless Persona sub-menu for Wild Cards.
        /// Integrates Skills and Change Persona into a single unified list.
        /// </summary>
        public PersonaMenuResult SelectPersonaAction(Combatant actor)
        {
            var result = new PersonaMenuResult();
            var skillNames = actor.GetConsolidatedSkills();

            List<string> labels = new List<string>();
            List<bool> disabled = new List<bool>();
            List<SkillData?> skillMapping = new List<SkillData?>();

            // 1. Map Skills
            foreach (var sName in skillNames)
            {
                if (Database.Skills.TryGetValue(sName, out var data))
                {
                    if (data.Category == "Passive Skills") continue;

                    var cost = data.ParseCost();
                    bool canAfford = cost.isHP ? actor.CurrentHP > (int)(actor.MaxHP * (cost.value / 100.0)) : actor.CurrentSP >= cost.value;

                    labels.Add($"{sName} ({data.Cost})");
                    disabled.Add(!canAfford);
                    skillMapping.Add(data);
                }
            }

            // 2. Inject Change Persona Option if Wild Card
            bool hasChangeOption = false;
            if (actor.Class == ClassType.WildCard)
            {
                labels.Add("[-- CHANGE PERSONA --]");
                disabled.Add(actor.HasSwappedThisTurn);
                skillMapping.Add(null); // Placeholder for non-skill action
                hasChangeOption = true;
            }

            labels.Add("Back");
            disabled.Add(false);

            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nPERSONA ({actor.ActivePersona?.Name})", labels, _skillMenuIndex, disabled, (idx) =>
            {
                if (idx >= 0 && idx < skillMapping.Count && skillMapping[idx] != null)
                {
                    var d = skillMapping[idx];
                    _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power}");
                }
            }, false);

            if (choice == -1 || choice == labels.Count - 1)
            {
                result.Cancelled = true;
                return result;
            }

            _skillMenuIndex = choice;

            // Check if user picked "Change Persona" (the second to last entry)
            if (hasChangeOption && choice == labels.Count - 2)
            {
                result.RequestSwap = true;
            }
            else
            {
                result.SelectedSkill = skillMapping[choice];
            }

            return result;
        }

        public string GetWildCardPersonaChoice(Combatant actor)
        {
            List<string> options = new List<string> { "Skills", "Change Persona", "Back" };

            // Disable "Change Persona" if the limit has been reached this turn
            List<bool> disabled = new List<bool> { false, actor.HasSwappedThisTurn, false };

            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nPERSONA COMMAND", options, 0, disabled);
            if (choice == -1 || choice == 2) return "Back";
            return options[choice];
        }

        /// <summary>
        /// Lists the Persona stock for the Wild Card to choose from.
        /// </summary>
        public Persona? SelectPersona(Combatant actor)
        {
            if (actor.PersonaStock == null || actor.PersonaStock.Count == 0)
            {
                _io.WriteLine("No other Personas available!");
                _io.Wait(800);
                return null;
            }

            int lastIdx = 0;
            while (true)
            {
                List<string> options = actor.PersonaStock
                    .Select(p => $"{p.Name,-15} (Lv.{p.Level})")
                    .ToList();
                options.Add("Back");

                // supportStatusInspect: true
                int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nCHOOSE PERSONA TO MANIFEST", options, lastIdx, null, null, true);

                if (choice == -1 || choice == options.Count - 1) return null;

                // Handle Inspect Signal
                if (choice <= -10)
                {
                    int inspectIdx = Math.Abs(choice) - 10;
                    ShowEntityStatus(actor.PersonaStock[inspectIdx]);
                    lastIdx = inspectIdx;
                    continue; // Loop back to selection
                }

                return actor.PersonaStock[choice];
            }
        }

        public List<Combatant>? SelectTarget(Combatant actor, SkillData? skill = null, ItemData? item = null, bool isTalk = false)
        {
            string context = GetBattleContext(actor);
            bool targetsAllies = false;
            bool targetsAll = false;
            Element element = Element.None;

            if (skill != null)
            {
                string nameLower = skill.Name.ToLower();
                string effect = skill.Effect.ToLower();

                // FIX: Self-Targeting logic for Charge skills
                if (nameLower.Contains("charge"))
                {
                    return new List<Combatant> { actor };
                }

                // FIX: Debuff Check for correct targeting side (Nda/Debilitate)
                bool isDebuff = nameLower.EndsWith("nda") || nameLower == "debilitate";
                bool isBuff = nameLower.EndsWith("kaja") || nameLower == "heat riser";

                targetsAllies = skill.Category.Contains("Recovery") ||
                    isBuff ||
                    effect.Contains("ally") ||
                    effect.Contains("allies") ||
                    effect.Contains("party");

                // Debuff logic overrides all: always target opponents
                if (isDebuff) targetsAllies = false;

                targetsAll = nameLower.StartsWith("ma") ||
                    nameLower.StartsWith("me") ||
                    effect.Contains("all foes") ||
                    effect.Contains("all allies") ||
                    effect.Contains("party") ||
                    nameLower == "debilitate";
                element = ElementHelper.FromCategory(skill.Category);
            }
            else if (item != null)
            {
                targetsAllies = true;
                targetsAll = item.Type == "Healing_All" || item.Name == "Amrita";
            }
            else if (isTalk)
            {
                targetsAllies = false;
                targetsAll = false;
            }

            var selectionPool = targetsAllies
                ? (item?.Type == "Revive" || (skill != null && skill.Effect.Contains("Revive")) ? _party.ActiveParty : _party.GetAliveMembers())
                : _enemies.Where(e => !e.IsDead).ToList();

            if (targetsAll)
            {
                // EFFECTIVENESS GATE (Multi-Target)
                // Prevents wasting a turn on group heals if everyone is healthy, or group ailments if all are afflicted.
                if (skill != null && _statusRegistry.IsActionRedundant(actor, skill, selectionPool))
                {
                    _io.WriteLine("This action would have no effect on any targets.", ConsoleColor.Yellow);
                    _io.Wait(1200);
                    return null;
                }
                return selectionPool;
            }

            List<string> targetLabels = new List<string>();
            foreach (var t in selectionPool)
            {
                string label = $"{t.Name} (HP: {t.CurrentHP}/{t.MaxHP})";
                if (!targetsAllies && skill != null)
                {
                    Affinity known = _knowledge.GetKnownAffinity(t.SourceId, element);
                    if (_knowledge.HasDiscovery(t.SourceId, element)) label += $" [{known.ToString().ToUpper()}]";
                }
                targetLabels.Add(label);
            }
            targetLabels.Add("Back");

            int choice = _io.RenderMenu($"{context}\nSelect Target:", targetLabels, 0);
            if (choice == -1 || choice == targetLabels.Count - 1) return null;

            Combatant selectedTarget = selectionPool[choice];

            // EFFECTIVENESS GATE (Single-Target)
            // Checks centralized logic in StatusRegistry. Correctly allows damaging skills (Toxic Sting).
            if (skill != null && _statusRegistry.IsActionRedundant(actor, skill, new List<Combatant> { selectedTarget }))
            {
                string reason = selectedTarget.CurrentHP >= selectedTarget.MaxHP ? "is healthy" : "already has that status";
                _io.WriteLine($"{selectedTarget.Name} {reason}.", ConsoleColor.Yellow);
                _io.Wait(1200);
                return null; // Return null to go back to previous menu and preserve turn icons
            }

            return new List<Combatant> { selectedTarget };
        }

        public string GetTacticsChoice(bool isBossBattle, bool isOperator)
        {
            List<string> options = new List<string> { "Escape", "Strategy", "Back" };
            List<bool> disabled = new List<bool> { isBossBattle, !isOperator, false };
            int choice = _io.RenderMenu($"{GetBattleContext(null)}\nTACTICS", options, 0, disabled);
            if (choice == -1 || choice == 2) return "Back";
            return options[choice];
        }

        public Combatant? SelectStrategyTarget()
        {
            var targets = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
            if (!targets.Any())
            {
                _io.WriteLine("No party members to command.");
                _io.Wait(800);
                return null;
            }
            var names = targets.Select(t => $"{t.Name} [{t.BattleControl}]").ToList();
            names.Add("Back");
            int choice = _io.RenderMenu($"{GetBattleContext(null)}\nSELECT DEMON TO COMMAND", names, 0);
            if (choice == -1 || choice == names.Count - 1) return null;
            return targets[choice];
        }

        /// <summary>
        /// Select Skill Menu. 
        /// Uses string-parsing lookup and disabled affordance list.
        /// </summary>
        public SkillData? SelectSkill(Combatant actor, string uiContext)
        {
            var skillNames = actor.GetConsolidatedSkills();
            if (skillNames.Count == 0) return null;

            List<string> labels = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var sName in skillNames)
            {
                if (Database.Skills.TryGetValue(sName, out var data))
                {
                    // Exclude Passive Skills from selection
                    if (data.Category == "Passive Skills") continue;

                    var cost = data.ParseCost();
                    bool canAfford = cost.isHP ? actor.CurrentHP > (int)(actor.MaxHP * (cost.value / 100.0)) : actor.CurrentSP >= cost.value;
                    labels.Add($"{sName} ({data.Cost})");
                    disabled.Add(!canAfford);
                }
            }
            labels.Add("Back");
            disabled.Add(false);

            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nSelect Skill:", labels, _skillMenuIndex, disabled, (idx) =>
            {
                if (idx >= 0 && idx < labels.Count - 1) // Adjusted for Passive removal
                {
                    string targetName = labels[idx].Split('(')[0].Trim();
                    if (Database.Skills.TryGetValue(targetName, out var d))
                        _io.WriteLine($"Effect: {d.Effect}\nPower: {d.Power}");
                }
            }, false);

            if (choice == -1 || choice == labels.Count - 1) return null;
            _skillMenuIndex = choice;

            string selectedName = labels[choice].Split('(')[0].Trim();
            return Database.Skills[selectedName];
        }

        public ItemData SelectItem(Combatant actor)
        {
            var ownedItems = Database.Items.Values.Where(i => _inv.GetQuantity(i.Id) > 0).ToList();
            if (!ownedItems.Any())
            {
                _io.WriteLine("Inventory is empty."); _io.Wait(800);
                return null;
            }

            List<string> labels = new List<string>();
            List<bool> disabled = new List<bool>();

            foreach (var item in ownedItems)
            {
                labels.Add($"{item.Name} x{_inv.GetQuantity(item.Id)}");

                // Disable Goho-M during battle
                bool battleForbidden = item.Name == "Goho-M";
                disabled.Add(battleForbidden);
            }
            labels.Add("Back");
            disabled.Add(false);

            int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nItems:", labels, _itemMenuIndex, disabled, (idx) =>
            {
                if (idx >= 0 && idx < ownedItems.Count)
                    _io.WriteLine(ownedItems[idx].Description);
            });

            if (choice == -1 || choice == labels.Count - 1) return null;
            _itemMenuIndex = choice;
            return ownedItems[choice];
        }

        /// <summary>
        /// Provides access to the COMP system for Operators.
        /// Updated for the Unified 12-Slot Model and Atomic Battle-Swapping.
        /// Handles the Inspect key loop safely in both standby and replacement selections.
        /// </summary>
        public (string action, Combatant? standby, Combatant? active) OpenCOMPMenu(Combatant actor)
        {
            int lastIdx = 0;
            while (true)
            {
                List<string> options = new List<string> { "Summon", "Return", "Analyze", "Back" };
                int choice = _io.RenderMenu($"{GetBattleContext(actor)}\nCOMP SYSTEM", options, lastIdx);

                if (choice == -1 || choice == options.Count - 1) return ("None", null, null);

                if (choice == 0) // Summon / Swap
                {
                    var allOwnedDemons = actor.DemonStock;

                    if (!allOwnedDemons.Any())
                    {
                        _io.WriteLine("No demons in COMP storage.");
                        _io.Wait(800);
                        lastIdx = 0; continue;
                    }

                    int stockIdx = 0;
                    while (true)
                    {
                        List<string> names = new List<string>();
                        List<bool> disabledSummons = new List<bool>();

                        foreach (var d in allOwnedDemons)
                        {
                            bool inParty = _party.ActiveParty.Contains(d);
                            string status = inParty ? "[IN PARTY]" : d.IsDead ? "[DEAD]" : "";

                            names.Add($"{d.Name,-15} Lv.{d.Level} {status}");
                    // Cannot summon if already in party OR if dead
                            disabledSummons.Add(inParty || d.IsDead);
                        }

                        names.Add("Back");
                        disabledSummons.Add(false);

                        // supportStatusInspect: true
                        int sub = _io.RenderMenu("Summon/Swap Demon:", names, stockIdx, disabledSummons, null, true);

                        if (sub == -1 || sub == names.Count - 1) break;

                        // Handle Inspect Signal
                        if (sub <= -10)
                        {
                            int inspectIdx = Math.Abs(sub) - 10;
                            ShowEntityStatus(allOwnedDemons[inspectIdx]);
                            stockIdx = inspectIdx;
                            continue;
                        }

                        Combatant standbyTarget = allOwnedDemons[sub];

                        if (_party.ActiveParty.Count < 4) return ("Summon", standbyTarget, null);

                        // Party is full: Selection to Replace with Peek Loop
                        List<Combatant> activeDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                        int activeIdx = 0;
                        while (true)
                        {
                            List<string> activeNames = activeDemons.Select(d => $"{d.Name,-15} (HP: {d.CurrentHP}/{d.MaxHP})").ToList();
                            activeNames.Add("Cancel");

                            // supportStatusInspect: true
                            int repIdx = _io.RenderMenu($"Replace who with {standbyTarget.Name}?", activeNames, activeIdx, null, null, true);

                            if (repIdx == -1 || repIdx == activeNames.Count - 1) { stockIdx = sub; break; }

                            if (repIdx <= -10)
                            {
                                int inspectActiveIdx = Math.Abs(repIdx) - 10;
                                ShowEntityStatus(activeDemons[inspectActiveIdx]);
                                activeIdx = inspectActiveIdx;
                                continue;
                            }

                            return ("Swap", standbyTarget, activeDemons[repIdx]);
                        }
                        continue;
                    }
                    lastIdx = 0; continue;
                }

                if (choice == 1) // Return
                {
                    var activeDemons = _party.ActiveParty.Where(c => c.Class == ClassType.Demon).ToList();
                    if (!activeDemons.Any())
                    {
                        _io.WriteLine("No active demons to return.");
                        _io.Wait(800); lastIdx = 1; continue;
                    }
                    int retIdx = 0;
                    while (true)
                    {
                        List<string> names = activeDemons.Select(d => d.Name).ToList();
                        names.Add("Back");

                        // supportStatusInspect: true
                        int sub = _io.RenderMenu("Return Demon:", names, retIdx, null, null, true);
                        if (sub == -1 || sub == names.Count - 1) break;

                        if (sub <= -10)
                        {
                            int inspectRetIdx = Math.Abs(sub) - 10;
                            ShowEntityStatus(activeDemons[inspectRetIdx]);
                            retIdx = inspectRetIdx;
                            continue;
                        }

                        return ("Return", null, activeDemons[sub]);
                    }
                    lastIdx = 1; continue;
                }

                if (choice == 2) // Analyze
                {
                    var targetList = SelectTarget(actor);
                    if (targetList == null) { lastIdx = 2; continue; }
                    return ("Analyze", null, targetList[0]);
                }
            }
        }

        /// <summary>
        /// Status Screen Renderer.
        /// Displays Level, Race, Parameters, Affinities, and Skill Set.
        /// </summary>
        private void ShowEntityStatus(object entity)
        {
            _io.Clear();
            string name = "Unknown";
            int level = 0;
            Dictionary<StatType, int> stats = new Dictionary<StatType, int>();
            List<string> skills = new List<string>();
            string race = "";

            if (entity is Combatant c)
            {
                name = c.Name; level = c.Level;
                race = c.ActivePersona?.Race ?? "Demon";
                foreach (StatType s in Enum.GetValues(typeof(StatType))) stats[s] = c.GetStat(s);
                skills = c.GetConsolidatedSkills();
            }
            else if (entity is Persona p)
            {
                name = p.Name; level = p.Level; race = p.Race;
                foreach (StatType s in Enum.GetValues(typeof(StatType))) stats[s] = p.StatModifiers.GetValueOrDefault(s, 0);
                skills = p.SkillSet;
            }

            _io.WriteLine($"=== STATUS: {name.ToUpper()} ===", ConsoleColor.Yellow);
            _io.WriteLine($"Race: {race,-15} | Level: {level}");
            _io.WriteLine("--------------------------------------------------");

            _io.WriteLine("STATS:");
            foreach (var stat in stats)
            {
                _io.WriteLine($" {stat.Key,-5}: {stat.Value}");
            }

            _io.WriteLine("\nRESISTANCES:");
            // Resolve Affinities from the Active Persona
            var activeP = (entity is Combatant com) ? com.ActivePersona : (Persona)entity;
            foreach (Element elem in Enum.GetValues(typeof(Element)))
            {
                if (elem == Element.None) continue;
                Affinity aff = activeP?.GetAffinity(elem) ?? Affinity.Normal;
                if (aff != Affinity.Normal)
                {
                    ConsoleColor color = aff == Affinity.Weak ? ConsoleColor.Red : ConsoleColor.Green;
                    _io.Write($" {elem,-10}: ");
                    _io.WriteLine($"{aff}", color);
                }
            }

            _io.WriteLine("\nSKILLS:");
            foreach (var s in skills) _io.WriteLine($" - {s}");

            _io.WriteLine("\n--------------------------------------------------");
            _io.WriteLine("Press any key to return...", ConsoleColor.Gray);
            _io.ReadKey();
        }

        public string GetBattleContext(Combatant actor)
        {
            string icons = $"Turns: {_turnEngine.GetIconsDisplay()}\n";
            string separator = "==================================================\n";

            string enemyGroup = "ENEMIES:\n";
            foreach (var e in _enemies)
            {
                string status = GetStatusIcons(e);
                enemyGroup += $" {e.Name,-15} {(e.IsDead ? "[DEAD]" : $"HP: {e.CurrentHP}")} {status}\n";
            }

            string partyGroup = "--------------------------------------------------\nPARTY:\n";
            foreach (var p in _party.ActiveParty)
            {
                string status = GetStatusIcons(p);
                partyGroup += $" {p.Name,-15} HP: {p.CurrentHP,4}/{p.MaxHP,4} SP: {p.CurrentSP,4}/{p.MaxSP,4} {status}\n";
            }
            return icons + separator + enemyGroup + partyGroup + separator;
        }

        private string GetStatusIcons(Combatant c)
        {
            string statusStr = "";

            // Render Buffs/Debuffs
            foreach (var buff in c.Buffs)
            {
                if (buff.Value == 0) continue;

                // Routing to specific UI labels for the independent tracks
                string key = buff.Key switch
                {
                    "PhysAtk" => "P-ATK",
                    "MagAtk" => "M-ATK",
                    "Defense" => "DEF",
                    "Agility" => "EVA",
                    _ => buff.Key.ToUpper()
                };

                string sign = buff.Value > 0 ? "+" : "-";
                // Formatting: [P-ATK+2], [M-ATK-1], [DEF-1], [EVA+3]
                statusStr += $"[{key}{sign}{Math.Abs(buff.Value)}]";
            }

            // Render Ailments/Guard
            if (c.CurrentAilment != null) statusStr += $"[{c.CurrentAilment.Name}]";
            if (c.IsGuarding) statusStr += "[Guard]";

            // Render Special States
            if (c.IsCharged) statusStr += "[Phys Charged]";
            if (c.IsMindCharged) statusStr += "[Mag Charged]";

            return statusStr;
        }
    }
}