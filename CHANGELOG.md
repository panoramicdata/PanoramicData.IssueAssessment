# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/). Versions are `major.minor.height`, where the height comes from
Nerdbank.GitVersioning.

## [1.1] - Unreleased

Adds rubrics: whole configurations written as JSON, with a built-in Panoramic Data ticket-quality rubric. Everything
is additive; 1.0 code compiles and behaves as before.

### Added

- `AssessmentRubric`: load options from rubric JSON (string or stream) and write them back; code-defined
  definitions plug in by name through `NamedAssessmentDefinition`.
- `TieredAssessment`: ordered tiers of condition, score, finding and recommendation over named measures.
- Conditions (`all`, `any`, `not`, `ref`, `measure`, `textMatches`, `fieldMatches`, `fieldEquals`, `fieldPresent`,
  `sectionPresent`, `placeholder`, `assessmentScore`) and measures (`patternMatchCount`, `occurrenceCount`,
  `wordCount`, `alphanumericCount`, `length`, `itemCount`, `number`, `criteriaHeadingCount`) over `TextSource`s
  that can narrow to a headed section.
- `AppliesWhen` and `NotApplicableExplanation` on assessments, `AppliesWhen` and `Weight` on categories; work that
  does not apply has the `NotApplicable` outcome and is left out of the score.
- Named conditions, score caps, grade bands and classifications on the options, and `IAssessmentResult.Overall`
  (`AssessmentOverall`: score, uncapped score, grade, applied caps, flags and classifications).
- `Recommendation` on assessments and results; `Outcome` on assessment and category results.
- `AssessmentReport`: a flat, serializable summary of a result, with `ToJson()`.
- `BuiltInRubrics.PanoramicDataTicketQuality`, matching Yoda's `IssueQualityCore.ps1` on its test fixtures and on
  320 real Jira tickets.
- `NormalizedIssue` with `JiraIssueMapper` (REST v2 and v3 / ADF), `GitHubIssueMapper` and
  `ServiceNowRecordMapper`.
- `IssueText`: section bodies in Jira wiki and Markdown, criteria headings, placeholder detection, word and
  alphanumeric counts.
- Validation of every rubric rule at construction, including undefined and cyclic condition references.

## [1.0.19] - 2026-10-13

First release.

### Added

- `IssueAssessor`: assesses a ticket (`AssessAsync`) or a stream of tickets (`AssessManyAsync`) from any system,
  given as a `JsonDocument`.
- Ten assessment types: presence, regex, string length, word count, numeric range, date range, enum, pattern count,
  JSONPath selector predicate and custom callback.
- Seven built-in categories: title quality, description quality, acceptance criteria, assignee ownership, due date
  timeline, reproducibility and AI suitability, with thresholds checked against real public GitHub issues.
- Series and parallel execution of categories and of the assessments within them; series gate conditions.
- Virtual extensions: a series assessment's output is stored under its `VirtualOutputName` and read by later
  built-in assessments as `$.<name>`.
- Weighted raw and normalized scores; aggregations (count, minimum, maximum, average, standard deviation,
  P50/P90/P95/P99 and a weighted category average).
- Per-assessment, per-category and global timeouts; `FailFast` and `CollectAll` failure handling at the options and
  category levels.
- Construction-time validation of the whole configuration, with messages naming the category and assessment.
- `InMemoryAssessmentCache`: thread-safe, bounded with least-recently-used eviction, optional time-to-live, and a
  pluggable `TimeProvider`.
- Examples for the Jira REST API, the GitHub REST API and the ServiceNow Table API, including mapping each to the
  normalized ticket shape.

### Security

- Regular expressions run with a 2-second match timeout (`RegexCache.MatchTimeout`), because they run against
  untrusted ticket text; a catastrophically backtracking pattern fails one assessment instead of hanging.
