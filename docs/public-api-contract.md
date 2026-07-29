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

### Combat Safety Additions

The guarded `0.1.0` surface exposes
`ProductionCombatRulesetConfig.MaximumHitsPerDamageEffect`; the supplied
default is `64`, and valid configurations remain within the schema-v6 absolute
limit of `1..1024`. This is an execution ceiling for one damage effect, applied
before random hit selection or allocation.

`SkillExecutionDiagnosticCode`, `ItemExecutionDiagnosticCode`, and
`BattleActionDiagnosticCode` append `AuthoredPercentageOutOfRange`. Effect-backed
assessment uses that diagnostic for authored percentages outside inclusive
`0..100`, with no target preparation, cost, mutation, or turn consumption.
Direct policy and lifecycle requests reject the same malformed values as
programming errors. The existing numeric values of earlier enum members remain
unchanged.

The pre-release action boundary now appends
`SkillExecutionDiagnosticCode.DuplicateResourceCost`. Programmatic assessment
and semantic content validation both reject more than one cost entry for the
same resource ID before target randomness or amount resolution.

`ActionTurnConsumption` and `TurnEconomyResolution` no longer expose public
`init` setters. Their constructors enforce legal enum and payload shapes, and
`BattleEncounterCommandResult` validates host-supplied status, outcome, team,
consumption, and event values. This is a deliberate guarded-`0.1.0` baseline
correction: no stable Convergence release used the former clone-mutable shape.

`BattleActionAssessment.TurnConsumption` and
`BattleActionExecutionResult.TurnConsumption` are likewise getter-only
Framework decisions. `HostMediatedBattleActionCommand.TurnConsumption` retains
a validating `init` setter so a host may deliberately clone a command to
another non-null cost without gaining authority to rewrite prepared or executed
results.

`ActionTokenTurnEconomy` exposes `Apply(ActionTurnConsumption)` as its only
public consumption mutation. The former policy-specific `ConsumeAction`,
`Pass`, and `TerminatePhase` methods are removed from the guarded pre-release
surface. Direct policy users express every transition through the same generic
protocol used by `BattleEncounterRunner`; this preserves one auditable economy
authority and prevents a retained policy instance from spending a command both
inside a host port and again when the runner applies its returned cost.

`IBattleEncounterDepartureLifecyclePort` is an optional extension to
`IBattleEncounterLifecyclePort`. Its immutable request identifies one
departing participant, the complete matching encounter graph, and an exact
typed cleanup reason. `BattleStatusEncounterLifecyclePort` implements the
extension; the runner invokes it for committed flee, roster recall, and newly
observed defeat without forcing lifecycle implementations that do not own
status cleanup to fabricate behavior.

`RuntimeSaveValidationCode` appends `MissingPassiveSkillState` and
`ConflictingActorAilmentExclusivityGroup`. These guarded `0.1.0` diagnostics
reject incomplete passive enabled-state coverage and mutually exclusive active
ailments before aggregate restore. They did not change then-current save
contract v13's wire
shape; they close validation paths for state that the existing contract already
expresses.

`EffectExecutionResult` retains its public record shape for custom effect
composition, but its scalar and collection `init` assignments now enforce the
same legal boundary during construction and record cloning. Undefined effect
or turn outcomes, invalid optional IDs/enums, invalid host request IDs, and
null result entries reject inside staged execution rather than being
reinterpreted by ordered effects or action-outcome aggregation.

The Order 2 charge correction deliberately changes
`IChargePolicyService.CompleteAction` from damage-element input to immutable
participating `ChargeDamageModifier` receipts. `EffectExecutionResult` exposes
`ParticipatingCharge` so a custom damage-effect executor can publish the exact
modifier it resolved. The supplied policy base consumes only the same retained
runtime charge represented by that receipt; later grants and same-kind
replacements are not removed accidentally.
The supplied policy base rejects a charged receipt that was not issued by its
`ResolveDamageModifier` path. A custom damage executor must therefore publish
the actual returned modifier rather than reconstructing one from its public
values. A direct custom `IChargePolicyService` implementation still owns its
own completion contract.

`DisabledChargePolicy` is the explicit no-charge composition and
`StandardChargePolicyIds.Disabled` is registered by
`ChargePolicyRegistry.CreateStandard()`. The standard authored combat factory
accepts `chargePolicy` values `split`, `unified`, and `disabled`, retaining
`split` when omitted. These are reviewed guarded-`0.1.0` corrections rather
than compatibility aliases for the superseded element-based completion call.

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

### Battle Knowledge Authority

Battle Knowledge has two state authorities. Durable entity-defense facts use
`RuntimeKnowledgeSnapshot`; current-target facts and Analyze disclosures use
`RuntimeEncounterKnowledgeSnapshot`. Canonical Analyze returns
`BattleAnalysisResult` and commits through the encounter transition. Save
contract v15 contains only the durable knowledge snapshot and deliberately
omits current encounter analysis.

Every runtime actor exposes one immutable `RuntimeCombatProfileIdentitySnapshot`
containing the source runtime actor, source entity definition, and revision
currently supplying combat-facing stats, defenses, skills, and passives.
Persistent observations use that source entity as their durable key. Encounter
facts, Analyze results, execution authority, queries, and automated seeds use
the exact profile identity. Rebinding a target to another source or revision
invalidates all of its encounter facts and current Analyze disclosures before
later evidence is accepted.

Public discovery writes pass through
`IPersistentBattleKnowledgeTransitionService`; the API exposes no independent
mutable dictionary store. Record-cloned undefined elements, affinities,
resistance levels, instant-defeat channels, and analysis fields return stable
typed diagnostics without mutation. `PersistentBattleKnowledgeView` rejects
the same malformed input immediately with its exact diagnostic path, and
aggregate save validation applies the same analyzed-defense field rules.
Almighty remains a valid damage element but is an intrinsic Normal affinity,
not a storable knowledge key. Public entry construction, standalone
transitions/views, encounter seeds, host decoding, and save validation reject
impossible stored Almighty facts before strategy or presentation can consume
them.

Instant-defeat observations have exactly two legal resistance shapes: bypassed
evidence omits the channel and both resistance values, while checked evidence
supplies all three. Partial tuples reject during construction and cannot be
silently discarded by a later knowledge transition.

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

Save contract v15 retains the canonical roster and pending skill-choice
authorities established by v9 and stores complete stat-modifier policy state.
It also stores and validates each actor's combat-profile source and revision.
`RuntimeSessionRestoreService` binds retained modifier policies explicitly,
derives the Active Hosted Entity dependency from `RuntimePartyRosterSnapshot`,
restores owned actors first, and returns a normalized aggregate whose derived
Vessel profile matches the restored source. Any non-current snapshot is
rejected unless the host registers an explicit migration path.

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
