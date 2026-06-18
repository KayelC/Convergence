# Problem: Host And Godot Boundary

## Current State

The framework exposes host-neutral contracts for content text, commands, events, randomness, runtime IDs, and save snapshots.

Test-only Godot-shaped adapters prove the framework can be consumed without taking a Godot dependency.

There is no real Godot project or production Godot adapter yet.

## Problem

The framework must remain engine-neutral, but the project still needs a practical host story.

Godot should own scenes, nodes, resources, animation, input, UI, and save-file format. The framework should own rules, validated content, runtime state, and transition results.

## Needed Data

Host-facing example mappings:

- content pack logical paths to host resource paths;
- runtime actor IDs to scene/node handles;
- event kinds to animation/presentation cues;
- command options to UI selections;
- save snapshots to host-owned save records.

## Decisions Still Needed

- Will the first real external host be Godot or a cleaner console sample?
- Should Godot adapters live in this repository or a separate package?
- What event/presentation detail does Godot need that the framework does not currently expose?
- Which runtime snapshots must be stable public contracts before a Godot prototype starts?

## Recommended Next Step

Do not add a Godot dependency yet.

Build a slightly richer clean console sample first, then design the Godot adapter around proven framework contracts rather than around speculation.
