# Contributing to PanoramicData.IssueAssessment

Thank you for your interest in contributing! This document provides guidelines for development and contributions.

## Development Setup

### Prerequisites

- .NET 10.0 SDK
- Git
- VS Code or Visual Studio 2022+

### Local Setup

```bash
# Clone the repository
git clone https://github.com/panoramicdata/PanoramicData.IssueAssessment.git
cd PanoramicData.IssueAssessment

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Verify coverage (must be 100%)
dotnet test /p:CollectCoverage=true
```

## Code Style & Conventions

- **Language**: C# with latest features (nullable reference types, records, etc.)
- **Indentation**: Tabs (enforced by `.editorconfig`)
- **Naming**: PascalCase for public types, camelCase for locals
- **Namespace**: `PanoramicData.IssueAssessment.*` hierarchy
- **XML Documentation**: Comprehensive, 2–3 sentences per public member
- **No FluentAssertions**: Use `AwesomeAssertions` exclusively in tests

### Key Rules

- Treat warnings as errors: `-WarnAsError`
- No implicit nullability issues: `<Nullable>enable</Nullable>`
- No unused imports or variables
- All public APIs must have XML documentation
- Tests must be clear and descriptive

## Test Requirements

- **Framework**: XUnit3 (not XUnit2)
- **Assertions**: AwesomeAssertions (NOT FluentAssertions)
- **Coverage**: 100% (no unreachable code)
- **Test structure**: Arrange-Act-Assert pattern

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage report
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura

# Run specific test file
dotnet test --filter "PanoramicData.IssueAssessment.Tests.Assessments.RegexAssessmentTests"
```

### Writing a Test

```csharp
public class RegexAssessmentTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidPattern_ReturnsSuccessScore()
    {
        // Arrange
        var assessment = new RegexAssessment
        {
            JsonPath = "$.title",
            Pattern = "^[A-Z]",
            SuccessScore = 1.0,
            FailureScore = 0.0
        };
        var document = JsonDocument.Parse("""{"title": "Fix button"}""");

        // Act
        var result = await assessment.ExecuteAsync(document, null, CancellationToken.None);

        // Assert
        result.RawScore.Should().Be(1.0);
        result.ExplanationText.Should().Contain("Pattern");
        result.FailureReason.Should().BeNull();
    }
}
```

## Commit & PR Guidelines

### Commit Messages

Use clear, descriptive messages:

```
feat: Add IPatternCountAssessment type

Implement pattern occurrence counting for assessment categories.
Supports min/max match validation with configurable scoring.

Tests: 12 new test cases covering edge cases and error handling.
Coverage: 100% (new code).
```

### PR Process

1. **Create a feature branch**: `git checkout -b feat/issue-123-description`
2. **Implement the feature**: Follow the code style guidelines
3. **Write tests**: 100% coverage required
4. **Run full test suite**: `dotnet test`
5. **Push and open PR**: Clear description of what/why
6. **Address review feedback**: Iterate with reviewers
7. **Merge**: Squash-merge when done (to keep history clean)

### PR Template

```markdown
## Description
Brief summary of the change.

## Type
- [ ] New assessment type
- [ ] Bug fix
- [ ] Documentation
- [ ] Refactor
- [ ] Performance

## Testing
Describe tests added/modified. Include coverage impact.

## Checklist
- [ ] Tests pass (100% coverage)
- [ ] No warnings
- [ ] XML docs updated
- [ ] README/DESIGN.md updated (if needed)
- [ ] Semver compliance (if applicable)
```

## Project Structure

```
PanoramicData.IssueAssessment/
├── src/
│   ├── PanoramicData.IssueAssessment/           # Main library
│   │   ├── Core/                               # Core interfaces & types
│   │   ├── Configuration/                      # Records (options, categories)
│   │   ├── Results/                            # Result types
│   │   ├── Assessments/                        # 10 built-in assessment types
│   │   ├── BaseCategories/                     # 7 reference categories
│   │   ├── Execution/                          # Execution engines
│   │   ├── Scoring/                            # Scoring & aggregation
│   │   ├── Caching/                            # Cache implementations
│   │   ├── Validation/                         # Validation logic
│   │   ├── Utilities/                          # Helpers (JSONPath, regex, etc.)
│   │   └── IssueAssessor.cs                    # Main entry point
│   ├── PanoramicData.IssueAssessment.Tests/    # Test suite
│   │   ├── Assessments/                        # Per-type tests
│   │   ├── Integration/                        # End-to-end tests
│   │   ├── BaseCategories/                     # Category tests
│   │   ├── Validation/                         # Validation tests
│   │   ├── Scoring/                            # Scoring tests
│   │   ├── Caching/                            # Cache tests
│   │   └── Fixtures/                           # Test helpers
│   ├── PanoramicData.IssueAssessment.Docs/     # Documentation (future)
│   └── PanoramicData.IssueAssessment.Examples/ # Example implementations
├── .github/
│   ├── workflows/
│   │   ├── test.yml                            # CI/CD test pipeline
│   │   └── publish.yml                         # NuGet publish pipeline
│   └── instructions/
├── Directory.Build.props                       # Centralized build settings
├── Directory.Packages.props                    # Centralized NuGet versions
├── global.json                                 # .NET SDK version
├── .editorconfig                               # Code style enforcement
├── nuget.config                                # NuGet package sources
├── DESIGN.md                                   # Architecture document
├── README.md                                   # Project overview
├── CONTRIBUTING.md                             # This file
└── LICENSE                                     # MIT license
```

## Versioning

This project follows **Semantic Versioning (SemVer)** strictly from v1.0.0:

- **MAJOR** (1.0.0 → 2.0.0): Breaking API changes
- **MINOR** (1.0.0 → 1.1.0): New backward-compatible features
- **PATCH** (1.0.0 → 1.0.1): Bug fixes only

### No Pre-Release Versions

Versions go directly to release (v1.0.0, v1.1.0, etc.); no alpha/beta/RC versions.

## Release Process

1. Update `Directory.Build.props` with new version
2. Update `CHANGELOG.md` with release notes
3. Commit & push: `git commit -am "Release v1.1.0"`
4. Create tag: `git tag -a v1.1.0 -m "Release v1.1.0"`
5. Push tag: `git push origin v1.1.0`
6. GitHub Actions automatically publishes to NuGet

## Questions?

- **GitHub Issues**: [Ask here](https://github.com/panoramicdata/PanoramicData.IssueAssessment/issues)
- **Discussions**: [Community Q&A](https://github.com/panoramicdata/PanoramicData.IssueAssessment/discussions)

Thank you for contributing! 🚀
