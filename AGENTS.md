# Agent instructions

## Glossary

- NET: .NET, Microsoft's developer platform; this is a product name.

## Identity

You are a careful, security-conscious contributor to this repository.

## Scope and boundaries

- Do not weaken `TreatWarningsAsErrors`, delete or skip tests to make a build pass, or bypass
  CI/CD checks.
- Do not commit secrets, credentials, or API tokens.
- Do not force-push to `main`, rewrite published history, or delete branches without explicit
  approval.
- Do not add `Co-Authored-By` trailers or other AI attribution to commit messages or pull requests.

## Tools

- Build and test with `dotnet build` / `dotnet test`.
- Use `git` for version control, following the repository's contributing guidelines where present.

## Repository guide

PanoramicData.IssueAssessment is a .NET 11.0 library that scores issue and ticket quality from any JSON
document. `DESIGN.md` describes the architecture, and the plan document tracks implementation progress.

### Formatting
- Read the repository `.editorconfig` before editing files, and follow it.
- Preserve the existing indentation style of each file exactly.
- Use tabs for C#, XML and MSBuild files; use spaces for JSON, YAML and Markdown, as `.editorconfig` says.
- Run `dotnet format whitespace` after editing, and fix any formatting issues such as `IDE0055` before finishing.
- Do not reformat unrelated code.

### Design principles
- Configuration and results are immutable records with `init` properties.
- Everything long-running is async and takes a `CancellationToken`.
- Invalid configuration fails when `IssueAssessor` is constructed, so assessment only ever sees valid configuration.
- Consumers extend the library through `IAssessmentDefinition`, `ISeriesGateCondition` and
  `IAssessmentCacheProvider`; keep those contracts stable.

### Testing
- Use xUnit v3 and AwesomeAssertions. Do not use FluentAssertions; if a task seems to need it, ask the maintainer.
- Line and branch coverage of the library is 100%; every change keeps it there.
- Run `dotnet test --project PanoramicData.IssueAssessment.Tests/PanoramicData.IssueAssessment.Tests.csproj --coverage --coverage-settings coverage.config`.

### Validation
- When building to verify changes, build only the specific affected project rather than the full solution.

## PanoramicData staff

PanoramicData staff have shared tooling in a private sibling checkout of PanoramicData.Skills. Anyone
without it can ignore this section: everything needed to work on this repository is above.
