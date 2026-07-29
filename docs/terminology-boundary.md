# Terminology Boundary

## Purpose

Convergence uses one active, host-neutral vocabulary across Framework, DemoHost, tests, content, and documentation. The pre-release vocabulary migration is complete; earlier migration and recovery records are preserved only in the non-built archive.

## Active Contracts

- `IBattleTurnEconomy` is the generic extension boundary. `ActionTokenTurnEconomy` is Convergence's optional supplied implementation and uses full and partial tokens.
- A `Vessel` may source stats from an `ActiveHostedEntity`. Additional owned entities use the Hosted Entity Roster, while independently deployable actors use the Companion Roster.
- `IndependentActor`, `Vessel`, and `Companion` are neutral actor-kind values. Ownership roles do not create separate content-definition classes.
- `Credits` is the currency used by active examples. Hosts remain free to present another currency name.
- Sample Depths, Battle Exit Charm, Return Beacon, Recovery Pulse, Catalyst, and Last Stand are the active neutral example fixtures.
- `Almighty` and `Ice Boost` are approved generic vocabulary.

Ruleset categories stay generic. In particular, `turn_economy` identifies the policy category and `standard_action_token` identifies the supplied Action Token policy. A game may replace that policy or omit the module.

## Version Boundary

Active content uses schema version `8`, and active runtime snapshots use save contract version `14`. Schema v8 retains explicit passive targeting and adds authored status lifetimes containing both expiration and permitted removal causes. Save v14 retains the actor, move-list, canonical-roster, stat-modifier, selected charge-policy, typed status-lifetime, and per-target passive activation authorities established by v13 while removing the obsolete actor-local Analyze field. Earlier pre-release shapes have no aliases or automatic translation; any non-current shape can be accepted only through an explicit host-supplied migration step. This is an intentional clean break made before a stable public release.

## Executable Guard

[`TerminologyBoundaryTests.cs`](../tests/Convergence.Framework.Tests/Architecture/TerminologyBoundaryTests.cs) scans active source, tests, content, documentation, project files, and root Markdown/JSON files. It checks file contents and active relative paths for retired public symbols, wire values, fixture names, and direct franchise references.

The scanner is token-aware:

- PascalCase, snake_case, and ordinary words are separated into identifier segments;
- multiword references and exact wire values use explicit rules;
- incidental words such as `personality`, `demonstration`, and `formula` remain valid;
- retail shop stock remains valid because stock is not globally prohibited.

`ArchiveDocs`, `bin`, and `obj` are excluded. Archived text is historical evidence and cannot become active implementation authority.

## Change Rule

New public names and example vocabulary should describe reusable concepts without assuming a particular franchise, presentation host, or game setting. A future stable release must use formal deprecation and migration policies rather than another unversioned clean break.
