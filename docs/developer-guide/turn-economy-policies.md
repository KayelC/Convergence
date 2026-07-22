# Turn Economy Policies

## Purpose

This guide shows how a game selects a supplied turn economy, passes it to the
encounter runner, presents typed state, and replaces it safely.

Turn economy counts action opportunities. It does not choose the next actor or
draw the UI. The encounter runner owns the current team-phase schedule; Godot
or another application host owns input, icons, animation, and localization.

## Choose A Supplied Policy

Convergence supplies two authored choices:

| `policyId` | Use when |
|---|---|
| `standard_actions` | Each priced command should spend one ordinary action. |
| `standard_action_token` | Affinity and accuracy outcomes should change a full/partial token pool. |

Both accept exactly these required parameters:

| Parameter | Type | Rule |
|---|---:|---|
| `maximumCommands` | integer | Must be positive. Counts every command in one phase. |
| `maximumConsecutiveFreeActions` | integer | Must be nonnegative and lower than `maximumCommands`. |

Example neutral ruleset:

```json
{
  "id": "standard_actions",
  "displayName": "Standard Actions",
  "description": "One action for every actor present at phase start.",
  "category": "turn_economy",
  "policyId": "standard_actions",
  "parameters": {
    "maximumCommands": 64,
    "maximumConsecutiveFreeActions": 8
  }
}
```

To select Action Token, change the record ID and `policyId` to
`standard_action_token`. The liveness parameters do not choose balance; they
bound a broken or unexpectedly self-extending phase.

Register the policy ID in the content vocabulary used to load the pack:

```csharp
SkillSystemRegistrationSnapshot registrations = new SkillSystemRegistrationBuilder()
    .RegisterPolicy("standard_actions")
    // Register the rest of this game's content vocabulary.
    .Build();
```

Registration permits the authored vocabulary. Runtime construction is a
separate explicit step.

## Bind At Startup

Use the same factory registry and resolver used by other ruleset categories:

```csharp
var factories = RuntimeRulesetPolicyFactoryRegistry.CreateStandard();
var resolver = new RuntimeRulesetBindingResolver(factories);

BattleTurnEconomyRuleset economy = resolver
    .BindTurnEconomy(catalog, turnEconomyRulesetId)
    .RequireService();
```

`BattleTurnEconomyRuleset` contains:

- `CreateEconomy`, which must create a fresh stateful economy for each phase;
- `PhaseProgress`, which contains the authored safety limits.

Do not cache and reuse one created economy across phases or encounters. Cache
the immutable bound ruleset and invoke its factory when the encounter asks.

A missing ruleset, category mismatch, unsupported policy, unknown parameter,
wrong type, or invalid liveness combination returns typed diagnostics. Stop
composition and present those diagnostics; do not instantiate a different
economy as a fallback.

## Pass The Bound Policy To An Encounter

```csharp
var services = new BattleEncounterServices(
    initiativePolicy,
    lifecyclePort,
    turnHandler,
    completionPolicy,
    economy.CreateEconomy,
    economy.PhaseProgress,
    events: encounterEventSink);

BattleEncounterResult result = await new BattleEncounterRunner().RunAsync(
    encounterRequest,
    services,
    cancellationToken);
```

The turn handler returns a validated `BattleEncounterCommandResult`. Its
`TurnConsumption` is the sole input to the selected economy:

- `None`: no Framework-required opportunity cost;
- `Normal`: ordinary action cost;
- `Pass`: explicit pass cost;
- `TurnEconomy`: effect-derived `TurnEconomyResolution`;
- `TerminatePhase`: clear the phase regardless of economy-specific outcomes.

The economy does not inspect the skill, item, effect list, or display text.

### Return A Coherent Command Result

`BattleEncounterCommandResult` validates the relationship between status,
outcome, winner, diagnostic, and turn cost at construction:

| Status | Turn cost | Requested outcome | Winner | Fault message |
|---|---|---|---|---|
| `Executed` | Any valid cost | No request, `Victory`, `Defeat`, `Escape`, or `Draw` | Only with victory/defeat | None |
| `Cancelled` | `None` | `Cancelled` | None | None |
| `Rejected` | `None` | `Faulted` | None | Required |
| `Faulted` | `None` | `Faulted` | None | Required |

Use the static factories for ordinary construction. A contradictory host
result throws inside the turn-handler port boundary; the runner reports a typed
fault before economy application or owner-turn-end lifecycle.

Framework-calculated assessment and execution-result turn costs are
getter-only. A host-mediated command may be cloned to another valid cost, but
its validating initializer rejects null.

### Return only port-owned events

A command or lifecycle adapter may attach detail evidence to its result, but
it does not author encounter structure. The allowed port event kinds are:

- command evidence: `CommandSelected`, `CommandPassed`, `ActionExecuted`, and
  `ActionRejected`;
- execution evidence: `EffectResolved`, `PassiveActivated`, `StatusChanged`,
  and `ResourceChanged`;
- deployment and host integration: `EncounterPresenceChanged` and
  `HostActionRequested`.

Do not return `ActorCreated`, battle/round/phase/turn events,
`TurnEconomyChanged`, `ActorDefeated`, `BattleFaulted`, or `BattleEnded` from a
port. Those are runner-owned structural facts. The runner validates the
allow-list before sequencing or publishing a returned event, and it rejects a
new or unclassified event kind by default.

For example, return `BattleEncounterCommandResult.Faulted(message)` when an
action cannot be completed. Add `ActionRejected` only when the host needs
typed action-level detail. The runner will publish the canonical
`BattleFaulted` and `BattleEnded` events. An invalid turn-handler event becomes
a typed `TurnHandlerExecutionFailed` result before the action cost is applied;
an invalid lifecycle event becomes `LifecycleExecutionFailed` before its
staged lifecycle transaction commits.

## Present Typed State In Godot

Implement `IBattleEncounterEventSink` and inspect the payload kind:

```csharp
public ValueTask PublishAsync(
    BattleEncounterEvent battleEvent,
    CancellationToken cancellationToken = default)
{
    switch (battleEvent.Payload)
    {
        case BattlePhaseStartedEventPayload started:
            PresentEconomy(started.TurnEconomyState);
            break;

        case BattleTurnEconomyChangedEventPayload changed:
            AnimateConsumption(changed.Consumption);
            PresentEconomy(changed.After);
            break;

        case BattlePhaseEndedEventPayload ended:
            PresentEconomy(ended.TurnEconomyState);
            break;
    }

    return ValueTask.CompletedTask;
}
```

Choose presentation from the concrete snapshot:

```csharp
private void PresentEconomy(BattleTurnEconomySnapshot snapshot)
{
    switch (snapshot)
    {
        case ActionTokenTurnEconomySnapshot tokens:
            actionTokenHud.Show(tokens.FullTokens, tokens.PartialTokens);
            break;

        case StandardActionTurnEconomySnapshot actions:
            actionCounter.Show(actions.RemainingActions);
            break;

        default:
            customEconomyPresenter.Show(snapshot);
            break;
    }
}
```

`DebugText` is optional diagnostics. Never parse it to recover token counts,
actor identity, or consumption type.

The structural payloads in this switch are runner-owned. Once received by the
sink, they are the accepted encounter state rather than a duplicate claim from
a command or lifecycle adapter.

## Action Token Worked Example

Suppose a phase starts with two actors:

```text
start          [full, full]
hit weakness   [partial, full]
pass           [full]
normal action  []
```

The pass consumes the existing partial token. It does not convert the full
token while a partial token exists.

If the phase instead starts with one full token:

```text
start          [full]
pass           [partial]
pass again     []
```

## Supply A Custom Economy

Implement `IBattleTurnEconomy` when opportunity accounting differs from both
supplied policies. A valid implementation must:

1. reject invalid phase-start input;
2. return a fresh immutable snapshot on every capture;
3. keep one valid `EconomyId` and one concrete snapshot type for the phase;
4. make `HasTurnsRemaining()` agree with `RemainingActions > 0`;
5. change state only when the encounter runner calls `Apply`;
6. validate every `ActionTurnConsumption` it uses; and
7. remain finite under the host's phase-progress limits.

For content-authored selection, also implement
`IRuntimeTurnEconomyRulesetPolicyFactory` and register it in the turn-economy
category of a host-created `RuntimeRulesetPolicyFactoryRegistry`. The factory
owns parameter validation and returns either one complete
`BattleTurnEconomyRuleset` or typed diagnostics.

Direct injection is useful for focused tests or a game that does not author
ruleset records. Authored binding is preferred when content selects the
policy. Neither route permits silent fallback.

## Understand The Scheduling Limit

The current encounter runner uses ordered team phases and rotates through
active actors after each executed command window. `ActionTurnConsumption.None`
does not run owner-turn-end lifecycle, but the current scheduler still rotates
to the next actor command window.

Therefore, a custom economy alone cannot implement:

- agility-sorted individual turns across teams;
- an immediate second command for the same actor;
- interruption of team order by a bonus action; or
- another definition of a turn window for lifecycle clocks.

Those require the future encounter-scheduling extension tracked under
Documentation Order 6. Keep this distinction visible in game architecture so a
replacement economy is not made responsible for actor ordering by accident.

## Diagnose Rejected Extensions

The runner converts supported economy failures into typed encounter faults:

| Fault condition | Result |
|---|---|
| Snapshot and liveness disagree | `TurnEconomyTransitionInvalid` |
| ID, type, or state changes outside `Apply` | `TurnEconomyTransitionInvalid` |
| `Apply`, snapshot, or liveness method throws | `TurnEconomyExecutionFailed` |
| Too many unchanged free commands | `ConsecutiveFreeActionLimitExceeded` |
| Too many commands in one phase | `PhaseCommandLimitExceeded` |

Initial and between-command contradictions are rejected before the next
lifecycle or command mutation. The runner also revalidates retained economy
authority after lifecycle, handler, event, and synchronization callbacks.
Staged lifecycle work is discarded when a callback changes the economy. If a
custom economy returns malformed state after an already committed host command,
the encounter faults, but the runner cannot retroactively roll back arbitrary
host-owned command work. Custom economies must therefore keep `Apply`
exception-safe and truthful, and host ports must not mutate a retained economy
instance.

## Related Documentation

- [Turn Economy Policy Family](../decisions/turn-economy-policy-family.md)
- [Combat, Defenses, And Turn Economy](../mechanics/combat-defenses-and-turns.md)
- [Turn Economy Runtime](../technical/turn-economy-runtime.md)
- [Ruleset Policy Contracts](../ruleset-policy-contracts.md)
- [Godot Integration Contract](../godot-integration-contract.md)
