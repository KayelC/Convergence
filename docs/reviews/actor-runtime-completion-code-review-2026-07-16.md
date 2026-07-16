# Actor Runtime Completion Code Review

Date: 2026-07-16
Reviewed revision: `7aefd87`
Checkpoint 7 re-audit revision: `062a686`
Scope: actor composition, progression, party/roster authority, stage scaling,
save v8 restoration, and the Training Annex evidence added by the D1-D6
roadmap.

## Review Method

This review was performed from current source and tests. Earlier review reports
and checkpoint summaries were not used as finding authority.

The review traced:

- public request and result boundaries;
- live mutation and rollback paths;
- actor, party, roster, and save authorities;
- high-level actor creation and direct actor restoration;
- move-list capacity and pending-choice behavior;
- source-first Vessel restoration;
- DemoHost coordination paths;
- focused tests and the complete 1,017-test gate.

A finding is included only when there is an intended invariant, a reachable
public path, a concrete consequence, and reproducible source evidence.

## Checkpoint 7 Re-Audit

Checkpoint 7 was re-audited directly from commit `35661bb` and current source
because its implementation crossed an interrupted task boundary. The audit
compared the commit diff with the current save validator, migration service,
aggregate restore service, actor factory, DemoHost codec, Godot codec, and
focused tests.

The following Checkpoint 7 requirements are present and source-confirmed:

- save contract v8 rejects unsupported versions unless an explicit migration
  path is supplied;
- pending skill-choice tokens, unlock metadata, and revisions are persisted;
- aggregate validation runs before Framework actor restoration;
- the canonical Active Hosted Entity is derived from the party roster;
- the Hosted Entity is restored before its dependent Vessel;
- the Vessel combat profile is recomposed from restored source state;
- retained passive runtime state survives recomposition;
- failed aggregate restoration returns diagnostics without a partial session;
- DemoHost performs host-owned JSON decoding followed by aggregate restore.

The re-audit reconfirmed M1 and L1 below as Checkpoint 7 boundary gaps. It also
found M5: the real Godot sample does not yet use the same aggregate restore
boundary. No additional defect was found in migration path selection,
dependency ordering, pending-choice serialization, passive-state retention, or
normalized Vessel snapshots.

## Findings

### M1. Roster capacity uses a duplicated owner level that can drift from actor progression

**Invariant:** one authoritative runtime value should determine a level-gated
roster capacity.

`RuntimePartyRosterSnapshot` stores `OwnerLevel` separately from the owner
actor's `RuntimeProgressionSnapshot.Level`. Its `With(...)` method preserves
that value and cannot update it. Growth transactions update actor progression
but do not return an updated party roster.

Both live transitions and save validation calculate capacity from the separate
roster value:

- [PartyRosterTransitions.cs](../../src/Convergence.Framework/Runtime/PartyRosterTransitions.cs),
  `RuntimePartyRosterSnapshot` and `OwnerLevel`, lines 53-108;
- [PartyRosterTransitions.cs](../../src/Convergence.Framework/Runtime/PartyRosterTransitions.cs),
  capacity checks around lines 337-375;
- [RuntimePersistenceSnapshots.cs](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs),
  save capacity checks around lines 926-941.

**Reachable path:** create a party roster while its owner is level 9, then grow
the owner to level 10. The roster remains level 9 unless a host manually
reconstructs it. The reverse is also possible in a restored or manually
constructed snapshot: an owner actor can be level 1 while `OwnerLevel` claims
40.

**Consequence:** a valid level-up may fail to unlock intended roster capacity,
or a stale/high roster value may permit ownership state that the actual owner
level should reject. Save validation does not compare the two values.

**Required correction:** remove the duplicate value or make capacity context an
explicit, validated authority derived from the current owner actor. Growth and
restore must update or derive it atomically.

**Resolution:** corrected. Save contract v9 removes `OwnerLevel`; transition
requests carry the current owner actor snapshot, and save validation derives
capacity from the canonical saved owner actor.

### M2. Live party transitions validate only a subset of party and roster invariants

**Invariant:** every public party transition must reject malformed incoming
state and return an unchanged snapshot.

`PartyRosterTransitionService.RejectInvalid(...)` calls only
`RuntimePartyRosterInvariantRules.Validate(...)`. That validator covers
duplicate Hosted Entity/Companion roster entries, Active Hosted Entity
ownership/reference matching, and Hosted Entity/Companion role collisions.

It does not cover:

- invalid/default runtime or content IDs in references;
- duplicate active-party entries;
- duplicate reserve entries;
- active/reserve overlap;
- incompatible owner/party/roster identity overlap;
- active-party capacity;
- Hosted Entity or Companion roster capacity.

Save validation has separate checks for these rules, but live transition
preflight does not reuse them.

Evidence:

- [PartyRosterTransitions.cs](../../src/Convergence.Framework/Runtime/PartyRosterTransitions.cs),
  `RejectInvalid`, lines 663-673;
- [RuntimePartyRosterInvariants.cs](../../src/Convergence.Framework/Runtime/RuntimePartyRosterInvariants.cs),
  complete validator body;
- [RuntimePersistenceSnapshots.cs](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs),
  the additional save-only checks, lines 918-967 and 970-1119;
- [RuntimeStateSnapshots.cs](../../src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs),
  `RuntimeActorReferenceSnapshot`, lines 235-255, which accepts default IDs.

**Reachable path:** construct an active party containing the same runtime actor
twice, then call `AddHostedEntityToRoster`. `RejectInvalid` reports no issue and
the transition returns `Applied`, preserving the duplicate active-party state.
A new roster reference with default IDs can likewise be appended.

**Consequence:** live runtime state can be accepted by transition services and
later rejected by save validation. Fusion, Compendium, battle actions, and host
party flows that reuse these transitions inherit the inconsistent boundary.

**Required correction:** introduce one reusable, capacity-aware party aggregate
validator and use it in live transitions, composition, and save validation.
Validate both incoming state and the proposed result.

**Resolution:** corrected. `RuntimePartyRosterInvariantRules` is now the shared
owner-aware, capacity-aware validator for live transitions, actor composition,
and save validation. It covers reference IDs, duplicate and conflicting roles,
active and roster capacities, ownership, and Active Hosted Entity consistency.
Every transition validates both its incoming snapshot and proposed result,
returning the original snapshot on rejection.

### M3. Move-list capacity is enforced during live growth but not actor creation or restoration

**Invariant:** the selected move-list capacity policy should govern every path
that produces equipped skill state.

`RuntimeSkillUnlockPlanner` correctly applies
`IRuntimeMoveListCapacityPolicy` during live growth. However,
`CatalogBattleActorFactory.Create(...)` adds every base skill and every unlock
available at the requested starting level directly to the learned and equipped
lists. The creation request has no move-list capacity policy and creates no
pending choices.

Direct and aggregate restoration validate skill structure and references, but
they do not validate equipped count against a move-list capacity policy.

Evidence:

- [CatalogBattleActorFactory.cs](../../src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs),
  starting-level skill collection and skill-state construction, lines 311-338
  and 452-454;
- [RuntimeSkillProgression.cs](../../src/Convergence.Framework/Runtime/RuntimeSkillProgression.cs),
  capacity assessment and pending-choice creation;
- [RuntimePersistenceSnapshots.cs](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs),
  actor skill validation around lines 741-813, which has no capacity input.

**Reachable path:** create the current Training Annex `annex_mentor` directly at
level 8. It has six base skills and three unlocks available by level 8.
`CatalogBattleActorFactory` equips all nine. Growing the same actor from level 5
through the progression transaction correctly equips two unlocks and makes the
ninth skill pending under the supplied capacity of eight.

**Consequence:** equivalent actors can have different legal move lists based
only on whether they were created at a level or grew to it. A restored snapshot
can also exceed the selected game's capacity while passing the framework
restore boundary.

**Required correction:** make initial actor hydration and restore profiles
capacity-aware. Initial unlock planning should produce the same equipped and
pending state as live progression for the selected policy.

### M4. A prepared level-growth result has no stale-state precondition

**Invariant:** applying a prepared mutation must not overwrite actor changes
that occurred after the result was calculated.

`LevelGrowthResult` contains only proposed after-state. It does not retain the
source progression, stat, resource, or revision precondition.
`RuntimeProgressionTransactionService.ApplyLevelGrowth(...)` applies any
`Applied` result to the current actor. `RuntimeActorGrowthCompositionService`
therefore cannot distinguish a current result from a stale or foreign result.

Evidence:

- [ProgressionPolicies.cs](../../src/Convergence.Framework/Runtime/ProgressionPolicies.cs),
  `LevelGrowthResult`, lines 490-517;
- [RuntimeStateSnapshots.cs](../../src/Convergence.Framework/Runtime/RuntimeStateSnapshots.cs),
  `ApplyLevelGrowth`, lines 682-709;
- [RuntimeActorGrowthComposition.cs](../../src/Convergence.Framework/Runtime/RuntimeActorGrowthComposition.cs),
  staging and commit, lines 119-249.

**Reachable path:** calculate a level-growth result, mutate the actor through
another accepted progression operation, then submit the old result. The old
progression, stats, resources, and base-resource values overwrite the newer
state. Reapplying one accepted result is also not rejected as stale.

**Consequence:** an asynchronous host, duplicate reward callback, or retried
command can lose newer progression changes despite the transaction reporting
success.

**Required correction:** include an immutable expected source snapshot or
revision in the prepared result/request, or move growth calculation inside the
transaction service so assessment and commit share one state precondition.

### M5. The Godot reference save path bypasses aggregate restoration

**Invariant:** a Godot host should create and expose live actors only after the
complete save aggregate has passed validation and dependency-ordered
restoration.

`GodotSaveCodec.DeserializeAndRestore(...)` currently:

1. deserializes its host-owned document;
2. creates and directly restores each actor through
   `ICatalogBattleActorFactory`;
3. constructs a new `RuntimeSaveGameSnapshot`;
4. validates that aggregate only after the live actor list already exists;
5. returns both the live actors and the validation result.

It does not call `IRuntimeSessionRestoreService`. It also restores every actor
with `RuntimeStatSourceKind.Actor`, so the sample cannot demonstrate the
source-first Vessel restoration contract that Checkpoint 7 introduced.

Evidence:

- [GodotSaveCodec.cs](../../samples/Convergence.GodotHost/Infrastructure/GodotSaveCodec.cs),
  `DeserializeAndRestore`;
- [ConvergenceSmokeRoot.cs](../../samples/Convergence.GodotHost/Scripts/ConvergenceSmokeRoot.cs),
  the save-smoke call validates the returned snapshot after actors were
  reconstructed;
- [RuntimeSessionRestoration.cs](../../src/Convergence.Framework/Runtime/RuntimeSessionRestoration.cs),
  `RuntimeSessionRestoreService`, which already owns the required atomic
  boundary.

**Reachable path:** a malformed host save can contain an invalid party owner,
roster reference, or future Vessel dependency while each individual actor is
otherwise constructible. The codec creates those actors before aggregate
validation reports the save invalid.

**Consequence:** the reference Godot integration teaches a weaker restore
pattern than the documented Framework contract and makes partial live state
available from a rejected aggregate.

**Required correction:** decode the complete host-owned snapshot first, then
restore it through `IRuntimeSessionRestoreService`. Return live actors and scene
metadata only on aggregate success, and add a failure-path smoke/contract test
proving invalid aggregate state exposes no actors.

### L1. Direct catalog actor restoration omits pending-skill catalog and provenance checks

**Invariant:** a public actor restore result should not contain a pending skill
that cannot later be resolved.

`CatalogBattleActorFactory.Restore(...)` resolves only learned and equipped
skill IDs. Pending choice IDs are not included in its `SnapshotSkillMissing`
checks, and their authored unlock level/entity provenance is not checked.

Aggregate `RuntimeSaveValidator` performs both checks, so the inconsistency is
limited to the public direct-restore path.

Evidence:

- [CatalogBattleActorFactory.cs](../../src/Convergence.Framework/Encounters/CatalogBattleActorFactory.cs),
  skill collection at lines 506-535;
- [RuntimePersistenceSnapshots.cs](../../src/Convergence.Framework/Runtime/RuntimePersistenceSnapshots.cs),
  pending choice catalog/provenance validation at lines 760-813.

**Reachable path:** call `CatalogBattleActorFactory.Restore(...)` directly with
a structurally valid pending choice whose skill is absent from the catalog. The
actor can be restored, but resolving that pending choice later returns a
missing-definition rejection.

**Consequence:** two public restore paths disagree about whether the same actor
snapshot is valid.

**Required correction:** validate pending choices in
`CatalogBattleActorFactory.Restore(...)`, or make direct restore require an
opaque result proving aggregate validation.

## Positive Findings

- Every supplied stage from `-4` through `+4` has a distinct tested
  multiplier, and authored overrides reject incomplete or malformed tables.
- Vessel composition stages stats, resources, defense, skills, and passives
  before committing, and retained passive runtime state is preserved.
- Hosted Entity growth and skill-choice transactions stage the source and
  dependent Vessel together.
- Pending skill choices have typed tokens, expected source levels, and skill
  revisions; replace and forget paths reject stale decisions.
- Save v8 removes actor-local roster authority, validates pending choice
  provenance, restores the Active Hosted Entity before the Vessel, normalizes
  derived Vessel state, and exposes no partial restored session.
- The framework remains host-neutral and the complete .NET 8, content,
  DemoHost, and Godot smoke gates are green.

## Health Assessment

No high-severity crash, data-loss-on-normal-path, host-coupling, or arithmetic
defect was found in the reviewed range. The approved D1-D6 design remains
sound.

The implementation is not ready to describe as fully complete yet. The five
medium findings affect ordinary framework integration choices, not impossible
inputs:

- owner progression can change after party creation;
- public hosts can construct and transition snapshots;
- high-level actors are a supported factory path;
- prepared growth results can outlive their source state.
- the shipped Godot reference consumer currently bypasses aggregate restore.

Until those corrections are implemented, `progression_and_resources`,
`party_and_rosters`, and `persistence_snapshots` return to `partial`. Other
roadmap capabilities remain complete.

## Verification Evidence

- focused actor/progression/roster/documentation tests: 134 passed;
- focused Training Annex host tests: 106 passed;
- full solution: 1,017 passed, 0 failed, 0 skipped;
- strict .NET 8 Release build: 0 warnings, 0 errors;
- architecture and documentation boundary tests: 45 passed;
- content validation: 6 packs, 36 documents, 94 definitions;
- all DemoHost modes and scripted Training Annex exit passed;
- real Godot 4.7.1 headless smoke passed with save contract v8;
- formatting and `git diff --check` passed before the review commit.
