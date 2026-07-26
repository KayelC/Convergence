# Status And Passive Lifecycle Order 4 R36 Closure Review

Date: 26 July 2026

Reviewed source revision: `94bdded`

Verdict: **complete; no unresolved realistic reachable defect found in the
reviewed Order 4 scope**

## Method

This review used the current source, tests, schemas, active content, and the
three audience documents as evidence. Earlier reports supplied the names of the
three correction checkpoints, but their healthy-path claims were not treated
as proof.

The source trace followed:

1. typed ailment definitions into application gates, resistance, chance, and
   transition policy;
2. live actor status through turn-start, owner-turn-end, action, phase, round,
   departure, battle-end, and reserve-clock boundaries;
3. passive definitions through target capture, condition evaluation,
   recursion/count policy, ordered effects, result validation, and commit;
4. lifecycle results through typed encounter-event mapping;
5. combat modifiers through stat-stage and ailment profile composition; and
6. saved status/passive state through integrity validation and live restore.

A concern was retained only when a supported path could violate a documented
invariant with an observable consequence. Host extension trust boundaries and
unimplemented product alternatives were recorded separately rather than
inflated into defects.

## Correction Verification

### O4-R33: encounter cancellation before lifecycle commit

`BattleEncounterRunner` now rechecks cancellation immediately before every
encounter-owned lifecycle transaction commit: departure, battle start, turn
start, turn end, phase end, round end, fault cleanup, and successful battle
end. The new round-end and successful battle-end regressions stage HP mutation,
cancel from the lifecycle port, and prove that the live graph remains
unchanged.

### O4-R34: replacement passive evidence controls commit

`ValidatingPassiveTriggerDispatcher` captures one staged participant graph,
validates the complete returned activation set, and commits only when at least
one activation has outcome `Executed`. Empty and wholly non-executed results
remain legitimate evaluation results but cannot commit hidden mutation. Focused
tests cover empty, non-executed, and executed replacement outcomes.

The framework deliberately does not attempt to prove that arbitrary replacement
dispatcher code semantically performed the authored effects. It validates
identity, event, trigger, pre-mutation target eligibility, outcome/effect shape,
and commit evidence. Replacement implementations remain trusted deterministic
rule extensions, not host-side scene or storage adapters.

### O4-R35: ailment combat-profile composition

`ProductionCombatRuleset.CreateCombatantProfile` resolves stat-stage channels
first. Active ailments then multiply generic damage dealt, stage-derived damage
taken, and stage-derived evasion; add critical vulnerability; and OR rigid-body
state. Physical and magical stage attack channels remain separate until damage
resolution. All arithmetic uses the established saturating helpers.

The ordinary-value regression combines +1 physical attack, defense, and
agility stages with two ailments. It proves generic damage dealt `3.0`, damage
taken `0.525`, hit `1.25`, evasion `0.5`, critical vulnerability `17`, physical
attack `1.25`, magical attack `1.0`, and rigid body enabled. This complements
the existing extreme-value saturation regression.

## Fresh Code Review

No high, medium, or low defect remained after the three corrections.

The following paths were independently checked:

- ailment application rejects guard, immunity, failed chance, malformed policy
  decisions, protected exclusivity replacement, and custom-service rejection
  without publishing staged mutation;
- turn-start and ailment-trigger schedules retain exact boundary-start
  instances, so removed, refreshed, or replaced entries do not execute stale
  work and additions wait for the next boundary;
- owner-turn-end order remains passive effects, ailment triggers, recovery, and
  duration/stat-modifier advancement;
- reserve actors suspend owner-turn-end processing, while injected phase or
  round reserve policies explicitly control aggregate clock aging;
- cleanup distinguishes deployment swap, defeat, flee, roster recall, battle
  end, and field transition and preserves each status's removal permissions;
- passive target eligibility is captured before mutation, activation counts and
  effects commit atomically, recursion is bounded, and public result
  collections remain immutable under constructor input and record cloning;
- encounter-owned departure cleanup executes once per observed cause and every
  lifecycle commit is protected by the caller cancellation token;
- save validation and direct restore enforce duration validity, complete passive
  enablement state, trigger-index/event coherence, target references, duplicate
  activation rejection, and ailment exclusivity; and
- content schema v8 requires typed lifetime, behavior, modifier, recovery,
  targeting, and effect records without display-text inference.

## Documentation Review

The mechanics, developer, and technical pages now agree on:

- optional module and host/policy boundaries;
- application, refresh, exclusivity replacement, and typed rejection;
- exact-instance turn scheduling and owner-turn-end order;
- action, actor, event, phase, round, battle, and permanent lifetimes;
- reserve suspension and opt-in aggregate aging;
- typed cleanup/departure causes;
- passive target, recursion, counting, evidence, and transaction authority;
- cancellation and extension failure rollback;
- ailment combat-profile composition; and
- save/restore validation and serializer ownership.

The diagrams and prose describe current code paths. Debug messages remain
non-authoritative; hosts consume typed results and events.

## Deliberate Boundaries

These are not closure defects:

- custom handlers and replacement dispatchers are trusted rule extensions;
  framework actor mutation is staged, but external scene, file, network, or
  platform side effects require host compensation;
- an event-sink exception after a committed framework transaction cannot rewind
  previously performed host presentation;
- manual deployment and roster operations outside the encounter runner must
  invoke lifecycle cleanup with their actual cause; and
- games may omit the entire status/passive module or replace its supplied
  transition, restriction, reserve-aging, stat-modifier, and event policies.

## Verification

Verification results are recorded from the O4-R36 gate in the closing commit:

- focused Order 4 source and documentation tests: **581 passed**, 0 failed,
  0 skipped;
- full solution: **1,663 passed** (1,483 Framework, 173 DemoHost, 7 content
  validator), 0 failed, 0 skipped;
- strict nonincremental Release build: **0 warnings, 0 errors**;
- active content validation: **6 packs, 36 documents, 98 qualified
  definitions** passed schema, deserialization, semantic, dependency,
  registration, and catalog checks;
- clean battle, field, save, and Training Annex demos all exited `0` without
  input; scripted interactive play remains covered by the passing DemoHost
  suite;
- `dotnet format --verify-no-changes`, documentation-link/architecture tests,
  framework boundary tests, and `git diff --check` passed; and
- active content and schema files were unchanged. The direct source search
  found no Godot, filesystem, console, archive, Newtonsoft, retired namespace,
  legacy IO, or legacy-adapter dependency in Framework; its only host-named hit
  was the approved test-only `InternalsVisibleTo("Convergence.DemoHost.Tests")`
  declaration.

## Promotion

`status_and_passive_lifecycle` is promoted from `partial` to `complete`. Its
mechanics, developer-guide, and technical documentation entries are promoted
from `existing_unreviewed` to `reviewed`. Demo coverage remains honestly
`focused`, and the module remains host-neutral and optional.

Order 5, `battle_knowledge`, becomes the next collaborative documentation
subject. This promotion does not claim that every optional game-specific status
policy or every host presentation has been implemented.
