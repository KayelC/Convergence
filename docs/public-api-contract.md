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
5. `RuntimeActorCombatProfileCompositionService`,
   `RuntimeActorGrowthCompositionService`, and
   `RuntimeSkillChoiceTransactionService` coordinate source-owned actor state
   and dependent Vessel profiles.
6. `BattleActionExecutor`, `BattleEncounterRunner`, and the focused runtime
   transition services apply rules and return typed immutable results.
7. `RuntimeSessionRestoreService` validates and restores aggregate snapshot
   state; the host still owns its save-file encoding and scene reconstruction.

The complete namespace ownership map is in
[Public API Namespaces](public-api-namespaces.md). DemoHost types are examples,
not part of this contract.

Damage policy implementations return one `DamagePolicyResolution` containing
immutable hit results and the effective affinity. The effect pipeline consumes
that result directly, which prevents hosts from composing separate damage
stages that reapply guard, critical, affinity, or stat modifiers.

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

### Stat Modifier Policy Migration

M1-1 removes `RuntimeActorState.ChangeStatStage` from the public contract. A
host or custom runtime module now constructs immutable modifier snapshots and
requests, then calls `IStatModifierPolicyService`. The service validates the
selected `IStatModifierPolicy`, contains extension faults, and returns typed
diagnostics, ordered events, and unchanged before/after state on rejection.
Direct live-actor commit remains Framework-owned. M1-5 routes skill, item,
passive, lifecycle, removal, and cleanup execution through the selected policy;
external callers must not replace it with reflection or another mutable stage
store. Retained modifier save state is completed separately by M1-7.

M1-3 adds `StatModifierLifecycleBoundary` and the supplied
`TimedExclusiveStatModifierPolicy`. Counted contributions retain their latest
observed boundary so same-boundary application is protected, duplicate ticks
are idempotent, and stale ticks reject without mutation. The tick request now
requires this typed event-and-sequence boundary; the former event-ID-only
constructor is deliberately removed from the guarded pre-release contract.

M1-4 adds `TimedContributionStatModifierPolicy`. It retains each accepted
application as an independently timed signed contribution, derives a bounded
aggregate, refreshes the oldest same-sign contribution at a configured cap,
and uses the same typed lifecycle-boundary contract per contribution.

Save contract v9 removes the roster's duplicated owner level in addition to
the actor restore profile's former duplicated Active Hosted Entity ID.
`RuntimeSessionRestoreService` now derives that dependency from
`RuntimePartyRosterSnapshot`, restores the owned actor first, and returns a
normalized aggregate whose derived Vessel profile matches the restored source.
Pending skill choices and their revision are part of the actor snapshot. A v7
snapshot is rejected unless the host registers an explicit migration step.

## Documentation And Build Tooling

Framework emits `Convergence.Framework.xml`. XML documentation is curated and intentionally incomplete; `CS1591` remains suppressed. Summaries cover selected
composition entry points, while concept documents explain how those entry
points fit together. The tested [Framework Source Ownership](reference/framework-source-ownership.md)
inventory accounts for every source file and exported namespace owner. Public
API analysis uses pinned, private build-only packages and a lock file. Those
packages do not become runtime assembly references and do not change the .NET 8
or source-project-reference distribution model.

The framework remains non-packable. Games integrate it through a source
`ProjectReference`; no NuGet publication contract is implied by the API
analyzer or its build-only compiler toolset.

The supported actor composition path is documented in
[Actors And Runtime State](developer-guide/actors-and-runtime-state.md). Its
authority, transaction, and restoration invariants are documented in
[Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md).
