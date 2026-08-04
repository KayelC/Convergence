# Encounter Orchestration Order 6 R20 Source-Closure Review

**Date:** 4 August 2026

**Capability:** `encounter_orchestration`

**Reviewed revision:** `1b055a1e`

**Review checkpoint:** O6-R20

**Result:** one bounded correction required

## Purpose

This review re-read the corrected encounter implementation before Order 6 was
allowed to close. Earlier reports and correction summaries were not treated as
proof. The review established behavior from current source and tests, then
compared that behavior with the active mechanics, developer, and technical
contracts.

The review distinguishes reachable integration-contract defects from trusted
host boundaries and theoretical hardening. O6-R15 through O6-R19 are materially
correct, but one public event shape still permits malformed command evidence to
evade the scheduled-actor invariant.

## Source Trace

The source-first trace covered:

- encounter request validation, startup, scheduling, command windows, turn
  economy, completion, reconciliation, cancellation, and fault finalization in
  `BattleEncounterRunner`;
- team-phase and Agility scheduling, immutable schedule revisions, bounded
  post-command retention, and phase/round completion;
- lifecycle staging and rollback in `BattleEncounterLifecycleTransaction`,
  `BattleStatusEncounterLifecyclePort`, and the explicit lifecycle clocks;
- event payload validation, frozen-participant ownership, nested effect/status
  validation, and scheduled command-evidence correlation;
- the automated runner's prepared-assessment checks, restriction handling,
  untargeted actions, host requests, escape outcomes, and action-token use; and
- the canonical `BattleActionExecutor` event producers used to determine which
  action event kinds genuinely lack an actor.

The current code confirms that repeated defeat periods, zero-survivor draws,
automated untargeted and terminal results, and non-null executed-action actor
correlation are now implemented as intended.

## Finding

### O6-R20-M1: ordinary executed-action evidence may omit its actor

**Invariant:** every `ActionExecuted` event associated with an actor-owned
action must identify the actor who owns the current command window. Only an
action event whose domain transition genuinely has no actor may omit the actor
ID. In the canonical executor, that exception is
`BattleActionEventKind.PartyRosterTransitioned`.

**Reachable path:** a custom Godot, console, or test turn-handler adapter returns
an otherwise valid `BattleEncounterCommandResult` containing
`BattleActionExecutedEventPayload(BattleActionEventKind.Executed)` without an
actor ID.

**Current behavior:** payload validation treats `ActorId` as optional for every
`BattleActionEventKind`. Scheduled-actor correlation runs only when that
optional value is present. The malformed ordinary event is therefore accepted,
sequenced, and published without command ownership:

- `BattleEncounterEvents.cs` validates only an optional actor for
  `BattleActionExecutedEventPayload`; and
- `CommandEvidenceActor` returns that absent value, bypassing the command-window
  comparison.

The canonical `BattleActionExecutor` supplies an actor for pass, guard, item,
effect, and host-mediated action events. Its only actorless action event is the
party-roster transition.

**Consequence:** an integration bug can publish an ordinary executed action
without an owner even though the encounter contract claims command evidence is
correlated with the scheduled actor. A presentation or telemetry host cannot
reliably map the event to a scene actor, and the malformed adapter receives no
typed encounter fault.

**Severity:** Medium integration correctness. This is not a player-controlled
security issue and does not bypass action authorization or mutate another actor.
It is a realistic public-port contract hole because custom turn handlers are a
supported host extension point.

**Required correction:** require a valid `ActorId` for every
`BattleActionExecutedEventPayload` except
`BattleActionEventKind.PartyRosterTransitioned`. Keep actorless roster evidence
valid. A non-null roster actor may still be correlated normally. Add direct
payload-shape tests and an encounter regression proving malformed actorless
ordinary evidence becomes a typed `TurnHandlerExecutionFailed` fault before
publication.

## Confirmed Healthy Areas

No additional reachable defect was found in the reviewed Order 6 scope:

- lifecycle actor mutations and lifecycle clock sequences are staged and roll
  back together when a lifecycle port fails validation;
- schedule identity, revision, step sequence, team membership, actor membership,
  and liveness bounds are checked before transitions are accepted;
- cancellation is checked before command reads and lifecycle commits, while
  typed command cancellation remains distinct from operational cancellation;
- repeated defeat cleanup is transition-aware and stable reconciliation does not
  duplicate cleanup or announcements;
- zero living teams complete immediately as a draw under both supplied
  schedulers;
- the automated runner preserves authorized untargeted actions, ordered host
  requests, escape, action outcomes, and bound turn economy; and
- final encounter results contain detached immutable participant snapshots and
  ordered immutable event snapshots.

## Trusted Boundaries That Are Not Findings

- A turn handler may perform action mutation before returning. Action-level
  atomicity belongs to the action executor used by that handler.
- A synchronizer is a trusted host adapter over live participants.
- Failure of an event sink after a committed transition cannot generically undo
  that transition.
- A status lifecycle port owns mutable lifecycle-clock sequences and must not be
  shared by overlapping encounters.

These constraints are explicit integration obligations rather than hidden
framework vulnerabilities.

## Correction Roadmap

| Checkpoint | Work | Required evidence |
|---|---|---|
| O6-R21 | Close the actorless ordinary-action event shape. | Direct event validation plus runner fault and actorless roster acceptance tests. |
| O6-R22 | Reconcile event-ownership guidance and maturity evidence. | Mechanics, developer, technical, API, matrix, roadmap, and documentation tests. |
| O6-R23 | Perform a fresh source-first closure review and full local gate. | No reachable Order 6 defect, all clean tests/builds/demos/checks green, and formal capability/documentation promotion. |

Each checkpoint receives an isolated green commit. Order 6 remains open until
O6-R23 concludes without another reachable correction.

## Closure Decision

Order 6 is **not yet ready for formal closure** at revision `1b055a1e`. The
remaining correction is narrow and does not invalidate the encounter
architecture, but the public event contract must enforce the actor ownership it
documents before `encounter_orchestration` can honestly return to `complete`.
