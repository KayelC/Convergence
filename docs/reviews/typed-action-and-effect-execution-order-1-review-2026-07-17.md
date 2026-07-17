# Typed Action And Effect Execution Order 1 Review

**Review date:** 17 July 2026

**Reviewed branch:** `main`

**Capability:** `typed_action_and_effect_execution`

## Verdict

The typed action and effect core is structurally healthy, host-neutral, and
well-tested, but Documentation Order 1 is not complete. One confirmed item
quantity defect and one incomplete reservation boundary require implementation.
The project owner has now confirmed the inventory-port and actor-action
authorization rules that resolve the two design questions raised by this
review. Those decisions remain pending implementation before the capability can
be promoted to reviewed documentation.

The current mechanics page also contains semantic inaccuracies. The missing
developer guide and shallow technical overview remain correctly recorded by the
documentation coverage matrix. This report is review evidence, not mechanics or
design authority.

## Review Method

The review inspected current source rather than relying on prior summaries. It
traced:

- battle action command construction, assessment tokens, and execution dispatch;
- skill and item assessment, target preparation, stale-state revalidation, and
  cost handling;
- shared runtime targeting and injected random-target policies;
- ordered effect execution, failure policies, and actor-state transactions;
- item reservation, commit, rollback, and result reporting;
- host-mediated command results and encounter integration;
- current mechanics, architecture, gameplay overview, documentation matrix, and
  capability matrix;
- focused tests, the full solution, documentation checks, and clean demo output.

## Confirmed Strengths

- Prepared action, skill, and item assessments are bound to their originating
  executor and request and are single-use.
- Random targets are selected during assessment and are not rerolled during
  execution.
- Prepared targets and skill costs are revalidated against current runtime state
  before mutation.
- Skill, item, basic-attack, analyze, and typed-effect actor mutations use staged
  runtime state and publish only after the execution boundary accepts the result.
- Thrown effect-handler failures reject before staged actor state is committed.
- Item reservation failure, commit rejection, and cancellation have focused
  rollback coverage for the currently tested one-item path.
- Skill and shared runtime random targeting require explicit injected policies;
  there is no silent random-selection fallback.
- Framework APIs remain independent of console, filesystem, serializer, and
  Godot types.

## Open Findings

### O1-M1: Item quantity and consumption disagree

**Status:** `open`

**Invariant:** one item action that reports `ConsumeOne` must consume exactly one
inventory quantity.

**Reachable path:** `ItemBattleActionCommand` accepts any positive `Quantity`.
`BattleActionExecutor` reserves and commits that quantity, while `ItemExecutor`
executes the authored effects once and reports `ConsumeOne`.

**Consequence:** a command created with quantity greater than one applies one
item use but removes multiple inventory units.

**Required correction:** remove arbitrary quantity from the one-use command or
replace the consumption contract with an explicitly reviewed multi-use model.
The current mechanic and result vocabulary support the first option. Add a
regression test proving exactly one unit is reserved and committed.

**Evidence:**

- `src/Convergence.Framework/Execution/BattleActionExecutor.cs`, item command
  construction and reservation;
- `src/Convergence.Framework/Execution/ItemExecutor.cs`, `ConsumeOne` decision;
- `tests/Convergence.Framework.Tests/SkillSystem/BattleActionExecutorTests.cs`,
  whose existing inventory cases all use quantity one.

### O1-M2: The reservation returned by a host is not validated

**Status:** `open`

**Invariant:** the reservation committed for an item action must be a live,
uncompleted reservation for the requested item and requested quantity.

**Reachable path:** `IItemActionReservation` exposes `ItemId`, `Quantity`,
`IsCommitted`, and `IsRolledBack`, but execution does not validate those values
after `Reserve` returns.

**Consequence:** a faulty host adapter can return a reservation for another item,
another quantity, or an already-completed reservation. The Framework can then
report the requested item while committing unrelated or inconsistent inventory
state.

**Required correction:** validate reservation identity, quantity, and initial
state before executing effects. Reject with a typed reservation diagnostic and
do not publish actor mutation when the contract is violated. Add wrong-item,
wrong-quantity, already-committed, already-rolled-back, and null-return boundary
tests.

### O1-D1: Inventory-backed action execution requires a port

**Status:** `approved_pending_implementation`

`BattleActionExecutionRequest.ItemInventory` is nullable. With no inventory port,
an item action can commit actor effects and return `ConsumeOne` with
`ItemConsumptionCommitted == false`. The current mechanics page instead describes
reservation as the canonical atomic item path.

**Confirmed decision:** `BattleActionExecutor` requires `IItemActionInventory`
for item commands and rejects before mutation when it is absent. `ItemExecutor`
remains a lower-level typed-effect service, not a complete inventory transaction.
The canonical facade reserves and conditionally commits exactly one item.

### O1-D2: Framework owns actor action authorization

**Status:** `approved_pending_implementation`

The direct action facade accepts a supplied `SkillDefinition` or basic-attack
profile. It does not prove that the actor has the skill equipped or that the
basic attack came from the actor's resolved equipment. The automated encounter
adapter validates catalog skill ownership, but direct hosts can call
`BattleActionExecutor` without that adapter.

**Confirmed decision:** the canonical battle-action facade validates that a
skill belongs to the actor's authorized equipped action loadout and that a basic
attack belongs to the actor's resolved action profile. The profile may come from
equipment, an authored natural attack, or another explicit policy. Hosts select
among authorized actions; arbitrary caller-supplied definitions do not create
authority.

The confirmed authority is recorded in
[Battle Action Ownership And Inventory Authority](../decisions/battle-action-ownership-and-inventory-authority.md).

## Documentation Corrections Required

The current mechanics page must be corrected before promotion:

- targeting selections are `None`, `Single`, `All`, and `Random`; there is no
  implemented `Automatic` selection;
- target relations include `Any`, which the page currently omits;
- `skill grants` are not an executable typed effect and must not be listed as
  implemented;
- assessment rejection, cancellation, and pre-commit exceptions do not spend a
  skill cost, but an executable action whose authored effect resolves as failure
  still commits its cost;
- typed interruption and authored failure policy are valid execution outcomes
  and may commit earlier effects;
- rollback guarantees cover staged Framework actor state and a compliant
  reservation port, not arbitrary side effects performed by a host callback;
- host-mediated actions request host work; the Framework does not perform or
  roll back that external work.

Order 1 must also add:

- a developer guide showing assess, present, execute, cancellation, inventory
  reservation, effect/result consumption, and host-mediated dispatch;
- a technical reference documenting assessment-token ownership, target
  preparation, stale-state checks, effect ordering, failure policies,
  transaction scope, and trusted-boundary decisions;
- sequence diagrams for ordinary skill execution and inventory-backed item
  execution.

## Active Correction Sequence

1. Correct one-use item quantity semantics and add regression coverage.
2. Validate host reservation identity and lifecycle state.
3. Implement the confirmed mandatory inventory-port rule.
4. Implement the confirmed actor action-authorization rule.
5. Re-review the corrected source and focused tests.
6. Rewrite the mechanics page.
7. Add the developer and technical documents with diagrams and examples.
8. Run documentation, subsystem, full-solution, demo, and boundary gates.
9. Promote the three documentation audience entries only after owner
   confirmation.

## Verification Evidence

At review time:

- 65 focused action/effect tests passed;
- 1,030 full-solution tests passed with zero skipped tests;
- the solution built with zero warnings and zero errors;
- six focused documentation contract/link tests passed;
- clean battle and Training Annex demos exited successfully;
- `git diff --check` passed and the worktree was clean.

These green gates establish regression health for covered behavior. They do not
invalidate the uncovered quantity and reservation findings above.
