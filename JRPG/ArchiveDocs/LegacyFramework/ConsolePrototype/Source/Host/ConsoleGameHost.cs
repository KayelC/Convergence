using System;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.Dungeon;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Hosting;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host
{
    /// <summary>
    /// Console host entry flow for the current prototype application.
    /// This class wires runtime services together and delegates gameplay to conductors.
    /// </summary>
    internal sealed class ConsoleGameHost
    {
        private readonly IGameIO _io;
        private readonly InteractiveConsoleHostContextFactory _contextFactory;

        public ConsoleGameHost(
            IGameIO io,
            IContentPackTextSource? contentSource = null,
            IHostEventSink<string>? eventSink = null)
        {
            _io = io;
            _contextFactory = new InteractiveConsoleHostContextFactory(
                contentSource ?? new LegacyFileContentPackSource(Path.Combine(AppContext.BaseDirectory, "Data", "Jsons")),
                eventSink ?? new GameIoEventSink(io));
        }

        internal InteractiveConsoleHostContext? LastStartupContext { get; private set; }

        public void Run(string[] args) =>
            RunAsync(args).GetAwaiter().GetResult();

        public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
        {
            _io.WriteLine("=== JRPG PROTOTYPE INITIALIZING ===");

            Database.LoadData(_io);
            LastStartupContext = await _contextFactory.CreateAsync(_io, cancellationToken)
                .ConfigureAwait(false);

            InventoryManager inventory = new InventoryManager();
            EconomyManager economy = new EconomyManager();
            DungeonState dungeonState = new DungeonState();
            CompendiumRegistry compendium = new CompendiumRegistry(_io);
            Combatant player = new Combatant("Hero");
            BattleKnowledge playerKnowledge = new BattleKnowledge();

            player.StatPoints = 0;

            ScenarioSetupResult setupResult = ScenarioFactory.SelectAndApplyScenario(
                player,
                inventory,
                economy,
                _io,
                playerKnowledge,
                compendium);

            if (setupResult.ShouldExit)
            {
                return;
            }

            ScenarioFactory.ApplyStandardPrototypeSetup(player, inventory, economy);

            if (setupResult.JumpToDebugBattle)
            {
                DebugScenarioRunner.RunAilmentTechnicalBattle(
                    player,
                    inventory,
                    economy,
                    _io,
                    playerKnowledge,
                    compendium);
                return;
            }

            RunFieldLoop(player, inventory, economy, dungeonState, playerKnowledge, compendium);
        }

        private void RunFieldLoop(
            Combatant player,
            InventoryManager inventory,
            EconomyManager economy,
            DungeonState dungeonState,
            BattleKnowledge playerKnowledge,
            CompendiumRegistry compendium)
        {
            FieldConductor field = new FieldConductor(
                player,
                inventory,
                economy,
                dungeonState,
                _io,
                playerKnowledge,
                compendium);

            bool appRunning = true;
            while (appRunning)
            {
                field.NavigateMenus();

                if (player.CurrentHP <= 0)
                {
                    _io.Clear();
                    _io.WriteLine("\n[GAME OVER] You have collapsed...", ConsoleColor.Red);
                    _io.Wait(2000);
                    _io.WriteLine("You are dragged back to the entrance by a mysterious force.");
                    _io.Wait(2000);

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

            _io.Clear();
            _io.WriteLine("\n[GAME SESSION ENDED]", ConsoleColor.Red);
            _io.WriteLine("Press any key to exit...");
            _io.ReadKey();
        }
    }
}
