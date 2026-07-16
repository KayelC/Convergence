# Convergence Framework

Convergence is an engine-neutral, modular JRPG rules framework. It combines reusable concepts from several JRPG traditions without requiring a particular game, presentation layer, content setting, or engine.

The framework is under active development and has not reached a stable public release.

## Product Layout

```text
src/Convergence.Framework/          reusable .NET 8 library
samples/Convergence.DemoHost/       optional console example
samples/Convergence.GodotHost/      Godot 4.7.1 .NET reference consumer
tools/Convergence.ContentValidator/ host-side authoring validator
tests/Convergence.Framework.Tests/  framework-only tests
tests/Convergence.DemoHost.Tests/   example-host tests
tests/Convergence.ContentValidator.Tests/ validator tests
content/                            generic reference and demo content
schemas/content/v3/                 Draft 2020-12 authoring contracts
docs/                               active product documentation
```

Historical prototype material is retained under `ArchiveDocs/LegacyFramework` and is not part of the active build.

## Supported Baseline

- .NET 8
- C# 12
- Godot 4.7.1 or another .NET 8-compatible host
- Framework/API version 0.1.0

`Convergence.Framework` has no runtime package dependency, is intentionally
non-packable, and is distributed as source. A game references the framework
project directly. Pinned private analyzer/compiler packages guard the API during
builds but do not become runtime assembly references. See the
[public API contract](docs/public-api-contract.md).

## Licensing

Convergence is publicly source-available for educational, research, personal,
nonprofit, and other noncommercial purposes. Commercial use requires a separate
written agreement from the copyright owner.

- Software is licensed under PolyForm Noncommercial 1.0.0.
- Documentation and original example content are licensed under CC BY-NC-SA
  4.0.
- Historical material under `ArchiveDocs/` is excluded from the active license.

See the [licensing overview](LICENSE.md), [plain-English guidance](docs/licensing.md),
and [contribution policy](CONTRIBUTING.md). Convergence is source-available, not
OSI-approved open-source software, because commercial use is reserved.

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

The checked-in `samples/Convergence.GodotHost` project is a working source-reference example. Its headless smoke mode loads `res://` content, executes a framework action and encounter, maps runtime IDs to Nodes, and validates a host-owned save.

## Build And Test

```powershell
dotnet --version
dotnet restore Convergence.sln
dotnet format Convergence.sln --no-restore --verify-no-changes
dotnet build Convergence.sln --configuration Release --no-restore --no-incremental -p:TreatWarningsAsErrors=true
dotnet test Convergence.sln --configuration Release --no-build --no-restore
dotnet run --project tools/Convergence.ContentValidator -- --content-root content --schema-root schemas/content/v3 --registrations config/content-validator/active-samples.registrations.json
dotnet run --project samples/Convergence.DemoHost -- --help
```

The repository `global.json` selects the .NET 8 SDK line. The clean solution builds Framework, DemoHost, and their independent test projects.

The same checks run automatically in
[`quality.yml`](.github/workflows/quality.yml) for changes to `main`, pull
requests, and manual workflow dispatch. CI also runs the architecture and
terminology boundaries explicitly and smoke-tests every noninteractive DemoHost
mode. Interactive Training Annex behavior is exercised through scripted host
tests rather than terminal piping.

Start with the [documentation index](docs/README.md), [architecture](docs/architecture.md), [mechanics and player rules](docs/mechanics/README.md), and [capability matrix](docs/framework-capability-matrix.md).
