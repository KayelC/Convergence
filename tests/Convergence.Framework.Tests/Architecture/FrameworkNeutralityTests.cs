using System.Reflection;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class FrameworkNeutralityTests
{
    [Fact]
    public void FrameworkSources_DoNotOwnPrototypeCurrencyNegotiationOrDungeonCompatibility()
    {
        string frameworkRoot = RepositoryPath("src", "Convergence.Framework");
        string[] forbiddenTokens =
        [
            string.Concat("Mac", "ca"),
            string.Concat("Full", " ", "Moon"),
            "Medicine",
            string.Concat("E_", "sli", "me"),
            string.Concat("legacy_455f", "736c696d65"),
            string.Concat("Runtime", "Field", "Dungeon", "Service"),
            string.Concat("Return", "To", "City")
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
            "Encounters",
            "BattleNegotiationAndRewards.cs"));
        Assert.DoesNotContain("Companion roster", negotiation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenericDungeonTraversal_IsFrameworkOwnedWithoutPrototypeFloorService()
    {
        Assert.Equal("Convergence.Framework", typeof(RuntimeDungeonTraversalService).Assembly.GetName().Name);
        Assert.False(File.Exists(RepositoryPath(
            "src",
            "Convergence.Framework",
            "Runtime",
            "FieldDungeonStateMachines.cs")));
    }

    [Fact]
    public void CurrencyLedgerAndEconomyPublicContracts_UseNeutralCurrencyTermsOnly()
    {
        Assert.Null(typeof(RuntimeCurrencyLedgerSnapshot).GetProperty(
            string.Concat("Mac", "ca"),
            BindingFlags.Public | BindingFlags.Instance));
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
