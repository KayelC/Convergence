# Product Roadmap

The [Production-Readiness Completion Roadmap](production-readiness-roadmap.md)
is the active release gate. The priorities below describe what follows or
expands that finite correction sequence; they do not replace its unresolved
`Blocker`, `High`, `Medium`, or `Low` findings.

## Current State

Phase 8 established the clean product boundary. Framework, DemoHost, tests, and generic content now build independently of the archived prototype. The executable capability matrix currently records 21 complete capabilities, one partial capability, and three deferred capabilities.

The [Terminology Boundary](terminology-boundary.md) checkpoint is complete. Active contracts use Action Token, Vessel, Hosted Entity, Companion, roster, schema-v3, and save-v7 vocabulary; an executable token-aware guard prevents retired names from returning outside the historical archive. Vessel stats now come from an explicit source policy rather than the removed weighted prototype model.

## Completed Semantic Correction

Catalyst rank shifting now uses explicit authored catalyst and target roles. It
moves the target by an exact offset within that target's catalog race, rejects
stale participant rank data, and returns a typed no-fusion result when an exact
destination does not exist. Schema v3 removes the provisional generic
rank-offset shape before the public API baseline is established.

Authored rulesets now resolve through an explicit host-supplied typed factory
registry. The standard damage factory exposes every existing combat setting,
roster tiers and Action Token liveness are authored, and the supplied fixed
growth/stat/reward/economy policies can be replaced by registering another
factory. Moon phase remains absent from the standard composition.

## Priority 1: Authoring Contract Completion

Expand checked-in authoring schemas and contract documentation so every implemented content family has a stable structural reference. Semantic graph and host-registration rules remain validator-owned. Add author-facing examples and diagnostics guidance without embedding game content into Framework.

## Priority 2: Real Godot Consumer

Create a separate Godot sample that references Framework source and implements resource loading, signal commands, event presentation, scene-instance mapping, and host-owned saves. This work must not move Godot types into Framework.

## Priority 3: Public API Stabilization

Review exported types by namespace, document supported composition paths, establish semantic versioning policy, and decide which APIs are ready for compatibility commitments. Keep Framework non-packable until distribution and support policy are approved.

## Priority 4: Persistence Evolution

Define save-contract migration only when a released contract actually requires it. Full deterministic replay remains optional; checkpoint breadcrumbs are currently diagnostics rather than replay authority.

## Priority 5: Example Breadth

Expand original example content only when it demonstrates a framework contract or reveals a missing reusable rule. DemoHost remains optional reference software, not the product architecture driver.

## Decision Rule

New work should answer one of these questions:

1. Does Framework lack a reusable rule or contract?
2. Does a real host expose an integration gap?
3. Does authoring need clearer validation or tooling?
4. Is a public API ready to stabilize?

Presentation-only work belongs in a host project and should not delay framework completion.
