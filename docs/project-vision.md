# Project Vision: Convergence

## Purpose

Convergence is a publicly source-available framework for composing expressive,
data-driven JRPG systems. Its name reflects the goal: reusable ideas can
converge in one toolkit without forcing a developer to reproduce any particular
game or setting. Public use is available for noncommercial purposes; commercial
use is reserved for separate licensing by the copyright owner.

## Product Direction

- Framework rules remain engine-neutral and host-neutral.
- Godot is the primary integration target, not a dependency of the reusable assembly.
- Mechanics are optional modules, policies, and registrations rather than mandatory genre assumptions.
- Example content is generic and original. Framework operation never requires proprietary game data.
- Hosts own scenes, input, presentation, assets, scheduling, and save files.
- Framework services own validation, rules, runtime state, transitions, diagnostics, and outcomes.

## Modularity

A developer may use battle without fusion, navigation without dungeon traversal, a custom turn economy instead of Press Turn, or no moon-phase mechanic at all. Optional vocabulary does not imply mandatory runtime behavior. Features activate because the host registers, binds, and calls them.

## Quality Direction

Convergence favors explicit commands and results, immutable contracts, injected randomness and policy, transactional mutation, qualified content identity, deterministic diagnostics, and tests at public boundaries.

## Content And IP

The framework supplies concepts and neutral examples, not a game world. Original stories, characters, art, terminology, and production data belong to downstream games. Historical prototype material is preserved only as unsupported archive evidence.

## Release Direction

The immediate product is the source-distributed .NET 8 library and its documentation. A real Godot integration sample, broader authored schema contracts, API stabilization, and release versioning are future product work. NuGet packaging is not currently part of the supported distribution model.
