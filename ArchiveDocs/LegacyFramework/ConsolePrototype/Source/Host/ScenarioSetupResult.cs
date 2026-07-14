namespace JRPGPrototype.Host
{
    internal readonly record struct ScenarioSetupResult(bool ShouldExit, bool JumpToDebugBattle)
    {
        public static ScenarioSetupResult Continue { get; } = new ScenarioSetupResult(false, false);
        public static ScenarioSetupResult Exit { get; } = new ScenarioSetupResult(true, false);
        public static ScenarioSetupResult DebugBattle { get; } = new ScenarioSetupResult(false, true);
    }
}
