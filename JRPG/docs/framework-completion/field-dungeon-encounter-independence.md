# Problem: Field, Dungeon, And Encounter Independence

## Current State

The framework has field and dungeon state-machine services. The Training Annex demo proves a tiny noninteractive dungeon traversal and encounter resolution.

The ordinary console field loop still owns most player-facing flow, menu presentation, legacy dungeon content, encounter hydration, and battle handoff.

## Problem

The framework needs a clean field/dungeon loop that can run from clean content without legacy `DungeonData`, `Database.Dungeons`, or console-only assumptions.

## Needed Data

Generic field/dungeon examples:

- one dungeon;
- one block;
- one safe floor;
- one encounter floor;
- one fixed encounter floor;
- one terminal or checkpoint;
- one barrier or transition rule if needed;
- two or three encounter definitions;
- one reward policy reference.

## Decisions Still Needed

- Is the framework sample dungeon floor-based, node-based, room-based, or host-defined?
- Should random encounters be framework-owned, host-owned, or policy-owned?
- What is the minimum interactive traversal loop?
- Should field actions and dungeon actions share one command surface?

## Recommended Next Step

Expand the clean sample dungeon modestly after adding more combat content.

Do not migrate legacy `tartarus.json` into clean production content.
