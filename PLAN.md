# PanoramicData.IssueAssessment — Implementation Plan

**Version:** 1.0 (Pre-Implementation)  
**Date:** May 12, 2026  
**Status:** In Progress - Scaffold Complete, Build Stabilization Pending  
**Target Completion**: Staged delivery (Core → Assessments → Tests → Polish)

---

## Project Overview

This plan details the complete scaffold and implementation of `PanoramicData.IssueAssessment`, a high-quality open-source MIT-licensed library for schema-agnostic issue/ticket quality assessment.

**Template:** Copy structure from `PanoramicData.NCalcExtensions`  
**Repository Location:** `C:\Users\david\source\repos\panoramicdata\PanoramicData.IssueAssessment`

## Current Progress Snapshot

### Completed
- Repository scaffold created locally with the intended solution layout, project folders, and namespace markers.
- Central build files are in place: `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, and `nuget.config`.
- Documentation scaffold is present: `README.md`, `CONTRIBUTING.md`, `repository.instructions.md`, `DESIGN.md`, and this plan.
- CI/CD scaffolding is present with test and publish workflows.
- The solution file and all four project files are in place for the library, tests, docs, and examples projects.
- The initial commit has already been created.

### In Progress
- Package/version management is being stabilized so the repository can complete a clean `dotnet restore` and `dotnet build -c Release`.
- The current focus is on making sure the project files and `Directory.Packages.props` agree on the package management model before moving into Phase 1 code.

### Blocked / Needs Verification
- Restore has been failing on package management issues rather than library code.
- The repository needs one clean validation pass after the package reference model is confirmed.
- Phase 1 implementation should not start until the build is green so the type surface can be added against a stable baseline.

### Handoff Notes
- The plan is no longer a pre-work checklist; it is now a live implementation tracker.
- Keep the next session focused on build stabilization first, then move into the core interfaces and immutable record types.
- Preserve the existing architecture and do not expand scope until the foundation compiles cleanly.

---

## Phase 0: Repository Initialization (Immediate)

### 0.1 Create GitHub Repository
- [ ] Create public GitHub repo: `panoramicdata/PanoramicData.IssueAssessment`
- [ ] Set MIT license in GitHub UI
- [ ] Enable branch protection: `main` branch requires PR review
- [ ] Clone to local: `C:\Users\david\source\repos\panoramicdata\PanoramicData.IssueAssessment`

### 0.2 Copy & Adapt NCalcExtensions Template
- [ ] Copy folder structure from `PanoramicData.NCalcExtensions`:
  ```
  PanoramicData.IssueAssessment/
  ├── .github/
  │   ├── workflows/          (CI/CD: dotnet test, nuget publish, etc.)
  │   └── instructions/
  ├── Directory.Build.props   (Centralized build properties)
  ├── Directory.Packages.props (Centralized package versions)
  ├── global.json             (.NET 10.0)
  ├── .editorconfig
  ├── .gitignore
  ├── nuget.config
  ├── README.md
  ├── LICENSE (MIT)
  ├── DESIGN.md               (Our design doc)
  └── src/
      ├── PanoramicData.IssueAssessment/         (Main library)
      ├── PanoramicData.IssueAssessment.Tests/   (XUnit3 tests)
      ├── PanoramicData.IssueAssessment.Docs/    (Documentation project)
      └── PanoramicData.IssueAssessment.Examples/ (Optional: example implementations)
  ```

- [ ] Update all `.csproj` files:
  - Target framework: `net10.0`
  - Enable nullable reference types: `<Nullable>enable</Nullable>`
  - Set `<ImplicitUsings>enable</ImplicitUsings>`
  - Set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

### 0.3 Package & Dependency Setup
- [ ] Create `Directory.Packages.props` with centralized versions:
  ```xml
  <ItemGroup>
    <PackageVersion Include="System.Text.Json" Version="10.0.0" />
    <PackageVersion Include="System.Collections.Immutable" Version="10.0.0" />
    <PackageVersion Include="PanoramicData.NCalcExtensions" Version="1.0.0" />
    <!-- XUnit3 & Testing -->
    <PackageVersion Include="xunit" Version="2.8.1" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.1" />
    <PackageVersion Include="AwesomeAssertions" Version="1.12.0" />
  </ItemGroup>
  ```

- [ ] Create `.editorconfig` (copy from NCalcExtensions, enforce tabs, line length 120)
- [ ] Set up `nuget.config` for any internal package sources (if needed)

### 0.4 CI/CD Workflows
- [ ] Create `.github/workflows/test.yml`:
  - Runs `dotnet test` on push & PR
  - Collects code coverage (target: 100%)
  - Fails if coverage < 100%
  - Runs on multiple TFMs (net10.0)

- [ ] Create `.github/workflows/publish.yml`:
  - Triggered on tag push (v*.*.*)
  - Runs tests
  - Builds NuGet package
  - Publishes to nuget.org (with API key in GitHub Secrets)

### 0.5 Documentation Skeleton
- [ ] Create `README.md` with:
  - Project description & use cases
  - Quick start example
  - Core concepts overview
  - Link to DESIGN.md for deep dive
  - Link to API docs (XML docs)
  - Contributing guidelines
  - License notice

- [ ] Create `CONTRIBUTING.md`:
  - Development setup instructions
  - Code style & conventions
  - Test requirements (100% coverage)
  - PR process
  - Semver policy (strict from v1.0.0)

---

## Phase 1: Core Library Structure (Days 1–2)

### 1.1 Create Base Interfaces & Types

**File:** `src/PanoramicData.IssueAssessment/Core/Interfaces.cs`

```csharp
namespace PanoramicData.IssueAssessment.Core;

using System.Collections.Immutable;
using System.Text.Json;

// Top-level interface for options
public interface IAssessmentOptions
{
    ImmutableList<AssessmentCategory> Categories { get; }
    CategoryExecutionStrategy ExecutionStrategy { get; }
    TimeSpan? GlobalTimeout { get; }
    FailureHandlingStrategy FailureHandling { get; }
    bool CaptureVirtualDocument { get; }
}

// Assessment definition interface
public interface IAssessmentDefinition
{
    Task<AssessmentExecutionResult> ExecuteAsync(
        JsonDocument input,
        JsonDocument? virtualExtension,
        CancellationToken cancellationToken);
}

// Series gate interface
public interface ISeriesGateCondition
{
    Task<bool> ShouldExecuteNextAsync(
        CategoryResult priorCategoryResult,
        ImmutableList<AssessmentFailure> priorFailures,
        CancellationToken cancellationToken);
}

// Cache provider interface
public interface IAssessmentCacheProvider
{
    Task<IAssessmentResult?> GetAsync(
        JsonDocument issue,
        IAssessmentOptions options,
        CancellationToken cancellationToken);

    Task<bool> SetAsync(
        JsonDocument issue,
        IAssessmentOptions options,
        IAssessmentResult result,
        CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}

// Result interface
public interface IAssessmentResult
{
    ImmutableList<CategoryResult> Categories { get; }
    ImmutableList<AssessmentFailure> Failures { get; }
    AssessmentAggregations Aggregations { get; }
    JsonDocument? VirtualDocument { get; }
}

// Enums
public enum CategoryExecutionStrategy
{
    Parallel,
    Series
}

public enum AssessmentExecutionStrategy
{
    Parallel,
    Series
}

public enum FailureHandlingStrategy
{
    FailFast,
    CollectAll
}

public enum AssessmentFailureReason
{
    JsonPathNotFound,
    InvalidRegex,
    InvalidJsonPath,
    TimeoutExceeded,
    ThrowingException,
    VirtualExtensionAttemptedInParallel,
    ConfigurationError
}
```

- [ ] Implement all record types as immutable (C# 10 records with `init` properties)

### 1.2 Create Configuration Records

**File:** `src/PanoramicData.IssueAssessment/Configuration/AssessmentOptions.cs`

- [ ] `AssessmentOptions` (top-level, record)
- [ ] `AssessmentCategory` (per-category, record)
- [ ] `Assessment` (per-assessment, record)
- [ ] All with full XML documentation

### 1.3 Create Result Records

**File:** `src/PanoramicData.IssueAssessment/Results/AssessmentResult.cs`

- [ ] `AssessmentResult`
- [ ] `CategoryResult`
- [ ] `AssessmentScore` (raw + normalized)
- [ ] `AssessmentExecutionResult`
- [ ] `AssessmentFailure`
- [ ] `AssessmentAggregations`
- [ ] `AggregationMetrics`, `PercentileMetrics`, `WeightedAggregationMetrics`

### 1.4 Create Main Assessor Class Skeleton

**File:** `src/PanoramicData.IssueAssessment/IssueAssessor.cs`

```csharp
public class IssueAssessor
{
    private readonly IAssessmentOptions _options;
    private readonly IAssessmentCacheProvider? _cache;

    public IssueAssessor(
        IAssessmentOptions options,
        IAssessmentCacheProvider? cacheProvider = null)
    {
        ValidateOptions(options);
        _options = options;
        _cache = cacheProvider;
    }

    public async Task<IAssessmentResult> AssessAsync(
        JsonDocument issue,
        CancellationToken cancellationToken = default)
    {
        // TODO: Full implementation in Phase 2
    }

    public async IAsyncEnumerable<IAssessmentResult> AssessManyAsync(
        IAsyncEnumerable<JsonDocument> issues,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // TODO: Batch assessment with optional parallelism
    }

    private void ValidateOptions(IAssessmentOptions options)
    {
        // TODO: Validation in Phase 1.5
    }
}
```

### 1.5 Validation Engine

**File:** `src/PanoramicData.IssueAssessment/Validation/OptionsValidator.cs`

- [ ] Validate regex patterns (compile & throw if invalid)
- [ ] Validate JSONPath expressions (parse & throw if malformed)
- [ ] Validate weight ranges [-100, 100]
- [ ] Validate timeout values (positive)
- [ ] Validate virtual extension config (fail if parallel + attempted extension)
- [ ] Provide clear error messages for construction-time failures

---

## Phase 2: Assessment Types (Days 3–5)

### 2.1 Base Assessment Type Library

**Directory:** `src/PanoramicData.IssueAssessment/Assessments/`

Create 10 assessment types, each in its own file:

- [ ] **RegexAssessment.cs**: Pattern matching
  ```csharp
  public record RegexAssessment : IAssessmentDefinition
  {
      public required string JsonPath { get; init; }
      public required string Pattern { get; init; }
      public required double SuccessScore { get; init; }
      public required double FailureScore { get; init; }
      public string? ExplanationTemplate { get; init; }

      public async Task<AssessmentExecutionResult> ExecuteAsync(...)
      {
          // Implementation
      }
  }
  ```

- [ ] **PresenceAssessment.cs**: Field presence
- [ ] **StringLengthAssessment.cs**: Text length range
- [ ] **WordCountAssessment.cs**: Word count range
- [ ] **NumericRangeAssessment.cs**: Numeric value range
- [ ] **DateRangeAssessment.cs**: Date range validation
- [ ] **EnumAssessment.cs**: Value in whitelist
- [ ] **PatternCountAssessment.cs**: Pattern occurrence counting
- [ ] **JsonPathSelectorAssessment.cs**: Custom logic on JsonPath result
- [ ] **CustomCallbackAssessment.cs**: Arbitrary async callback

### 2.2 JSONPath Helper

**File:** `src/PanoramicData.IssueAssessment/Utilities/JsonPathHelper.cs`

- [ ] Parse JSONPath expressions using `System.Text.Json` (no external libraries)
- [ ] Navigate JsonDocument/JsonNode
- [ ] Return selected values or null if path not found
- [ ] Throw `InvalidJsonPathException` for malformed paths

### 2.3 Regex Compiler Cache

**File:** `src/PanoramicData.IssueAssessment/Utilities/RegexCache.cs`

- [ ] Thread-safe cache of compiled regex patterns
- [ ] Validate on first access, throw if invalid
- [ ] Cache hit for repeated patterns

### 2.4 Score Calculation Engine

**File:** `src/PanoramicData.IssueAssessment/Scoring/ScoreCalculator.cs`

- [ ] Calculate `MinPossibleRaw` & `MaxPossibleRaw` for category/assessments
- [ ] Normalize score: `(raw - min) / (max - min)`
- [ ] Handle edge case: if min == max, return normalized score = 1.0 (or 0.5?)
- [ ] Support weighted aggregation (simple & exponential)

### 2.5 Built-in Base Categories

**Directory:** `src/PanoramicData.IssueAssessment/BaseCategories/`

Create 7 category definitions (as static factories returning `AssessmentCategory`):

- [ ] `TitleQualityCategory.cs`
- [ ] `DescriptionQualityCategory.cs`
- [ ] `AcceptanceCriteriaCategory.cs`
- [ ] `AssigneeOwnershipCategory.cs`
- [ ] `DueDateTimelineCategory.cs`
- [ ] `ReproducibilityCategory.cs`
- [ ] `AiSuitabilityCategory.cs`

Each returns fully configured `AssessmentCategory` with assessments, weights, documentation.

---

## Phase 3: Execution Engine (Days 6–7)

### 3.1 Category Executor

**File:** `src/PanoramicData.IssueAssessment/Execution/CategoryExecutor.cs`

- [ ] Execute single category (parallel vs. series assessments)
- [ ] Handle virtual object extension chaining (series only)
- [ ] Manage timeouts per category
- [ ] Aggregate assessment results into category score
- [ ] Return `CategoryResult`

### 3.2 Assessment Executor

**File:** `src/PanoramicData.IssueAssessment/Execution/AssessmentExecutor.cs`

- [ ] Execute single assessment
- [ ] Handle failures (null score + reason)
- [ ] Manage assessment-level timeout
- [ ] Capture explanation text & virtual extension output
- [ ] Return `AssessmentExecutionResult`

### 3.3 Series Executor

**File:** `src/PanoramicData.IssueAssessment/Execution/SeriesExecutor.cs`

- [ ] Execute categories in series (one after another)
- [ ] Pass virtual extension from prior category to next
- [ ] Evaluate gate conditions before each category
- [ ] Short-circuit if gate returns false
- [ ] Collect failures throughout

### 3.4 Parallel Executor

**File:** `src/PanoramicData.IssueAssessment/Execution/ParallelExecutor.cs`

- [ ] Execute categories in parallel using `Task.WhenAll`
- [ ] Validate no virtual extensions in parallel (fail-fast)
- [ ] Manage global timeout
- [ ] Collect failures from all parallel tasks

### 3.5 Main Assessment Flow

**File:** `src/PanoramicData.IssueAssessment/IssueAssessor.cs` (continuation)

- [ ] Implement `AssessAsync(JsonDocument, CancellationToken)`
- [ ] Check cache first
- [ ] Choose executor (series or parallel)
- [ ] Run executor
- [ ] Calculate aggregations
- [ ] Optionally capture virtual document
- [ ] Cache result
- [ ] Return `IAssessmentResult`

---

## Phase 4: Aggregation Engine (Day 8)

### 4.1 Aggregation Calculator

**File:** `src/PanoramicData.IssueAssessment/Aggregation/AggregationCalculator.cs`

- [ ] Calculate percentiles (0, 5, 10, 25, 50, 75, 90, 95, 100)
- [ ] Calculate sum, sum of squares, count
- [ ] Calculate weighted mean (simple: `Σ(score × weight) / Σ(weight)`)
- [ ] Calculate exponential weighted mean: `Σ(score^weight) / Σ(1)` or similar
- [ ] Normalize weighted means
- [ ] Handle null scores gracefully (skip or return null aggregation)

### 4.2 Virtual Document Finalizer

**File:** `src/PanoramicData.IssueAssessment/Execution/VirtualDocumentFinalizer.cs`

- [ ] Combine virtual extensions from all assessments/categories
- [ ] Build final `JsonDocument` with all extensions
- [ ] Ensure no conflicts (assessment A and B both output to same path? → fail or merge?)

---

## Phase 5: Caching (Day 9)

### 5.1 Built-in In-Memory Cache

**File:** `src/PanoramicData.IssueAssessment/Caching/InMemoryAssessmentCache.cs`

```csharp
public class InMemoryAssessmentCache : IAssessmentCacheProvider
{
    private readonly ConcurrentDictionary<string, (IAssessmentResult, DateTime)> _cache;
    private readonly int _maxEntries;
    private readonly TimeSpan? _ttl;

    public InMemoryAssessmentCache(int maxEntries = 1000, TimeSpan? ttl = null)
    {
        _cache = new();
        _maxEntries = maxEntries;
        _ttl = ttl;
    }

    public async Task<IAssessmentResult?> GetAsync(...)
    {
        // Look up cache key; check TTL; return or evict
    }

    public async Task<bool> SetAsync(...)
    {
        // Add to cache; evict LRU if full
    }

    public async Task ClearAsync(...)
    {
        // Clear all entries
    }
}
```

- [ ] Use `ConcurrentDictionary` for thread safety
- [ ] Cache key: hash of (JsonDocument content, options definition)
- [ ] Implement LRU eviction when max entries reached
- [ ] Support optional TTL (default: no TTL)

### 5.2 Cache Key Generation

**File:** `src/PanoramicData.IssueAssessment/Caching/CacheKeyGenerator.cs`

- [ ] Generate stable hash from JsonDocument (content-based)
- [ ] Generate stable hash from IAssessmentOptions (definition-based)
- [ ] Combine into cache key

---

## Phase 6: Test Suite (Days 10–15)

### 6.1 Test Project Setup

**File:** `src/PanoramicData.IssueAssessment.Tests/PanoramicData.IssueAssessment.Tests.csproj`

- [ ] Reference XUnit3, AwesomeAssertions, Microsoft.NET.Test.Sdk
- [ ] No FluentAssertions (use AwesomeAssertions exclusively)
- [ ] Enable code coverage analysis

### 6.2 Unit Tests by Assessment Type

**Directory:** `src/PanoramicData.IssueAssessment.Tests/Assessments/`

For each assessment type, create comprehensive tests:

- [ ] `RegexAssessmentTests.cs` (> 10 test cases)
  - Valid regex, match, no match, invalid regex, null input, timeout, etc.

- [ ] `PresenceAssessmentTests.cs` (> 10 test cases)
  - Field present, field missing, null value, empty string, etc.

- [ ] `StringLengthAssessmentTests.cs` (> 10 test cases)
  - Within range, below min, above max, null input, etc.

- [ ] Similar for all 10 types

### 6.3 Integration Tests

**Directory:** `src/PanoramicData.IssueAssessment.Tests/Integration/`

- [ ] `CategoryExecutionTests.cs`
  - Single category execution, parallel assessments, series assessments, aggregation
  
- [ ] `SeriesExecutionTests.cs`
  - Multiple categories in series, virtual extension chaining, gate conditions

- [ ] `ParallelExecutionTests.cs`
  - Multiple categories in parallel, no virtual extension sharing

- [ ] `TimeoutTests.cs`
  - Per-assessment timeout, category timeout, global timeout, cancellation token

- [ ] `FailureHandlingTests.cs`
  - FailFast vs. CollectAll strategies

- [ ] `CachingTests.cs`
  - Cache hit, cache miss, TTL expiry, eviction

### 6.4 Base Category Tests

**Directory:** `src/PanoramicData.IssueAssessment.Tests/BaseCategories/`

- [ ] `TitleQualityCategoryTests.cs` (mock Jira/GitHub issues)
- [ ] `DescriptionQualityCategoryTests.cs`
- [ ] Similar for all 7 categories

### 6.5 Validation & Error Handling Tests

**Directory:** `src/PanoramicData.IssueAssessment.Tests/Validation/`

- [ ] `OptionsValidationTests.cs`
  - Invalid regex patterns, invalid JSONPath, invalid weights, timeout values

- [ ] `ConstructionErrorTests.cs`
  - Virtual extension in parallel mode, configuration conflicts

### 6.6 Scoring & Normalization Tests

**Directory:** `src/PanoramicData.IssueAssessment.Tests/Scoring/`

- [ ] `ScoreCalculationTests.cs`
  - Weighted average, percentiles, normalization, negative scores

- [ ] `AggregationTests.cs`
  - Calculate all aggregation types, null handling

### 6.7 Test Fixtures & Helpers

**File:** `src/PanoramicData.IssueAssessment.Tests/Fixtures/`

- [ ] `SampleIssuesFixture.cs` → Mock Jira, GitHub, ServiceNow documents
- [ ] `AssessmentBuilderFixture.cs` → Factory for creating test assessments
- [ ] `OptionsBuilderFixture.cs` → Factory for creating test options

### 6.8 Code Coverage Verification

- [ ] Run `dotnet test` with code coverage collection
- [ ] Target: **100% coverage** (all lines, all branches)
- [ ] Use OpenCover or similar
- [ ] Fail build if coverage < 100%

---

## Phase 7: Documentation & Polish (Days 16–18)

### 7.1 XML Documentation

- [ ] Add comprehensive XML docs to all public types & members
- [ ] Follow PanoramicData style guide (clear, concise, 1–3 sentences for explanations)
- [ ] Include `<param>`, `<returns>`, `<exception>`, `<remarks>` tags

### 7.2 README.md Completion

- [ ] Quick start example
- [ ] Concepts overview (categories, assessments, scoring)
- [ ] Platform-agnostic JSON mapping examples (Jira, GitHub)
- [ ] Link to DESIGN.md
- [ ] Contributing guidelines

### 7.3 API Documentation Site (Optional)

- [ ] Generate from XML docs using DocFX
- [ ] Deploy to GitHub Pages

### 7.4 Code Quality Checks

- [ ] Run `dotnet build` with all warnings as errors
- [ ] Run `dotnet test` with 100% coverage gate
- [ ] Run code analyzer (StyleCop, etc.) via Roslyn
- [ ] Ensure all warnings resolved

### 7.5 Example Implementation

**Directory:** `src/PanoramicData.IssueAssessment.Examples/`

- [ ] `JiraIssueAssessmentExample.cs` → Real Jira JSON fixture
- [ ] `GitHubIssueAssessmentExample.cs` → Real GitHub issue JSON fixture
- [ ] `ServiceNowTicketAssessmentExample.cs` → ServiceNow ticket JSON fixture

Each example:
- Loads sample ticket JSON
- Creates `IssueAssessor` with base categories
- Runs assessment
- Prints formatted results

### 7.6 Release Notes

- [ ] Create `CHANGELOG.md` with v1.0.0 entry
- [ ] Highlight key features, architecture decisions, breaking changes (none at v1.0)

---

## Phase 8: GitHub & NuGet Publishing (Day 19)

### 8.1 Create GitHub Release

- [ ] Tag: `v1.0.0`
- [ ] Release notes: Copy from CHANGELOG
- [ ] Attach example files

### 8.2 Publish NuGet Package

- [ ] Run `dotnet pack` → produces `.nupkg`
- [ ] Publish to nuget.org (or internal feed)
- [ ] Verify package on NuGet.org

### 8.3 Update PanoramicData Docs

- [ ] Update consuming repo list (if applicable)
- [ ] Register in NuGetManagement assessment tool

---

## Phase 9: Sync to Consuming Repos (Day 20)

### 9.1 Update PanoramicData.Skills (if applicable)

- [ ] Add `IssueAssessment.Skill.md` if a skill is to be created
- [ ] Sync to consuming repositories via `Sync-Skills.ps1`

### 9.2 Initial PR to PanoramicData.Operations (Optional)

- [ ] If OPS Dashboard will use it, create PR to add as dependency
- [ ] Integration testing with real Jira tickets

---

## Milestone Checklist

- [ ] **Phase 0 Complete**: Repo initialized, template copied, CI/CD configured
- [ ] **Phase 1 Complete**: Core interfaces, types, validators in place
- [ ] **Phase 2 Complete**: All 10 assessment types + 7 base categories
- [ ] **Phase 3 Complete**: Execution engines (series, parallel, assessment)
- [ ] **Phase 4 Complete**: Aggregation & normalization engine
- [ ] **Phase 5 Complete**: Caching layer
- [ ] **Phase 6 Complete**: Full test suite (100% coverage)
- [ ] **Phase 7 Complete**: Documentation & examples
- [ ] **Phase 8 Complete**: Published to NuGet
- [ ] **Phase 9 Complete**: Integrated with consuming repos (if applicable)

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| JSONPath .NET implementation complexity | Use simple recursive descent; test thoroughly; document limitations |
| Virtual object mutation conflicts | Validate at construction time; fail-fast for parallel + extension |
| Timeout management complexity | Create helper utilities; test all timeout scenarios |
| Score normalization edge cases | Test with negative, zero, extreme weights; document formulas |
| 100% code coverage target difficult | Write tests first (TDD); refactor unreachable code early |

---

## Design Review Gates

Before proceeding to next phase:

1. **Phase 1 → Phase 2**: Interfaces & types locked; validation engine working
2. **Phase 2 → Phase 3**: All 10 assessment types + 7 categories implemented & tested
3. **Phase 3 → Phase 4**: Execution engine working (parallel & series)
4. **Phase 6 → Phase 7**: 100% code coverage achieved
5. **Phase 8 → Phase 9**: NuGet package published & verified on nuget.org

---

## Success Criteria

✅ **Functional**
- All 10 assessment types working
- All 7 base categories functional
- Series & parallel execution modes work
- Virtual object extension chaining works
- Gate conditions work
- Timeouts & cancellation work

✅ **Quality**
- 100% code coverage (no unreachable code)
- All warnings as errors
- Zero StyleCop violations
- XML docs complete
- Tests comprehensive (> 100 test cases)

✅ **Documentation**
- DESIGN.md complete
- README.md complete
- API docs generated
- Examples functional

✅ **Open Source**
- MIT license
- Published to nuget.org
- CI/CD working (public repo)
- Contributing guidelines clear

✅ **Production-Ready**
- Semantic versioning (strict from v1.0.0)
- Performance baseline established
- Error handling comprehensive
- Caching optional but available

---

## What Next

1. Confirm the package management strategy is consistent across the repository, then run a clean `dotnet restore` followed by `dotnet build -c Release`.
2. If restore/build succeeds, start Phase 1 by implementing the immutable configuration and result records plus the validation surface described in the design.
3. Add focused tests alongside the first real types so the eventual coverage target stays realistic and measurable.
4. Keep the next handover centered on the current build state, any restore/build errors, and the first Phase 1 files touched so another session can continue without rediscovery.

**Document Status**: Ready for scaffold phase. Next: Initialize GitHub repo & begin Phase 0.

