# Problem: Field, Dungeon, And Encounter Independence

## Current State

The framework has an optional generic outer-navigation service plus a separate optional generic dungeon-traversal service. Both use arbitrary `ContentId` identities and injected access policy; neither assumes a city/dungeon split, floor topology, menu, scene model, or spatial controls. Dungeon traversal owns logical current/visited nodes, checkpoints, and defeated-boss flags, but never selects or starts an encounter. The older floor-oriented state machine remains an optional compatibility/sample module. The Training Annex demo separately proves a tiny noninteractive authored-floor traversal and encounter resolution.

It also has a small host-owned encounter-start planner. A host can select an encounter by ID from a placed scene entity, patrol, interaction, script, or optional floor rule; receive catalog actor-creation requests; hydrate those actors through the existing catalog battle actor factory; and then run battle resolution.

The ordinary console field loop still owns most player-facing flow, menu presentation, legacy dungeon content, encounter hydration, and battle handoff.

The clean Training Annex play shell uses a list only because it is a console host. A Godot doorway/area trigger, VN hotspot, map cursor, or script can issue the same generic transition request. Scene movement and visual transitions stay host-owned.

The current clean demos are intentionally text-first. They can treat floor transitions as immediate encounter triggers because they are repeatable framework proofs, not final exploration design.

## Problem

The framework needs a clean field/dungeon loop that can run from clean content without legacy `DungeonData`, `Database.Dungeons`, or console-only assumptions.

The production-facing design must not assume that every floor transition forces a battle. A Godot host will likely have actual scenes, rooms, corridors, enemy bodies, patrols, touch/attack triggers, scripted events, and spawn points. In that model, the host decides when an encounter starts and passes the selected encounter or formation into the framework.

The framework should therefore support both:

- fixed or scripted encounters for tutorials, bosses, event fights, and test/demo floors;
- host-triggered entity encounters where Godot owns the scene object and asks the framework to resolve the battle once contact or interaction occurs.

Floor-based random encounters can remain a sample policy or console-demo convenience, but they should not become the required framework model.

## Needed Data

Generic field/dungeon examples:

- one dungeon;
- one block;
- one safe floor;
- one encounter pool or encounter source;
- one fixed/scripted encounter floor or event;
- one placed/entity encounter concept owned by the host scene;
- one terminal or checkpoint;
- one barrier or transition rule if needed;
- two or three encounter definitions;
- one reward policy reference.

## Decisions Still Needed

- What is the minimum host-triggered encounter contract? For example: scene instance ID, encounter ID, formation override, battlefield context, and post-battle resolution.
- Is the framework sample dungeon floor-based, node-based, room-based, or host-defined for the next demo layer?
- Should random encounters remain a framework sample policy, a host policy, or both behind an optional adapter?
- What is the minimum interactive traversal loop?
- Should field actions and dungeon actions share one command surface?

## Recommended Next Step

Keep the current floor-triggered Training Annex flow as a deterministic test/demo path.

The first host-owned encounter-start proof now exists through `CatalogEncounterStartPlanner` and the Training Annex demo. The next production-facing dungeon pass should make that model more scene-like:

1. represent a placed enemy or encounter trigger outside the framework as host scene state;
2. ask the framework to resolve a chosen encounter by ID when that host trigger fires;
3. return battle outcome, rewards, defeat/escape state, and any persistence updates without the framework knowing about Godot nodes or scene files;
4. eventually replace the text demo's floor-ascent encounter shortcut with an interactive clean host loop.

Do not migrate legacy `tartarus.json` into clean production content.
