# Public API Contract 0.1

## Supported Boundary

`Convergence.Framework` version `0.1.0` is the reusable product assembly. Its
public surface consists of immutable content definitions, host ports, policy
interfaces, runtime snapshots, typed requests/results/diagnostics, and the
default services needed to compose those contracts. JSON DTOs, converters,
mappers, and internal execution helpers are not public API.

The primary composition path is:

1. A host implements `IContentPackTextSource`, `IHostCommandSource<T>`,
   `IHostEventSink<T>`, and `IRandomSource` as needed.
2. `SkillSystemCatalogLoader` validates and builds a `GameDataCatalog` from
   host-supplied text and explicit registrations.
3. `RuntimeRulesetBindingResolver` binds authored rulesets through a
   host-supplied `RuntimeRulesetPolicyFactoryRegistry`.
4. `CatalogBattleActorFactory` creates runtime actors from catalog definitions.
5. `BattleActionExecutor`, `BattleEncounterRunner`, and the focused runtime
   transition services apply rules and return typed immutable results.
6. `RuntimeSessionRestoreService` validates and restores aggregate snapshot
   state; the host still owns its save-file encoding and scene reconstruction.

The complete namespace ownership map is in
[Public API Namespaces](public-api-namespaces.md). DemoHost types are examples,
not part of this contract.

## Compatibility Policy

The checked-in `PublicAPI.Shipped.txt` file is the textual `0.1.0` baseline.
`Microsoft.CodeAnalysis.PublicApiAnalyzers` compares every build against it.
Removing or changing a listed member fails the build; additions must first be
recorded in `PublicAPI.Unshipped.txt` and reviewed.

- Patch releases in the `0.1` line may not break the shipped baseline.
- A reviewed minor pre-release may revise the API with explicit migration
  notes and a deliberate baseline update.
- This is a guarded pre-release contract, not a `1.0` stability promise.
- Content schema and save-contract compatibility are versioned separately.

## Documentation And Build Tooling

Framework emits `Convergence.Framework.xml`. XML summaries cover the supported
composition entry points, while this document explains how those entry points
fit together. Public API analysis uses pinned, private build-only packages and
a lock file. Those packages do not become runtime assembly references and do
not change the .NET 8 or source-project-reference distribution model.

The framework remains non-packable. Games integrate it through a source
`ProjectReference`; no NuGet publication contract is implied by the API
analyzer or its build-only compiler toolset.
