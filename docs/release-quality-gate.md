# Release Quality Gate

## Purpose

Convergence `0.1` uses one repeatable gate for the supported Framework, content contracts, authoring tool, DemoHost, and Godot reference consumer. The authoritative automation is `.github/workflows/quality.yml`; local commands exercise the same boundaries.

## Dependency And Build Gate

All active projects use checked-in NuGet lock files. Restore runs in locked mode with NuGet vulnerability auditing enabled and treats `NU1901` through `NU1904` as errors.

```powershell
dotnet restore Convergence.sln --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet format Convergence.sln --no-restore --verify-no-changes
dotnet build Convergence.sln --configuration Release --no-restore --no-incremental -p:TreatWarningsAsErrors=true -p:ContinuousIntegrationBuild=true
```

The Framework build also enforces the shipped public API baseline, emits XML documentation, and runs the pinned .NET 8 trim analyzer. Build-only analyzer packages are private assets; the compiled Framework retains no runtime package dependency.

## Test And Coverage Gate

The architecture suite checks the product boundary, public API, terminology, documentation links, Framework neutrality, real Godot sample structure, roadmap integrity, and security policy. The complete solution must pass with no skips.

Framework coverage is collected independently with Coverlet's Cobertura output. `eng/Assert-CoberturaCoverage.ps1` requires at least 90% line coverage and 70% branch coverage for the `Convergence.Framework` package. On Windows systems that prohibit unsigned local scripts, invoke it with `powershell.exe -ExecutionPolicy Bypass -File`.

## Content And Host Gate

`Convergence.ContentValidator` validates every active JSON document against schema v10 and the Framework's deserialization, semantic, dependency, registration, and catalog rules. The gate then runs all four noninteractive DemoHost modes and a scripted `--clean-training-annex-play` session.

The Godot gate downloads the official Godot 4.7.1 .NET Linux artifact into the CI runner's temporary directory, verifies SHA-256 `6ca7ff0459f1b806900be683c1b0837c607a9c16834c530dc68c81b9fc3ae1f6`, and runs `samples/Convergence.GodotHost` headlessly. Success requires the marker `CONVERGENCE_GODOT_SMOKE_OK` and process exit code `0`.

For local Windows verification, use the official Godot 4.7.1 .NET executable:

```powershell
dotnet build samples/Convergence.GodotHost/Convergence.GodotHost.csproj --configuration Debug --no-restore --no-incremental -warnaserror
godot --headless --path samples/Convergence.GodotHost -- --convergence-smoke
```

An unpacked repository-local copy may also be placed at
`tests/Godot_v4.7.1-stable_mono_win64/` and invoked with its console executable.
That versioned directory is ignored by Git. The official distribution is about
256 MiB and CI downloads and verifies its own platform-specific copy, so
committing the engine would inflate every clone without improving verification.
An external tools cache remains equally valid.

## Security And Release Status

Report security issues through the private process in `SECURITY.md`. A green gate establishes a pre-release candidate. The separate [consolidated source review](reviews/convergence-production-readiness-consolidated-review-2026-07-16.md) and its demonstrated correction are complete, so every production-readiness ledger item is now `verified` for the guarded `0.1.0` baseline.
