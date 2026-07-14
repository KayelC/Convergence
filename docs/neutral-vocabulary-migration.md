# Neutral Vocabulary Migration

## Purpose

This record defines the pre-release migration from franchise-shaped prototype vocabulary to Convergence-owned, host-neutral contracts. It is the implementation authority for the Action Token, Vessel, Hosted Entity, and Companion changes.

The migration begins from a clean .NET 8 baseline of 706 passing tests: 558 Framework tests and 148 DemoHost tests, with no failures or skips.

## Breaking Contract Boundary

Convergence has not published a stable API, content schema, or production save format. The migration therefore replaces the old contracts directly:

- content schema version 1 is replaced by version 2;
- runtime save contract version 6 is replaced by version 7;
- old C# symbols, JSON fields, enum values, and policy IDs receive no aliases;
- old content and saves are rejected rather than silently translated;
- active source, content, tests, DemoHost, and documentation move together.

Future breaking changes after a stable release require deprecation and migration policies. This pre-release exception must not be treated as the normal release process.

## Vocabulary Decisions

The supplied turn economy is named **Action Token**. `IBattleTurnEconomy` remains the generic extension boundary, while the supplied policy uses full and partial tokens. Passing consumes a partial token before converting a full token. Consequently `[partial, full]` becomes `[full]`; a pass with only full tokens converts one full token into one partial token.

Ruleset content deliberately keeps the generic category `turn_economy`. Categories identify the framework contract family, while `standard_action_token` identifies Convergence's supplied implementation. There is no `action_token` category: a host may register a different turn-economy policy without changing the structural category. This refines the original migration table in favor of the optional-module boundary.

Ownership uses these neutral roles:

- a **Vessel** may use an **Active Hosted Entity** as its stat source;
- inactive hosted entities live in the **Hosted Entity Roster**;
- independently deployable owned actors live in the **Companion Roster**;
- an **Independent Actor** uses its own stats;
- Hosted Entity and Companion are runtime ownership roles, not separate definition classes.

Application-host contracts such as `IHostCommandSource<T>` retain their names. In those APIs, host means the Godot, console, or test application integrating Framework. Domain APIs use the full term Hosted Entity and do not introduce a standalone `Host` actor role.

## Vessel Stat Authority

The old weighted actor-plus-form calculation is removed. Stat sourcing becomes an explicit request rather than an inference from actor-kind names.

- Actor sourcing uses actor base stats.
- Active Hosted Entity sourcing uses the hosted entity's base stats and ignores Vessel base stats.
- Optional implemented equipment stat modifiers are added after the selected source.
- The raw stat cap is applied before the Vessel actor's battle-stage multipliers.
- Missing hosted entities produce either a typed rejection or an explicit actor-base fallback selected by the host.
- Missing hosted entities never resolve to zero stats.
- Composition updates effective stats and resources atomically or leaves the actor unchanged.

Standard growth profiles remain optional supplied policies:

- Independent Actor: one manual stat point and the existing base HP/SP growth per level;
- Vessel: no manual core-stat points, with the existing base HP/SP growth;
- Owned Entity: one random capped core-stat increase and no actor base-resource roll.

Training Annex will exercise the model by making Echo Adept a Vessel and Annex Mentor its active hosted entity. Other owned Training Annex entities become Companions.

## Active Example Vocabulary

Active examples and tests are part of the public learning surface. They use neutral names rather than retaining borrowed terms as fixtures. Credits, Sample Depths, Battle Exit Charm, Return Beacon, Recovery Pulse, Catalysts, and Last Stand replace the identified prototype vocabulary. `Almighty` and `Ice Boost` are explicitly retained as approved generic terms.

Historical material under `ArchiveDocs` is excluded from the active terminology rule and remains unsupported evidence.

## Deferred Equipment Work

This migration uses only equipment behavior that already works: typed basic-attack profiles and implemented stat modifiers. A separate framework-first pass must complete:

- armor defense and evasion consumption;
- equipment-granted skills or passives;
- basic-attack rule modifiers;
- typed secondary equipment effects.

The vocabulary migration must not claim those consumers are complete.

## Verification

Completion requires all active content to load under schema version 2, save version 7 to round-trip through DemoHost-owned serialization, all Framework and DemoHost tests to pass without skips, .NET 8 builds to produce no warnings, all five DemoHost modes to run, Godot contract tests to pass, active documentation links to resolve, and terminology searches to report no retired names outside the archive.
