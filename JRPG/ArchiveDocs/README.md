# Archived Documentation

## Status

Everything under `ArchiveDocs` is non-authoritative historical material. These files are preserved for context, archaeology, and recovering useful reasoning. They do not define current production requirements.

## Why Documents Are Archived

Documents belong here when they are:

- completed or superseded execution plans,
- abandoned migration reports,
- unapproved broad proposals,
- strategic maps that no longer match the chosen baseline,
- generated class-by-class notes that are easier to verify from source and tests.

## Contents

### Planning

- `skill-system-redesign-plan.md`: execution history through the proposed destructive cleanup tracks. Track 12 is the recovery point; later removal plans are not approved production work.
- `content-schema-v1-proposal.md`: mixed implemented and speculative schema material. It must be split into focused approved contracts before reuse.
- `refactor-roadmap.md`: pre-redesign framework roadmap.
- `host-core-boundary.md`: earlier extraction map for the console prototype.
- `bridge-contracts.md`: earlier bridge and adapter design charter.
- `migration_report.md`: discarded legacy-to-v2 data conversion report.

### TechnicalDocs

Generated fusion implementation walkthroughs from the legacy console architecture. The corresponding source and tests remain the implementation authority.

### LegacyFramework

Retirement holding area for legacy framework or console-host source files after a capability reaches `clean_parity` and its parity-ledger entry explicitly authorizes removal from active code. No active source has been moved there yet.

## Reusing Archived Material

Do not move an archived document back into `docs` unchanged. Extract the still-relevant decision, verify it against current code and goals, discuss it where necessary, and publish a smaller active document with a clear status.
