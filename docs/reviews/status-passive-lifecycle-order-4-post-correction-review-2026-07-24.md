# Status And Passive Lifecycle Order 4 Post-Correction Review

**Review date:** 24 July 2026

**Reviewed revision:** `98e790a`

**Capability:** `status_and_passive_lifecycle`

**Verdict:** complete; no unresolved realistic correctness finding remains in
the reviewed Order 4 contract

## Review Method

This review treated current source and executable tests as evidence. Earlier
reports and commit summaries were used only to identify the intended review
scope, not to prove that a correction worked.

The trace covered:

1. status lifetime, removal, ailment, and passive content contracts;
2. every live timed-state family and its mutation, tick, expiry, cleanup, and
   restore boundaries;
3. ailment gates, chance, resistance, transition, recovery, and turn
   restriction resolution;
4. passive targeting, owner eligibility, recursion, activation accounting,
   ordered effects, and replacement-dispatch validation;
5. outer ordered-effect completion and Instant-state expiry;
6. battle-start, turn-start, turn-end, phase-end, round-end, fault, and
   battle-end encounter lifecycle transactions;
7. save validation and restoration of retained status and passive state;
8. schema-v8 lifetime mapping and active status content;
9. focused and complete executable tests; and
10. the mechanics, developer, and technical Order 4 documents and diagrams.

## Findings

### Runtime findings

No high-, medium-, or low-severity runtime correctness finding was reproduced.

The corrected source now preserves these invariants:

- direct and effect-driven ailment application use one staged transaction;
- finite lifetimes cannot retain an impossible expiry contract;
- non-ailment dispels identify every committed removal by ID, state family,
  and cause;
- Instant state expires once at the outermost ordered-effect boundary, and
  passive or ailment trigger results retain that completion evidence;
- replacement passive dispatchers cannot commit evidence for an unloaded
  skill, wrong trigger, wrong event, ineligible target, duplicate activation,
  mismatched effect, or non-executed outcome;
- battle-start passive owners are deployed-only by default, with an explicit
  all-participants opt-in;
- reserve aging is policy-controlled and cannot be inferred from action count;
- cleanup uses the exact departure reason and removal permissions;
- encounter lifecycle steps stage the complete participant graph and commit
  only after returned lifecycle evidence is accepted; and
- save validation restores the same lifetime and passive-activation domains
  accepted by live runtime state.

### Documentation finding corrected during review

The developer and technical evidence lists named three obsolete test classes.
The real coverage existed, but the references were not executable navigation
evidence. Commit `98e790a` now names
`PassiveSkillRuntimeTests` and `RuntimePersistenceSnapshotTests` and removes
the nonexistent duplicate transition-test entry.

No remaining mechanics, developer-guide, technical, diagram, or evidence
contradiction was found after that correction.

## Documentation Alignment

All three audiences describe the same contract:

- [Mechanics](../mechanics/status-passive-lifecycle.md) explains observable
  application order, restrictions, owner-turn order, clocks, removal,
  cleanup, passives, and rollback without making presentation authoritative.
- [Developer guide](../developer-guide/status-passive-lifecycle.md) identifies
  required composition, selectable policies, explicit encounter clocks,
  event consumption, custom-handler boundaries, JSON authoring, and
  persistence responsibilities.
- [Technical reference](../technical/status-passive-lifecycle.md) traces the
  actual staged mutation, validation, ordering, event, and restore paths.

The diagrams match the executable control flow. In particular, they do not
conflate a duration clock with removal permission, reserve-owner eligibility
with reserve-target eligibility, or an Instant effect scope with the next
player-selected command.

## Verification

- Focused status, passive, clock, mapper, encounter, immutability, and effect
  coverage: **323 passed, 0 failed, 0 skipped**.
- Documentation foundation, synchronization, and product-boundary coverage:
  **22 passed, 0 failed, 0 skipped**.
- Complete solution: **1,629 passed, 0 failed, 0 skipped**:
  - Framework: 1,449;
  - DemoHost: 173;
  - ContentValidator: 7.
- Strict .NET 8 Release solution build: **0 warnings, 0 errors**.
- `dotnet format --verify-no-changes`: passed.
- Framework coverage: **90.67% lines, 76.25% branches**.
- Active content validation: **6 packs, 36 documents, 98 definitions**.
- Framework trimming analysis: **0 warnings, 0 errors**.
- DemoHost battle, field, save, Training Annex runtime, and scripted
  Training Annex play modes: passed.
- Godot 4.7.1 .NET headless smoke: passed after redirecting the local
  executable's `APPDATA` and `LOCALAPPDATA` to a writable artifact directory.
  The first local attempt could not create `user://logs` in the sandbox and
  crashed in native Godot before loading Convergence; this was an execution
  environment issue, not a managed Framework failure.
- `git diff --check`: passed.

## Residual Boundaries

These are documented integration boundaries, not unresolved Order 4 defects:

- framework transactions cannot reverse file, network, scene, animation, or
  other irreversible work performed inside a host extension;
- an event sink can fail after committed framework state, so host-side effects
  must be idempotent or compensating;
- hosts that execute effects outside the standard executors must dispatch the
  explicit action-end lifecycle boundary; and
- `demoCoverage` remains `focused`; framework completeness does not claim that
  DemoHost presents every status mechanic.

## Closure

O4-R12 through O4-R16 are verified by the current source trace and release
gate. O4-R17 is complete. The framework capability returns from `partial` to
`complete`, and its mechanics, developer, and technical documentation entries
return from `existing_unreviewed` to `reviewed`.

Order 5, `battle_knowledge`, is the next capability in the collaborative
documentation roadmap.
