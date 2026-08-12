# Architecture

## Product Boundary

`Convergence.Framework` is a dependency-free .NET 8 class library. It owns game rules and state transitions but never owns a presentation technology, filesystem, scene graph, save-file encoding, or game-specific content source.

`Convergence.DemoHost` is an optional reference consumer. It demonstrates how a host supplies content, commands, events, randomness, inventory reservations, and save serialization. It is not required by Framework and is not a compatibility layer.

`Convergence.GodotHost` is a separate Godot 4.7.1 .NET reference consumer. It references Framework source, reads canonical example content through `res://`, maps runtime IDs to Nodes, and keeps engine APIs and JSON save encoding outside the reusable assembly.

Session restore is aggregate and framework-owned. Hosts decode their save envelope, then supply the snapshot, catalog, actor factory, actor-profile resolver, validator, and any real version-migration steps. Framework restores dependencies in order and returns no live session until every actor and aggregate invariant succeeds; scene/node reconstruction and host-context application happen afterward.

## Core Principles

- Framework first: reusable rules are designed without console or engine assumptions.
- Explicit authority: hosts request transitions; services return immutable results and diagnostics.
- Serializer-neutral APIs: JSON DTOs and converters remain internal to `Convergence.Serialization`.
- Host-owned presentation: display names may be rendered but never determine behavior.
- Injected policy: optional mechanics and game-specific decisions are supplied through policies or registrations.
- Deterministic testing: randomness enters through `IRandomSource`.
- Atomic mutation: assessment, execution tokens, reservations, and rollback boundaries prevent partial state changes.
- Portable persistence: framework snapshots describe runtime state; the host owns the wire format.

## Content Flow

Host-supplied JSON is checked against the strict Draft 2020-12 contracts in
`schemas/content/v10` before Framework deserialization and semantic catalog
validation. JSON Schema owns document shape; Framework validation owns graph,
dependency-visibility, registration, and host-capability rules. This keeps the
reusable assembly free of schema-evaluation and filesystem dependencies while
giving authoring tools an independent contract.

```text
host text source
    -> deserializer
    -> semantic validator + explicit registrations
    -> dependency-aware catalog loader
    -> immutable GameDataCatalog
    -> runtime factories and services
```

Hosts provide all JSON text and diagnostic source names. The framework validates pack versions, paths, records, references, host vocabulary, dependency visibility, and qualification. Runtime services consume catalog definitions, never serializer-owned values.

Ruleset records are bound through a host-supplied
`RuntimeRulesetPolicyFactoryRegistry`. Each category has its own typed factory
interface, so an authored policy cannot be resolved as an unrelated service.
The supplied standard registry is opt-in; unknown policies fail with typed
diagnostics and no hidden standard fallback. See
[Ruleset Policy Contracts](ruleset-policy-contracts.md).

Stat resolution/scaling and stat-modifier lifecycle are separate policy
families. The `stat` category resolves raw values and numeric stage
multipliers; `stat_modifier` selects how applications accumulate, expire, and
clear. A host binds both explicitly before constructing execution services.

## Runtime Flow

Runtime actors are identified by `RuntimeInstanceId` and content records by
`ContentId`. Actor state, party and rosters, inventory, equipment, currency,
navigation, traversal, Compendium, knowledge, and session state have immutable
snapshot boundaries.

Inventory is the sole owner of equipment instances. Actor loadouts reference
those runtime instance IDs under authored slot `ContentId` keys. The selected
`IEquipmentSlotLayoutPolicy` validates definition profiles and assignment
compatibility; the supplied standard policy preserves the conventional weapon,
armor, boots, and accessory layout without making those four positions a
framework-wide enum.

`RuntimeActorEquipmentProfileSource` is the shared live projection from
inventory ownership, actor equipment instance references, catalog definitions,
and slot-layout validation. Weapon attacks, equipped-only skill grants,
accessory modifiers, armor Defense, and armor/boots Evasion are all derived
there. Grants remain outside learned/move-list state: active grants feed live
action authorization, while passive grants feed the existing passive runtime.
Numeric contributions enter actor composition and then the existing production
damage/hit policies; the equipment layer owns no parallel combat formula.

The bound economy service owns one `IRuntimeShopOfferResolver`. Its selected
default `IShopPricingPolicy` resolves fixed authored purchase prices, while an
explicit policy-shaped offer may select another factory from the same
`ShopPricingPolicyFactoryRegistry`. Resolution produces one transient immutable
pricing profile carried by the runtime offer. Host quotes and atomic shop
transactions consume that profile rather than rebuilding price logic in a
presentation adapter. The supplied standard policy preserves authored purchase
price and derives resale from a configured percentage; Luck adjustment is a
separately selected optional policy.

The same resolved offer carries a composite `(shopId, offerId)` identity and
one immutable stock profile. `RuntimeShopStockSnapshot` is the sole durable
quantity authority for tracked offers. Fixed limited stock binds the supplied
standard policy; explicitly authored policies resolve through the registered
stock factory set without fallback. Shop transactions calculate candidate
inventory, currency, and stock snapshots and expose them only as one atomic
result. Unlimited offers contribute no stock entry.

Recovery is an optional service selected by an economy ruleset. An
`IRecoveryPolicy` plans from an immutable actor snapshot and names the exact
currency, resources, ailment treatment, temporary-state cleanup, and cost. The
generic `RecoveryService` stages canonical actor cleanup and the named-currency
debit, then commits the actor only when every operation succeeds. Rulesets that
do not select recovery expose no recovery service and receive no hidden HP/SP,
cost, or cleanup behavior.

Battle knowledge deliberately uses two snapshot authorities. Persistent facts
are keyed by entity definition and belong to session persistence. Encounter
facts are keyed by runtime target identity, take query precedence for that
target, and are discarded after battle. Framework execution results supply
typed observation and Analyze evidence to an atomic transition service; hosts
render the result but do not rediscover defenses from private catalog data.

Actor authority is split deliberately. Individual actor state owns identity,
progression, resources, equipment, skills, status, affiliation, and encounter
presence. `RuntimePartyRosterSnapshot` exclusively owns active/reserve
placement, Hosted Entity ownership, Companion ownership, and the Active Hosted
Entity selection.

The standard Vessel model atomically composes core stats, defenses, active
skills, and passives from the selected Hosted Entity while retaining the
Vessel's identity, progression, resources, equipment, status, affiliation, and
presence. Source progression, pending skill decisions, dependent Vessel
recomposition, and aggregate restoration share that authority model. See
[Actors And Runtime State](developer-guide/actors-and-runtime-state.md) and
[Runtime Actor State And Restoration](technical/runtime-actor-state-and-restoration.md).

Action execution reuses typed targeting, conditions, effects, lifecycle rules,
and turn economy. Encounter orchestration accepts explicit initiative,
scheduling, lifecycle, command, completion, synchronization, economy, and event
ports. The supplied team-phase scheduler rotates available actors; the supplied
Agility scheduler freezes a descending actor order per round. Schedulers receive
detached participant and accepted-economy evidence, so they cannot mutate actors
or manufacture battle opportunities.

Every encounter event carries a kind-specific immutable payload for initiative,
rounds, teams, actors, commands, effects, turn economy, deployment, faults, or
outcomes. The runner alone emits structural start/end, economy, fault, and
terminal events. Port events are validated against a command/lifecycle
allow-list and the frozen participant graph. Command evidence is correlated to
the scheduled command-window owner. Executed actions require that actor except
for the canonical actorless `PartyRosterTransitioned` evidence. Status
application, refresh, replacement, rejection, duration
advancement, expiry, cleanup, and passive effects retain typed transition
evidence through action and encounter results. Optional debug text is diagnostic
only; hosts localize and present the typed payload rather than parsing prose.
Normal encounter results never carry fault fields; normal completion detail is
optional `BattleEnded` debug text. The result owns the complete event history,
which may include result-only terminal evidence after the event sink itself
fails.
Hosts remain responsible for selecting when an encounter begins and how
resulting events are presented. See
[Encounter Orchestration Integration](developer-guide/encounter-orchestration.md)
and [Encounter Orchestration Runtime](technical/encounter-orchestration-runtime.md).

Passive dispatch freezes each authored trigger's eligible target IDs before
effects execute, so validation cannot reinterpret eligibility after staged
health or deployment mutation. Supplied battle-start and defeat-prevention
event policies are defaults installed only when the host has not registered an
explicit policy.

Encounter lifecycle composition may also implement the optional typed departure
port. The supplied status lifecycle adapter uses it so canonical flee,
roster-recall, and newly observed defeat transitions receive their exact
cleanup cause through one staged participant graph. Recovery closes a defeat
period, allowing a later defeat to receive cleanup again without duplicating
work while the actor remains defeated. Manual host-owned
deployment changes remain explicit host cleanup boundaries.

Status lifetime uses typed actor-turn, action, team-phase, round, and custom
clock boundaries. Encounter composition maps team IDs to separate authored
phase and event IDs; the Framework never infers one vocabulary from another.
Reserve advancement is one selected policy per lifecycle service: suspension
is the supplied default, while the supplied advancing policy permits only an
explicit owning-team phase or round clock, never per-action aging.

Turn economy is a policy family inside that flow, not the encounter scheduler.
The supplied neutral and Action Token implementations bind through authored
rulesets and control only action-opportunity state. The runner owns structural
validation and lifecycle windows; an injected scheduler owns actor and phase
order. Immediate follow-up selection is an optional, finitely bounded scheduler
extension and can use only an opportunity already retained by the economy. See
[Turn Economy Runtime](technical/turn-economy-runtime.md).

## Optional Modules

Navigation, dungeon traversal, Action Token, ailments/passives, party and rosters, economy, negotiation, fusion, Compendium, and persistence are independently composable. A developer does not need to register or instantiate a module that their game does not use.

Runtime save contract v19 is a deliberately broad interoperability aggregate,
not the module activation mechanism. When a host chooses to use it, required
but unused components are represented by neutral snapshots. The minimal party
roster still identifies the session owner while its placement and ownership
lists may remain empty. Field state is nullable. A future change to make other
components absent would require a new versioned save contract.

Moon-phase IDs remain nullable vocabulary for games that choose such a mechanic. The supplied ruleset registry has no moon-phase factory, and DemoHost does not require or bind a moon-phase system.

## Distribution

The supported distribution is a Git checkout, submodule, subtree, or copied source tree plus a `ProjectReference` to `src/Convergence.Framework/Convergence.Framework.csproj`. Framework is non-packable until a separate release decision establishes package versioning and compatibility policy.

## Pre-Release Contract Boundary

The active product uses the neutral contracts defined by the [Terminology Boundary](terminology-boundary.md). Content schema version `10` and runtime save contract version `19` are deliberate pre-release breaks with no compatibility aliases. Save v19 retains v18's typed currency ledger and adds immutable remaining stock keyed by the qualified shop ID plus its shop-local offer ID. Actor loadouts still contain only inventory-owned instance references. Persistent knowledge remains in `RuntimeKnowledgeSnapshot`; current-target analysis remains in `RuntimeEncounterKnowledgeSnapshot` and is not part of an ordinary session save. Save validation rejects missing or contradictory combat-profile source references, missing or multiply assigned equipment instances, equipment/actor ID collisions, slot-layout incompatibility, malformed shop-stock identities or quantities, a retained passive target that is absent from the aggregate actor set, missing enabled/disabled state for an equipped passive, and multiple active ailments in one exclusivity group. Save validation and aggregate restoration must bind retained stat-modifier and charge policies explicitly; no default policy is inferred. Any non-current save requires an explicit host-supplied migration step. A token-aware architecture test scans active source, tests, content, and documentation so archived vocabulary cannot re-enter the product unnoticed.

Assembly version `0.1.0` is guarded by a checked-in textual API baseline. The
[Public API Contract](public-api-contract.md) identifies the supported
composition entry points and the pre-release compatibility policy. Build-only
API analyzers and compiler tooling are private development dependencies; the
compiled framework retains no runtime package dependency.

Framework is marked trimming-aware and builds with the pinned .NET 8 ILLink analyzer. The [Release Quality Gate](release-quality-gate.md) combines locked dependency auditing, API and documentation checks, schema/catalog validation, coverage thresholds, DemoHost modes, and a checksum-verified Godot headless run.
