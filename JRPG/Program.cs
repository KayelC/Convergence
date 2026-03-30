using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            IGameIO io = new ConsoleIO();
            io.WriteLine("=== JRPG PROTOTYPE INITIALIZING ===");

            Database.LoadData(io);

            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();
            DungeonState dungeonState = new DungeonState();
            CompendiumRegistry compendium = new CompendiumRegistry(io);

            Combatant player = new Combatant("Hero");

            // Persistent Knowledge Bank: This allows the player to "remember" affinities 
            // across different battles in the same session.
            BattleKnowledge playerKnowledge = new BattleKnowledge();

            player.StatPoints = 0;

            // Scenario Logic
            io.WriteLine("Select Test Scenario:");
            io.WriteLine("1. Human (Basic)");
            io.WriteLine("2. Persona User (Orpheus)");
            io.WriteLine("3. Wild Card (Orpheus + Stock)");
            io.WriteLine("4. Operator (Demons + COMP)");
            io.WriteLine("5. DEBUG: Battle Simulator");
            io.WriteLine("6. MONTE CARLO: Fusion & Curse Gate Stress Test");
            io.WriteLine("7. TEST: Compendium Auto-Registration");
            io.WriteLine("8. TEST: Unified 12-Slot Stock Model");

            var key = io.ReadKey();
            bool jumpToBattle = false; // Flag to skip field menus for debugging

            switch (key.KeyChar)
            {
                case '1':
                    player.Class = ClassType.Human;
                    break;
                case '2':
                    player.Class = ClassType.PersonaUser;
                    if (Database.Personas.TryGetValue("orpheus", out var p1))
                        player.ActivePersona = p1.ToPersona();
                    break;
                case '3':
                    player.Class = ClassType.WildCard;
                    if (Database.Personas.TryGetValue("orpheus", out var p2))
                        player.ActivePersona = p2.ToPersona();
                    if (Database.Personas.TryGetValue("pixie", out var p3))
                        player.PersonaStock.Add(p3.ToPersona());
                    if (Database.Personas.TryGetValue("high_pixie", out var p4))
                        player.PersonaStock.Add(p4.ToPersona());
                    break;
                case '4':
                    player.Class = ClassType.Operator;
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("michael", 99));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("pixie", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("high_pixie", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("orpheus", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("io", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("hermes", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("medea", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("mou_ryo", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("flaemis", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("aquans", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("erthrys", 50));
                    player.DemonStock.Add(CombatantFactory.CreatePlayerDemon("yurlungur", 50));
                    break;

                case '5':
                    // DEBUG SUITE SETUP
                    player.Class = ClassType.WildCard;
                    player.Level = 50;
                    // Give the player a custom persona with the tools needed to test
                    if (Database.Personas.TryGetValue("orpheus", out var debugP))
                    {
                        var pInstance = debugP.ToPersona();

                        // Inject specific skills for testing
                        pInstance.SkillSet.Clear();

                        pInstance.SkillSet.Add("Dormina");
                        pInstance.SkillSet.Add("Lullaby");
                        pInstance.SkillSet.Add("Shibaboo");
                        pInstance.SkillSet.Add("Binding Cry");
                        pInstance.SkillSet.Add("Bash");
                        pInstance.SkillSet.Add("Stun Needle");
                        pInstance.SkillSet.Add("Toxic Sting");
                        pInstance.SkillSet.Add("Venom Bite");
                        pInstance.SkillSet.Add("Patra");
                        pInstance.SkillSet.Add("Tarukaja");
                        pInstance.SkillSet.Add("Makakaja");
                        pInstance.SkillSet.Add("Sukukaja");
                        pInstance.SkillSet.Add("Rakukaja");
                        pInstance.SkillSet.Add("Sukunda");


                        player.ActivePersona = pInstance;
                    }
                    jumpToBattle = true; // Signal to jump straight to combat
                    break;

                case '6':
                    RunMonteCarloSimulation(io);
                    return;

                case '7':
                    // --- TASK 4 TEST CASE SETUP ---
                    player.Class = ClassType.Operator;
                    player.Level = 10;
                    economy.AddMacca(5000);

                    io.Clear();
                    io.WriteLine("=== SCENARIO 7: COMPENDIUM AUTO-SAVE TEST ===", ConsoleColor.Yellow);
                    io.WriteLine("1. Start battle with a Pixie.");
                    io.WriteLine("2. Use 'Talk' to recruit her.");
                    io.WriteLine("3. After battle, the code will check if she is in the Compendium.");
                    io.WriteLine("Press any key to begin encounter...");
                    io.ReadKey();

                    List<Combatant> testEnemies7 = new List<Combatant> {
                        CombatantFactory.CreateEnemy("pixie")
                    };

                    PartyManager testPm7 = new PartyManager(player);
                    BattleConductor autoRegBattle = new BattleConductor(testPm7, testEnemies7, inventory, economy, io, playerKnowledge, compendium, false);
                    autoRegBattle.StartBattle();

                    io.Clear();
                    io.WriteLine("=== POST-BATTLE REGISTRY CHECK ===", ConsoleColor.Yellow);
                    var registered = compendium.GetAllRegisteredDemons();
                    if (registered.Count > 0)
                    {
                        foreach (var entry in registered)
                        {
                            io.WriteLine($"[FOUND] {entry.Name} (Lv.{entry.Level}) was automatically snapshotted!", ConsoleColor.Green);
                        }
                    }
                    else
                    {
                        io.WriteLine("[FAILED] No demons were registered in the Compendium.", ConsoleColor.Red);
                    }
                    io.WriteLine("\nPress any key to exit test.");
                    io.ReadKey();
                    return;

                case '8':
                    // --- TASK 4 UNIFIED STOCK TEST ---
                    player.Class = ClassType.Operator;
                    player.Level = 25; // At Lv 25, CalculateMaxStock returns 12 slots
                    player.CurrentHP = 5000;
                    player.CurrentSP = 5000;
                    PartyManager testPm8 = new PartyManager(player);

                    // Add 5 demons to the Master Stock (DemonStock)
                    var d1 = CombatantFactory.CreatePlayerDemon("michael", 25);
                    var d2 = CombatantFactory.CreatePlayerDemon("pixie", 25);
                    var d3 = CombatantFactory.CreatePlayerDemon("high_pixie", 25);
                    var d4 = CombatantFactory.CreatePlayerDemon("orpheus", 25);
                    var d5 = CombatantFactory.CreatePlayerDemon("angel", 25);

                    player.DemonStock.Add(d1);
                    player.DemonStock.Add(d2);
                    player.DemonStock.Add(d3);
                    player.DemonStock.Add(d4);
                    player.DemonStock.Add(d5);

                    // Deploy 3 to the Active Party.
                    // Under the Unified Model, they stay in player.DemonStock but appear in testPm8.ActiveParty.
                    testPm8.SummonDemon(player, d1);
                    testPm8.SummonDemon(player, d2);
                    testPm8.SummonDemon(player, d3);

                    io.Clear();
                    io.WriteLine("=== SCENARIO 8: UNIFIED 12-SLOT MODEL TEST ===", ConsoleColor.Yellow);
                    io.WriteLine($"Total COMP Ownership: {player.DemonStock.Count} / 12");
                    io.WriteLine($"Active Party Count (incl. Leader): {testPm8.ActiveParty.Count}");

                    io.WriteLine("\n[LOGIC CHECK]");
                    io.WriteLine("The 3 active demons MUST still exist in the master DemonStock list.");
                    int overlapping = player.DemonStock.Count(d => testPm8.ActiveParty.Contains(d));
                    io.WriteLine($"Overlap Count: {overlapping} (Expected: 3)", overlapping == 3 ? ConsoleColor.Green : ConsoleColor.Red);

                    io.WriteLine("\n[UI CHECK]");
                    io.WriteLine("Entering battle. Open 'COMP' -> 'Summon'.");
                    io.WriteLine("Michael, Pixie, and High Pixie should be grayed out as [IN PARTY].");
                    io.WriteLine("Orpheus and Angel should be summonable.");
                    io.WriteLine("Press any key to enter battle...");
                    io.ReadKey();

                    List<Combatant> testEnemies8 = new List<Combatant> { CombatantFactory.CreateEnemy("E_slime") };
                    BattleConductor stockTestBattle = new BattleConductor(testPm8, testEnemies8, inventory, economy, io, playerKnowledge, compendium, false);
                    stockTestBattle.StartBattle();
                    return;
            }

            // Default Setup Logic for standard scenarios
            player.Level = 80;
            player.StatPoints = 5;

            player.RecalculateResources();

            //player.CurrentHP = player.MaxHP;
            //player.CurrentSP = player.MaxSP;

            player.CurrentHP = 5000;
            player.CurrentSP = 5000;

            inventory.AddItem("101", 5);
            inventory.AddItem("108", 2);
            inventory.AddItem("114", 3);
            inventory.AddItem("113", 3);
            inventory.AddEquipment("1", ShopCategory.Weapon);
            inventory.AddEquipment("201", ShopCategory.Armor);
            inventory.AddEquipment("301", ShopCategory.Boots);
            inventory.AddEquipment("401", ShopCategory.Accessory);

            if (Database.Weapons.TryGetValue("1", out var w)) player.EquippedWeapon = w;
            if (Database.Armors.TryGetValue("201", out var a)) player.EquippedArmor = a;
            if (Database.Boots.TryGetValue("301", out var b)) player.EquippedBoots = b;
            if (Database.Accessories.TryGetValue("401", out var acc))
                player.EquippedAccessory = acc;

            economy.AddMacca(5000000);

            // Advance Moon to a neutral phase for standard testing
            for (int i = 0; i < 4; i++)
            {
                MoonPhaseSystem.Advance();
            }

            // DEBUG INJECTION: If Scenario 5 is chosen, run a controlled battle immediately
            if (jumpToBattle)
            {
                io.Clear();
                io.WriteLine("=== DEBUG SESSION: AILMENT & TECHNICAL TESTING ===", ConsoleColor.Yellow);
                io.WriteLine("Testing Sleep (Wake on Hit), Bind (No Skills), Stun (1-Turn), and Phys Techs.");

                // Create a "Target Dummy" Enemy
                List<Combatant> enemies = new List<Combatant> {
                    CombatantFactory.CreateEnemy("E_slime")
                };
                enemies[0].Name = "Target Dummy";

                // Override stats for a long-lasting test subject
                enemies[0].BaseHP = 9999;
                enemies[0].CurrentHP = 9999;
                // Set stats to 1 so the dummy doesn't accidentally kill the tester
                foreach (var stat in Enum.GetValues(typeof(StatType)))
                {
                    enemies[0].CharacterStats[(StatType)stat] = 1;
                }

                PartyManager pm = new PartyManager(player);
                BattleConductor debugBattle = new BattleConductor(pm, enemies, inventory, economy, io, playerKnowledge, compendium, false);
                debugBattle.StartBattle();

                io.WriteLine("\nDebug Battle Concluded. Press any key to exit.");
                io.ReadKey();
                return; // End the program after the debug battle
            }

            FieldConductor field = new FieldConductor(
                player,
                inventory,
                economy,
                dungeonState,
                io,
                playerKnowledge,
                compendium
            );

            bool appRunning = true;
            while (appRunning)
            {
                field.NavigateMenus();

                if (player.CurrentHP <= 0)
                {
                    io.Clear();
                    io.WriteLine("\n[GAME OVER] You have collapsed...", ConsoleColor.Red);
                    io.Wait(2000);
                    io.WriteLine("You are dragged back to the entrance by a mysterious force.");
                    io.Wait(2000);

                    player.CurrentHP = 1;
                    player.RemoveAilment();
                    player.CleanupBattleState();

                    dungeonState.ResetToEntry();
                }
                else
                {
                    appRunning = false;
                }
            }

            io.Clear();
            io.WriteLine("\n[GAME SESSION ENDED]", ConsoleColor.Red);
            io.WriteLine("Press any key to exit...");
            io.ReadKey();
        }

        static void RunMonteCarloSimulation(IGameIO io)
        {
            io.Clear();
            io.WriteLine("=== STARTING MONTE CARLO SIMULATION (10,000 TRIALS) ===", ConsoleColor.Cyan);

            IFusionMessenger messenger = new FusionMessenger();
            FusionCalculator calculator = new FusionCalculator(io, messenger);
            Random rnd = new Random();

            int totalTrials = 10000;

            int accidents = 0;
            int mutationsAttempted = 0;
            int mutationsSucceeded = 0;
            int rankUps = 0;
            int rankDowns = 0;
            int curseGateTrials = 0;
            int curseGateSuccesses = 0;

            // Using Angel + Pixie (Valid recipe for Fallen result)
            //Combatant parentA = CombatantFactory.CreatePlayerDemon("angel", 10);
            Combatant parentB = CombatantFactory.CreatePlayerDemon("pixie", 10);
            Combatant parentA = CombatantFactory.CreatePlayerDemon("michael", 10);

            Combatant boss = CombatantFactory.CreateEnemy("E_slime");
            if (boss.ActivePersona != null)
                boss.ActivePersona.AffinityMap[Element.Curse] = Affinity.Null;

            io.WriteLine("Running Simulation...");

            for (int i = 0; i < totalTrials; i++)
            {
                var result = calculator.CalculateResult(parentA, parentB, 8);
                if (result.isAccident) accidents++;

                if (result.isAccident)
                {
                    var pickable = calculator.GetInheritableSkills(parentA, parentB);
                    int maxSlots = calculator.GetInheritanceSlotCount(parentA, parentB);
                    var sample = pickable.Take(maxSlots).ToList();

                    foreach (var skill in sample)
                    {
                        mutationsAttempted++;
                        if (rnd.Next(0, 100) < 20)
                        {
                            mutationsSucceeded++;
                            string mutated = calculator.GetMutatedSkill(skill);

                            Database.Skills.TryGetValue(skill, out var oldData);
                            Database.Skills.TryGetValue(mutated, out var newData);

                            if (oldData != null && newData != null)
                            {
                                // Using TryParse to handle non-numeric ranks like "-"
                                if (int.TryParse(oldData.Rank, out int oldR) && int.TryParse(newData.Rank, out int newR))
                                {
                                    if (newR > oldR) rankUps++;
                                    else if (newR < oldR) rankDowns++;
                                }
                            }
                        }
                    }
                }

                curseGateTrials++;
                if (CombatMath.CalculateInstantKill(parentA, boss, "100%"))
                {
                    curseGateSuccesses++;
                }
            }

            io.WriteLine("\n=== SIMULATION RESULTS ===", ConsoleColor.Yellow);
            io.WriteLine($"Total Trials: {totalTrials}");
            io.WriteLine($"Accident Rate (Full Moon): {(double)accidents / totalTrials:P2} (Expected ~12%)");
            io.WriteLine($"Mutation Chance: {(double)mutationsSucceeded / mutationsAttempted:P2} (Expected ~20%)");
            io.WriteLine($"Mutation Balance: Ups: {rankUps} | Downs: {rankDowns}");
            io.WriteLine($"Curse Gate Breaches: {curseGateSuccesses} / {curseGateTrials} (Expected: 0)");

            if (curseGateSuccesses == 0) io.WriteLine("CURSE GATE: VERIFIED", ConsoleColor.Green);
            else io.WriteLine("CURSE GATE: FAILED", ConsoleColor.Red);

            io.WriteLine("\nPress any key to return to menu.");
            io.ReadKey();
        }
    }
}