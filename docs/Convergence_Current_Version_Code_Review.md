# Convergence Current-Version Code Review

**Original external review date:** 16 July 2026
**Reconciled against:** live `main` source and executable checks
**Purpose:** independent perspective, challenged findings, and correction status

## Authority

This document began as an external static review of `Convergence-main(3).zip`.
The external review could not compile or execute the repository. Convergence
subsequently checked every concrete claim against the live source, active tests,
project configuration, documentation, and Release builds.

This is a review record, not implementation authority. Current source, tests,
machine-readable content contracts, and the active product documentation remain
authoritative. Strategic observations below are not defects merely because they
describe work that a future game may need.

## Independently Reproduced Baseline

The reconciled review established the following live facts before corrections:

- 178 active C# files under `src`, `samples`, `tests`, and `tools`;
- 36,652 lines of Framework C# and 32,778 lines of test C#;
- 851 public type declarations and 119 public interfaces;
- 9,548 signatures in `PublicAPI.Shipped.txt`;
- 27 Framework files at or above 500 lines, 16 at or above 800 lines, and
  eight above 1,000 lines;
- 940 tests in the clean baseline with no skips;
- a strict Release build with zero warnings and zero errors; and
- 26 focused `ProductionCombatRulesetTests` passing.

Adding the unmodified external report beneath active `docs` temporarily caused
one terminology-boundary failure and introduced 15 `git diff --check` failures.
Those were properties of the imported report, not product-code regressions. This
reconciled form follows the active documentation and terminology boundaries.

## Accepted Findings

### R1. GodotHost Release configuration builds Debug

**Severity:** Medium
**Status:** Implemented pending final review

`Convergence.sln` maps the GodotHost project's solution-level Release
configuration to the project's Debug configuration. A live command confirmed
that `dotnet build Convergence.sln --configuration Release` emitted
`Convergence.GodotHost.dll` beneath the Godot Debug output directory. Building
the Godot project directly as Release succeeded beneath its Release directory,
so the defect is isolated to the solution mapping.

**Implemented correction:** both GodotHost Release entries now map to
`Release|Any CPU`. An architecture test rejects any solution project that maps
Release configuration or build entries back to Debug.

### R2. The canonical damage contract loses effective affinity state

**Severity:** Medium
**Status:** Implemented pending final review

The external review correctly identified an ambiguous relationship among
`ResolveDamage`, `CalculateRawDamage`, and `ApplyDamage`, but static inspection
stopped short of the reachable consequence.

The active skill/item pipeline calls `IDamageExecutionPolicy.Resolve`. The
supplied `ProductionCombatRuleset` normalizes Weak affinity while a target is
guarding and calculates the correct guarded damage. Its interface result carries
only hit, damage, and critical state. `DamageEffectExecutor` therefore retains
the original Weak affinity and can still report a Weakness turn-economy outcome.

In practical terms, guarding can prevent Weak damage while still rewarding the
attacker as though weakness was struck. Existing tests document that the
supplied policy intends guard to normalize weakness, but those assertions cover
the alternative `ApplyDamage` method rather than the canonical execution path.

`CalculateRawDamage` and `ApplyDamage` have no active host or Framework callers;
their public composition is also unsafe because both can apply critical or
affinity stages already applied elsewhere.

**Implemented correction:** `IDamageExecutionPolicy.Resolve` now returns one
immutable `DamagePolicyResolution` containing both hit results and the
effective affinity. `DamageEffectExecutor` uses that effective affinity for
Null, Repel, Absorb, weakness, and normal turn outcomes. The unused public
`CalculateRawDamage` and `ApplyDamage` stages were removed so consumers cannot
accidentally reapply critical, guard, affinity, or stage logic. Focused tests
cover guard normalization through the supplied ruleset, executor consumption
of a policy-normalized affinity, result immutability, and saturating arithmetic.

### R3. Framework lifecycle events are mapped repeatedly

**Severity:** Low
**Status:** Implemented pending final review

`BattleEncounterRunner` and `BattleStatusEncounterLifecyclePort` contain nearly
identical Framework transformations from `BattleStatusLifecycleEvent` to
`BattleEncounterEvent`. The Training Annex host has a related host-specific
mapping with presentation differences.

The Framework copies can drift when payload contracts change. The host-specific
mapping should remain host-owned where its semantics genuinely differ.

**Implemented correction:** a small internal
`BattleStatusLifecycleEventMapper` now owns the lifecycle-event kind and typed
payload transformation. The status lifecycle port still supplies its generated
diagnostic text, while the encounter runner still preserves lifecycle-provided
detail. No public event abstraction or host-presentation rule was introduced.

### R4. Active documentation reports inconsistent capability totals

**Severity:** Medium for project ownership
**Status:** Open

The executable capability matrix records 25 capabilities: 22 complete, one
partial, and two deferred. `framework-capability-matrix.md` reports those values,
while `roadmap.md` still reports 21 complete, one partial, and three deferred.

This does not change runtime behavior, but it directly undermines the active
documentation authority intended to help maintainers understand the product.

**Required correction:** align the active text and add an architecture test that
checks displayed totals against the machine-readable matrix.

### R5. Public API discoverability is weaker than its compatibility guard

**Severity:** Medium
**Status:** Open

The public API baseline is useful and the `0.1` compatibility policy explicitly
allows reviewed changes in a minor pre-release. The baseline is therefore not a
mistaken `1.0` promise.

The documentation gap is nevertheless real. Framework currently suppresses
`CS1591`; generated XML contains 240 documented members while the shipped API
contains thousands of signatures. Existing architecture tests prove selected
composition entry points are documented, not that all supported concepts are
discoverable. No repository-level `AGENTS.md` currently preserves the project's
engineering rules for future automated work.

**Required correction for this review:** add persistent repository instructions,
state the documentation coverage honestly, and add an executable inventory that
accounts for every Framework source file and its public type ownership. Full
consumer, developer, and technical manuals remain a dedicated documentation
initiative rather than low-value generated comments.

## Strategic Observations, Not Defects

### GodotHost is an integration proof

The checked-in Godot project proves a real Godot 4.7.1 source reference,
`res://` content loading, runtime-ID-to-Node mapping, command and event contracts,
an action and bounded encounter, and host-owned save encoding. Its command source
uses a pre-submitted queue and does not yet wait for a future UI signal.

That is appropriate for a headless smoke sample. A playable host will eventually
need pending asynchronous command requests, scene-unload cancellation, input,
animation, and scene transitions. Those are product-development priorities, not
evidence that Framework rules are currently defective.

Godot is already pinned correctly: the sample uses `Godot.NET.Sdk` 4.7.1 and CI
pins the official 4.7.1 archive and SHA-256. The external recommendation to pin
the version was already satisfied.

### The Godot save codec is intentionally partial

The Godot codec demonstrates host ownership of serialization. It is not a
production save-slot system and does not claim atomic file replacement, backup,
cloud storage, corruption recovery, or complete scene reconstruction. A future
playable Godot slice should demonstrate `RuntimeSessionRestoreService` and a
complete game-owned save envelope before developers are encouraged to reuse it
as production persistence.

### Public surface and file size are review signals

The public API is broad and several files are large. Neither count proves a bug
or careless abstraction. Content discriminators, immutable request/result types,
and extension ports naturally create public contracts in a modular framework.

Before `1.0`, real Godot usage should drive an API audit. Types used only by tests
or superseded helper paths should be internalized. Large files should be split
only where independently evolving responsibilities and ownership boundaries are
demonstrable.

## Documentation Direction

The repository needs three coordinated documentation audiences:

```text
docs/
  technical/        architecture, invariants, state machines, transactions
  developer-guide/  integration, composition, content, extension recipes
  mechanics/        player-visible rules, formulas, outcomes, examples
  reference/        generated source/API ownership inventories
```

One hand-written Markdown file per C# file would duplicate source and become
stale. Instead, every source file should appear in a generated, tested inventory,
while detailed prose follows stable concepts and flows. Public XML summaries
should describe supported composition points; technical pages should explain
cross-file invariants and state machines; mechanics pages should remain readable
without source-code knowledge.

## Corrective Sequence

1. Correct the GodotHost Release mapping.
2. Repair the canonical damage-resolution contract and guarded weakness.
3. Centralize the duplicated Framework lifecycle-event mapping.
4. Reconcile capability totals and guard them against the executable matrix.
5. Add persistent repository instructions and source/API documentation evidence.
6. Run the full quality gate.
7. Conduct a fresh source-based review of the corrected code.

The playable Godot vertical slice and comprehensive three-audience documentation
system follow this correction sequence. They should inform API reduction before
Convergence declares a stable `1.0` surface.
