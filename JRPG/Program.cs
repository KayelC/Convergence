using JRPGPrototype.Host;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Contains("--clean-battle-demo", StringComparer.Ordinal))
            {
                return new CleanBattleDemoHost(Console.Out).Run();
            }

            IGameIO io = new ConsoleIO();
            ConsoleGameHost host = new ConsoleGameHost(io);

            host.Run(args);
            return 0;
        }
    }
}
