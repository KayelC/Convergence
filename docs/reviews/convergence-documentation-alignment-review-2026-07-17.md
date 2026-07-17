# Convergence Documentation Alignment Review

**Review date:** 17 July 2026  
**Reviewed commit:** `8d13bbe538161f7fac264db19b8dbef78b2f6102`  
**Scope:** active root guidance and every Markdown document under `docs`; archived
material was treated as unsupported history rather than current authority.

## Verdict

The active documentation expresses the intended Convergence product clearly:
the reusable product is a modular, host-neutral .NET 8 framework; Godot is the
primary reference host without becoming a Framework dependency; content is
generic and original; hosts own presentation, files, scenes, and save encoding;
and typed policies and services own reusable rules.

The documentation is not yet synchronized or complete enough to call the
consumer surface production-ready. One current wire-contract fact is wrong in
multiple authority documents, one extension signature is misstated, and most
capabilities have not completed the collaborative documentation workflow. These
are documentation defects and coverage gaps, not evidence that the underlying
Framework architecture must be rewritten.

## Findings

### H1. Current-authority documents advertise save contract v8 while code requires v9

**Intended invariant:** current architecture, terminology, and mechanics pages
must identify the save contract accepted by `RuntimeSaveValidator` and the
reference hosts.

The Framework constant is `RuntimeSaveGameSnapshot.CurrentContractVersion = 9`.
DemoHost tests explicitly require version 9, and the actor guide, public API
contract, Godot guide, and technical restoration section correctly describe v9.

The following current-authority artifacts still describe v8 as current:

- [Architecture](../architecture.md), pre-release contract boundary;
- [Terminology Boundary](../terminology-boundary.md), version boundary;
- [Saving, Loading, And Suspend Saves](../mechanics/saving-loading-and-suspend.md),
  save contents and migration example;
- the persistence developer-guide reason in
  [`documentation-coverage-matrix.json`](../../tests/Convergence.Framework.Tests/Fixtures/documentation-coverage-matrix.json).

The actor roadmap also retains v8 in its top progress table and target-model
diagram even though its later correction record explains the v9 change.

**Reachable consequence:** a developer following the current mechanics or
architecture page can emit or expect v8 and receive a typed unsupported-contract
rejection from the current Framework. They can also mistake a v7-to-v8 migration
for the path to the current contract.

**Required correction:** make v9 the current contract everywhere, describe the
v8-to-v9 owner-level removal, keep older version references only inside clearly
labelled historical checkpoint evidence, and update the executable coverage
ledger.

### M1. The ruleset factory table gives the wrong stat-factory result type

**Intended invariant:** developer documentation for a public extension interface
must match its compiled signature.

[Ruleset Policy Contracts](../ruleset-policy-contracts.md) says
`IRuntimeStatRulesetPolicyFactory` produces `IStatResolutionPolicy`. The public
interface actually returns `RulesetBindingResult<StatRulesetServices>`.
`StatRulesetServices` carries both the stat-resolution policy and the stage-
scaling policy.

**Reachable consequence:** a developer implementing a custom stat ruleset from
the table will implement the wrong method shape or overlook the required stage
policy. Their code will not satisfy the interface.

**Required correction:** change the table result to `StatRulesetServices` and
state that the result contains `IStatResolutionPolicy` plus
`IStatStageScalingPolicy`.

### M2. Optional-module wording does not explain the mandatory empty save aggregates

**Intended invariant:** “optional module” should tell an integrating developer
whether the corresponding contract may be omitted or must be represented by an
empty snapshot.

The project vision correctly says games need not activate every mechanic. The
save mechanics page then says a `RuntimeSaveGameSnapshot` “can include” party,
inventory, equipment, wallet, Compendium, knowledge, and session state. In save
contract v9, those constructor arguments are required. `Field` is nullable;
checkpoints and host context have empty defaults. A game that does not use the
other modules still supplies their canonical empty snapshots.

This does not force the game to execute those mechanics, but the distinction is
not documented. It is also a legitimate future design choice whether later save
contracts should permit absent component snapshots.

**Required decision:** either confirm that v9 deliberately uses mandatory empty
aggregates and document that meaning of optionality, or schedule a future
versioned save-contract change. Do not make the fields nullable merely to make
the prose true.

### M3. The documentation architecture is sound, but most capabilities are not reviewed

This is a declared completeness gap rather than a hidden contradiction. The
machine-readable matrix contains 75 audience entries:

- 11 `reviewed`;
- 37 `existing_unreviewed`;
- 20 `missing`;
- 7 `not_applicable`.

By audience, mechanics has 3 reviewed entries and 15 unreviewed entries;
developer guidance has 4 reviewed, 7 unreviewed, and 14 missing entries;
technical documentation has 4 reviewed, 15 unreviewed, and 6 missing entries.

The largest practical gap is developer integration. There is one detailed actor
guide, while actions/effects, combat, Action Token, lifecycle, encounters,
knowledge, inventory/economy, navigation/traversal, negotiation/rewards, fusion,
and Compendium lack task-oriented guides. Existing mechanics pages are useful
overviews, but their matrix status correctly says they have not been verified
with the project owner capability by capability.

**Consequence:** the Framework can be executable and tested while still being
difficult for another developer, or the project owner months later, to compose
without reading source. That falls short of the documentation vision even though
the ledger reports it honestly.

**Required continuation:** make documentation completion an active roadmap with
one capability reviewed at a time. Do not bulk-promote existing pages merely
because their links resolve or their current statements look plausible.

### L1. Active roadmap records mix current status with superseded checkpoint state

The actor roadmap preserves useful chronology, but its opening progress table and
target diagram still say save v8 while the correction appendix and current code
say v9. It also records the temporary 20-complete/3-partial state before later
returning to 23-complete/0-partial. The sequence is historically accurate, but a
reader must reach the end to learn which statements are superseded.

The production-readiness roadmap also uses commit messages instead of the known
hashes for checkpoints 7 through 11, weakening an otherwise auditable completion
record.

**Required correction:** add a concise current-state banner to the actor roadmap,
mark superseded v8/partial passages as historical checkpoint evidence, update the
target diagram to v9, and record the existing hashes for production-readiness
checkpoints 7 through 11. Preserve the history rather than deleting it.

### L2. Documentation tests validate structure, but not current contract facts

`DocumentationFoundationTests` correctly validates capability coverage, state
vocabulary, referenced files, indexes, and the collaborative authority model.
`ProductBoundaryTests` validates links and prevents active links into the
archive. Neither binds current-authority prose to the current save contract.
That is why all tests remained green while H1 existed.

**Required correction:** add a narrow synchronization test that reads the
compiled `RuntimeSaveGameSnapshot.CurrentContractVersion` and requires the
current architecture, terminology, save mechanics, and coverage reason to name
that version. Similar checks should be reserved for a few central versioned
facts, not used to freeze ordinary prose word-for-word.

## Confirmed Alignment

The following claims were checked against current source, projects, schemas,
content, tests, and executable commands and remain aligned:

- the Git root opens on `Convergence.sln`, which contains only the seven clean
  Framework, host, tool, and test projects;
- Framework targets .NET 8 and C# 12, is non-packable, has no runtime package
  dependency, and does not depend on Godot, console, filesystem, or a host
  serializer;
- Godot and DemoHost are consumers rather than rule authorities;
- the public namespace map and active inline type references match the shipped
  API baseline; implementation-only serialization namespaces export no public
  types;
- schema v3 contains 14 Draft 2020-12 artifacts and the authoring validator
  accepts 6 packs, 36 documents, and 94 qualified definitions;
- the source ownership reference reports 94 Framework C# files, matching the
  active filesystem inventory;
- the documented supplied stage tables match `StandardStatStageScalingPolicy`;
- Action Token passing consumes a partial token first and converts a full token
  only when no partial token exists;
- catalyst rank shifting, first-acquisition Compendium preservation, explicit
  registration updates, and familiar-knowledge import match current services;
- the five DemoHost command routes, Godot source-reference boundary, content
  root confinement, terminology boundary, and release workflow are represented
  accurately;
- historical reviews are explicitly classified as evidence rather than design
  authority.

## Verification Evidence

- Framework tests: 855 passed, 0 failed, 0 skipped;
- DemoHost tests: 167 passed, 0 failed, 0 skipped;
- ContentValidator tests: 7 passed, 0 failed, 0 skipped;
- total: 1,029 passed, 0 failed, 0 skipped;
- active content validation: 6 packs, 36 documents, 94 definitions;
- active documentation link, index, coverage-ledger, product-boundary, and
  terminology tests passed as part of the Framework suite;
- active inline public-type references were compared with
  `PublicAPI.Shipped.txt`; unmatched names were test-class evidence rather than
  claimed Framework contracts.

## Recommended Correction Order

1. Correct save v9 authority text and add the focused synchronization test.
2. Correct the stat ruleset factory result and roadmap current-state evidence.
3. Confirm the intended v9 empty-aggregate semantics before changing its prose.
4. Begin collaborative subsystem documentation with actions/targeting/effects,
   then combat and Action Token, status lifecycle, encounter orchestration and
   knowledge, resource management, world runtime, and fusion/Compendium.

After the factual corrections, documentation should still be described as
“well structured and partially reviewed,” not “complete,” until the executable
coverage ledger records the remaining collaborative reviews.
