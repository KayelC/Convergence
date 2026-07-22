# Combat Resolution Order 2 Registration-Parity Corrections Review

**Review date:** 22 July 2026

**Reviewed baseline:** `61d539b` plus the O2-R34 documentation changes

**Result:** verified; no unresolved reachable defect found in the corrected
assessment, registration, target-preparation, mutation, or turn-consumption
paths

## Findings

No unresolved finding remains in the reviewed scope.

The review did identify one documentation-completeness issue while tracing the
source: the three audience documents described explicit registrations but did
not explain the new shared preflight order. O2-R34 corrected that prose and
added an executable synchronization assertion before this verdict was recorded.

## Method

This review started from current implementation and tests rather than treating
the earlier review or correction roadmap as proof. It traced:

1. public battle-action assessment and execution;
2. lower-level skill and item assessment;
3. effect executor and runtime-registration validation;
4. recursive condition composition;
5. prepared target rebinding and stale-target rejection;
6. staged actor mutation and item reservation boundaries; and
7. final action-outcome and turn-consumption construction.

The active mechanics, developer, technical, decision, schema, roadmap, API,
and source-inventory records were then compared with that implementation.

## Source Trace

| Invariant | Current implementation | Result |
|---|---|---|
| One registration authority | `EffectConfigurationValidator` validates ailment, formula, escape, custom-effect, and recursively nested custom-condition dependencies. | verified |
| Skills reject before random targets | `SkillExecutor.Preflight` validates percentages, effect support, and registrations before target and cost resolution. | verified |
| Items reject before random targets or inventory | `ItemExecutor.AssessCore` runs the same preflight before target resolution; the battle facade reserves only after accepted assessment. | verified |
| Direct actions have assessment parity | `BattleActionExecutor.AssessEffectAction` runs the shared preflight before `RuntimeTargetResolver`; basic attacks and escape return `EffectConfigurationInvalid`. | verified |
| Rejection is free | Rejected assessment and execution results use `ActionTurnConsumption.None`; missing escape registration cannot become an ordinary failed escape. | verified |
| Prepared targets are stable | Execution rebinds captured runtime IDs, revalidates eligibility, and rejects stale state without rerolling. | verified |
| Actor mutation is staged | Skills, items, and direct effects execute against transaction clones; pre-commit exceptions do not publish actor state. | verified |
| Registered behavior still executes | Existing valid formula, ailment, custom-effect, custom-condition, and escape tests remain green. | verified |
| Public and repository boundaries remain coherent | The new diagnostic is in the shipped API baseline; the internal validator is in the source inventory and does not expose host types. | verified |

## Regression Reproduction

Six focused public-boundary cases passed:

- four direct basic-attack configurations missing an ailment, formula handler,
  nested custom-condition handler, or custom-effect handler;
- one direct escape attempt missing its rule handler; and
- one paired skill/item formula-registration case proving both reject before
  random target selection.

All returned typed rejection, no effect mutation, no target reroll, and no turn
consumption. The broader combat/action policy slice passed 274 tests.

## Documentation Reconciliation

- Current authoring and product guidance derives schema v6 from active content
  manifests; historical schema-v3 and schema-v5 records remain historical.
- Instant-defeat resistance uses `Vulnerable`, `Normal`, `Resistant`, and
  `Immune`; elemental Null, Repel, and Absorb are not mapped into that channel.
- Mechanics, developer, and technical action documents now explain shared
  registration preflight, ordering, diagnostics, and no-mutation behavior.
- O2-R16 remains the original ordered-effects milestone. O2-R30 through O2-R34
  are the later registration-parity and documentation correction chain.

## Verification Evidence

- focused combat/action policy tests: 274 passed;
- focused correction and documentation tests: 16 passed;
- architecture, documentation-link, API, and boundary tests: 53 passed;
- complete Release suite: 1,458 passed, 0 failed, 0 skipped;
- strict nonincremental Release solution build: 0 warnings, 0 errors;
- `dotnet format --verify-no-changes`: passed;
- active content: 6 packs, 36 documents, and 98 definitions validated;
- clean battle, field, save, and Training Annex demos: exited 0;
- scripted Training Annex behavior: covered by the 173 passing DemoHost tests;
- `git diff --check`: passed.

## Residual Boundaries

Custom handlers remain host extension code. Actor mutations made through their
staged execution context are transactional, while unrelated scene, file,
network, or service side effects cannot be rolled back by Framework. The public
guides require such work to be represented as host-action requests. This is an
explicit ownership boundary rather than an unresolved Order 2 defect.

Lifecycle-trigger composition, future turn-economy families, and later
documentation orders retain their own review scope. They were not used to weaken
or condition this registration-parity verdict.

## Verdict

O2-R30 through O2-R34 are complete. The corrected source, executable tests, and
current audience documentation agree. Order 2 may return to verified status and
Order 3 may begin when the project owner chooses.
