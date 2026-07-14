# Problem: Host And Godot Boundary

## Current State

The framework exposes host-neutral contracts for content text, commands, events, randomness, runtime IDs, and save snapshots.

Test-only Godot-shaped adapters prove the framework can be consumed without taking a Godot dependency.

There is no real Godot project or production Godot adapter yet.

The acquisition and runtime baseline are decided: Godot is the primary host, the framework targets .NET 8/C# 12, and developers obtain the source from GitHub and use a `ProjectReference`. NuGet publication is not required for framework integration.

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

- What is the smallest real Godot project that should replace the current test-shaped proof?
- Should production Godot adapter source live beside the framework or in a separate repository folder?
- What event/presentation detail does Godot need that the framework does not currently expose?
- Which runtime snapshots must be stable public contracts before a Godot prototype starts?

## Recommended Next Step

Do not add a Godot dependency to `JRPG.Framework`.

When host work resumes, create the smallest real Godot adapter over the existing .NET 8 contracts. Keep Nodes, scenes, signals, resource loading, presentation, scheduling, and save serialization in that host project. Framework capability work may continue independently and should not wait for console presentation polish.
