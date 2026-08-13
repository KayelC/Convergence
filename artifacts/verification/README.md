# Verification Evidence Index

Canonical verification bundles live under a checkpoint and the exact clean
commit that was tested. See
[`docs/verification-evidence.md`](../../docs/verification-evidence.md) for the
capture and validation contract.

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
