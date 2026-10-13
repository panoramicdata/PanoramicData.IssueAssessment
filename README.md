# PanoramicData.IssueAssessment

[![NuGet version](https://img.shields.io/nuget/v/PanoramicData.IssueAssessment.svg)](https://www.nuget.org/packages/PanoramicData.IssueAssessment/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/panoramicdata/PanoramicData.IssueAssessment/actions/workflows/ci.yml/badge.svg)](https://github.com/panoramicdata/PanoramicData.IssueAssessment/actions/workflows/ci.yml)
[![Codacy Badge](https://app.codacy.com/project/badge/grade/PanoramicData.IssueAssessment)](https://app.codacy.com/gh/panoramicdata/PanoramicData.IssueAssessment/dashboard)

Score the quality of issues and tickets from any system — Jira, GitHub, ServiceNow or your own — with composable,
weighted, explainable assessments.

You give it a ticket as a `JsonDocument` and a set of assessment categories; it returns a score per assessment,
per category and overall, each with a plain-English explanation of why. Seven ready-made categories cover the
usual questions (is there a clear title, a real description, acceptance criteria, an owner, a due date, steps to
reproduce?), and ten building blocks let you write your own.

## Installation

```bash
dotnet add package PanoramicData.IssueAssessment
```

Targets .NET 10. No dependencies beyond the framework.

## Quick start

```csharp
using System.Text.Json;
using PanoramicData.IssueAssessment;
using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

var assessor = new IssueAssessor(new AssessmentOptions
{
	Categories =
	[
		TitleQualityCategory.Create(),
		DescriptionQualityCategory.Create(),
		AssigneeOwnershipCategory.Create(),
		DueDateTimelineCategory.Create(),
	],
	ExecutionStrategy = CategoryExecutionStrategy.Parallel,
});

using var ticket = JsonDocument.Parse("""
	{
		"title": "Export to CSV drops rows with line breaks",
		"description": "When a description contains a line break, the CSV export silently skips that ticket.",
		"assignee": "alex",
		"assigneeState": "Assigned",
		"dueDate": "2026-11-20"
	}
	""");

// cancellationToken: your request's token, or CancellationToken.None.
IAssessmentResult result = await assessor.AssessAsync(ticket, cancellationToken);

Console.WriteLine($"Overall: {result.Aggregations.Weighted.WeightedAverage:P0}");
foreach (var category in result.Categories)
{
	Console.WriteLine($"{category.CategoryName}: {category.Score.NormalizedScore:P0}");
	foreach (var assessment in category.Assessments)
	{
		Console.WriteLine($"  {assessment.AssessmentName}: {assessment.ExplanationText}");
	}
}
```

## Assessing tickets from Jira, GitHub or ServiceNow

The built-in categories read a small normalized shape:

| Property | Meaning |
|---|---|
| `title` | One-line summary |
| `description` | Full description |
| `acceptanceCriteria` | What "done" means |
| `reproductionSteps` | Steps to reproduce, ideally as a numbered or bulleted list |
| `assignee` | Who owns it |
| `assigneeState` | `Assigned`, `InProgress` or `Owned` when someone has taken it |
| `dueDate` | Any date or date-time string (`2026-11-20`, `2026-11-20T17:00:00Z`) |

Map your platform's JSON to that shape before assessing. The
[Examples project](PanoramicData.IssueAssessment.Examples) has complete, runnable mappers for the Jira REST API v2,
the GitHub REST API and the ServiceNow Table API, including two details that matter in practice:

- GitHub has no fields for reproduction steps or acceptance criteria, so they are read from the headed sections
  that issue templates produce.
- Template comments and collapsed `<details>` blocks are stripped first. Without that, an issue whose template was
  never filled in scores as a long, detailed description.

Your own assessments can of course read any JSONPath in the raw ticket directly.

```bash
dotnet run --project PanoramicData.IssueAssessment.Examples
```

## Concepts

### Assessments

An assessment is one check with a score. Ten types are built in:

| Type | Scores |
|---|---|
| `PresenceAssessment` | Whether a value exists and is not null or blank |
| `RegexAssessment` | Whether a string matches a pattern |
| `StringLengthAssessment` | Whether a string's length is in an inclusive range |
| `WordCountAssessment` | Whether a string's word count is in an inclusive range |
| `NumericRangeAssessment` | Whether a number (or numeric string) is in range |
| `DateRangeAssessment` | Whether a date is in range; exact format or flexible parsing |
| `EnumAssessment` | Whether a string is one of an allowed set |
| `PatternCountAssessment` | Whether a pattern occurs between a minimum and maximum number of times |
| `JsonPathSelectorAssessment` | Your async predicate over the selected value |
| `CustomCallbackAssessment` | Your async callback over the whole ticket |

Values are selected with a JSONPath subset: `$`, `$.a.b` and `$.a[0].b`. Each assessment has a `Weight`
(-100 to 100), an optional timeout, and an explanation, which you can replace with `ExplanationTemplate`.

### Categories

A category groups assessments and runs them in series or in parallel. Categories themselves also run in series or
in parallel (`AssessmentOptions.ExecutionStrategy`). In series, a category can carry an `ISeriesGateCondition`
that decides, from its result, whether the remaining categories run at all — for example, skip expensive checks
when the title is missing.

### Built-in categories

| Category | Checks |
|---|---|
| `TitleQualityCategory` | Title present, 5–150 characters, 3–30 words |
| `DescriptionQualityCategory` | Description present, 20–10,000 characters, 8–1,500 words |
| `AcceptanceCriteriaCategory` | Acceptance criteria present and at least 50 characters |
| `AssigneeOwnershipCategory` | Assignee present and in an ownership state |
| `DueDateTimelineCategory` | Due date present and between yesterday and two years from now |
| `ReproducibilityCategory` | Reproduction steps present and written as a list |
| `AiSuitabilityCategory` | Title present and at least 15 words of description |

The thresholds were checked against 75 recent public issues from dotnet/runtime, microsoft/vscode and
PowerShell/PowerShell: detailed bug reports score 0.9–1.0, and placeholder tickets ("Problems", "Doesn't work")
score below 0.4. Each `Create()` returns an ordinary immutable record, so you can adjust any of it with `with`.

### Scores

Every score carries the raw value, its possible range, and a normalized value in [0, 1]:

```csharp
AssessmentScore score = assessment.Score; // RawScore, NormalizedScore, MinPossibleRaw, MaxPossibleRaw
```

A category's raw score is the weighted sum of its assessments, normalized against the weighted range; negative
weights invert an assessment's contribution. `result.Aggregations` adds:

- `AllAssessments` and `ByCategory[name]`: count, minimum, maximum, average and standard deviation of normalized
  assessment scores;
- `Percentiles`: P50, P90, P95 and P99;
- `Weighted.WeightedAverage`: the average of the category scores.

### Failures

`result.Failures` lists what could not be assessed normally: a value not found (`JsonPathNotFound`), a timeout, or
an exception thrown by your code. A missing value still scores (it gets the failure score), so a ticket without a
description simply scores low; filter `Failures` by `Reason` if you only want execution errors.

`FailureHandling` is `CollectAll` (the default: record and continue) or `FailFast` (stop the series at the first
failure). A category can override it with `CategoryFailureHandling`.

### Timeouts and cancellation

Timeouts can be set per assessment, per category and for the whole run (`GlobalTimeout`). A timeout is recorded as
a `TimeoutExceeded` failure and the result is partial, so a slow custom check cannot block an assessment
indefinitely. Cancelling the token you pass to `AssessAsync` throws `OperationCanceledException`, as usual.

Regular expressions run against ticket text, which is untrusted input, so every match has a 2-second timeout
(`RegexCache.MatchTimeout`). A pattern that backtracks catastrophically fails that one assessment instead of hanging.

### Chaining assessments with virtual extensions

In a series, an assessment can derive a value that later assessments score. Set `AllowVirtualExtension` and a
unique `VirtualOutputName`; whatever the definition returns as `VirtualExtensionOutput` is stored under that name,
and every later built-in assessment can read it as `$.<name>` (the ticket's own values always win).

```csharp
public sealed class FirstSentence : IAssessmentDefinition
{
	public Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		string description = input.RootElement.GetProperty("description").GetString() ?? "";
		string sentence = description.Split('.')[0];
		return Task.FromResult(new AssessmentExecutionResult(
			RawScore: 1,
			ExplanationText: "Extracted the first sentence.",
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: JsonDocument.Parse(JsonSerializer.Serialize(sentence))));
	}
}

AssessmentCategory summary = new()
{
	Name = "Summary",
	Description = "Is the opening sentence a useful summary?",
	AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
	Assessments =
	[
		new Assessment
		{
			Name = "Extract",
			Description = "First sentence of the description",
			Definition = new FirstSentence(),
			AllowVirtualExtension = true,
			VirtualOutputName = "summary",
		},
		new Assessment
		{
			Name = "Summary length",
			Description = "The opening sentence has 5 to 40 words",
			Definition = new WordCountAssessment { JsonPath = "$.summary", MinimumWordCount = 5, MaximumWordCount = 40 },
		},
	],
};
```

Set `CaptureVirtualDocument = true` to get the final virtual document back in `result.VirtualDocument`. Virtual
extensions need series execution; configuring one in a parallel category is rejected at construction.

### Validation

Configuration is validated when `IssueAssessor` is constructed — invalid JSONPath or regex, inverted ranges,
out-of-range weights, non-positive timeouts, duplicate category or output names — so a bad configuration fails at
start-up with a message naming the category and assessment, never halfway through a batch.

### Batches and caching

`AssessManyAsync` assesses an `IAsyncEnumerable<JsonDocument>` and streams the results.

Pass an `IAssessmentCacheProvider` to the constructor to reuse results. `InMemoryAssessmentCache` is thread-safe,
bounded (least recently used entries are evicted first) and supports an optional time-to-live. Keys combine a
SHA-256 of the ticket with a description of the configuration that does not depend on the current culture; callbacks,
evaluators, gates and custom definitions are keyed by instance, so two different callbacks never share an entry.

```csharp
var assessor = new IssueAssessor(options, new InMemoryAssessmentCache(new InMemoryAssessmentCacheOptions
{
	MaxEntries = 10_000,
	TimeToLive = TimeSpan.FromHours(1),
}));
```

## Extending

- **A new kind of check:** implement `IAssessmentDefinition` (as in the virtual extension example), or use
  `CustomCallbackAssessment` for a one-off.
- **Conditional execution:** implement `ISeriesGateCondition`.
- **A shared cache** (Redis, a database): implement `IAssessmentCacheProvider`.

## Performance

Measured over 3,000 assessments of 75 real GitHub issues with all seven built-in categories (Release build, one
thread): median 0.22 ms, 95th percentile 0.49 ms, 99th percentile 0.95 ms per ticket.

## Quality

- 544 unit tests; 100% line coverage of the library (the one unmeasured branch is the compiler-generated null check
  in `await foreach`).
- Builds with zero warnings under `TreatWarningsAsErrors` and the `latest-recommended` analyzers.
- CodeQL and Codacy analysis on every push.

## Documentation

- [Design document](DESIGN.md): architecture and the reasoning behind it.
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md) and [security policy](SECURITY.md)

## License

MIT. See [LICENSE](LICENSE).
