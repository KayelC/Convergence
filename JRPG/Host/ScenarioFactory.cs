using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host
{
    /// <summary>
    /// Builds the current prototype scenarios without making Program.cs own scenario content.
    /// </summary>
    internal static class ScenarioFactory
    {
        public static ScenarioSetupResult SelectAndApplyScenario(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            IGameIO io,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium)
        {
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

            switch (key.KeyChar)
            {
                case '1':
                    player.Class = ClassType.Human;
                    return ScenarioSetupResult.Continue;

                case '2':
                    player.Class = ClassType.PersonaUser;
                    if (Database.Personas.TryGetValue("orpheus", out var personaUserPersona))
                    {
                        player.ActivePersona = personaUserPersona.ToPersona();
                    }
                    return ScenarioSetupResult.Continue;

                case '3':
                    player.Class = ClassType.WildCard;
                    if (Database.Personas.TryGetValue("orpheus", out var wildCardPersona))
                    {
                        player.ActivePersona = wildCardPersona.ToPersona();
                    }
                    if (Database.Personas.TryGetValue("pixie", out var pixie))
                    {
                        player.PersonaStock.Add(pixie.ToPersona());
                    }
                    if (Database.Personas.TryGetValue("high_pixie", out var highPixie))
                    {
                        player.PersonaStock.Add(highPixie.ToPersona());
                    }
                    return ScenarioSetupResult.Continue;

                case '4':
                    player.Class = ClassType.Operator;
                    AddOperatorStock(player);
                    return ScenarioSetupResult.Continue;

                case '5':
                    ConfigureDebugBattlePlayer(player);
                    return ScenarioSetupResult.DebugBattle;

                case '6':
                    DebugScenarioRunner.RunMonteCarloSimulation(io);
                    return ScenarioSetupResult.Exit;

                case '7':
                    DebugScenarioRunner.RunCompendiumAutoRegistrationTest(
                        player,
                        inventory,
                        economy,
                        io,
                        playerKnowledge,
                        compendium);
                    return ScenarioSetupResult.Exit;

                case '8':
                    DebugScenarioRunner.RunUnifiedStockModelTest(
                        player,
                        inventory,
                        economy,
                        io,
                        playerKnowledge,
                        compendium);
                    return ScenarioSetupResult.Exit;

                default:
                    return ScenarioSetupResult.Continue;
            }
        }

        public static void ApplyStandardPrototypeSetup(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy)
        {
            player.Level = 80;
            player.StatPoints = 5;

            player.RecalculateResources();

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

            if (Database.Weapons.TryGetValue("1", out var weapon))
            {
                player.EquippedWeapon = weapon;
            }
            if (Database.Armors.TryGetValue("201", out var armor))
            {
                player.EquippedArmor = armor;
            }
            if (Database.Boots.TryGetValue("301", out var boots))
            {
                player.EquippedBoots = boots;
            }
            if (Database.Accessories.TryGetValue("401", out var accessory))
            {
                player.EquippedAccessory = accessory;
            }

            economy.AddMacca(5000000);

            for (int i = 0; i < 4; i++)
            {
                MoonPhaseSystem.Advance();
            }
        }

        private static void AddOperatorStock(Combatant player)
        {
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
        }

        private static void ConfigureDebugBattlePlayer(Combatant player)
        {
            player.Class = ClassType.WildCard;
            player.Level = 50;

            if (Database.Personas.TryGetValue("orpheus", out var debugPersonaData))
            {
                var debugPersona = debugPersonaData.ToPersona();

                debugPersona.SkillSet.Clear();
                debugPersona.SkillSet.Add("Dormina");
                debugPersona.SkillSet.Add("Lullaby");
                debugPersona.SkillSet.Add("Shibaboo");
                debugPersona.SkillSet.Add("Binding Cry");
                debugPersona.SkillSet.Add("Bash");
                debugPersona.SkillSet.Add("Stun Needle");
                debugPersona.SkillSet.Add("Toxic Sting");
                debugPersona.SkillSet.Add("Venom Bite");
                debugPersona.SkillSet.Add("Patra");
                debugPersona.SkillSet.Add("Tarukaja");
                debugPersona.SkillSet.Add("Makakaja");
                debugPersona.SkillSet.Add("Sukukaja");
                debugPersona.SkillSet.Add("Rakukaja");
                debugPersona.SkillSet.Add("Sukunda");

                player.ActivePersona = debugPersona;
            }
        }
    }
}
