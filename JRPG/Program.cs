using JRPGPrototype.Host;
using JRPGPrototype.Host.CleanConsole.TrainingAnnex;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Contains("--clean-battle-demo", StringComparer.Ordinal))
            {
                return await new CleanBattleDemoHost(Console.Out).RunAsync().ConfigureAwait(false);
            }

            if (args.Contains("--clean-field-demo", StringComparer.Ordinal))
            {
                return await new CleanFieldDemoHost(Console.Out).RunAsync().ConfigureAwait(false);
            }

            if (args.Contains("--clean-save-demo", StringComparer.Ordinal))
            {
                return await new CleanSaveDemoHost(Console.Out).RunAsync().ConfigureAwait(false);
            }

            if (args.Contains("--clean-training-annex-demo", StringComparer.Ordinal))
            {
                return await new CleanTrainingAnnexDemoHost(Console.Out).RunAsync().ConfigureAwait(false);
            }

            if (args.Contains("--clean-training-annex-play", StringComparer.Ordinal))
            {
                return await new CleanTrainingAnnexPlayHost(new ConsoleIO()).RunAsync().ConfigureAwait(false);
            }

            IGameIO io = new ConsoleIO();
            ConsoleGameHost host = new ConsoleGameHost(io);

            host.Run(args);
            return 0;
        }
    }
}
