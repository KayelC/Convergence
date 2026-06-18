using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using Xunit;

namespace Convergence.Tests.Architecture;

public sealed class FrameworkBoundaryTests
{
    private static readonly string[] ForbiddenPublicTypeFragments =
    [
        "System.Console",
        "System.IO.File",
        "System.IO.Directory",
        "System.Text.Json",
        "Newtonsoft",
        "Godot",
        "JRPGPrototype.Data.Database",
        "JRPGPrototype.Data.SkillData",
        "JRPGPrototype.Data.PersonaData",
        "JRPGPrototype.Data.ItemData",
        "JRPGPrototype.Entities.Combatant",
        "JRPGPrototype.Entities.Persona",
        "JRPGPrototype.Services.IGameIO"
    ];

    [Fact]
    public void FrameworkAssembly_HasNoConsoleHostDependencyOrExternalPackageReference()
    {
        Assembly framework = typeof(ContentId).Assembly;

        Assert.Equal("JRPG.Framework", framework.GetName().Name);
        Assert.DoesNotContain(
            framework.GetReferencedAssemblies(),
            reference => reference.Name == "JRPG.ConsoleHost");

        string project = File.ReadAllText(RepositoryPath("JRPG.Framework", "JRPG.Framework.csproj"));
        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("JRPG.ConsoleHost", project, StringComparison.Ordinal);
    }

    [Fact]
    public void FrameworkPublicApi_ExposesNoHostSerializerEngineOrLegacyTypes()
    {
        Assembly framework = typeof(IContentPackTextSource).Assembly;
        foreach (Type type in framework.GetExportedTypes())
        {
            AssertAllowed(type);
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                switch (member)
                {
                    case MethodInfo method:
                        AssertAllowed(method.ReturnType);
                        foreach (ParameterInfo parameter in method.GetParameters()) AssertAllowed(parameter.ParameterType);
                        break;
                    case PropertyInfo property:
                        AssertAllowed(property.PropertyType);
                        break;
                    case FieldInfo field:
                        AssertAllowed(field.FieldType);
                        break;
                    case EventInfo eventInfo when eventInfo.EventHandlerType is not null:
                        AssertAllowed(eventInfo.EventHandlerType);
                        break;
                }
            }
        }
    }

    [Fact]
    public void FrameworkSources_DoNotUseConsoleFilesystemSleepingOrNewtonsoft()
    {
        string frameworkRoot = RepositoryPath("JRPG.Framework");
        string[] forbidden = ["Console.", "File.", "Directory.", "Thread.Sleep", "Newtonsoft", "Godot"];

        foreach (string file in Directory.EnumerateFiles(frameworkRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
            }
        }
    }

    private static void AssertAllowed(Type type)
    {
        foreach (Type candidate in Expand(type))
        {
            string identity = candidate.FullName ?? candidate.Name;
            Assert.DoesNotContain(
                ForbiddenPublicTypeFragments,
                forbidden => identity.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is Type element)
        {
            foreach (Type nested in Expand(element)) yield return nested;
        }
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Expand(argument)) yield return nested;
        }
    }

    private static string RepositoryPath(params string[] segments)
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "JRPG.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        Assert.NotNull(current);
        return Path.Combine([current!, .. segments]);
    }
}
