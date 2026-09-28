# Verification Evidence Index

Canonical verification bundles live under a checkpoint and the exact clean
commit that was tested. See
[`docs/verification-evidence.md`](../../docs/verification-evidence.md) for the
capture and validation contract.

## Evidence Retention Gate

- [Successful complete retention gate](evidence-retention-20260813/1cb2478194a0d017b4fe5173fc5cbd0626d6cd8e/README.md):
  verifies the clean commit that introduced the canonical O7-R10 bundle,
  recovered every still-available historical artifact, and added executable
  checksum/decompression guards. All 23 commands passed.

## Order 7 R10

- [Successful complete gate](order7-r10/96f58e47a77e31878bf89452bf7cad91cca5db55/README.md):
  23 commands passed against commit
  `96f58e47a77e31878bf89452bf7cad91cca5db55`, reviewing
  `23cf50c14d52959a7d7bdfd4797cc4a249bef42a..996cc120059a6cc85a8bb56289cdc9da4d48ddb8`.
- [Retained failed first attempt](order7-r10-failed-20260813T123556Z/52936d151b98475616a757f4d85c1ddb2006e593/README.md):
  restore failed because the generated batch command did not escape a
  semicolon-separated MSBuild property. It is failure evidence, not a passing
  gate.
- [Recovered earlier local artifacts](../historical-verification-recovery/2026-08-13/README.md):
  surviving pre-contract outputs retained with provenance and hashes, without
  claiming missing context.

## Order 7 R11 Closure

- [Successful complete closure gate](order-7-r11-closure/a9aded21b135813bb9999b0f4669f986cf0dd4fd/README.md):
  23 commands passed against commit
  `a9aded21b135813bb9999b0f4669f986cf0dd4fd`, reviewing
  `25c0a78a23df1526ad53fbdbf151afd2efd693ad..a9aded21b135813bb9999b0f4669f986cf0dd4fd`.

## Order 7 Post-Correction Closure

- [Successful complete post-correction gate](order-7-post-correction-closure/0ecbd5c5c2eb8a482b9dacfccb6ba252db43e6cf/README.md):
  23 commands passed against commit
  `0ecbd5c5c2eb8a482b9dacfccb6ba252db43e6cf`, reviewing the implementation and
  closure range
  `91a4f2ec15e7811ea13289b23de4dbc179bf68c1..fadcf31366c7ab9a256526d55eddb4e16e7ae1b8`.
- [Retained failed console-binary attempt](order-7-post-correction-closure-failed-20260824T065831Z/fadcf31366c7ab9a256526d55eddb4e16e7ae1b8/README.md):
  commands 00 through 17 passed against `fadcf31366c7ab9a256526d55eddb4e16e7ae1b8`;
  the repository-local Godot `_console.exe` then crashed in native code while
  opening `user://logs`. This is failure evidence, not a passing gate.

## Order 7 R15 Closure

- [Successful complete R15 closure gate](order-7-r15-final-closure/357563aa51345b2649674968ec3a0db5f303fbf8/README.md):
  23 commands passed against commit
  `357563aa51345b2649674968ec3a0db5f303fbf8`, reviewing
  `a184282e0def13aa78452b980da6f275f647ac29..357563aa51345b2649674968ec3a0db5f303fbf8`.

## Order 7 Post-R15 Independent Audit

- [Successful complete post-R15 gate](order-7-post-r15-independent-audit/51f011f392b7b35f4044d42aa1cc9e9675080f92/README.md):
  23 commands passed against commit
  `51f011f392b7b35f4044d42aa1cc9e9675080f92`, reviewing
  `ef4e129eb50d4dffaa5791bc5a0e509fe446890c..b35de48929f9d0a1ed90e63b30f90c126d97ffd6`.
- [Retained failed sandbox attempt](order-7-post-r15-independent-audit-failed-20260904T061429Z/b35de48929f9d0a1ed90e63b30f90c126d97ffd6/README.md):
  commands 00 through 17 passed against `b35de48929f9d0a1ed90e63b30f90c126d97ffd6`;
  Godot then failed to open its sandbox-confined `user://logs` path and
  terminated in native code. This is failure evidence, not a passing gate.

## Order 7 R23 Final Closure

- [Successful complete R23 closure gate](order-7-r23-final-closure/3b56606a25e88c4651cd88e720309f369fbdfe66/README.md):
  23 commands passed against commit
  `3b56606a25e88c4651cd88e720309f369fbdfe66`, reviewing the corrected Order 7
  range
  `aaacbd9729d26fea2d6c65b84a6b367205fe6f25..e61725b65d55901efb2ad01a2e25f9d8f3e9dc8d`.
- [Retained failed sandbox attempt](order-7-r23-final-closure-failed-20260914T055200Z/e61725b65d55901efb2ad01a2e25f9d8f3e9dc8d/README.md):
  commands 00 through 17 passed against `e61725b65d55901efb2ad01a2e25f9d8f3e9dc8d`;
  Godot then failed to open its sandbox-confined `user://logs` path and
  terminated in native code. The elevated rerun above is the authoritative
  passing gate.

## Order 9 C4 Post-Correction Closure

- [Successful complete post-correction gate](o9-c4-verified/299cbc158e8cb91c7a9f2e786b1685d457aa02b9/README.md):
  all 23 commands passed against commit
  `299cbc158e8cb91c7a9f2e786b1685d457aa02b9`, reviewing
  `d6608d95c7432d7b4eedea224bed09cd5d84c8b4..299cbc158e8cb91c7a9f2e786b1685d457aa02b9`.
- [Retained default-log failure](o9-c4-verified-failed-20260928T060134Z/511fc387d270139c8fed7361711a23b288d7d1df/README.md):
  commands 00 through 17 passed, then Godot crashed before project execution
  while opening its default `user://logs` path. This exposed O9-C4-L2.
- [Retained whole-environment redirect restore failure](o9-c4-verified-failed-20260928T060527Z/511fc387d270139c8fed7361711a23b288d7d1df/README.md):
  the experimental workaround changed the .NET/NuGet user environment and the
  locked vulnerability-audit restore failed. It is not a passing gate.
- [Retained whole-environment redirect build failure](o9-c4-verified-failed-20260928T060613Z/511fc387d270139c8fed7361711a23b288d7d1df/README.md):
  the same broad workaround reached the Godot build but retained a NuGet audit
  warning as an error. The successful gate instead redirects only Godot's log.
