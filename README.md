# PanoramicData.IssueAssessment

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/PanoramicData.IssueAssessment.svg)](https://www.nuget.org/packages/PanoramicData.IssueAssessment/)

A high-performance, extensible .NET 10 library for schema-agnostic issue/ticket quality assessment. Assess tickets from any system (Jira, GitHub Issues, ServiceNow, custom platforms) by normalizing them to JSON and evaluating them through pluggable, composable assessment strategies.

## Overview

Ticket quality is hard to measure consistently across platforms. `PanoramicData.IssueAssessment` abstracts away platform-specific schemas and provides:

- **Schema-agnostic assessment**: Works with any ticketing system via `System.Text.Json.JsonDocument`
- **Composable strategies**: Combine assessments into categories; execute in parallel or series
- **Async-first design**: Full `CancellationToken` support throughout
- **Immutable configuration**: Type-safe, thread-safe option composition via C# 10 records
- **Extensible architecture**: Implement custom assessment types, gates, and aggregation strategies
- **10 built-in assessment types**: Regex, presence, length, count, ranges, enums, custom callbacks
- **7 base categories**: Title, description, acceptance criteria, ownership, timeline, reproducibility, AI suitability
- **Powerful aggregation**: Percentiles, weighted means, raw/normalized scoring
- **Optional caching**: In-memory or bring your own cache provider

## Quick Start

```csharp
using System.Text.Json;
using PanoramicData.IssueAssessment;
using PanoramicData.IssueAssessment.BaseCategories;

// Create a ticket JSON document (from any source: Jira API, GitHub API, etc.)
var ticketJson = """
{
    "title": "Fix login button styling",
    "description": "The login button on the homepage is misaligned on mobile devices...",
    "assignee": "alice@example.com",
    "dueDate": "2026-05-20"
}
""";

var document = JsonDocument.Parse(ticketJson);

// Create assessor with base categories
var options = new AssessmentOptions
{
    Categories = new[]
    {
        TitleQualityCategory.Create(),
        DescriptionQualityCategory.Create(),
        AssigneeOwnershipCategory.Create(),
        DueDateTimelineCategory.Create()
    }.ToImmutableList(),
    ExecutionStrategy = CategoryExecutionStrategy.Parallel
};

var assessor = new IssueAssessor(options);

// Assess the ticket
var result = await assessor.AssessAsync(document);

// Inspect results
foreach (var category in result.Categories)
{
    Console.WriteLine($"{category.CategoryName}: {category.Score.NormalizedScore:P}");
    foreach (var assessment in category.Assessments)
    {
        Console.WriteLine($"  - {assessment.AssessmentName}: {assessment.ExplanationText}");
    }
}

// View aggregations
Console.WriteLine($"Overall Mean: {result.Aggregations.AllAssessments.Mean:P}");
Console.WriteLine($"Overall Median: {result.Aggregations.AllAssessments.Median:P}");
```

## Core Concepts

### Categories

Group related assessments. Execute in parallel or series with optional gate conditions.

- **Parallel**: Categories run simultaneously; no dependency or data sharing
- **Series**: Categories execute sequentially; later categories can access results from prior ones via "virtual extensions"

### Assessments

Individual quality checks (10 built-in types available):

- `RegexAssessment` — Pattern matching
- `PresenceAssessment` — Field presence
- `StringLengthAssessment` — Length range
- `WordCountAssessment` — Word count range
- `NumericRangeAssessment` — Numeric value range
- `DateRangeAssessment` — Date range
- `EnumAssessment` — Whitelist validation
- `PatternCountAssessment` — Occurrence counting
- `JsonPathSelectorAssessment` — Custom logic on selected JSON nodes
- `CustomCallbackAssessment` — Arbitrary async logic

### Virtual Objects & Chaining

In **series execution**, assessments can optionally produce "virtual extensions" that subsequent assessments consume:

```csharp
// Assessment A extracts summary from description
var extractionAssessment = new CustomCallbackAssessment
{
    Callback = async (input, virtualExt, ct) =>
    {
        var description = input.RootElement.GetString("description");
        var summary = description?.Substring(0, 50) ?? "";
        // Store in virtual extension for Assessment B
        return (1.0, "Extracted summary", null);
    }
};

// Assessment B reads from virtual extension
// (configured with AllowVirtualExtension = true)
```

### Scoring & Aggregation

Each assessment returns `RawScore` (0–1 or custom range) and `NormalizedScore` (always 0–1):

```csharp
// Individual assessment score
var score = assessment.Score; // { RawScore: 0.75, NormalizedScore: 0.75 }

// Category aggregations
var metrics = result.Aggregations.ByCategory["Title Quality"];
// { Min: 0.5, Mean: 0.75, Median: 0.8, Percentiles: {...}, WeightedMean: 0.72 }
```

## Base Categories

The library ships with 7 reference implementations, all customizable:

### 1. Title Quality
Assesses title presence, length, and meaningfulness.

### 2. Description Quality
Assesses description presence, length, structure, and keyword presence.

### 3. Acceptance Criteria Clarity
Assesses whether acceptance criteria are documented and structured.

### 4. Assignee & Ownership
Assesses whether the ticket has clear ownership.

### 5. Due Date & Timeline
Assesses whether timeline expectations are clear.

### 6. Reproducibility & Steps
Assesses whether the issue is reproducible with clear steps.

### 7. AI Testing Suitability
Assesses whether the ticket is suitable for AI/LLM-based testing.

## Extensibility

### Custom Assessment Type

```csharp
public record MyAssessment : IAssessmentDefinition
{
    public async Task<AssessmentExecutionResult> ExecuteAsync(
        JsonDocument input,
        JsonDocument? virtualExtension,
        CancellationToken cancellationToken)
    {
        // Your logic here
        return new AssessmentExecutionResult(
            RawScore: score,
            ExplanationText: "...",
            FailureReason: null,
            InnerException: null,
            VirtualExtensionOutput: null);
    }
}
```

### Custom Gate Condition

```csharp
public record MyGate : ISeriesGateCondition
{
    public async Task<bool> ShouldExecuteNextAsync(
        CategoryResult priorResult,
        ImmutableList<AssessmentFailure> failures,
        CancellationToken cancellationToken)
    {
        // Decide whether to execute next category
        return priorResult.Score.NormalizedScore >= 0.5;
    }
}
```

### Custom Cache Provider

```csharp
public class MyRedisCache : IAssessmentCacheProvider
{
    public async Task<IAssessmentResult?> GetAsync(...) { /* ... */ }
    public async Task<bool> SetAsync(...) { /* ... */ }
    public async Task ClearAsync(...) { /* ... */ }
}

var assessor = new IssueAssessor(options, cacheProvider: new MyRedisCache());
```

## Features

- ✅ **Schema-agnostic**: Works with any JSON-serializable ticket format
- ✅ **Async throughout**: Full `CancellationToken` support, no blocking operations
- ✅ **Immutable configuration**: Record types, thread-safe, composable
- ✅ **Parallel & series execution**: Flexible category orchestration
- ✅ **Virtual object extension**: Data chaining in series mode
- ✅ **10 built-in assessment types**: Cover most common quality checks
- ✅ **7 base categories**: Reference implementations for ticket quality
- ✅ **Extensible**: Custom assessments, gates, cache providers, aggregations
- ✅ **Powerful aggregation**: Percentiles, weighted means, raw/normalized scores
- ✅ **Comprehensive error handling**: Timeouts, failures, detailed diagnostics
- ✅ **100% code coverage**: Thoroughly tested with XUnit3
- ✅ **Production-grade**: Strict semver from v1.0.0, full documentation

## Installation

```bash
dotnet add package PanoramicData.IssueAssessment
```

## Documentation

- [Design Document](DESIGN.md) — Deep dive into architecture, concepts, and design decisions
- [API Documentation](https://github.com/panoramicdata/PanoramicData.IssueAssessment/wiki) — Full reference
- [Contributing](CONTRIBUTING.md) — Development setup, test requirements

## Performance

- Single assessment: < 5ms (regex, presence, length)
- Full category (5 assessments, parallel): < 25ms
- Full assessment (7 categories): < 150ms (local checks; network I/O scales with latency)
- Caching: Cache hits in < 1ms

## License

MIT — See [LICENSE](LICENSE) for details.

## Support

- GitHub Issues: [Report bugs or request features](https://github.com/panoramicdata/PanoramicData.IssueAssessment/issues)
- Discussions: [Q&A and architecture discussions](https://github.com/panoramicdata/PanoramicData.IssueAssessment/discussions)

---

**Built by [PanoramicData](https://www.panoramicdata.com) — Quality software for complex systems.**
