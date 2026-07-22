# Turn Economy Order 3 Owner-Closure Audit

**Date:** 22 July 2026
**Revision reviewed:** `7aa3467e`
**Capability:** `turn_economy`
**Verdict:** not ready for formal closure

## Review Method

This audit was performed from the current implementation, tests, active
content, sample-host composition, and audience documentation. Earlier review
reports and their conclusions were not used as evidence.

The source trace covered:

- `ActionTurnConsumption` and `TurnEconomyResolution` construction;
- `StandardActionTurnEconomy` and `ActionTokenTurnEconomy` transitions;
- complete-action outcome aggregation for skills, basic attacks, and items;
- ruleset factory registration and authored binding;
- phase start, command, lifecycle, event, liveness, and phase-end handling in
  `BattleEncounterRunner`;
- automated, DemoHost, and Godot consumption paths;
- direct turn-economy, encounter, action-outcome, and ruleset tests; and
- mechanics, developer, technical, decision, and matrix documentation.

An actionable finding in this report identifies an intended invariant, a
reachable supported path, a concrete consequence, and reproducible evidence.

## Findings

### M1. Port-provided events can impersonate runner-owned encounter authority

**Severity:** Medium
**Scope:** Order 3 typed turn-economy evidence, with a shared Order 6 event-boundary implication

**Intended invariant**

`PhaseStarted`, `TurnEconomyChanged`, `PhaseEnded`, and `BattleEnded` are
structural facts produced by encounter orchestration. A host event sink should
be able to treat their typed payloads as the runner's accepted state rather
than as suggestions supplied by another port.

**Reachable path**

1. A normal host implements `IBattleEncounterTurnHandler`, which is a required
   encounter extension point.
2. `BattleEncounterCommandResult` accepts any non-null
   `BattleEncounterEvent`; it does not restrict event kinds to command-owned
   events.
3. Before applying the command's `ActionTurnConsumption`, the runner
   resequences and publishes every returned command event.
4. Public event constructors allow the handler to return a structurally valid
   but false `TurnEconomyChanged`, `PhaseEnded`, or `BattleEnded` event.
5. The runner later publishes its own canonical structural event without any
   provenance marker that lets the sink distinguish the two.

The same unrestricted `AddRangeAsync` path is used for lifecycle-returned
events, so the problem is not limited to one built-in handler.

**Concrete consequence**

The Framework's actual economy state remains protected, but the ordered event
stream can contain contradictory authoritative claims. A Godot UI may animate
or cache a false token state, and a sink reacting to a forged `BattleEnded`
event may begin battle teardown before the encounter has ended.

This is reachable without malformed IDs, reflection, impossible numeric
values, or mutation of Framework internals. It requires only a custom host
handler returning a public event type that the current API accepts. The active
DemoHost demonstrates the consequence: its event sink trusts every
`TurnEconomyChanged` event and records the first matching after-state.

**Reproduction shape**

```csharp
return BattleEncounterCommandResult.Executed(
    ActionTurnConsumption.Pass,
    [new BattleEncounterEvent(
        0,
        BattleEncounterEventKind.TurnEconomyChanged,
        new BattleTurnEconomyChangedEventPayload(
            actorId,
            new ActionTokenTurnEconomySnapshot(1, 0),
            new ActionTokenTurnEconomySnapshot(0, 0),
            ActionTurnConsumption.Normal))]);
```

For a one-token phase, the sink first receives the port-authored false state
`[0 full, 0 partial]`. The runner then applies the real Pass transition and
publishes `[0 full, 1 partial]`. Both events are valid, sequenced, and
indistinguishable by origin.

**Source evidence**

- `BattleEncounterCommandResult` snapshots arbitrary non-null events in
  `src/Convergence.Framework/Encounters/BattleEncounterRunner.cs` around
  lines 355-364.
- `AddRangeAsync` resequences and publishes every supplied event around lines
  797-806.
- command events are published before the runner-owned `Apply` call around
  lines 1128-1188.
- the canonical turn-economy event is created only by the later
  `AddTurnEconomyAsync` call around lines 1291-1296.
- `TrainingAnnexTurnEconomyEventSink` trusts the event kind and payload in
  `samples/Convergence.DemoHost/Hosts/TrainingAnnex/TrainingAnnexBattleActionAdapter.cs`
  around lines 1692-1705.
- the active technical document calls these payloads authoritative in
  `docs/technical/turn-economy-runtime.md` under **Typed Events**.

**Required correction**

Define and enforce event provenance at port ingress. At minimum:

1. classify runner-owned structural kinds separately from lifecycle- and
   command-owned kinds;
2. reject a port result containing a runner-owned kind before publishing it;
3. convert rejection into the existing typed port-fault boundary;
4. test false `PhaseStarted`, `TurnEconomyChanged`, `PhaseEnded`,
   `BattleFaulted`, and `BattleEnded` events from both command and lifecycle
   ports; and
5. retain one canonical runner event for each structural transition.

The correction should be designed once for the encounter event family rather
than special-casing only `TurnEconomyChanged`. Its narrow implementation is an
Order 3 closure dependency because current Order 3 documentation asks hosts to
trust typed economy events. The broader event-ownership model belongs to Order
6 documentation.

## Verified Mechanics

No defect was found in the supplied transition rules themselves:

- Action Token starts with one full token per active phase-start actor.
- Normal consumes partial before full.
- Pass consumes partial before full; with no partial token it converts one
  full token to partial.
- Weakness and Critical convert a full token or consume a lone partial token.
- Miss and Null consume up to two tokens, partial first.
- Repel, Absorb, and explicit termination clear the phase.
- neutral standard actions charge one opportunity for Normal, Pass, and
  effect-derived outcomes while deliberately ignoring affinity pricing.
- non-escape items use one Normal outcome under the supplied combat default;
  `effect_driven` remains an explicit authored alternative.

No defect was found in the supported state and liveness boundary:

- consumption and resolution shapes reject undefined or contradictory values;
- supplied snapshots reject invalid IDs, negative counts, and checked-total
  overflow;
- the runner validates initial snapshot/liveness agreement before a command;
- economy identity, concrete snapshot type, state equality, and liveness are
  checked throughout the command window;
- a paid command must advance the selected economy;
- unchanged free commands and expanding custom economies are bounded by
  separate authored limits; and
- both supplied policies bind through the typed ruleset factory registry with
  no silent fallback.

## Documentation Alignment

The confirmed mechanics, developer, technical, and decision documents agree
with the actual supplied transition table, optional-policy design, pass
precedence, item default, liveness rules, persistence exclusion, and separation
between opportunity counting and actor scheduling.

The sole material mismatch is event authority. The documents tell hosts to
trust typed structural payloads, but the current runner does not prove that
those events came from the runner. Those audience entries should remain
reopened until M1 is corrected and the event sequence is reverified.

## Adjacent Orders

Do not jump directly into the full Order 6 encounter-orchestration review yet.
The current issue should be fixed as a narrow shared-boundary checkpoint, then
Order 3 should receive one focused closure recheck. Actor scheduling remains a
separate, already documented Order 6 responsibility; changing scheduling is
not required to correct Action Token rules.

Order 4 remains the next full documentation order after this closure defect is
resolved. Its lifecycle review should reuse the event-provenance rule rather
than inventing another event path.

## Verification

Focused verification at `7aa3467e`:

```text
dotnet test tests/Convergence.Framework.Tests/Convergence.Framework.Tests.csproj
  --no-restore
  --filter TurnEconomyContractTests|BattleEncounterRunnerTests|
           ActionOutcomeAggregationPolicyTests|RuntimeRulesetBindingTests

Passed: 170
Failed: 0
Skipped: 0
```

The green focused suite confirms the existing transition and guard behavior;
it does not contradict M1 because no current test returns a runner-owned
structural event through a port and asserts rejection.

## Closure Decision

Order 3 is mechanically sound but is **not formally complete** at
`7aa3467e`. M1 must be corrected, regression-tested, documented, and followed
by another source-first closure review. No Action Token balance or transition
rule needs redesign.
