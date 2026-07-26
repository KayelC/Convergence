# Status And Passive Lifecycle Order 4 Final Closure Review

## Review Identity

- Review date: 26 July 2026
- Reviewed revision: `530d8e40`
- Branch: `main`
- Scope: authored ailments and passives, runtime status state, ailment
  application, turn restrictions, lifecycle clocks, cleanup, passive dispatch,
  encounter integration, save validation and restoration, schema-v8 content,
  focused tests, and all three active documentation audiences
- Method: current source, tests, schemas, content, and executable behavior were
  inspected directly. Earlier review conclusions were not accepted as evidence
  for this verdict.

## Findings

No unresolved realistic, reachable Order 4 correctness defect was found.

The final trace did not identify a security issue, framework-to-host dependency
leak, partial framework-state commit, unsupported hidden fallback, or
documentation contradiction in the reviewed paths. Order 4 is ready for formal
closure at this revision.

## Code-Derived Review

### Ailment application

`BattleAilmentApplicationTransaction` clones the source, target, and complete
participant graph before an application policy or transition policy can mutate
state. The staged path:

1. validates the selected lifetime;
2. rejects a defeated target;
3. evaluates the explicit application gate;
4. resolves resistance and rule modifiers;
5. evaluates chance through the configured policy;
6. resolves same-ailment and exclusivity behavior;
7. validates custom result coherence; and
8. commits only an accepted transition.

Rejected gates, immunity, misses, invalid policy decisions, protected
replacement, exceptions, and malformed replacement-service results leave live
actor state unchanged.

`RuntimeActorState.ApplyAilment` independently enforces valid IDs, runtime-valid
lifetimes, exclusivity, and protected replacement. Restore validation now
rejects two active ailments in one exclusivity group, so live application and
restoration admit the same ailment set.

### Turn-start restrictions

`BattleStatusLifecycleService.ProcessTurnStart` runs against a staged actor.
Guard clears first. The service snapshots ordered `(ailment ID, exact active
instance)` pairs, then rechecks each pair immediately before resolving it.

Therefore:

- a removed ailment does not receive a later slot;
- refreshing or replacing an ailment invalidates its old slot;
- an ailment added during the boundary waits for the next turn start; and
- a custom handler exception rolls back Guard clearing and every staged
  mutation.

`MostRestrictiveBattleTurnPolicy` resolves typed restrictions with deterministic
precedence. Equal limited-action restrictions intersect their action sets; an
empty intersection becomes `Skip`.

### Owner-turn-end lifecycle

For a deployed actor, the source executes this order:

1. passive triggers;
2. ailment triggers;
3. authored or natural ailment recovery;
4. ailment, other-status, and stat-modifier duration ticks.

The ailment-trigger step also uses an exact-instance boundary schedule. An
ailment removed, refreshed, or replaced before its slot cannot execute stale
effects. An ailment created by an earlier passive exists when the later
ailment-step schedule is captured and may execute there. An ailment created
during ailment-trigger processing waits until the next matching boundary.

An undeployed actor does not receive owner-turn-end processing.

### Duration and cleanup authority

The runtime executes all authored duration kinds:

- Instant state expires at the outermost action-end boundary;
- counted state advances only on its authored event ID;
- phase state expires on its authored phase ID;
- battle state expires during battle-end cleanup; and
- permanent state has no automatic expiration.

Reserve advancement is an injected policy. The supplied policy suspends it.
The opt-in advancing policy accepts only an exact team-phase or round event, and
an individual counted duration can still suspend itself while reserve.

Cleanup maps deployment swap, defeat, flee, roster recall, battle end, and
field transition to distinct typed removal causes. Guard, timed state, and the
selected stat-modifier policy are handled in one actor transaction.

### Passive authority

`ValidatingPassiveTriggerDispatcher` validates the request graph before
dispatch, snapshots enabled passive definitions and pre-mutation target
eligibility, dispatches against staged participants, and validates the complete
result before commit.

The result must identify:

- the requested event;
- an enabled passive;
- an authored trigger index for that event;
- a target eligible when dispatch began;
- unique activation evidence;
- authored effect indices and local IDs;
- participant-owned effect and lifecycle actor IDs; and
- no committed effects for a non-executed outcome.

The standard dispatcher preserves deterministic loadout, trigger, target, and
effect order. Conditions that fail do not consume an activation. Recursion and
per-dispatch or per-target activation limits remain explicit event policies.
The supplied defeat-prevention policy is registered only when the host has not
already supplied one.

### Encounter integration

`BattleStatusEncounterLifecyclePort` connects battle start, turn start,
owner-turn end, team phase, round, battle end, and actor departure to the same
lifecycle service.

`BattleEncounterRunner` performs framework-owned flee, roster recall, and newly
observed defeat cleanup through an outer participant transaction. Cleanup
events are published before defeat narration or terminal outcome processing.
Cancellation and lifecycle failure before commit preserve the live participant
graph.

Manual deployment swaps and external roster commands remain host-owned. Their
host must call the corresponding cleanup operation until those commands are
executed inside the canonical encounter transaction. Changing a Vessel's Active
Hosted Entity is not actor departure.

### Save and restore

Actor integrity validation now requires:

- one enabled/disabled state for every equipped passive;
- no state for an unloaded passive;
- every activation key to reference an equipped passive;
- a valid trigger index;
- the exact event authored at that trigger index;
- unique activation keys;
- valid per-target actor references at aggregate save validation;
- available ailment definitions; and
- no active exclusivity-group conflict.

Direct actor restore repeats the definition-sensitive checks before replacing
runtime status or passive state. Fusion and Compendium snapshot producers
preserve passive state instead of constructing incomplete activation snapshots.

### Typed evidence and immutability

Lifecycle result collections defensively snapshot constructor and record-clone
inputs. Ailment transitions, duration ticks, removals, modifier changes,
passive activations, passive effects, departure reasons, and resource changes
carry typed evidence. `Detail` remains optional debug text rather than rule
authority.

`BattleStatusLifecycleEventMapper` strictly validates the specialized passive
and passive-effect combinations it translates. Generic status events preserve
their typed lifecycle event as `BattleStatusChangedEventPayload`.

### Content and host neutrality

Schema-v8 ailments require explicit lifetime, turn behavior, modifiers, and
recovery data. Passive triggers require explicit event, targeting, and typed
effects. Semantic validation checks registrations, percentages, duration
references, supported handler types, groups, conditions, and effect
configuration.

The reviewed framework source contains no console, filesystem, Godot,
archived-product, legacy runtime, or third-party serializer dependency. Its
content implementation uses the .NET BCL JSON APIs to parse host-supplied text,
but no serializer type enters the runtime lifecycle or public snapshot
contracts. Random decisions use the host-supplied `IRandomSource`.

## Documentation Review

The mechanics, developer, and technical documents agree with current source on:

- optional composition and host ownership;
- ailment application and exclusivity;
- exact-instance turn-start and turn-end scheduling;
- passive-before-ailment owner-turn-end order;
- restriction precedence;
- explicit duration clocks and reserve policy;
- independent expiration and removal profiles;
- departure-specific cleanup;
- passive owner eligibility, targeting, recursion, and counting;
- pre-mutation passive eligibility;
- transaction limits around irreversible host work;
- typed event evidence; and
- exact passive and ailment restore validation.

The diagrams use runtime states and real transition boundaries. They do not
represent rejection as stored state or imply that presentation text controls
rules.

## Deliberate Boundaries, Not Findings

- Custom policies and handlers are trusted extension code. Framework actor
  mutation is staged, but an extension's external file, network, scene, or
  animation side effects cannot be rolled back.
- The supported save flow captures framework session state outside an active
  encounter. Mid-encounter scheduler replay is not claimed by Order 4.
- Hosts still own cleanup for manual deployment and roster transitions that
  occur outside the canonical encounter runner.
- Field-time status aging occurs only when a host explicitly dispatches a
  lifecycle clock.

These limits are documented and do not contradict the implemented capability.

## Verification

Before the final documentation promotion:

- focused lifecycle, passive, mapper, persistence, encounter, catalog, schema,
  and content-validation tests passed in two groups: `277/277` and `142/142`;
- the full solution passed `1,657/1,657` tests:
  - Framework tests: `1,477`;
  - DemoHost tests: `173`;
  - ContentValidator tests: `7`;
  - skipped: `0`.

The final release gate against the promoted documentation and executable
matrices passed:

- locked dependency restore and vulnerability audit: clean;
- strict Release solution build: `0` warnings, `0` errors;
- Framework trimming analysis: `0` warnings, `0` errors;
- formatting verification: clean;
- Framework coverage: `90.68%` lines and `76.44%` branches;
- active content validation: `6` packs, `36` documents, and `98` qualified
  definitions;
- all four noninteractive DemoHost modes: successful;
- scripted Training Annex play: successful;
- Godot 4.7.1 headless smoke: `CONVERGENCE_GODOT_SMOKE_OK`;
- active architecture, documentation, API, and boundary tests: `56/56`;
- Framework forbidden-reference search: clean; and
- `git diff --check`: clean.

## Closure Decision

O4-R32 passes the source, documentation, and release-gate review.
`status_and_passive_lifecycle` is promoted from `partial` to `complete`, its
three documentation audiences are promoted from `existing_unreviewed` to
`reviewed`, and Order 5 `battle_knowledge` becomes the next collaborative
documentation subject.
