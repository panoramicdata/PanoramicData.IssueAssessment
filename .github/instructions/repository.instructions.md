---
applyTo: "**"
---

# PanoramicData.IssueAssessment — Repository Instructions

This repository contains a high-performance .NET 10 library for schema-agnostic issue/ticket quality assessment.

## Key Development Principles

1. **100% Code Coverage**: All public code must be tested. No unreachable code.
2. **Immutable Configuration**: Use C# 10 records with `init` properties throughout.
3. **Async First**: All long-running operations are async with `CancellationToken` support.
4. **Extensibility**: Design for consumer-provided implementations (assessments, gates, cache providers).
5. **Performance**: Keep individual assessments < 5ms; full assessment < 150ms.
6. **Validation at Construction**: Fail fast during `IssueAssessor` construction, not at assessment time.

## Directory Structure

- **src/PanoramicData.IssueAssessment/** — Main library
- **src/PanoramicData.IssueAssessment.Tests/** — XUnit3 test suite with AwesomeAssertions
- **src/PanoramicData.IssueAssessment.Docs/** — Documentation (DocFX, future)
- **src/PanoramicData.IssueAssessment.Examples/** — Platform-specific examples

## Code Quality Gates

All PRs must pass:

- ✅ `dotnet build` with `/p:TreatWarningsAsErrors=true`
- ✅ `dotnet test` with 100% code coverage
- ✅ `.editorconfig` compliance (tabs, line length, naming)
- ✅ Nullable reference type validation (`<Nullable>enable</Nullable>`)

## Testing

Use **XUnit3** with **AwesomeAssertions**. Never use FluentAssertions.

```bash
dotnet test                                          # Run all tests
dotnet test /p:CollectCoverage=true                 # With coverage report
```

## CI/CD Workflows

- **test.yml** — Runs on every push/PR; tests + coverage validation
- **publish.yml** — Triggered on tag push (v*.*.*.); publishes to NuGet.org

## Versioning

Strict **Semantic Versioning** from v1.0.0. No pre-release versions.

## XML Documentation

All public types and methods require comprehensive XML docs:

```csharp
/// <summary>
/// Assesses a ticket against configured assessment categories.
/// </summary>
/// <param name="issue">The ticket document to assess.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Assessment result with scores, explanations, and aggregations.</returns>
/// <exception cref="ArgumentNullException">Thrown if issue is null.</exception>
/// <exception cref="OperationCanceledException">Thrown if cancellation requested.</exception>
public async Task<IAssessmentResult> AssessAsync(
    JsonDocument issue,
    CancellationToken cancellationToken = default)
```

## Contributing

See [CONTRIBUTING.md](../CONTRIBUTING.md) for detailed guidelines.

## Questions?

- **Design & Architecture**: See [DESIGN.md](../DESIGN.md)
- **Implementation Plan**: See [PLAN.md](../PLAN.md) (in parent repo during development)
- **GitHub Issues**: [Report bugs or request features](https://github.com/panoramicdata/PanoramicData.IssueAssessment/issues)
