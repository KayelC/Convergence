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

`Convergence.ContentValidator` validates every active JSON document against schema v3 and the Framework's deserialization, semantic, dependency, registration, and catalog rules. The gate then runs all four noninteractive DemoHost modes and a scripted `--clean-training-annex-play` session.

The Godot gate downloads the official Godot 4.7.1 .NET Linux artifact into the CI runner's temporary directory, verifies SHA-256 `6ca7ff0459f1b806900be683c1b0837c607a9c16834c530dc68c81b9fc3ae1f6`, and runs `samples/Convergence.GodotHost` headlessly. Success requires the marker `CONVERGENCE_GODOT_SMOKE_OK` and process exit code `0`.

For local Windows verification, use the official Godot 4.7.1 .NET executable:

```powershell
godot --headless --path samples/Convergence.GodotHost -- --convergence-smoke
```

The engine belongs in an external tools cache; it is not installed or committed by Convergence.

## Security And Release Status

Report security issues through the private process in `SECURITY.md`. A green gate establishes an `implemented_pending_review` pre-release candidate. It does not mark roadmap findings `verified`; that status requires the separate consolidated source review and any resulting corrections.
