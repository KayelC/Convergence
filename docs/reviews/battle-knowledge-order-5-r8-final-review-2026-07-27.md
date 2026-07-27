# Battle Knowledge Order 5 R8 Final Review

Status: implementation reviewed; owner confirmation and a successful external
Godot headless smoke remain before formal closure

Date: 2026-07-27

Reviewed revision: `48dda7ca`

## Review Method

This review was performed from current source and executable tests after all
seven Order 5 checkpoints. It did not treat an earlier report as proof. The
review traced:

- persistent knowledge storage, querying, and atomic transitions;
- encounter knowledge identity, observations, temporary influences, and
  cleanup;
- Analyze disclosure and knowledge promotion;
- aggregate action-result processing;
- automated encounter team knowledge and deterministic strategy use;
- acquisition and Compendium familiarity import;
- DemoHost manual-battle integration and save serialization; and
- save validation, audience documentation, capability evidence, and boundary
  tests.

A finding qualified only when a supported caller could reach it, an intended
invariant was violated, and a concrete consequence could be reproduced.

## Final Corrections

The independent review cycle found four reachable defects. Each was corrected
and committed separately.

| Commit | Correction | Consequence prevented |
|---|---|---|
| `6893092a` | Validate every persistent and encounter query key at the public boundary. | Malformed default IDs can no longer enter a delayed or misleading lookup path. |
| `eb52b03d` | Preserve analyzed-defense markers in the DemoHost save codec. | A save round trip can no longer forget that an omitted sparse defense was already known as Normal. |
| `6a4e3715` | Prevent the supplied deterministic selector from treating temporarily influenced encounter facts as timeless. | A stale shield, Break, guard, override, or passive observation can no longer drive later default AI decisions after the influence may have expired. |
| `48dda7ca` | Validate observation and Analyze targets against the enclosing executed effect before applying the aggregate transition. | A custom execution result cannot write evidence for a different runtime target while being accepted as the current effect. |

The fourth correction rejects atomically: both persistent and encounter
`After` snapshots remain their original `Before` instances when provenance is
invalid.

## Source-Backed Behavior

### Persistent knowledge

`RuntimeKnowledgeSnapshot` stores elemental, ailment, instant-defeat, and
analyzed-profile facts by authored entity ID. Public views reject malformed
query IDs. `PersistentBattleKnowledgeTransitionService` validates discoveries,
detects duplicate keys, merges immutable snapshots, and leaves the original
snapshot unchanged on rejection.

### Encounter knowledge

`RuntimeEncounterKnowledgeSnapshot` stores effective facts by runtime target ID
plus authored entity ID. One runtime target cannot identify two entities inside
the snapshot. Ordinary automated encounters create one fresh snapshot per team;
teammates share updates during that run, and final snapshots are returned as
diagnostic evidence rather than silently persisted. Explicit seeds are
validated against current participants.

### Discovery

Framework effect executors emit typed observations. A complete miss reveals
nothing. Landed elemental contact may reveal the effective encounter affinity,
but persistent promotion requires an unmodified authored defense. Ailment and
instant-defeat attempts reveal an exact tier only when typed evidence confirms
immunity. Temporary influence flags preserve why an effective fact differed
from authored state.

### Analyze

`BattleAnalysisService` asks an injected policy for one decision per requested
field. Unknown fields contain no hidden value. Current HP, SP, core stats, and
skills remain encounter-only. Disclosed authored defenses may update persistent
knowledge, including complete-profile markers that make omitted sparse entries
known Normal. The supplied restricted policy can hide boss or special-target
resources, skills, elemental affinities, ailment defenses, and instant-defeat
defenses without inspecting names or IDs.

### Familiarity

Familiarity import is explicit and optional. Standard, disabled, and custom
policies can distinguish acquisition, explicit registration, registered-entry
synchronization, and direct requests. The service does not recruit, fuse,
register, or spend resources. A batch may return accepted imports together with
diagnostics for rejected entries; hosts must deliberately choose partial-batch
or all-or-nothing application.

### Save boundary

Only persistent player knowledge belongs in the aggregate session save.
Encounter snapshots and actor-local runtime-target analysis are not durable
session state. Save validation checks duplicate facts, analyzed markers,
catalog references, and the unsupported actor-local analysis path. DemoHost now
round-trips all persistent knowledge domains and analyzed markers.

## Documentation Review

The mechanics, developer, and technical pages agree with current source on:

- the two knowledge scopes;
- contact and conservative resistance discovery;
- temporary-defense handling;
- Analyze disclosure and restricted fields;
- familiarity imports and their optional policy boundary;
- team-local AI lifetime and explicit seeds; and
- persistence and Godot-host responsibilities.

The final reconciliation renamed the mechanics page to
`docs/mechanics/battle-knowledge.md` and clarified that familiarity imports may
return a partially advanced `After` snapshot with diagnostics. No documentation
claim requires the host to inspect hidden catalog data or parse presentation
text.

## Verification

The corrected revision passed:

- strict .NET 8 Release solution build: zero warnings and zero errors;
- full tests: 1,731 passed, zero failed, zero skipped;
- Framework coverage: 90.73% line and 76.38% branch;
- architecture, documentation-link, and boundary tests: 56 passed;
- content validation: 6 packs, 36 documents, 98 qualified definitions;
- locked dependency restore/audit and trimming analysis;
- all five DemoHost modes, including scripted Training Annex play;
- formatting verification and `git diff --check`; and
- preservation of active content, schema, and configuration files.

The Godot sample project builds with zero warnings, its source-contract tests
pass, and the local executable reports Godot 4.7.1. On this machine, both a
plain headless project bootstrap and the Convergence smoke invocation terminate
inside native Godot with signal 11 before host smoke logic runs. Repeating the
run outside the repository sandbox and with an isolated user directory produced
the same native startup failure. This is not evidence of an Order 5 rule
failure, but it also is not a successful real-engine smoke result. A compatible
environment or CI run must still certify that gate.

## Residual Boundaries

These are intentional boundaries, not unresolved Order 5 defects:

- temporary observations are encounter history; a custom strategy must compare
  influence flags with authoritative live state before treating one as current;
- familiarity imports support partial batches by design, while a host may
  compose atomic behavior by retaining `Before` when diagnostics exist;
- the Godot save codec is a focused integration proof rather than a complete
  game save-file implementation; and
- UI layout, iconography, animation, and target-hover behavior remain host
  presentation responsibilities.

## Verdict

No unresolved realistic reachable Battle Knowledge defect was found at
`48dda7ca` after the four corrections above. The implementation and all three
audience documents are ready for owner confirmation. Formal Order 5 closure
should record that confirmation and should not claim a completely green
repository release gate until the real Godot headless smoke succeeds in a
compatible environment.
