using JRPGPrototype.Host;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            IGameIO io = new ConsoleIO();
            ConsoleGameHost host = new ConsoleGameHost(io);

            host.Run(args);
            await Task.CompletedTask.ConfigureAwait(false);
            return 0;
        }
    }
}
