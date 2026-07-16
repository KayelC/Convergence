# Decision: Actor Composition, Progression, And Rosters

Status: confirmed
Date: 2026-07-16

## Context

The first clean actor model preserved several assumptions from the archived
prototype:

- stage magnitude did not have a distinct effect at every supported stage;
- a Vessel inherited only statistics from its Active Hosted Entity;
- actor snapshots and the party aggregate both appeared to own roster state;
- party placement and encounter presence could contradict one another;
- skills unlocked during live progression were not applied;
- ownership and command-routing metadata were not separated clearly.

Those assumptions made the reusable runtime harder to reason about and did not
match the intended design.

## Decision

### D1: Stage Scaling

The supplied policy supports stages `-4` through `+4`. Every stage has a
distinct multiplier:

| Stage | -4 | -3 | -2 | -1 | 0 | +1 | +2 | +3 | +4 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| offense, hit, evasion | 0.50 | 0.625 | 0.75 | 0.875 | 1.00 | 1.25 | 1.50 | 1.75 | 2.00 |
| damage taken | 2.00 | 1.75 | 1.50 | 1.25 | 1.00 | 0.875 | 0.75 | 0.625 | 0.50 |

`physical_attack`, `magical_attack`, generic `attack`, `defense`, and
`agility` map to explicit calculation channels. A game may replace individual
tables or the complete `IStatStageScalingPolicy`.

### D2: Move-List Capacity

The supplied shared policy permits eight equipped skills. Active and passive
skills share that capacity. Alternate policies may provide another capacity or
separate active and passive groups.

### D3: Full Move Lists

Level and stat growth commit even when a newly unlocked skill cannot enter a
full move list. The skill becomes a persisted pending choice with a typed
token. Closing a menu, cancelling presentation, saving, or suspending does not
discard it.

### D4: Skill Decisions

The supplied replacement behavior removes the selected old skill from both the
learned set and equipped move list, then adds the new skill to both. Forgetting
the new skill removes only the pending choice. A game that supports later
loadout editing may inject a retention policy that keeps unequipped learned
skills.

### D5: Ownership And Command Metadata

Individual actor snapshots do not contain an owner ID. The party aggregate is
the ownership authority.

`CommandAuthorityId` is an opaque host-routing key. A Godot host may use it to
select the player, AI, remote peer, or script responsible for commands.
Framework combat rules do not interpret its text.

`TeamId` is the framework-consumed combat affiliation used by targeting and
encounter rules.

### D6: Party Placement And Encounter Presence

Active and reserve placement exist only in `RuntimePartyRosterSnapshot`.
Encounter participation exists only in
`RuntimeEncounterPresenceSnapshot.IsDeployed`.
`HasSwappedThisTurn` remains encounter-local state.

The host or encounter planner establishes encounter presence explicitly.
Framework does not infer it from a party menu, scene, location, or reserve
label.

## Actor Composition

A Vessel keeps its own identity, progression, current resources, equipment,
affiliation, encounter presence, statuses, and host-routing metadata. Its
effective core stats, defense profile, equipped active skills, and equipped
passives come from the Active Hosted Entity selected by the canonical party
roster.

Composition is atomic. The complete profile is validated and staged before the
live Vessel is changed. Rejection preserves the original actor state.

## Progression And Restoration

The owned Hosted Entity is authoritative for its own level, statistics,
learned skills, equipped skills, pending choices, and skill-state revision.
When it grows or resolves a skill choice, the dependent Vessel profile is
recomposed in the same transaction.

Runtime save contract v9 stores source actor state and the canonical party
roster. Aggregate restoration validates the save, restores the Active Hosted
Entity before its Vessel, recomposes the Vessel, and returns no live session
when any dependency fails.

## Alternatives

- Keeping weighted actor-plus-Hosted-Entity statistics was rejected because it
  was an obsolete prototype rule and produced unintended stat growth.
- Copying roster ownership into each actor was rejected because duplicated
  authority can drift.
- Encoding active and reserve placement in actor state was rejected because it
  can contradict the party aggregate.
- Silently discarding a skill when the move list is full was rejected because
  host interruption must not change progression outcomes.

## Consequences

- Hosts must retain one canonical `RuntimePartyRosterSnapshot`.
- A Vessel composition request that uses an Active Hosted Entity must supply
  that roster and the matching source actor state.
- Hosts decide when and how to present pending skill choices.
- Save restoration requires an actor-profile resolver but remains atomic.
- Stage tuning is authored or injected rather than inferred from display text.

## Evidence

- [Actor mechanics](../mechanics/actors-progression-and-resources.md)
- [Party and roster mechanics](../mechanics/party-inventory-and-economy.md)
- [Actor developer guide](../developer-guide/actors-and-runtime-state.md)
- [Actor state technical reference](../technical/runtime-actor-state-and-restoration.md)
- [Ruleset policy contracts](../ruleset-policy-contracts.md)
- [Actor correction roadmap](../roadmap/actor-composition-progression-roster-roadmap.md)
