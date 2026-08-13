# Recovered Historical Verification Artifacts

This directory preserves every verification-related file that was still
available on 2026-08-13 in ignored repository artifact paths or identifiable
Convergence paths under `%TEMP%`.

## Recovery Scope

- 22 ignored repository-local API/build/Godot logs;
- 4 O7-R3 raw outputs and its full commit diff from `%TEMP%`;
- 6 post-O6 refactor stage diffs from `%TEMP%`; and
- 2 Cobertura reports from the earlier O7-R9 and pre-canonical O7-R10 runs.

The 34 recovered sources contained 23,189,974 original bytes. Text and diff
files are stored verbatim. The two large coverage XML files are stored as
lossless gzip; `RECOVERED-SOURCES.csv` records each uncompressed original size
and SHA-256.

## How To Verify

- `RECOVERED-SOURCES.csv` maps the original location to its tracked destination
  and records the original timestamp, byte count, storage method, and SHA-256.
- `SHA256SUMS.txt` covers every tracked file in this directory except the
  checksum file itself.
- For an entry marked `gzip-lossless`, decompress the tracked `.gz` and compare
  its hash with `originalSha256` in the CSV.

## Evidence Status

These files are historical recovery, not canonical gate bundles. Several were
created before Convergence recorded exact commands, tested commits, complete
companion outputs, and checksums together. Their bytes are useful for audit and
comparison, but their presence alone must not be interpreted as proof that a
complete checkpoint passed.

The authoritative complete O7-R10 rerun is indexed in
[`artifacts/verification/README.md`](../../verification/README.md).
