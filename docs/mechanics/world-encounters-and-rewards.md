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

Dungeon traversal is separate from navigation. It uses arbitrary dungeon/node
IDs and an injected policy to allow or block requested node transitions; a
barrier can be represented by a rejected transition. Currently the game
explicitly reports checkpoint unlocks and boss defeats, which are recorded
idempotently but are not checked against a battle result or dungeon content.
Order 9 is reviewing that progress-validation boundary. Entering a location
does not automatically move through dungeon nodes or start combat.

**Host responsibility:** scenes, doors, stairs, spatial enemies, animations, and map presentation. The host calls traversal or encounter services when its world logic says an event occurred.

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
