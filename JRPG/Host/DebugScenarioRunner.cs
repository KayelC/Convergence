using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host
{
    internal delegate void LegacyDebugBattleRunner(
        PartyManager party,
        List<Combatant> enemies,
        bool isBoss);

    internal readonly record struct MonteCarloSimulationSummary(
        int TotalTrials,
        int Accidents,
        int MutationsAttempted,
        int MutationsSucceeded,
        int RankUps,
        int RankDowns,
        int CurseGateTrials,
        int CurseGateSuccesses);

    /// <summary>
    /// Prototype-only debug and validation scenarios for the console host.
    /// </summary>
    internal static class DebugScenarioRunner
    {
        public static void RunAilmentTechnicalBattle(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium)
        {
            RunAilmentTechnicalBattle(
                player,
                inventory,
                economy,
                io,
                playerKnowledge,
                compendium,
                (party, enemies, isBoss) =>
                {
                    BattleConductor battle = new BattleConductor(
                        party,
                        enemies,
                        inventory,
                        economy,
                        io,
                        playerKnowledge,
                        compendium,
                        isBoss);
                    battle.StartBattle();
                });
        }

        internal static void RunAilmentTechnicalBattle(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium,
            LegacyDebugBattleRunner battleRunner)
        {
            io.Clear();
            io.WriteLine("=== DEBUG SESSION: AILMENT & TECHNICAL TESTING ===", ConsoleColor.Yellow);
            io.WriteLine("Testing Sleep (Wake on Hit), Bind (No Skills), Stun (1-Turn), and Phys Techs.");

            List<Combatant> enemies = new List<Combatant>
            {
                CombatantFactory.CreateEnemy("E_slime")
            };
            enemies[0].Name = "Target Dummy";

            enemies[0].BaseHP = 9999;
            enemies[0].CurrentHP = 9999;
            foreach (var stat in Enum.GetValues(typeof(StatType)))
            {
                enemies[0].CharacterStats[(StatType)stat] = 1;
            }

            PartyManager partyManager = new PartyManager(player);
            battleRunner(partyManager, enemies, false);

            io.WriteLine("\nDebug Battle Concluded. Press any key to exit.");
            io.ReadKey();
        }

        public static void RunMonteCarloSimulation(IGameIO io)
        {
            _ = RunMonteCarloSimulation(
                io,
                totalTrials: 10000,
                fusionRandom: new Random(),
                mutationRandom: new Random(),
                waitForInput: true);
        }

        internal static MonteCarloSimulationSummary RunMonteCarloSimulation(
            IGameIO io,
            int totalTrials,
            Random fusionRandom,
            Random mutationRandom,
            bool waitForInput)
        {
            if (totalTrials <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalTrials));
            }

            io.Clear();
            io.WriteLine($"=== STARTING MONTE CARLO SIMULATION ({totalTrials:N0} TRIALS) ===", ConsoleColor.Cyan);

            IFusionMessenger messenger = new FusionMessenger();
            FusionCalculator calculator = new FusionCalculator(io, messenger, fusionRandom);
            Random rnd = mutationRandom;

            int accidents = 0;
            int mutationsAttempted = 0;
            int mutationsSucceeded = 0;
            int rankUps = 0;
            int rankDowns = 0;
            int curseGateTrials = 0;
            int curseGateSuccesses = 0;

            Combatant parentB = CombatantFactory.CreatePlayerDemon("pixie", 10);
            Combatant parentA = CombatantFactory.CreatePlayerDemon("michael", 10);

            Combatant boss = CombatantFactory.CreateEnemy("E_slime");
            if (boss.ActivePersona != null)
            {
                boss.ActivePersona.AffinityMap[Element.Curse] = Affinity.Null;
            }

            io.WriteLine("Running Simulation...");

            for (int i = 0; i < totalTrials; i++)
            {
                var result = calculator.CalculateResult(parentA, parentB, 8);
                if (result.isAccident)
                {
                    accidents++;
                }

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
                                if (int.TryParse(oldData.Rank, out int oldRank) &&
                                    int.TryParse(newData.Rank, out int newRank))
                                {
                                    if (newRank > oldRank)
                                    {
                                        rankUps++;
                                    }
                                    else if (newRank < oldRank)
                                    {
                                        rankDowns++;
                                    }
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
            double mutationChance = mutationsAttempted == 0
                ? 0
                : (double)mutationsSucceeded / mutationsAttempted;
            io.WriteLine($"Mutation Chance: {mutationChance:P2} (Expected ~20%)");
            io.WriteLine($"Mutation Balance: Ups: {rankUps} | Downs: {rankDowns}");
            io.WriteLine($"Curse Gate Breaches: {curseGateSuccesses} / {curseGateTrials} (Expected: 0)");

            if (curseGateSuccesses == 0)
            {
                io.WriteLine("CURSE GATE: VERIFIED", ConsoleColor.Green);
            }
            else
            {
                io.WriteLine("CURSE GATE: FAILED", ConsoleColor.Red);
            }

            if (waitForInput)
            {
                io.WriteLine("\nPress any key to return to menu.");
                io.ReadKey();
            }

            return new MonteCarloSimulationSummary(
                totalTrials,
                accidents,
                mutationsAttempted,
                mutationsSucceeded,
                rankUps,
                rankDowns,
                curseGateTrials,
                curseGateSuccesses);
        }

        public static void RunCompendiumAutoRegistrationTest(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium)
        {
            RunCompendiumAutoRegistrationTest(
                player,
                inventory,
                economy,
                io,
                playerKnowledge,
                compendium,
                (party, enemies, isBoss) =>
                {
                    BattleConductor battle = new BattleConductor(
                        party,
                        enemies,
                        inventory,
                        economy,
                        io,
                        playerKnowledge,
                        compendium,
                        isBoss);
                    battle.StartBattle();
                });
        }

        internal static void RunCompendiumAutoRegistrationTest(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium,
            LegacyDebugBattleRunner battleRunner)
        {
            player.Class = ClassType.Operator;
            player.Level = 10;
            PrepareStandaloneBattlePlayer(player);
            economy.AddMacca(5000);

            io.Clear();
            io.WriteLine("=== SCENARIO 7: COMPENDIUM AUTO-SAVE TEST ===", ConsoleColor.Yellow);
            io.WriteLine("1. Start battle with a Pixie.");
            io.WriteLine("2. Use 'Talk' to recruit her.");
            io.WriteLine("3. After battle, the code will check if she is in the Compendium.");
            io.WriteLine("Press any key to begin encounter...");
            io.ReadKey();

            List<Combatant> testEnemies = new List<Combatant>
            {
                CombatantFactory.CreateEnemy("pixie")
            };

            PartyManager partyManager = new PartyManager(player);
            battleRunner(partyManager, testEnemies, false);

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
        }

        public static void RunUnifiedStockModelTest(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium)
        {
            RunUnifiedStockModelTest(
                player,
                inventory,
                economy,
                io,
                playerKnowledge,
                compendium,
                (party, enemies, isBoss) =>
                {
                    BattleConductor battle = new BattleConductor(
                        party,
                        enemies,
                        inventory,
                        economy,
                        io,
                        playerKnowledge,
                        compendium,
                        isBoss);
                    battle.StartBattle();
                });
        }

        internal static void RunUnifiedStockModelTest(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium,
            LegacyDebugBattleRunner battleRunner)
        {
            player.Class = ClassType.Operator;
            player.Level = 25;
            PrepareStandaloneBattlePlayer(player);
            PartyManager partyManager = new PartyManager(player);

            var michael = CombatantFactory.CreatePlayerDemon("michael", 25);
            var pixie = CombatantFactory.CreatePlayerDemon("pixie", 25);
            var highPixie = CombatantFactory.CreatePlayerDemon("high_pixie", 25);
            var orpheus = CombatantFactory.CreatePlayerDemon("orpheus", 25);
            var angel = CombatantFactory.CreatePlayerDemon("angel", 25);

            player.DemonStock.Add(michael);
            player.DemonStock.Add(pixie);
            player.DemonStock.Add(highPixie);
            player.DemonStock.Add(orpheus);
            player.DemonStock.Add(angel);

            partyManager.SummonDemon(player, michael);
            partyManager.SummonDemon(player, pixie);
            partyManager.SummonDemon(player, highPixie);

            io.Clear();
            io.WriteLine("=== SCENARIO 8: UNIFIED 12-SLOT MODEL TEST ===", ConsoleColor.Yellow);
            io.WriteLine($"Total COMP Ownership: {player.DemonStock.Count} / 12");
            io.WriteLine($"Active Party Count (incl. Leader): {partyManager.ActiveParty.Count}");

            io.WriteLine("\n[LOGIC CHECK]");
            io.WriteLine("The 3 active demons MUST still exist in the master DemonStock list.");
            int overlapping = player.DemonStock.Count(d => partyManager.ActiveParty.Contains(d));
            io.WriteLine($"Overlap Count: {overlapping} (Expected: 3)", overlapping == 3 ? ConsoleColor.Green : ConsoleColor.Red);

            io.WriteLine("\n[UI CHECK]");
            io.WriteLine("Entering battle. Open 'COMP' -> 'Summon'.");
            io.WriteLine("Michael, Pixie, and High Pixie should be grayed out as [IN PARTY].");
            io.WriteLine("Orpheus and Angel should be summonable.");
            io.WriteLine("Press any key to enter battle...");
            io.ReadKey();

            List<Combatant> testEnemies = new List<Combatant>
            {
                CombatantFactory.CreateEnemy("E_slime")
            };
            battleRunner(partyManager, testEnemies, false);
        }

        private static void PrepareStandaloneBattlePlayer(Combatant player)
        {
            player.RecalculateResources();
            player.CurrentHP = 5000;
            player.CurrentSP = 5000;
        }
    }
}
