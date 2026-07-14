using System.Reflection;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Architecture;

public sealed class FrameworkNeutralityTests
{
    [Fact]
    public void FrameworkSources_DoNotOwnPrototypeCurrencyNegotiationOrDungeonCompatibility()
    {
        string frameworkRoot = RepositoryPath("src", "Convergence.Framework");
        string[] forbiddenTokens =
        [
            "Macca",
            "Full Moon",
            "Medicine",
            "E_slime",
            "legacy_455f736c696d65",
            "RuntimeFieldDungeonService",
            "ReturnToCity"
        ];

        foreach (string file in SourceFiles(frameworkRoot))
        {
            string source = File.ReadAllText(file);
            foreach (string token in forbiddenTokens)
            {
                Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
            }
        }

        string negotiation = File.ReadAllText(RepositoryPath(
            "src",
            "Convergence.Framework",
            "Logic",
            "Battle",
            "Runtime",
            "BattleNegotiationAndRewards.cs"));
        Assert.DoesNotContain("Demon Stock", negotiation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyFloorDungeonService_IsHostOwnedWhileGenericTraversalRemainsFrameworkOwned()
    {
        Assert.Equal("JRPG.ConsoleHost", typeof(RuntimeFieldDungeonService).Assembly.GetName().Name);
        Assert.Equal("Convergence.Framework", typeof(RuntimeDungeonTraversalService).Assembly.GetName().Name);
        Assert.False(File.Exists(RepositoryPath(
            "src",
            "Convergence.Framework",
            "Logic",
            "Runtime",
            "FieldDungeonStateMachines.cs")));
        Assert.True(File.Exists(RepositoryPath(
            "Logic",
            "Field",
            "Dungeon",
            "LegacyFieldDungeonStateMachines.cs")));
    }

    [Fact]
    public void WalletAndEconomyPublicContracts_UseNeutralCurrencyTermsOnly()
    {
        Assert.Null(typeof(RuntimeWalletSnapshot).GetProperty("Macca", BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            ["Credit", "Debit"],
            typeof(IEconomyTransactionService)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) &&
                !file.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));

    private static string RepositoryPath(params string[] segments)
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Convergence.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        Assert.NotNull(current);
        return Path.Combine([current!, .. segments]);
    }
}
