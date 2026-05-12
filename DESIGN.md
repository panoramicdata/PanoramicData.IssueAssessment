# PanoramicData.IssueAssessment — Design Document

**Version:** 1.0 (Pre-Implementation)  
**Date:** May 12, 2026  
**License:** MIT (Open Source)  
**Status:** Design Phase — Ready for Scaffold

---

## Executive Summary

`PanoramicData.IssueAssessment` is a high-performance, extensible .NET 10 library for assessing the quality and metadata of ticketing system issues against arbitrary dimensions. It abstracts away platform-specific schemas (Jira, GitHub Issues, ServiceNow, custom systems) by normalizing all tickets to a `System.Text.Json.JsonDocument` and evaluating them through pluggable, composable assessment strategies.

**Core Design Principles:**
- **Schema-agnostic**: Works with any ticket representation as JSON
- **Composable assessments**: Categories → Assessments, with configurable parallelism & execution order
- **Async-first**: All assessment operations are asynchronous with mandatory `CancellationToken` support
- **Immutable configuration**: Record types for type-safe, thread-safe option composition
- **Extensible**: Users implement interfaces, provide callbacks, or customize aggregation strategies
- **Performance-aware**: Optional caching, batch assessment, and controlled parallelism
- **Production-grade**: 100% code coverage, strict semver from v1.0.0, full XML documentation

---

## Architecture Overview

### High-Level Flow

```
┌─────────────────────────────┐
│   JsonDocument (Ticket)     │
│   + IAssessmentOptions      │
└──────────────┬──────────────┘
               │
               ▼
        ┌──────────────┐
        │ IssueAssessor│
        │  .Assess()   │
        └──────┬───────┘
               │
        ┌──────▼───────────────────────────┐
        │ Execution Strategy               │
        │ (Parallel or Series Categories)  │
        └──────┬───────────────────────────┘
               │
        ┌──────▼──────────────────────────────────────┐
        │ Category 1 Execution                       │
        │ ┌────────────────────────────────────────┐ │
        │ │ Assessment A, B, C (Parallel/Series)  │ │
        │ │ [Output: Optional Virtual Extension]  │ │
        │ └────────────────────────────────────────┘ │
        └──────┬──────────────────────────────────────┘
               │
        ┌──────▼──────────────────────────────────────┐
        │ Category 2 Execution (Optional Gate)       │
        │ ┌────────────────────────────────────────┐ │
        │ │ Assessment D, E (uses Virtual Input)   │ │
        │ │ [Output: Optional Virtual Extension]  │ │
        │ └────────────────────────────────────────┘ │
        └──────┬──────────────────────────────────────┘
               │
               ▼
        ┌──────────────────────────┐
        │ IAssessmentResult        │
        │ ├─ Category Results[]    │
        │ ├─ Aggregations          │
        │ ├─ VirtualDocument?      │
        │ └─ Failures[]            │
        └──────────────────────────┘
```

### Virtual Object Extension Mechanism

Assessments executed in series can **virtually extend** (but not modify) the input `JsonDocument`:

- **Example**: Assessment A extracts a value from `description`, stores it as `extracted_summary` in virtual extension
- **Series execution**: Assessment B reads from virtual extension using `$.extracted_summary`
- **Parallel execution**: Virtual extensions are **NOT shared** between parallel assessments; attempting to configure this raises `InvalidOperationException` at construction time
- **Final result**: Optional `VirtualDocument` in result contains the final extended state for visibility
- **Read-only promise**: Virtual extensions never modify the original input; all extensions are side-effect-free

---

## Core Types & Interfaces

### 1. Main Entry Point

```csharp
public class IssueAssessor
{
    // Constructor
    public IssueAssessor(
        IAssessmentOptions options,
        IAssessmentCacheProvider? cacheProvider = null);

    // Core method
    public async Task<IAssessmentResult> AssessAsync(
        JsonDocument issue,
        CancellationToken cancellationToken = default);

    // Batch assessment
    public async Task<IAssessmentResult[]> AssessAsync(
        IAsyncEnumerable<JsonDocument> issues,
        CancellationToken cancellationToken = default);
}
```

### 2. Configuration: Immutable Records

```csharp
// Top-level options (record, immutable, C# 10 init syntax)
public record AssessmentOptions : IAssessmentOptions
{
    public required ImmutableList<AssessmentCategory> Categories { get; init; }
    public required CategoryExecutionStrategy ExecutionStrategy { get; init; }
    public TimeSpan? GlobalTimeout { get; init; }
    public FailureHandlingStrategy FailureHandling { get; init; }
    public bool CaptureVirtualDocument { get; init; }
}

// Per-category configuration
public record AssessmentCategory
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ImmutableList<Assessment> Assessments { get; init; }
    public required AssessmentExecutionStrategy AssessmentExecutionStrategy { get; init; }
    public TimeSpan? CategoryTimeout { get; init; }
    public FailureHandlingStrategy CategoryFailureHandling { get; init; }
    public ISeriesGateCondition? SeriesGateCondition { get; init; }
}

// Per-assessment configuration
public record Assessment
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IAssessmentDefinition Definition { get; init; }
    public TimeSpan? AssessmentTimeout { get; init; }
    public double Weight { get; init; }
    public bool AllowVirtualExtension { get; init; }
    public string? VirtualOutputName { get; init; }
}
```

### 3. Assessment Execution Strategies

```csharp
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
    FailFast,        // Stop on first failure
    CollectAll       // Collect all failures, return in result
}
```

### 4. Assessment Definitions (10 Built-In Types)

All implement `IAssessmentDefinition`:

```csharp
public interface IAssessmentDefinition
{
    Task<AssessmentExecutionResult> ExecuteAsync(
        JsonDocument input,
        JsonDocument? virtualExtension,
        CancellationToken cancellationToken);
}

public record AssessmentExecutionResult(
    double? RawScore,
    string? ExplanationText,
    AssessmentFailureReason? FailureReason,
    Exception? InnerException,
    JsonDocument? VirtualExtensionOutput);

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

#### Type 1: IRegexAssessment
```csharp
public record RegexAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public required string Pattern { get; init; }
    public required double SuccessScore { get; init; }
    public required double FailureScore { get; init; }
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Presence and match of regex pattern against JsonPath-selected text
- **Score**: Success score (0–1, typically 1.0) if match; failure score if no match or JsonPath fails
- **Explanation**: "Pattern [regex] found in [field]" or configurable template

#### Type 2: IPresenceAssessment
```csharp
public record PresenceAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public double PresentScore { get; init; } = 1.0;
    public double AbsentScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether a JsonPath selector returns a non-null value
- **Score**: Present score if value exists and is not empty/null; absent score otherwise
- **Explanation**: "Field present" or "Field missing"

#### Type 3: IStringLengthAssessment
```csharp
public record StringLengthAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether text length at JsonPath falls within [min, max]
- **Score**: Success if within range; failure otherwise
- **Explanation**: "Length [actual] is within [min]–[max]" or custom

#### Type 4: IWordCountAssessment
```csharp
public record WordCountAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public int? MinWords { get; init; }
    public int? MaxWords { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether word count at JsonPath falls within [min, max]
- **Score**: Success if within range; failure otherwise
- **Explanation**: "Word count [actual] is [status]"

#### Type 5: INumericRangeAssessment
```csharp
public record NumericRangeAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether numeric value at JsonPath falls within [min, max]
- **Score**: Success if within range; failure otherwise
- **Explanation**: "Value [actual] is [status]"

#### Type 6: IDateRangeAssessment
```csharp
public record DateRangeAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public DateTime? MinDate { get; init; }
    public DateTime? MaxDate { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? DateFormat { get; init; } = "O";
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether date at JsonPath falls within [min, max]
- **Score**: Success if in range; failure otherwise
- **Explanation**: "Date [actual] is [status]"

#### Type 7: IEnumAssessment
```csharp
public record EnumAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public required ImmutableHashSet<string> AllowedValues { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether value at JsonPath is in allowed whitelist
- **Score**: Success if in list; failure otherwise
- **Explanation**: "Status [actual] is allowed"

#### Type 8: IPatternCountAssessment
```csharp
public record PatternCountAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public required string Pattern { get; init; }
    public int? MinMatches { get; init; }
    public int? MaxMatches { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Whether pattern occurrence count at JsonPath falls within [min, max]
- **Score**: Success if count in range; failure otherwise
- **Explanation**: "Found [count] matches; expected [min]–[max]"

#### Type 9: IJsonPathSelectorAssessment
```csharp
public record JsonPathSelectorAssessment : IAssessmentDefinition
{
    public required string JsonPath { get; init; }
    public required Func<JsonNode?, Task<bool>> Evaluator { get; init; }
    public double SuccessScore { get; init; } = 1.0;
    public double FailureScore { get; init; } = 0.0;
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Arbitrary custom logic on JsonPath-selected node
- **Score**: Success if evaluator returns true; failure if false or throws
- **Explanation**: Custom (from consumer)

#### Type 10: ICustomCallbackAssessment
```csharp
public record CustomCallbackAssessment : IAssessmentDefinition
{
    public required Func<JsonDocument, JsonDocument?, CancellationToken, Task<(double? Score, string Explanation, Exception? Exception)>> Callback { get; init; }
    public string? ExplanationTemplate { get; init; }
}
```
- **Assesses**: Custom async logic provided by consumer
- **Score**: Whatever consumer returns (can be negative, > 1.0)
- **Explanation**: Consumer-provided
- **Flexibility**: Full access to both input and virtual extension; can perform I/O, call APIs, etc.

---

## Result Structure

### IAssessmentResult

```csharp
public interface IAssessmentResult
{
    ImmutableList<CategoryResult> Categories { get; }
    ImmutableList<AssessmentFailure> Failures { get; }
    AssessmentAggregations Aggregations { get; }
    JsonDocument? VirtualDocument { get; }
}

public record CategoryResult(
    string CategoryName,
    AssessmentScore Score,
    string Reason,
    ImmutableList<AssessmentResult> Assessments);

public record AssessmentResult(
    string AssessmentName,
    AssessmentScore Score,
    string ExplanationText,
    AssessmentFailureReason? FailureReason,
    Exception? InnerException);

public record AssessmentScore(
    double? RawScore,
    double? NormalizedScore,
    double MinPossibleRaw,
    double MaxPossibleRaw);

public record AssessmentFailure(
    string CategoryName,
    string AssessmentName,
    AssessmentFailureReason Reason,
    string Message,
    Exception? InnerException);
```

### Aggregations

```csharp
public record AssessmentAggregations(
    AggregationMetrics AllAssessments,
    AggregationMetrics CategoryAggregates,
    Dictionary<string, AggregationMetrics> ByCategory);

public record AggregationMetrics(
    int? Count,
    double? Sum,
    double? SumOfSquares,
    double? Min,
    double? Mean,
    double? Median,
    double? Max,
    PercentileMetrics Percentiles,
    WeightedAggregationMetrics WeightedMetrics);

public record PercentileMetrics(
    double? P0,    // Min
    double? P5,
    double? P10,
    double? P25,
    double? P50,   // Mean
    double? P75,
    double? P90,
    double? P95,
    double? P100); // Max

public record WeightedAggregationMetrics(
    double? WeightedSum,
    double? WeightedMean,
    double? ExponentialWeightedMean,
    double? NormalizedWeightedMean);
```

---

## Series Execution & Gating

### Series Gate Conditions

When categories execute in series, a gate condition can prevent execution of subsequent categories:

```csharp
public interface ISeriesGateCondition
{
    Task<bool> ShouldExecuteNextAsync(
        CategoryResult priorCategoryResult,
        ImmutableList<AssessmentFailure> priorFailures,
        CancellationToken cancellationToken);
}

// Built-in: Score Threshold Gate
public record ScoreThresholdGate : ISeriesGateCondition
{
    public required string PriorCategoryName { get; init; }
    public required double MinimumScore { get; init; }
    public required bool NormalizedScoreOnly { get; init; }
}

// Built-in: No Failures Gate
public record NoFailuresGate : ISeriesGateCondition
{
    public required string PriorCategoryName { get; init; }
}
```

**Mechanism:**
- After Category 1 completes, gate evaluates prior result
- If gate returns `false`, Category 2+ skipped entirely
- Useful for "skip expensive AI tests if title is missing" scenarios
- Virtual extension still available to next category (if it executes)

---

## Caching

### Cache Provider Interface

```csharp
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

// Built-in simple in-memory cache
public class InMemoryAssessmentCache : IAssessmentCacheProvider
{
    public InMemoryAssessmentCache(int maxEntries = 1000, TimeSpan? ttl = null);
    // Implementation using ConcurrentDictionary + TTL
}
```

**Cache Key**: Hash of (JsonDocument content, AssessmentOptions definition)

---

## Base Categories (QA Dashboard Set)

These ship as reference implementations and can be customized:

### 1. **Title Quality**
Assesses whether the title is present, meaningful, and suitable for assessment.

**Assessments:**
- `PresenceAssessment`: Title field exists (path: `$.title` or `$.fields.summary`)
- `StringLengthAssessment`: Length 5–150 chars
- `RegexAssessment`: No leading/trailing whitespace; no placeholder text like "Untitled"

**Aggregation**: Weighted average (presence: 0.3, length: 0.3, regex: 0.4)

---

### 2. **Description Quality**
Assesses description presence, length, and structural clarity.

**Assessments:**
- `PresenceAssessment`: Description exists (path: `$.description` or `$.body`)
- `StringLengthAssessment`: Length 20–10,000 chars (meaningful length)
- `WordCountAssessment`: 5–1,000 words
- `RegexAssessment`: Contains at least one of: "reproduce", "issue", "problem", "error" (case-insensitive)

**Aggregation**: Weighted average (presence: 0.25, length: 0.25, word count: 0.25, keyword: 0.25)

---

### 3. **Acceptance Criteria Clarity**
Assesses whether the issue includes clear, measurable acceptance criteria.

**Assessments:**
- `PresenceAssessment`: AC section detected (via regex: "acceptance criteria|AC:|expected behavior")
- `PatternCountAssessment`: At least 1, at most 10 bullet points or numbered items
- `RegexAssessment`: Contains "should", "must", "given/when/then" (BDD-style) or numbered steps
- `StringLengthAssessment`: AC section ≥50 chars

**Aggregation**: Weighted average (presence: 0.3, pattern count: 0.25, structure: 0.25, length: 0.2)

---

### 4. **Assignee & Ownership**
Assesses whether the issue has clear ownership.

**Assessments:**
- `PresenceAssessment`: Assignee field non-null (path: `$.assignee.name` or `$.assignee`)
- `EnumAssessment`: Assignee is in approved list (or negative if "Unassigned")

**Aggregation**: Weighted average (presence: 0.6, validity: 0.4)

---

### 5. **Due Date & Timeline**
Assesses whether timeline expectations are clear.

**Assessments:**
- `PresenceAssessment`: Due date field present (path: `$.dueDate` or `$.due_date`)
- `DateRangeAssessment`: Due date is within reasonable future (not past, not >2 years out)
- `RegexAssessment`: Issue contains timeline keywords ("urgent", "blocking", "by Friday", etc.)

**Aggregation**: Weighted average (presence: 0.4, date range: 0.4, keywords: 0.2)

---

### 6. **Reproducibility & Steps**
Assesses whether the issue is reproducible.

**Assessments:**
- `PresenceAssessment`: "Steps to reproduce" or "environment" section detected
- `PatternCountAssessment`: At least 3 steps, at most 50
- `RegexAssessment`: Steps include environment context ("OS:", "version:", "browser:", etc.)
- `WordCountAssessment`: Steps section ≥30 words

**Aggregation**: Weighted average (presence: 0.3, pattern count: 0.25, context: 0.25, word count: 0.2)

---

### 7. **AI Testing Suitability**
Assesses whether the issue is suitable for automated AI/LLM-based testing.

**Assessments:**
- `RegexAssessment`: Issue has clear expected vs. actual behavior ("Expected:", "Actual:")
- `RegexAssessment`: Contains examples or sample data (code blocks, URLs, JSON examples)
- `WordCountAssessment`: Description ≥100 words (sufficient context for AI)
- `RegexAssessment`: No ambiguous language (avoid "maybe", "possibly", "probably" in acceptance criteria)
- `PresenceAssessment`: Acceptance criteria or expected behavior documented

**Aggregation**: Weighted average (expected vs actual: 0.3, examples: 0.25, word count: 0.2, clarity: 0.15, AC present: 0.1)

---

## Validation & Construction-Time Checking

When constructing `IssueAssessor` or `AssessmentOptions`, validate:

1. **Regex patterns**: Compile and validate all regex; throw `RegexParseException` if invalid
2. **JsonPath expressions**: Validate all JSONPath selectors; throw `InvalidJsonPathException` if malformed
3. **Weights**: Ensure weights fit in [-100.0, 100.0]; throw if outside bounds
4. **Score scales**: Warn (but don't fail) if custom callbacks return scores outside [0, 1]
5. **Virtual extension config**: If any parallel assessment attempts `AllowVirtualExtension = true`, throw `InvalidOperationException` with clear guidance
6. **Timeout values**: Ensure positive; throw if ≤0

---

## Scoring & Normalization

### Raw Score Calculation

For a single assessment:
```
RawScore = SuccessScore (if condition met) or FailureScore (if not)
```

For a category (weighted average):
```
RawScore = Σ(Assessment.RawScore × Assessment.Weight) / Σ(Assessment.Weight)
```

### Normalized Score Calculation

Before assessment starts, calculate possible range:
```
MinPossible = Σ(min(SuccessScore, FailureScore) × Weight) / Σ(Weight)
MaxPossible = Σ(max(SuccessScore, FailureScore) × Weight) / Σ(Weight)
```

After assessment:
```
NormalizedScore = (RawScore - MinPossible) / (MaxPossible - MinPossible)
                  [clamped to [0, 1]]
```

This handles negative scores gracefully.

---

## Extensibility Patterns

### Pattern 1: Custom Assessment Type
```csharp
public record MyCustomAssessment : IAssessmentDefinition
{
    // fields...
    public async Task<AssessmentExecutionResult> ExecuteAsync(
        JsonDocument input,
        JsonDocument? virtualExtension,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Pattern 2: Custom Callback
```csharp
var assessment = new CustomCallbackAssessment(
    Callback: async (input, virtualExt, ct) =>
    {
        // Arbitrary async logic
        var value = input.RootElement.GetProperty("field").GetString();
        var score = string.IsNullOrEmpty(value) ? 0.0 : 1.0;
        return (score, "Field evaluated", null);
    }
);
```

### Pattern 3: Custom Gate Condition
```csharp
public record MyCustomGate : ISeriesGateCondition
{
    public async Task<bool> ShouldExecuteNextAsync(
        CategoryResult priorResult,
        ImmutableList<AssessmentFailure> failures,
        CancellationToken cancellationToken)
    {
        // Decision logic
        return priorResult.Score.NormalizedScore >= 0.5;
    }
}
```

### Pattern 4: Custom Cache Provider
```csharp
public class MyRedisCache : IAssessmentCacheProvider
{
    // Redis-backed caching logic
}
```

---

## Error Handling & Resilience

### Assessment Failure Flow

```csharp
try
{
    var result = await executionAsync(cancellationToken);
    // Success path
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // Timeout or cancellation
    return new AssessmentExecutionResult(
        RawScore: null,
        ExplanationText: null,
        FailureReason: AssessmentFailureReason.TimeoutExceeded,
        InnerException: ex,
        VirtualExtensionOutput: null);
}
catch (Exception ex)
{
    // Unexpected exception
    return new AssessmentExecutionResult(
        RawScore: null,
        ExplanationText: null,
        FailureReason: AssessmentFailureReason.ThrowingException,
        InnerException: ex,
        VirtualExtensionOutput: null);
}
```

### Category-Level Failure Handling

- **FailFast**: First assessment failure stops category; return immediately
- **CollectAll**: All assessments execute; failures collected; return full result with failure list
- **Configured per category** via `Category.CategoryFailureHandling`

---

## JSON Serialization

- **NO built-in serialization** of results to JSON; consumers use standard `System.Text.Json` if needed
- **Options are immutable records** → easily serializable if consumers choose
- Design intentionally keeps serialization concerns to consumers

---

## Testing Strategy

- **XUnit3** (latest) with `AwesomeAssertions` (no FluentAssertions)
- **100% code coverage**: No unreachable code, all branches tested
- **Test categories**:
  - Unit tests for each built-in assessment type
  - Integration tests for category/assessment composition
  - Series vs. parallel execution scenarios
  - Virtual object extension chaining
  - Gate condition evaluation
  - Timeout & cancellation handling
  - Caching behavior
  - Error handling & failure propagation
  - Aggregation calculations (percentiles, weighted, etc.)

---

## Performance Characteristics

- **Single assessment**: < 5ms (regex, presence, length checks)
- **Category (5 assessments, parallel)**: < 25ms
- **Full assessment (7 categories, parallel)**: < 150ms for local checks; async I/O operations scale per network latency
- **Caching**: First-access cost + cache hit (< 1ms after)
- **Memory**: Assessments designed to be stateless & GC-friendly

---

## Dependencies

- `.NET 10.0`
- `System.Text.Json` (built-in)
- `PanoramicData.NCalcExtensions` (for potential future NCalc-based assessments)
- **Test-only**: XUnit3, AwesomeAssertions

---

## Future Extensibility (Out of Scope for v1.0)

- **LLM-backed assessments**: INCalcAssessment for formula-driven scoring
- **Machine learning model assessments**: Load trained models for quality prediction
- **Cross-ticket assessments**: Compare one ticket against others (anomaly detection)
- **Caching federation**: Multiple distributed cache backends
- **GraphQL support**: Query assessments via GraphQL endpoint

---

## Version & Stability

- **Target Release**: v1.0.0
- **Semver Strict**: Breaking changes only in major versions
- **Public API**: Frozen at v1.0.0; no breaking changes in v1.x
- **Pre-release**: Not anticipated; go straight to v1.0.0

---

**Document Status**: Awaiting implementation scaffold & feedback.

