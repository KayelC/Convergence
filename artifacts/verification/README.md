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
