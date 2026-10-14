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

Map your platform's JSON to that shape before assessing. Since 1.1 the package includes mappers to
`NormalizedIssue`, which also carries issue type, priority, status, project, components, labels, fix versions and
an attachment count:

```csharp
using PanoramicData.IssueAssessment.Mapping;

NormalizedIssue issue = JiraIssueMapper.FromRestJson(jiraIssueJson, new Uri("https://jira.example.com/"));
// or GitHubIssueMapper.FromRestJson(githubIssueJson), ServiceNowRecordMapper.FromTableJson(recordJson)
using JsonDocument ticket = issue.ToJsonDocument();
```

`JiraIssueMapper` reads REST API v2 (wiki text) and v3 (Atlassian Document Format, flattened to wiki text). If you
already hold typed objects, such as a Jira client's `Issue`, set the `NormalizedIssue` properties directly.

The [Examples project](PanoramicData.IssueAssessment.Examples) has complete, runnable mappers for the Jira REST
API v2, the GitHub REST API and the ServiceNow Table API, including two details that matter in practice:

- GitHub has no fields for reproduction steps or acceptance criteria, so they are read from the headed sections
  that issue templates produce.
- Template comments and collapsed `<details>` blocks are stripped first. Without that, an issue whose template was
  never filled in scores as a long, detailed description.

Your own assessments can of course read any JSONPath in the raw ticket directly.

```bash
dotnet run --project PanoramicData.IssueAssessment.Examples
```

## Rubrics

A rubric is a whole configuration written as JSON: categories, assessments, named conditions, score caps, grade
bands and classifications. It suits teams that share one definition of a good ticket between several tools.

### The built-in ticket-quality rubric

`BuiltInRubrics.PanoramicDataTicketQuality` is the rubric Panoramic Data uses for Jira and GitHub tickets. It
scores components, description, testability, steps to reproduce, current versus expected behaviour, environment,
attachments, documentation, certifications, UI consistency, business case and rollback plan for ordinary tickets,
and a separate set of criteria for epics. It grades the result EXCELLENT, GOOD, ADEQUATE, NEEDS IMPROVEMENT or
POOR, and caps a ticket below EXCELLENT when a mandatory element (steps to reproduce on a bug, a rollback plan on a
change request) is missing.

```csharp
using PanoramicData.IssueAssessment.Rubrics;

var assessor = new IssueAssessor(BuiltInRubrics.PanoramicDataTicketQuality);
AssessmentReport report = AssessmentReport.From(await assessor.AssessAsync(ticket, cancellationToken));

Console.WriteLine($"{report.Score}/100 {report.Grade}");
report.Findings.ForEach(Console.WriteLine);         // "✓ Steps to Reproduce: 3 numbered steps", ...
report.Recommendations.ForEach(Console.WriteLine);  // what to add to raise the score
Console.WriteLine(report.ToJson());
```

`BuiltInRubrics.PanoramicDataTicketQualityJson` returns its JSON, a good starting point for your own.

### Writing a rubric

```jsonc
{
	"executionStrategy": "series",
	"conditions": {
		"isBug": { "type": "fieldEquals", "path": "$.issueType", "values": [ "Bug", "Defect" ] }
	},
	"categories": [ {
		"name": "Ticket Quality", "description": "Is the ticket ready to work on?", "assessmentExecutionStrategy": "series",
		"assessments": [ {
			"name": "Steps to reproduce", "description": "Bugs say how to reproduce them",
			"appliesWhen": { "type": "ref", "name": "isBug" },
			"recommendation": "Add numbered steps to reproduce.",
			"definition": {
				"type": "tiered",
				"measures": {
					"steps": { "type": "occurrenceCount", "source": { "paths": [ "$.description" ], "section": "reproduc" }, "pattern": "(?m)^\\s*\\d+\\." }
				},
				"tiers": [
					{ "when": { "type": "measure", "measure": "steps", "atLeast": 3 }, "score": 100, "finding": "✓ {steps} steps to reproduce" },
					{ "when": { "type": "sectionPresent", "source": { "paths": [ "$.description" ], "section": "reproduc" } }, "score": 50, "finding": "⚠ Steps are not a numbered list" },
					{ "score": 0, "finding": "✗ No steps to reproduce" }
				]
			}
		} ]
	} ],
	"scoreCaps": [ { "name": "Steps to reproduce", "cap": 0.79, "group": "mandatory",
		"when": { "type": "assessmentScore", "assessment": "Steps to reproduce", "lessThan": 100 } } ],
	"gradeBands": [ { "name": "GOOD", "minimumScore": 0.6 }, { "name": "POOR", "minimumScore": 0 } ]
}
```

```csharp
AssessmentOptions options = AssessmentRubric.Load(File.ReadAllText("rubric.json"));
```

The building blocks:

- **Tiered assessments** (`"type": "tiered"`): tiers are tried in order and the first whose `when` holds gives the
  score (0–100 by default), the finding and an optional recommendation. The last tier has no condition.
- **Measures** count things: `patternMatchCount`, `occurrenceCount`, `wordCount`, `alphanumericCount`, `length`,
  `itemCount`, `number` and `criteriaHeadingCount`.
- **Conditions**: `all`, `any`, `not`, `ref` (a named condition), `measure`, `textMatches`, `fieldMatches`,
  `fieldEquals`, `fieldPresent`, `sectionPresent`, `placeholder` (a section that only says "TBD") and, for caps and
  classifications, `assessmentScore`.
- **Text sources** join one or more fields and can narrow to a section by heading, in Jira wiki or Markdown.
- **Placeholders** in findings and recommendations: `{measure}`, `{measure:text}`, `{$.path}` and
  `{$.path~pattern}` (the first value matching the pattern).
- **Applicability**: `appliesWhen` on an assessment or a category. An assessment that does not apply is reported as
  `NotApplicable` (with its `notApplicableExplanation`) and is left out of the score entirely.
- **Category weights**, **score caps** (the lowest applicable cap wins), **grade bands** and **classifications**
  (first matching rule gives the value). Named conditions are reported back as flags.

Every rule is validated when `IssueAssessor` is constructed, as below. Definitions written in code can sit in a
rubric as `{ "type": "named", "name": "..." }` and be supplied to `AssessmentRubric.Load(json, namedDefinitions)`.

The text tools the rubric uses are public too: `IssueText.SectionBody`, `CriteriaHeadings`, `IsPlaceholder`,
`WordCount` and `AlphanumericCount`.

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

- 815 unit tests; 100% line coverage of the library.
- The built-in rubric reproduces the scores, grades, findings and recommendations of the PowerShell engine it
  replaced on all 74 of that engine's test fixtures and on 320 real Jira tickets.
- Builds with zero warnings under `TreatWarningsAsErrors` and the `latest-recommended` analyzers.
- CodeQL and Codacy analysis on every push.

## Documentation

- [Design document](DESIGN.md): architecture and the reasoning behind it.
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md) and [security policy](SECURITY.md)

## License

MIT. See [LICENSE](LICENSE).
