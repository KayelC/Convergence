# Technical Diagram Review, 20 July 2026

## Scope

This review began with all 16 Mermaid diagrams in `docs/technical` and checked
each one against the active Framework source that owns the depicted state or
transaction. Splitting the overloaded charge picture into three focused views
leaves 18 diagrams across four technical references. Historical reports were
not treated as implementation authority.

The review asked four questions:

1. Does every state node correspond to retained runtime state?
2. Does the diagram preserve assessment, staging, commit, and rollback order?
3. Does it distinguish host-owned work from Framework-owned mutation?
4. Can the graph be read without long crossing edges or hidden cardinality?

## Corrections

### Charge lifecycle

The old charge diagram represented an actor as either `Empty` or `Charged`.
That model was incorrect because `SplitChargePolicy` may retain Physical and
Magical slots simultaneously, while `UnifiedChargePolicy` retains one General
slot. It also drew duplicate rejection as a self-transition, which could be
misread as charge re-entry or timer refresh.

The replacement separates:

- one retained slot's actual lifecycle;
- grant assessment and unchanged rejection;
- outer-action damage-category collection;
- policy mapping and once-per-slot staged consumption; and
- live publication versus transaction rollback.

This was checked against `ChargePolicies.cs`, `OrderedEffectExecutor.cs`,
`BattleRuntimeState.cs`, and `ChargePolicyTests.cs`.

### Timed-exclusive stat modifiers

The old five-state graph was incomplete and represented a weaker same-sign
rejection as a stored self-transition. The replacement follows the policy's
actual decision algorithm: no-track creation, same-sign strength comparison,
opposite-sign combination, neutral removal, and the surviving signal's timer
ownership.

This was checked against `TimedExclusiveStatModifierPolicy.cs` and
`TimedExclusiveStatModifierPolicyTests.cs`.

### Lifecycle boundary clock

The lifecycle graph was semantically correct but its nested yes/no questions
made the sequence comparison difficult to parse. The replacement gives the
incoming sequence one explicit older/same/newer branch before reserve
suspension and decrement behavior.

This was checked against the stat-modifier policy implementations,
`BattleStatusLifecycle.cs`, and their focused policy tests.

## Complete Diagram Inventory

| Technical reference | Diagram | Result |
|---|---|---|
| `combat-resolution-pipeline.md` | combat policy composition | retained; matches ruleset binding and execution-policy ownership |
| `combat-resolution-pipeline.md` | damage sequence | retained; matches prepared assessment, staged execution, and publication order |
| `combat-resolution-pipeline.md` | retained charge-slot lifecycle | redesigned; models one real slot rather than a false actor-wide binary state |
| `combat-resolution-pipeline.md` | charge grant assessment | added; separates unchanged rejection from stored state |
| `combat-resolution-pipeline.md` | outer-action charge consumption | added; separates staged removal from live publication |
| `combat-resolution-pipeline.md` | action atomicity | retained; correctly separates staged mutation from publication |
| `stat-modifier-policy-runtime.md` | authority flow | retained; selected policy service remains the only state authority |
| `stat-modifier-policy-runtime.md` | extension-policy containment | retained; rejection exposes identical before/after state |
| `stat-modifier-policy-runtime.md` | timed-exclusive application | redesigned; now covers the complete policy decision algorithm |
| `stat-modifier-policy-runtime.md` | ordered modifier transaction | retained; matches actor and inventory transaction ordering |
| `stat-modifier-policy-runtime.md` | lifecycle boundary clock | clarified; sequence comparison now has one readable decision point |
| `stat-modifier-policy-runtime.md` | save and aggregate restore | retained; matches validation-before-publication behavior |
| `runtime-actor-state-and-restoration.md` | actor and roster authority | retained; accurately separates actor-owned and party-owned state |
| `runtime-actor-state-and-restoration.md` | growth transaction | retained; matches staged growth, unlock planning, composition, and commit |
| `runtime-actor-state-and-restoration.md` | pending skill choice | retained; rejection correctly returns Pending to Pending |
| `runtime-actor-state-and-restoration.md` | aggregate restoration | retained; matches dependency-ordered, all-or-nothing restoration |
| `typed-action-and-effect-execution.md` | skill transaction | retained; matches reauthorization, staged costs/effects, and commit |
| `typed-action-and-effect-execution.md` | item transaction | retained; matches reservation validation, outcome, commit/rollback, and actor publication |

## Result

No other active technical diagram invents a retained state, reverses a commit
boundary, or makes presentation text authoritative. The diagram standards now
explicitly prohibit the two patterns corrected here: rejected-command
self-transitions and binary diagrams for multi-entry aggregate state.
