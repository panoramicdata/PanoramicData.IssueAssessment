# PanoramicData.IssueAssessment — Design

**Status:** Implemented (1.0)
**License:** MIT

This document explains how the library is built and why. The [README](README.md) explains how to use it.

---

## Purpose

Ticket quality is hard to measure consistently: every platform (Jira, GitHub, ServiceNow, in-house systems) has its
own schema, and "good" means different things to different teams. The library separates the two problems:

- **Schema:** every ticket is a `System.Text.Json.JsonDocument`. Assessments select values with JSONPath, so the
  library never depends on a platform's types.
- **Judgement:** quality is a set of small, explainable, weighted checks grouped into categories, which teams compose
  and tune instead of accepting one opaque number.

## Principles

- **Explainable:** every score comes with a sentence saying why.
- **Immutable configuration:** options, categories and assessments are records with `init` properties; one
  `IssueAssessor` can be shared across threads.
- **Fail at construction:** invalid configuration throws when `IssueAssessor` is constructed, never part way through
  a batch.
- **Async and cancellable:** every operation takes a `CancellationToken`; long-running custom checks can be bounded
  by timeouts.
- **Untrusted input:** ticket text comes from users. Regular expressions have a match timeout; nothing in a ticket is
  executed or trusted.
- **No dependencies** beyond .NET 10.

---

## Flow

```
JsonDocument (ticket) ──► IssueAssessor.AssessAsync
                              │  cache lookup (optional)
                              ▼
                    categories, in series or parallel
                              │  (series: gate after each category)
                              ▼
                    each category: assessments in series or parallel
                              │  (series: virtual extension accumulates)
                              ▼
                    AssessmentExecutor: timeout, exception and failure mapping
                              │
                              ▼
          scores ─► category scores ─► aggregations ─► IAssessmentResult
```

| Type | Responsibility |
|---|---|
| `IssueAssessor` | Entry point; validates options at construction; cache; global timeout; caller cancellation |
| `SeriesExecutor`, `ParallelExecutor` | Run categories; gates; options-level failure handling |
| `CategoryExecutor` | Runs a category's assessments; category timeout; virtual extension chaining; category score |
| `AssessmentExecutor` | Runs one definition; assessment timeout; turns exceptions and timeouts into failures |
| `ScoreCalculator` | Raw score ranges, normalization, weighted averages |
| `AggregationCalculator` | Result-wide statistics |
| `OptionsValidator` | Construction-time validation |
| `JsonPathHelper`, `RegexCache` | Value selection; compiled, time-limited regular expressions |

The executors are internal; the extension points are public interfaces (below).

---

## Configuration

```csharp
public sealed record AssessmentOptions : IAssessmentOptions
{
	public required ImmutableList<AssessmentCategory> Categories { get; init; }
	public required CategoryExecutionStrategy ExecutionStrategy { get; init; }   // Parallel | Series
	public TimeSpan? GlobalTimeout { get; init; }
	public FailureHandlingStrategy FailureHandling { get; init; }               // CollectAll (default) | FailFast
	public bool CaptureVirtualDocument { get; init; }
}

public sealed record AssessmentCategory
{
	public required string Name { get; init; }
	public required string Description { get; init; }
	public required ImmutableList<Assessment> Assessments { get; init; }
	public required AssessmentExecutionStrategy AssessmentExecutionStrategy { get; init; }
	public TimeSpan? CategoryTimeout { get; init; }
	public FailureHandlingStrategy? CategoryFailureHandling { get; init; }      // null: inherit the options' setting
	public ISeriesGateCondition? SeriesGateCondition { get; init; }
}

public sealed record Assessment
{
	public required string Name { get; init; }
	public required string Description { get; init; }
	public required IAssessmentDefinition Definition { get; init; }
	public TimeSpan? AssessmentTimeout { get; init; }
	public double Weight { get; init; } = 1.0;                                   // [-100, 100]
	public bool AllowVirtualExtension { get; init; }
	public string? VirtualOutputName { get; init; }
}
```

---

## Assessment definitions

Every definition implements:

```csharp
public interface IAssessmentDefinition
{
	Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken);
}

public sealed record AssessmentExecutionResult(
	double? RawScore,
	string? ExplanationText,
	AssessmentFailureReason? FailureReason,
	Exception? InnerException,
	JsonDocument? VirtualExtensionOutput);
```

The ten built-in definitions are records. Each JSONPath-based one has `JsonPath`, a success and a failure score
(defaulting to 1 and 0) and an optional `ExplanationTemplate` that replaces every explanation it would produce.

| Definition | Settings | Notes |
|---|---|---|
| `PresenceAssessment` | `PresentScore`, `AbsentScore` | Null, blank strings and missing values are absent; absence is a score, not a failure |
| `RegexAssessment` | `Pattern` | Strings only |
| `StringLengthAssessment` | `MinimumLength`, `MaximumLength` | Inclusive |
| `WordCountAssessment` | `MinimumWordCount`, `MaximumWordCount` | Words are runs of non-whitespace |
| `NumericRangeAssessment` | `MinValue?`, `MaxValue?` | JSON numbers and invariant numeric strings; NaN, infinities and overflow are not numbers |
| `DateRangeAssessment` | `MinDate?`, `MaxDate?`, `DateFormat` | Exact format, or flexible invariant parsing when `DateFormat` is blank; values with an offset compare as UTC |
| `EnumAssessment` | `AllowedValues` | Uses the set's comparer, so case sensitivity is the caller's choice |
| `PatternCountAssessment` | `Pattern`, `MinMatches?`, `MaxMatches?` | Inclusive |
| `JsonPathSelectorAssessment` | `Evaluator: Func<JsonElement, CancellationToken, Task<bool>>` | An evaluator exception is a failure |
| `CustomCallbackAssessment` | `Callback: Func<JsonDocument, JsonDocument?, CancellationToken, Task<(double? Score, string Explanation, Exception? Exception)>>` | Raw scores taken to lie in [0, 1] |

When a selected value is missing, or is the wrong JSON type, a definition returns its failure score **and** a
`JsonPathNotFound` failure reason. The score keeps the ticket's grade honest; the failure tells the caller which
fields were absent.

### JSONPath subset

`$`, `$.a.b` and `$.a[0].b`: property names and non-negative integer indexes. Paths are validated at construction.
Filters, wildcards and recursive descent are deliberately out of scope; a custom definition can use any query
library it likes.

---

## Virtual extensions

A series of assessments can build on each other without modifying the ticket. When an assessment with
`AllowVirtualExtension` returns `VirtualExtensionOutput`, the executor stores it under the assessment's
`VirtualOutputName`, giving `{ "name": output }`, and merges it into the category's virtual document. That document
is passed to every later assessment in the category and, in a series of categories, to later categories.

Built-in definitions look a path up in the ticket first and in the virtual document second, so a later
`WordCountAssessment { JsonPath = "$.summary" }` can score a value an earlier assessment derived. The ticket always
wins: an extension adds values but cannot change the ticket's own.

Rules, all enforced at construction:

- virtual extensions require series execution of a category's assessments;
- an extending assessment needs a `VirtualOutputName`, and names are unique across the options, so outputs never
  collide.

Categories executed in parallel do not share virtual documents.

---

## Gates

```csharp
public interface ISeriesGateCondition
{
	Task<bool> ShouldExecuteNextAsync(
		CategoryResult priorCategoryResult,
		ImmutableList<AssessmentFailure> priorFailures,
		CancellationToken cancellationToken);
}
```

A gate is attached to a category and asked, after that category runs in a series, whether the remaining categories
should run. `ScoreThresholdGate { MinimumScore }` and `NoFailuresGate` are built in. A gate that throws is recorded
as a `ThrowingException` failure and stops the series; gates are not consulted when categories run in parallel.

---

## Scoring

For an assessment, the possible raw range is its two scores, ordered; custom definitions are taken to score in
[0, 1]. For a category:

```
RawScore    = Σ (assessment raw score × weight)
MinPossible = Σ (weight ≥ 0 ? weight × min : weight × max)
MaxPossible = Σ (weight ≥ 0 ? weight × max : weight × min)
Normalized  = clamp((RawScore − MinPossible) / (MaxPossible − MinPossible), 0, 1)    // 1 when the range collapses
```

Negative weights therefore invert a check's contribution without breaking normalization. A category is unscored
(null) if any of its assessments produced no raw score (a timeout or exception).

### Aggregations

```csharp
public sealed record AssessmentAggregations(
	AggregationMetrics AllAssessments,                              // over normalized assessment scores
	ImmutableDictionary<string, AggregationMetrics> ByCategory,
	PercentileMetrics Percentiles,                                  // P50, P90, P95, P99 (linear interpolation)
	WeightedAggregationMetrics Weighted);                           // average of category scores

public sealed record AggregationMetrics(int Count, double? Minimum, double? Maximum, double? Average, double? StandardDeviation);
```

`ScoreCalculator.CalculateWeightedAverages` also offers an exponentially weighted average for callers that want to
favour later entries.

---

## Failures, timeouts and cancellation

| Situation | Outcome |
|---|---|
| Value missing or of the wrong type | Failure score, `JsonPathNotFound` failure |
| Definition throws | No score, `ThrowingException` failure with the exception |
| Assessment, category or global timeout | No score for the assessments it cut short, `TimeoutExceeded` failures; partial result |
| Gate throws | `ThrowingException` failure; series stops |
| Caller cancels the token | `OperationCanceledException` from `AssessAsync` |
| Regex match exceeds `RegexCache.MatchTimeout` (2 s) | `ThrowingException` failure with `RegexMatchTimeoutException` |

`CollectAll` records failures and continues. `FailFast` stops a series category at its first failure, and a series
of categories after the first category with a failure. In parallel, work that has started cannot be stopped, so
`FailFast` reports only the first failure. A category's `CategoryFailureHandling` overrides the options' setting
for that category's assessments.

A callback or evaluator that is cancelled by the token it was given lets the `OperationCanceledException`
propagate, so it is reported as a timeout or cancellation rather than as an error in the callback.

---

## Validation

`OptionsValidator` rejects: no categories; blank names or descriptions; empty categories; non-positive timeouts;
weights outside [-100, 100]; invalid JSONPath or regex; inverted or negative ranges; empty enum sets; missing
evaluators or callbacks; virtual extensions in parallel categories or without a `VirtualOutputName`; and duplicate
category or virtual output names. Messages name the category and assessment.

---

## Caching

```csharp
public interface IAssessmentCacheProvider
{
	Task<IAssessmentResult?> GetAsync(JsonDocument issue, IAssessmentOptions options, CancellationToken cancellationToken);
	Task<bool> SetAsync(JsonDocument issue, IAssessmentOptions options, IAssessmentResult result, CancellationToken cancellationToken);
	Task ClearAsync(CancellationToken cancellationToken);
}
```

`CacheKeyGenerator` combines a SHA-256 of the ticket's JSON with a SHA-256 of a description of the options:

- built-in definitions are described by value, formatted with the invariant culture, so the key does not change with
  the machine's culture;
- delegates (callbacks, evaluators), gates and custom definition types cannot be described by value, so each instance
  gets a per-process token: different callbacks never share an entry.

`InMemoryAssessmentCache` is a thread-safe dictionary with a maximum entry count (least recently used evicted
first), an optional time-to-live, and a `TimeProvider` for testing. Cached results are shared instances; treat them
as read-only.

---

## Built-in categories

| Category | Assessments (each weight 1) |
|---|---|
| Title Quality | presence of `$.title`; 5–150 characters; 3–30 words |
| Description Quality | presence of `$.description`; 20–10,000 characters; 8–1,500 words |
| Acceptance Criteria | presence of `$.acceptanceCriteria`; 50–8,000 characters |
| Assignee Ownership | presence of `$.assignee`; `$.assigneeState` in Assigned, InProgress, Owned |
| Due Date Timeline | presence of `$.dueDate`; between yesterday and two years from now (flexible date parsing) |
| Reproducibility | presence of `$.reproductionSteps`; at least one numbered or bulleted list item |
| AI Suitability | presence of `$.title`; 15–1,500 words of description |

### Calibration

The thresholds were run against 75 recent public issues from dotnet/runtime, microsoft/vscode and
PowerShell/PowerShell. That run changed two things:

- **Title word count:** with length alone, one-word placeholder titles ("Problems", "Recept") scored as well as
  good ones. A 3–30 word check now separates them.
- **Due date parsing:** the due-date window used the exact round-trip format, so real due dates (`2026-11-20`,
  `2026-12-01T08:00:00Z`) never parsed. It now parses flexibly.

After the changes, detailed bug reports score 0.9–1.0 and placeholder tickets score below 0.4. Issue-template
boilerplate still inflates description length, which is why the GitHub example strips template comments and
`<details>` blocks before assessing.

### Differences from the original design

The pre-implementation design proposed richer categories: description keyword checks ("reproduce", "error"),
BDD-style acceptance criteria detection, environment context in reproduction steps, and ambiguity detection for AI
suitability. They were left out of 1.0 on purpose. Keyword checks penalise legitimate feature requests and
non-English tickets, and none of them could be calibrated against real data in time. The building blocks to add
them (`RegexAssessment`, `PatternCountAssessment`) are in place; each `Create()` returns a record that can be
extended with `with`.

The original design also placed `PanoramicData.NCalcExtensions` in the dependencies "for future use". It was never
used and was removed: it pulled in a vulnerable NCalc version.

---

## Out of scope for 1.0

- LLM-backed assessments (a `CustomCallbackAssessment` can call one today).
- Cross-ticket assessments, such as duplicate or anomaly detection.
- Serialization of results; results are records that `System.Text.Json` can serialize if needed.
- A full JSONPath implementation.

---

## Versioning

Semantic versioning from 1.0. Versions come from Nerdbank.GitVersioning (`version.json` sets `major.minor`; the
patch is the git height), and releases are published by tagging with `Publish.ps1`.
