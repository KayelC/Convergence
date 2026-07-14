# Convergence JRPG Framework

This repository contains an engine-neutral JRPG rules framework and a retained console compatibility host. The framework is under active development and is not yet a stable public release.

## Supported Baseline

- .NET 8
- C# 12
- Godot 4.5 or another .NET 8-compatible host

`global.json` keeps repository builds on the .NET 8 SDK line even when newer SDKs are installed. `JRPG.Framework` has no external package dependencies and is intentionally not published as a NuGet package at this stage.

## GitHub Source Integration

Download or clone the repository, keep the framework project outside the Godot project's source directory, and reference it from the Godot C# project:

```text
MyGameRepository/
|- Game/
|  |- project.godot
|  `- MyGame.csproj
`- Framework/
   `- JRPG.Framework/
      `- JRPG.Framework.csproj
```

```xml
<ItemGroup>
  <ProjectReference Include="..\Framework\JRPG.Framework\JRPG.Framework.csproj" />
</ItemGroup>
```

Keeping the framework outside the Godot project directory prevents the host project from compiling the framework source once through its default source glob and again through the project reference. A Git submodule, subtree, or ordinary checked-in copy may be used to obtain the source; none of those choices changes the framework API.

Godot owns Nodes, Resources, scenes, input, presentation, scheduling, and save-file serialization. `JRPG.Framework` owns serializer-neutral content, rules, runtime state, transitions, diagnostics, and results. Godot types must remain in the host adapter and must not enter framework APIs.

See [Godot Integration Contract](docs/godot-integration-contract.md) and [Architecture](docs/architecture.md) for the complete boundary.

## Build And Test

```powershell
dotnet --version
dotnet build JRPG.Framework/JRPG.Framework.csproj
dotnet test JRPG.sln
```

The first command should report an installed .NET 8 SDK selected through `global.json`. `JRPG.ConsoleHost` remains a compatibility and demonstration host; a Godot game only needs a reference to `JRPG.Framework`.
