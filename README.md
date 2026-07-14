# Convergence Framework

Convergence is an engine-neutral, modular JRPG rules framework. It combines reusable concepts from several JRPG traditions without requiring a particular game, presentation layer, content setting, or engine.

The framework is under active development and has not reached a stable public release.

## Product Layout

```text
src/Convergence.Framework/          reusable .NET 8 library
samples/Convergence.DemoHost/       optional console example
tests/Convergence.Framework.Tests/  framework-only tests
tests/Convergence.DemoHost.Tests/   example-host tests
content/                            generic reference and demo content
docs/                               active product documentation
```

Historical prototype material is retained under `ArchiveDocs/LegacyFramework` and is not part of the active build.

## Supported Baseline

- .NET 8
- C# 12
- Godot 4.5 or another .NET 8-compatible host

`Convergence.Framework` has no external package dependency, is intentionally non-packable, and is distributed as source. A game references the framework project directly.

## Godot Source Integration

Keep the framework outside the Godot project directory so Godot's source glob does not compile the same files a second time:

```text
MyGameRepository/
|- Game/
|  |- project.godot
|  `- MyGame.csproj
`- Convergence/
   `- src/
      `- Convergence.Framework/
         `- Convergence.Framework.csproj
```

```xml
<ItemGroup>
  <ProjectReference Include="..\Convergence\src\Convergence.Framework\Convergence.Framework.csproj" />
</ItemGroup>
```

Godot owns nodes, resources, scenes, input, presentation, scheduling, and save-file serialization. Convergence owns serializer-neutral content, rules, runtime state, transitions, diagnostics, and results. See the [Godot integration contract](docs/godot-integration-contract.md).

## Build And Test

```powershell
dotnet --version
dotnet restore Convergence.sln
dotnet build Convergence.sln --no-restore
dotnet test Convergence.sln --no-restore
dotnet run --project samples/Convergence.DemoHost -- --help
```

The repository `global.json` selects the .NET 8 SDK line. The clean solution builds Framework, DemoHost, and their independent test projects.

Start with the [documentation index](docs/README.md), [architecture](docs/architecture.md), [mechanics and player rules](docs/mechanics/README.md), and [capability matrix](docs/framework-capability-matrix.md).
