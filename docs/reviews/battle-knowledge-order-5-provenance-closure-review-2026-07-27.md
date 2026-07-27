# Battle Knowledge Order 5 Provenance Closure Review

**Date:** 27 July 2026

**Revision reviewed:** `5e33f71ba52cd8b573fc81e2f76e85eb31982964`

**Branch:** `main`

**Disposition:** Order 5 complete

## Review Method

This review re-read the corrected source, public contracts, executable tests,
and all three active Battle Knowledge audience documents. Earlier reports were
not used as a finding checklist or implementation authority.

The trace covered:

- persistent and encounter-local knowledge snapshots and query precedence;
- elemental, ailment, and instant-defeat observation transitions;
- Analyze disclosure, restricted profiles, and persistent promotion;
- execution-result aggregation and its complete provenance preflight;
- ordinary and custom effect execution;
- automated team-local learning and optional encounter seeds;
- familiarity import through acquisition and Compendium integration;
- save validation and aggregate restoration; and
- DemoHost action IDs, runtime/entity mappings, battle reset, and persistence.

A qualifying defect required an intended invariant, a reachable supported path,
a concrete consequence, and reproducible source evidence.

## Source Review Result

No unresolved realistic reachable Battle Knowledge defect was found in the
corrected revision.

The corrected aggregate boundary in
[`BattleKnowledgeExecutionTransitions.cs`](../../src/Convergence.Framework/Knowledge/BattleKnowledgeExecutionTransitions.cs)
requires an immutable `BattleKnowledgeExecutionAuthority`. Before any lower
transition runs, the aggregate validates every observation and Analyze result
against:

1. the accepted source action;
2. the acting runtime actor;
3. the enclosing effect index;
4. the runtime target; and
5. the authoritative entity bound to that runtime target.

Malformed authority input, missing target bindings, substituted provenance, and
mixed valid/invalid batches return stable diagnostics and the original
persistent and encounter snapshots. A late invalid item therefore cannot leave
an earlier accepted knowledge mutation behind.

The canonical producers were also traced:

- DemoHost derives the authority from the accepted command, live acting actor,
  and current encounter participants;
- ordinary automated skills derive it from the selected skill and actor;
- restricted automated turns isolate the matching command and direct effect
  events before integration; and
- registered custom handlers retain their normal outer effect-result
  normalization while their nested knowledge evidence is independently checked.

## Gameplay Alignment

Current source matches the confirmed Battle Knowledge rules:

- player knowledge persists by entity definition;
- ordinary AI learning starts fresh for each encounter and remains team-local;
- temporary affinity changes may inform the current encounter but do not
  overwrite permanent authored facts;
- misses and ambiguous chance outcomes do not reveal hidden resistance;
- exact ailment or instant-defeat immunity may be learned;
- `Almighty` is excluded from stored elemental discovery;
- Analyze disclosure is policy-controlled, including the restricted boss
  profile;
- familiar acquisition imports authored knowledge only through the explicit
  configured policy; and
- saves contain persistent player knowledge, not encounter-only analysis or AI
  learning.

## Documentation Review

The three active audience documents agree with the corrected implementation:

- [`docs/mechanics/battle-knowledge.md`](../mechanics/battle-knowledge.md)
  describes player-visible discovery, persistence, Analyze, boss restriction,
  AI lifetime, and familiarity;
- [`docs/developer-guide/battle-knowledge.md`](../developer-guide/battle-knowledge.md)
  shows authority construction from the accepted action and encounter
  participants; and
- [`docs/technical/battle-knowledge-runtime.md`](../technical/battle-knowledge-runtime.md)
  documents complete-batch provenance preflight before lower transitions.

All three documentation-coverage entries remain `reviewed`.

## Intentional Extension Boundary

`ICustomEffectHandler` remains a trusted game-rule extension. Convergence can
verify that custom knowledge evidence belongs to the action that actually
executed, but it cannot independently prove that a game-specific custom
handler's semantic value is truthful. A handler that claims a custom effect
discovered a particular affinity is responsible for that custom rule.

This is an integration responsibility, not an unresolved provenance defect:
identity and transaction authority are framework-owned, while custom semantic
meaning is deliberately extension-owned.

## Verification

- strict nonincremental Release build: **0 warnings, 0 errors**;
- full solution tests: **1,741 passed, 0 failed, 0 skipped**
  (`1,559` Framework, `175` DemoHost, `7` ContentValidator);
- Framework coverage: **90.74% lines, 76.40% branches**;
- content validation: **6 packs, 36 documents, 98 qualified definitions**;
- clean battle, field, save, and Training Annex demos: all exited `0`;
- scripted Training Annex interaction: exited `0` without mutation;
- trim-aware Framework build: **0 warnings, 0 errors**;
- real Godot 4.7.1 headless smoke: emitted
  `CONVERGENCE_GODOT_SMOKE_OK` and exited `0`;
- formatting verification and `git diff --check`: passed; and
- framework boundary, public API, documentation-link, capability-matrix, and
  active-content checks: covered by the green Framework suite.

The managed-process Godot invocation initially failed before project startup
because the sandbox denied `user://logs`. The identical executable and project
passed outside that restricted process. No Convergence assertion failed.

## Closure

O5-F1 is corrected and independently verified. O5-R9 through O5-R13 are
complete, the Battle Knowledge capability has no named implementation gap, and
Order 5 is promoted from `partial` to `complete`.
