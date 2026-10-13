# Copilot Repository Instructions

PanoramicData.IssueAssessment is a .NET 10 library that scores issue and ticket quality from any JSON
document. `DESIGN.md` describes the architecture and `PLAN.md` tracks implementation progress.

## Formatting
- Always read and follow the repository `.editorconfig` before editing files.
- Preserve the existing indentation style of each file exactly.
- Use tabs for C#, XML and MSBuild files; use spaces for JSON, YAML and Markdown, as `.editorconfig` says.
- Run `dotnet format whitespace` after editing, and fix any formatting issues such as `IDE0055` before finishing.
- Do not reformat unrelated code.

## Design principles
- Configuration and results are immutable records with `init` properties.
- Everything long-running is async and takes a `CancellationToken`.
- Invalid configuration fails when `IssueAssessor` is constructed, never at assessment time.
- Consumers extend the library through `IAssessmentDefinition`, `ISeriesGateCondition` and
  `IAssessmentCacheProvider`; keep those contracts stable.

## Testing
- Use xUnit v3 and AwesomeAssertions. Never use FluentAssertions.
- Line and branch coverage of the library is 100%; every change keeps it there.
- Run `dotnet test --project PanoramicData.IssueAssessment.Tests/PanoramicData.IssueAssessment.Tests.csproj --coverage --coverage-settings coverage.config`.

## Validation
- When building to verify changes, build only the specific affected project rather than the full solution.
