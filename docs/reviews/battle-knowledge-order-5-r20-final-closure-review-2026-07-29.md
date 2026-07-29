# Battle Knowledge Order 5 R20 Final Closure Review

**Date:** 29 July 2026

**Source revision reviewed:** `2d92cdafb5d3c1bfc501134bcd4283527e49c671`

**Verdict:** complete; no unresolved realistic reachable Order 5 defect

## Review Method

This review re-read the corrected implementation and tests before consulting
earlier reports for checkpoint names. It traced current Framework source,
DemoHost integration, persistence, public API evidence, and all three active
Battle Knowledge audience documents. Prior summaries were not treated as proof.

The review used the repository finding standard: an actionable defect needs an
intended invariant, a supported reachable path, a concrete consequence, and
reproducible source or test evidence. Hypothetical mutation of already-invalid
state and alternative product designs were not promoted into vulnerabilities.

## Source Trace

The review followed these complete paths:

- damage, ailment, instant-defeat, and Analyze execution evidence;
- aggregate action, actor, effect, runtime-target, and entity provenance;
- immutable encounter and persistent transitions plus query precedence;
- automated team-local knowledge creation, sharing, seeding, and disposal;
- Training Annex player persistence and per-battle encounter reset;
- acquisition, Compendium registration, and familiar-defense import;
- save-v14 validation and aggregate session restoration; and
- public API, source-inventory, architecture, and documentation guards.

## Correction Verification

| Checkpoint | Verified result |
|---|---|
| O5-R15 | Runtime actors no longer expose or serialize a competing Analyze store. Current-target analysis exists only in encounter knowledge. |
| O5-R16 | The three disconnected mutable discovery stores and their public keys are absent from source and the API baseline. |
| O5-R17 | Standalone persistent views and transitions reject every cloned undefined enum and invalid persistent analysis field before dictionary construction. |
| O5-R18 | Instant-defeat evidence permits only a complete bypass shape or a complete checked-resistance shape. |
| O5-R19 | Public API and all three audience documents describe the same two authorities and save-v14 boundary. |

## Fresh Finding Corrected During R20

The source trace found one low-severity sibling extension-contract defect.
`BattleKnowledgeObservation.Ailment` accepted an `Immune` application status
with a missing or non-immune effective resistance. The canonical executor did
not produce that shape, but a supported custom integration could construct it;
the transition would then silently ignore the claimed immunity.

Commit `2d92cdaf` now rejects that contradictory receipt at construction.
Focused tests cover null, vulnerable, normal, and resistant effective values,
while canonical immune and temporary-immunity paths remain valid. This changes
no shipped ailment rule or player-visible discovery behavior.

## Confirmed Runtime Invariants

- Persistent player facts are keyed by entity definition in
  `RuntimeKnowledgeSnapshot` and are the only knowledge written to a session
  save.
- Encounter facts are keyed by runtime target plus entity identity in
  `RuntimeEncounterKnowledgeSnapshot`; they override persistent facts only for
  that live target and ordinary encounters do not retain them.
- Misses and random ailment or instant-defeat failures teach no exact defense
  tier. Confirmed typed immunity may teach immunity. Temporary guard, shield,
  Break, override, or passive influence never overwrites the authored
  persistent defense profile.
- Analyze stores only fields disclosed by the injected policy. HP, SP, stats,
  and skills remain encounter-only; authored defense profiles may persist.
  Restricted boss policies can hide resources, skills, elemental affinities,
  ailment resistances, and instant-defeat resistances as `Unknown`.
- Each automated team owns one fresh encounter snapshot per run unless the host
  explicitly supplies a validated seed. Teammates share discoveries; opposing
  teams and later ordinary encounters do not.
- Familiarity import is explicit, policy-selected, player-scoped, and routed
  through the canonical persistent transition. It never trains enemy AI.
- Aggregate knowledge application preflights all nested provenance and returns
  the original immutable before snapshots on any rejection.

## Documentation Review

The mechanics page accurately states the player-visible discovery rules and
boss disclosure behavior. The developer guide accurately assigns snapshot,
policy, event, Godot, and save responsibilities. The technical page accurately
documents authority, ordering, immutability, failure containment, and source
ownership. All three now include the coherent ailment-immunity receipt rule.

The documentation coverage matrix therefore promotes the developer and
technical entries to `reviewed`; mechanics remains `reviewed`. The framework
capability matrix promotes `battle_knowledge` from `partial` to `complete`.

## Verification Record

| Gate | Result |
|---|---|
| Broad focused Framework trace | 220 passed, 0 failed, 0 skipped |
| Focused DemoHost and save trace | 124 passed, 0 failed, 0 skipped |
| Full Release solution suite | 1,745 passed: 1,563 Framework, 175 DemoHost, 7 ContentValidator; 0 failed, 0 skipped |
| Strict nonincremental Release build | 0 warnings, 0 errors |
| Formatting and `git diff --check` | passed |
| Framework coverage | 90.80% lines, 76.50% branches |
| Active content validation | 6 packs, 36 documents, 98 qualified definitions |
| DemoHost | all four noninteractive modes exited 0; scripted Training Annex play exited 0 |
| Trimming analysis | 0 warnings, 0 errors |
| Godot contract tests | 6 passed |
| Godot 4.7.1 headless smoke | `CONVERGENCE_GODOT_SMOKE_OK`, exit 0 |
| Offline locked restore | passed for all seven active projects |
| Online vulnerability audit | not refreshed locally: sandboxed NuGet access failed and the external-metadata approval was denied; no project or lock file changed, and connected CI remains the release authority for this check |

The Framework boundary search found no Godot, console-host, archived, or legacy
runtime dependency. Framework's internal `System.Text.Json` content
implementation remains behind serializer-neutral public contracts and is not a
host/save-format dependency.

## Closure

Order 5 is formally closed. The corrected framework has one durable player
knowledge authority, one encounter-local authority per team, typed and
provenance-checked evidence, policy-controlled disclosure and familiarity, and
validated save restoration. No unresolved realistic reachable defect remains
in the reviewed Order 5 scope.
