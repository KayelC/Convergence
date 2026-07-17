# Developer Guide

## Purpose

This section will explain how a game developer composes Convergence through its
public contracts. Godot is the primary reference host, but the patterns remain
engine-neutral.

Developer guides focus on:

- required services and repositories;
- host-supplied commands, events, content, randomness, and persistence;
- framework-owned versus host-owned state;
- cancellation, diagnostics, and rejected operations;
- optional modules and replaceable policies;
- focused integration examples.

## Guides

- [Actors And Runtime State](actors-and-runtime-state.md): actor creation,
  canonical party and roster ownership, Vessel composition, growth, pending
  skill choices, and aggregate restoration.
- [Typed Actions And Effects](typed-actions-and-effects.md): canonical action
  composition, authorization, assess/present/execute flow, item reservations,
  cancellation, results, and host-mediated work.
- [Stat Modifier Policies](stat-modifier-policies.md): modifier authority,
  supplied policy models, counted lifecycle clocks, boundary sequences,
  removal, and Godot responsibilities.

The actor guide has completed collaborative review. Typed action/effect and stat
modifier guidance remains `existing_unreviewed` while the completed policy
family receives its fresh source and documentation reviews. Other subsystem guides remain tracked as
`existing_unreviewed` or `missing` in
the [documentation coverage matrix](../reference/documentation-coverage.md).

New guides must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
