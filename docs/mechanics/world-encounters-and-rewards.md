# Navigation, Dungeons, Encounters, Negotiation, And Rewards

## Generic Navigation

Navigation uses arbitrary location `ContentId` values and explicit transitions containing transition, source, and destination IDs.

**Framework rule:** a transition applies only when the current source matches and the injected `IRuntimeNavigationPolicy` allows it. Reverse travel requires its own explicit transition. Rejected movement preserves the current location.

Convergence does not define cities, menus, world maps, collisions, scene loading, or player movement. A Godot area trigger, VN hotspot, console option, or script can all request the same transition.

## Optional Dungeon Traversal

Dungeon traversal is separate from navigation. It uses arbitrary dungeon/node IDs and injected policies for legal edges, checkpoints, barriers, and progress. Entering a location does not automatically move through dungeon nodes or start combat.

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
