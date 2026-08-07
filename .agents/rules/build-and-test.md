# Build and Test

## Build

- Core: `dotnet build Physiquinator.Core/Physiquinator.Core.csproj`
- UI: `dotnet build Physiquinator.UI/Physiquinator.UI.csproj`
- Web: `dotnet build Physiquinator.Web/Physiquinator.Web.csproj` (also compiles Core and UI)
- Tests: `dotnet build Physiquinator.Tests/Physiquinator.Tests.csproj`
- The MAUI host project (`Physiquinator.csproj`) is not built by default. For the Windows target:
  `dotnet build Physiquinator.csproj -f net11.0-windows10.0.19041.0`
  Android, iOS, and MacCatalyst targets require their platform workloads.

## Test

- Full suite: `dotnet test Physiquinator.Tests/Physiquinator.Tests.csproj`
- A subset: add `--filter "FullyQualifiedName~ClassName"`

## Format

- Check one project, e.g. Core: `dotnet format Physiquinator.Core/Physiquinator.Core.csproj --verify-no-changes --severity warn`
- CI runs the same check on Core, UI, Web, and Tests (see .github/workflows/ci.yml).

## Requirements

- Builds must be warning-free. TreatWarningsAsErrors is on for non-MAUI projects.
- Non-MAUI projects also run SonarAnalyzer and the style analyzers, so a build failure can come from analyzers rather than the compiler.
- Code style rules live in .editorconfig. Error-severity rules fail the build; suggestion-capped rules are enforced by the CI format step, not the build.

## Conventions

- Edit only the files your task covers. Files outside your scope belong to other workstreams.
- No comments unless necessary.
- No em dashes in code, docs, or prose.
- No semicolons in prose.
- Follow the repo style: file-scoped namespaces, `var` for locals, primary constructors.
