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

## Current State

The documentation foundation is established, but subsystem guides have not yet
completed owner review. Existing integration material is tracked as
`existing_unreviewed` in the
[documentation coverage matrix](../reference/documentation-coverage.md).

New guides must follow the
[Documentation Design Pattern](../documentation-design-pattern.md).
