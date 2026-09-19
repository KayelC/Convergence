# Navigation Order 8 Pre-Closure Independent Audit

## Scope And Verdict

This review re-read the current navigation implementation, save boundary,
Training Annex adoption, Godot-shaped contract test, direct navigation tests,
and all three audience pages. The earlier source review identified the approved
Order 8 scope; it was not treated as evidence that a correction works.

**Implementation:** `navigation` is `implemented`. The three previously named
framework gaps are resolved in source and direct tests. **Order:** still
`open`. There is no remaining demonstrated core navigation defect that
requires an Order 8 correction, but the new audience pages have not received
explicit owner confirmation, and the retained online release gate is blocked.
This is a pre-closure finding, not a closure certificate.

## Source-First Checks

| Invariant | Current source and direct evidence | Result |
|---|---|---|
| Caller controls logical state | `RuntimeNavigationService.Navigate` returns a candidate `After`; it never stores a session. `RuntimeNavigationTests` checks before/after identity and rejection non-mutation. | Holds |
| Invalid request precedes access policy | `RuntimeNavigationRequestValidation.FirstInvalidField` checks current, transition, source, destination IDs in that order. `Navigate` returns `InvalidRequest` without calling `Evaluate`; the four-field theory and stable-order test exercise this. | Holds |
| Source and reverse travel | Source mismatch returns a rejected event without policy evaluation. The reverse route is an independent transition; the arbitrary-location test checks both. Same-location approval remains policy-controlled. | Holds |
| Custom policy boundary | Throwing and null-returning policies produce `PolicyFaulted` with unchanged state and typed `FaultKind`. Cancellation and out-of-memory propagate. Direct fault tests cover exception, null, and cancellation paths. | Holds |
| Public result authority | `RuntimeNavigationResult.ValidateOutcome` checks request validity, before/after state, event count/kind/IDs, reason/message agreement, invalid field, and fault kind. Results copy event input. Direct tests construct contradictory results and attempt collection mutation. | Holds |
| Presentation/adoption boundary | Training Annex calls the service, presents, and adopts `After` only on `Applied`; returning to staging retains dungeon progress while save context follows the navigation location. The Godot-shaped contract test proves scene failure leaves the host's current snapshot unchanged and scene success adopts it. | Holds for the demonstrated hosts |
| Optional save shape | `RuntimeFieldSnapshot` requires navigation if present and permits optional dungeon progress; root `Field` is nullable. Save validation checks the location ID. v19 supports absent field, navigation-only, and navigation-plus-dungeon, not dungeon-only. | Holds |

## Documentation Cross-Check

- The mechanics page describes optional logical travel rather than spatial
  movement, automatic traversal, or scene loading. This matches source.
- The developer guide now checks the *whole* authored transition in its
  single-route policy example, rather than only its ID; otherwise another
  source/destination pair reusing that ID would be accepted by the example.
  It distinguishes structural applied events from completed scene work and
  leaves scene handles host-owned.
- The technical result matrix now says that its diagnostic reason IDs are
  outputs of the supplied service, not mandatory IDs for every custom
  `IRuntimeNavigationService`. Its diagrams reflect validation, policy,
  candidate adoption, and separate dungeon/save authorities.
- All three navigation audience entries remain `existing_unreviewed`. Their
  content has been source-reconciled here, but owner confirmation is still
  required by the documentation completion rule.

## Bounded Residuals, Not Core Findings

- The real Godot smoke sample does not yet navigate a live scene. The
  Godot-shaped contract test is not represented as a production Godot
  implementation. Live Godot host integration belongs to Order 20.
- Training Annex's fixed menu is designed around the two locations and
  dungeon states it creates itself. A synthetically injected entrance save
  with no dungeon snapshot could leave that sample menu without a travel
  choice. The ordinary sample flow creates dungeon state on entry; this is
  sample-host hardening rather than a demonstrated normal-path framework
  defect. It does not justify changing the generic navigation contract.
- The v19 aggregate does not make navigation and dungeon fields
  independently nullable. The approved Order 8 decision retains that shape;
  broader save design belongs to Order 13.

## Verification And Closure Gate

The retained raw bundle at
[`artifacts/verification/navigation-order-8-preclosure-approved-failed-20260918T082840Z/4273502fb317a618a364590d903c82c2a71b3ac0/README.md`](../../artifacts/verification/navigation-order-8-preclosure-approved-failed-20260918T082840Z/4273502fb317a618a364590d903c82c2a71b3ac0/README.md)
records strict builds, focused tests, all 2,070 solution tests (zero
failed/skipped), 90.32% Framework line and 77.08% branch coverage, content
validation, and all five DemoHost modes. The bundle is **failed**, not green:
the strict Godot build stopped at NuGet `NU1900` because the vulnerability
service index at `https://api.nuget.org/v3/index.json` was unavailable. The
raw output and checksums remain in the repository. Separate offline local
builds, tests, formatting, and a headless Godot smoke run passed, but they do
not replace the online audit requirement of the retained gate.

On this documentation/tracking revision, the focused navigation/capability/
documentation filter passed 40 tests, the full solution passed 2,070 tests
(Framework 1,877; DemoHost 186; ContentValidator 7), and the strict
nonincremental Release solution build passed with zero warnings/errors.
`dotnet format --verify-no-changes` also passed. None of these checks performs
the missing online dependency audit.

A fresh retained gate attempt against audit commit `bd994d61` stopped at
`01-restore-audit` with the same `NU1900` service-index failure, before any
build or test step. Its raw output and manifest are at
[`artifacts/verification/navigation-order-8-r8-gate-failed-20260919T072144Z/bd994d61536ab2781a3f058de8d3dfa409dfa385/README.md`](../../artifacts/verification/navigation-order-8-r8-gate-failed-20260919T072144Z/bd994d61536ab2781a3f058de8d3dfa409dfa385/README.md).
The earlier bundle remains the evidence for checks that this attempt could
not reach.

To close Order 8, obtain explicit owner confirmation of the mechanics,
developer, and technical navigation pages; rerun the complete retained
release gate against a clean reviewed commit with the dependency audit
available; then independently recheck the resulting evidence and update the
documentation/order states. Do not promote either state based on this report
alone.
