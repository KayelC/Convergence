# Battle Knowledge Order 5 Pre-Closure Independent Audit

## Review Basis

This review was performed against the current source at `4ebb0e55`. Prior
review reports and their conclusions were not used as implementation evidence.
The review traced the exported contracts, standard implementations, call sites,
tests, active audience documentation, and executable quality gates directly.

The reviewed surface included:

- persistent and encounter knowledge snapshots and views;
- ordinary execution observations and Analyze disclosure;
- aggregate execution provenance and rollback;
- combat-profile identity and Hosted Entity replacement;
- automated-team learning, fresh encounters, and explicit seeds;
- familiar-entity imports from acquisition and Compendium flows;
- save validation and aggregate session restoration;
- Training Annex player and enemy knowledge ownership; and
- mechanics, developer, and technical Battle Knowledge documentation.

## Findings

### Low: Familiar imports can report success for malformed current knowledge

`FamiliarEntityKnowledgeService.Import` checks current knowledge identifiers
and duplicate keys at
[`CompendiumRuntimeServices.cs`](../../src/Convergence.Framework/Fusion/CompendiumRuntimeServices.cs),
but it does not validate the current snapshot's enum and analyzed-field domains
before processing the policy. If the policy selects no fields, or no requested
entity reaches the import stage, the early return at line 932 bypasses the
canonical persistent transition at line 937.

This is reachable through the public API without reflection. The public
persistent entry records expose `init` properties, so a record clone can replace
a constructor-validated value with an undefined enum or with the non-storable
`Almighty` element. The existing adversarial cases in
[`PersistentBattleKnowledgeTests.cs`](../../tests/Convergence.Framework.Tests/Runtime/PersistentBattleKnowledgeTests.cs)
prove that this is a supported malformed-host-input scenario for the canonical
view and transition boundaries.

A concrete path is:

1. clone a valid elemental entry with an undefined affinity;
2. place it in `RuntimeKnowledgeSnapshot`;
3. use `DisabledFamiliarKnowledgeImportPolicy`; and
4. call `Import` for a valid catalog entity.

The disabled policy returns no fields, `imported.Count` remains zero, and the
service returns `IsSuccess == true` with the malformed snapshot unchanged.
The persistent view, save validator, or a later non-empty import will reject the
same state, so this does not corrupt a valid snapshot or create a player-facing
knowledge exploit. It is nevertheless a false success at a public framework
boundary and conflicts with the developer guide's promise that malformed
host-supplied knowledge rejects through typed diagnostics.

**Required correction, O5-R26:** validate the complete current snapshot before
any policy or empty-import return. Map undefined enums, invalid persistent
analysis fields, intrinsic-element entries, invalid identifiers, and duplicates
to stable familiar-import diagnostics. Rejection must retain the exact `Before`
snapshot, return it as `After`, and report no imported entity IDs.

**Required evidence:** cover every clone-bypassed persistent domain with the
disabled policy and with an empty or non-importable request. Preserve the valid
disabled-policy no-op and partial-batch semantics.

**Impact of correction:** malformed host state fails earlier and consistently.
Valid acquisition, Compendium, Analyze, save, AI, and battle behavior does not
change.

## Confirmed Healthy Behavior

No High- or Medium-severity finding was reproduced.

- Persistent player facts are keyed to the combat-profile source entity.
- Encounter facts are keyed to runtime target plus exact source actor, source
  entity, and profile revision.
- A successful profile replacement invalidates elemental, ailment,
  instant-defeat, and Analyze encounter state together.
- Typed damage teaches on contact and not on a complete miss.
- Temporary guard, shield, Break, override, and passive influence cannot
  overwrite persistent authored knowledge.
- Ailment and instant-defeat attempts teach exact resistance only after a
  confirmed immunity block.
- `Almighty` is excluded from ordinary persistent and encounter storage.
- Boss-safe Analyze is policy-controlled and can hide HP, SP, skills,
  elemental affinities, ailment resistances, and instant-defeat resistances.
- Execution evidence is preflighted against accepted action, actor, effect,
  target, and exact combat profile before any lower transition is published.
- Player and enemy knowledge are separate. Ordinary AI starts each encounter
  empty, shares discoveries only with its own team, and retains nothing unless
  the host explicitly supplies a valid seed.
- Familiarity import remains optional and player-owned. It does not train AI.
- Session saves retain persistent knowledge and deliberately omit ordinary
  encounter knowledge.
- Public result and snapshot collections inspected in this surface are
  immutable defensive copies.

## Documentation Review

The three active audience documents accurately describe the approved gameplay
model and current standard implementation:

- [`mechanics/battle-knowledge.md`](../mechanics/battle-knowledge.md)
- [`developer-guide/battle-knowledge.md`](../developer-guide/battle-knowledge.md)
- [`technical/battle-knowledge-runtime.md`](../technical/battle-knowledge-runtime.md)

Their player/AI lifetime rules, profile identity, temporary-defense behavior,
Analyze restrictions, familiar import, and save ownership match source. The one
over-broad malformed-input guarantee in the developer guide becomes accurate
once O5-R26 routes the familiarity boundary through complete validation.

No mechanics rewrite is required.

## Verification

The following local gates passed:

- focused Framework Battle Knowledge and adjacent tests: 267 passed;
- focused DemoHost knowledge, Compendium, and save tests: 22 passed;
- full solution: 1,761 passed, 0 failed, 0 skipped;
- Release nonincremental build with warnings as errors: 0 warnings, 0 errors;
- architecture and documentation boundary tests: 56 passed;
- `dotnet format --verify-no-changes`;
- Framework coverage: 90.75% lines and 76.46% branches;
- content validation: 6 packs, 36 documents, and 98 definitions;
- Training Annex noninteractive runtime demo;
- refined Framework forbidden-reference search; and
- `git diff --check`.

The online NuGet vulnerability audit was not refreshed in the restricted local
environment. It remains a connected CI release gate and is unrelated to this
Order 5 finding.

## Closure Decision

Order 5 is **not formally closed at this revision**. The approved mechanics and
their principal runtime paths are healthy, but the public familiarity boundary
must not report success for malformed current knowledge.

After O5-R26 and its focused regression coverage, O5-R27 should perform a short
source and documentation recheck plus the complete local gate. If that review
finds no realistic reachable defect, `battle_knowledge` may return to
`complete` and Order 5 may close.
