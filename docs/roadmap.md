# Product Roadmap

## Current State

Phase 8 established the clean product boundary. Framework, DemoHost, tests, and generic content now build independently of the archived prototype. The executable capability matrix currently records 21 complete capabilities, one partial capability, and three deferred capabilities.

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
