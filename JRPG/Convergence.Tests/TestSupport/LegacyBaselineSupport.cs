using System;
using System.IO;
using JRPGPrototype.Data;
using JRPGPrototype.Logic.Core;

namespace Convergence.Tests.TestSupport;

internal static class LegacyBaselineSupport
{
    public const string CollectionName = "Legacy recovery baseline";

    public static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
        }
    }

    public static string JsonPath(string fileName) =>
        Path.Combine(RepositoryRoot, "Data", "Jsons", fileName);

    public static void ResetAndLoadLegacyDatabase(ScriptedGameIO? io = null)
    {
        ResetLegacyState();
        Database.LoadData(io ?? new ScriptedGameIO());
    }

    public static void ResetLegacyState()
    {
        Database.Skills.Clear();
        Database.Personas.Clear();
        Database.Ailments.Clear();
        Database.Items.Clear();
        Database.Dungeons.Clear();
        Database.Weapons.Clear();
        Database.Armors.Clear();
        Database.Boots.Clear();
        Database.Accessories.Clear();
        Database.FusionRecipes.Clear();
        Database.ShopInventory.Clear();
        MoonPhaseSystem.ResetForTests();
    }
}

[Xunit.CollectionDefinition(LegacyBaselineSupport.CollectionName, DisableParallelization = true)]
public sealed class LegacyBaselineCollectionDefinition
{
}
