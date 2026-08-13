# Verification Evidence

## Purpose

Convergence keeps checkpoint conclusions in review documents, but a conclusion
is not a replacement for raw evidence. A checkpoint that claims the complete
local release gate must preserve its command lines, unedited combined console
output, exit codes, source identity, coverage, and checksums in Git.

The authoritative location is:

```text
artifacts/verification/<checkpoint>/<tested-commit>/
```

Ordinary scratch under `artifacts/`, operating-system logs, build output, and a
local Godot engine remain ignored. Existing historical tracked artifacts are
retained, but new complete-gate evidence uses only the directory above.

The current repository index is
[`artifacts/verification/README.md`](../artifacts/verification/README.md).

## Capture A Checkpoint

Begin from a clean commit. On Windows, provide the official Godot 4.7.1 .NET
console executable and, when reviewing a bounded change, the exact diff range:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\eng\Invoke-VerificationEvidence.ps1 `
  -Checkpoint order7-r10 `
  -ReviewedBase 23cf50c1 `
  -ReviewedHead 996cc120 `
  -GodotExecutable .\tests\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe
```

The runner refuses a dirty worktree, an invalid checkpoint ID, an existing
destination, an absent Godot executable, a failed command, or an omitted Godot
run. It never overwrites prior evidence.

A failed command stops the gate. The runner still finalizes the partial raw
bundle and moves it under
`artifacts/verification/<checkpoint>-failed-<utc>/<tested-commit>/`, leaving the
canonical destination free for a corrected retry. Failed evidence is retained
as failure evidence; it is never presented as a successful gate.

## Bundle Contract

Every successful bundle contains:

- `manifest.json`: schema version, checkpoint, tested commit, reviewed range,
  timestamps, each exact command, exit code, and coverage metrics;
- `README.md`: a human-readable identity and result summary;
- `git-status-before.txt` and `git-status-after.txt`;
- `reviewed-range.diff` and `reviewed-range-commits.txt` when a range is given;
- one portable `.cmd` wrapper and one unedited `.raw.txt` combined-output file
  for every command;
- `coverage/coverage.cobertura.xml.gz`, preserving the exact collected XML in
  compressed form, plus its uncompressed SHA-256 in the manifest; and
- `SHA256SUMS.txt`, covering every bundle file except the checksum file itself.

The command set includes dependency restore/audit, focused Order 7 tests, strict
builds, architecture and full tests, formatting, coverage and threshold checks,
active-content validation, all DemoHost modes, scripted Training Annex play,
the Debug Godot build and real headless smoke, trimming analysis, and
`git diff --check`.

`VerificationEvidenceContractTests` validates every committed bundle. A
successful bundle requires successful commands, the complete mandatory command
set, and coverage above the release thresholds. A failed bundle requires a
typed failure message and at least one nonzero command. Both require no missing
or extra checksum entries and matching SHA-256 values.

## Commit Ordering

The evidence directory is named after the clean commit that was actually
tested. The following evidence-only commit does not change that tested source;
it merely stores the results. A later source change requires a new bundle under
its own commit ID. Never edit an existing successful bundle in place.

## Limits

Evidence proves what ran on one identified revision and environment. It does not
prove that a review interpreted the mechanics correctly, replace independent
source review, or make ignored local logs authoritative. Review records must
continue to distinguish verified results from owner-approved game design.

## Recovered Historical Evidence

Files created before the canonical bundle contract sometimes survived only in
ignored repository paths or the operating-system temporary directory. The
surviving files recovered on 2026-08-13 are tracked under
[`artifacts/historical-verification-recovery/2026-08-13`](../artifacts/historical-verification-recovery/2026-08-13/README.md).

That collection preserves exact bytes, provenance, original hashes, and a
checksum inventory. It is historical evidence, not a complete successful gate:
some source commands, tested commits, or companion outputs were never recorded
and cannot be reconstructed honestly. Losslessly compressed coverage files
record the SHA-256 of their uncompressed original bytes.
