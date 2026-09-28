# Navigation, Dungeons, Encounters, Negotiation, And Rewards

## Generic Navigation

Navigation uses arbitrary location `ContentId` values and explicit transitions containing transition, source, and destination IDs.

**Framework rule:** a transition applies only when its IDs are valid, the
current location matches its source, and the selected access rule allows it.
The result offers a new logical location; the game adopts it after its own
presentation or scene work succeeds. A rejected or faulted request leaves the
logical location unchanged. Reverse travel requires a separate explicit
transition. Returning to the same location is permitted if the access rule
allows it, for example to re-enter or refresh a scene.

**Configured rule:** the game supplies the access rule. It can change as story
flags or other host-owned state change. An unavailable route reports a typed
rejection; an invalid request or malfunctioning access rule reports a distinct
result. The game's UI should use those result categories and its own localized
text, not parse Framework diagnostic sentences.

**Host responsibility:** the game detects a door, map choice, hotspot, or
scripted trigger; requests the transition; then loads or presents the target.
Framework approval does not prove that a Godot scene loaded. If host work
fails, the game retains its previous logical location. For example, a player
may return from the Training Annex to the staging area while their dungeon
checkpoint progress remains saved; retained progress does not mean they are
still physically in the dungeon.

Convergence does not define cities, menus, world maps, collisions, scene loading, or player movement. A Godot area trigger, VN hotspot, console option, or script can all request the same transition.

Navigation is optional. A game may use dungeon traversal without building a
navigation service. When using the broad save contract v19, a saved field
snapshot includes a logical location if field state is present; a dungeon-only
game may supply a stable neutral location. The wider save shape is revisited
under Order 13.

## Optional Dungeon Traversal

Dungeon traversal is optional and separate from navigation. Navigation answers
which logical location the player occupies; traversal tracks a meaningful node
inside a dungeon, visited nodes, unlocked checkpoints, and defeated bosses.
It does not move a character through every step of a 3D scene. A door, stairs,
barrier, or script can request a node transition. Reverse travel needs its own
transition.

**Framework rule:** a move first checks the dungeon, current node, retained
visited/checkpoint/boss history, and transition IDs. It then checks whether the
current dungeon and source node match. The game's supplied route rule decides
whether a valid move is allowed. A sealed barrier can therefore reject a move
without changing the player's node or visited history. Invalid current or
retained IDs, wrong dungeon, wrong source, legal route denial, and a
malfunctioning route rule have distinct outcomes. Cancellation is not turned
into a denial.

**Progress rule:** the game may report a checkpoint unlock or boss defeat only
after its own success condition. The Framework checks a game-supplied list of
eligible checkpoint/boss IDs, their dungeon, and allowed areas before recording
progress. Malformed, unknown, wrong-dungeon, and wrong-area reports leave
progress unchanged. Repeating a valid report is harmless. Merely entering a
node never unlocks a checkpoint or defeats a boss. Success may come from a
battle, puzzle, or story script; the Framework does not require battle-result
proof for every game design.

Saved checkpoint and boss records are checked against that same declared list
before restoration. A record must have the expected checkpoint/boss kind,
belong to the saved dungeon, and name an eligible area that appears in visited
history. This prevents a save from granting progress that live play could not
have recorded. Games with no retained checkpoint or boss records do not need
to configure this optional validation module.

**Authored content:** a dungeon may describe floor ranges, encounter pools,
and fixed-floor metadata. These are optional catalog facts, not orders to move
the player or start combat. A floor may have no visible enemy, one enemy, or
several host-triggered encounters. The game chooses when to prepare an
encounter. A fixed battle floor's encounter ID is a reference that the host
may use; it is not a one-battle-per-entry limit.

**Host responsibility:** Godot scenes, spatial movement, doors, visible
enemies, animations, UI, and map presentation remain with the game. Framework
approval offers a candidate node. The game adopts it only after its scene work
succeeds. Leaving a dungeon can retain remembered progress while disabling
active dungeon actions. Re-entry selects an entrance or an unlocked checkpoint
explicitly; it does not silently resume at the last visited node. A host that
requires an inside-dungeon position must reject an inside save missing that
position before adoption. Generic games may still save navigation without
dungeon state.

The [dungeon progress decision](../decisions/dungeon-progress-reporting.md)
records the approved rule, and the [developer guide](../developer-guide/dungeon-traversal.md)
shows how a game supplies the route rule and progress list.

## Encounter Content And Preparation

Encounter definitions contain ordered or weighted formations, member entity IDs, runtime levels, boss flags, rewards, and environment metadata. Preparation resolves a selected formation and hydrates actors through the catalog factory.

Runtime instance IDs remain unique even when a formation contains the same entity more than once. Preparation reports catalog or creation failures instead of inserting fallback enemies.

## Encounter Triggers

An encounter trigger request identifies the authored encounter and a host-owned trigger instance. Trigger consumption and battle start are distinct operations. This supports visible scene enemies, one-shot events, respawning enemies, random checks, or scripted bosses without making every floor transition force a battle.

## Negotiation

Negotiation content defines personalities, questions, answers, scores, demands, and familiar dialogue. The session service returns ordered prompts/events and a typed outcome.

**Configured rule:** mood thresholds, demand selection, currency/item amounts, familiarity behavior, and randomness come from content and policy. The host supplies answers and applies approved payments or rewards through atomic economy/inventory services.

Negotiation success does not silently mutate a roster. Recruitment is a separate validated transaction that checks recruitability, duplicate ownership, previous recruitment constraints, roster capacity, and runtime identity.

When recruitment succeeds and the game enables a Compendium, the host should pass the acquired actor to `RecordAcquisition`. See [Fusion, Inheritance, Acquisition, And Compendium](fusion-acquisition-and-compendium.md).

## Rewards

Reward services calculate immutable experience and currency totals from defeated participants and the active reward policy. Calculation and application are separate steps.

**Framework rule:** aggregate arithmetic cannot wrap on extreme input.
Application uses progression and explicit currency-ledger services, allowing
the host to present a preview before committing.

**Configured rule:** reward formulas, participating recipients, reserve sharing, bonus conditions, and terminology belong to the selected ruleset or host composition.
