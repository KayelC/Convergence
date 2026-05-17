using JRPGPrototype.Host;
using JRPGPrototype.Services;

namespace JRPGPrototype
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            IGameIO io = new ConsoleIO();
            ConsoleGameHost host = new ConsoleGameHost(io);

            host.Run(args);
        }
    }
}
