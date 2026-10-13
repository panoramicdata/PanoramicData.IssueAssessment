# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/). Versions are `major.minor.height`, where the height comes from
Nerdbank.GitVersioning.

## [1.0] - Unreleased

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
