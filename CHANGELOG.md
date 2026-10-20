# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/). Versions are `major.minor.height`, where the height comes from
Nerdbank.GitVersioning.

## [1.2] - Unreleased

Adds two Magic Suite ticket checks to the built-in rubric and a justified "not needed" option for documentation and
certifications. Everything is additive; 1.1 rubrics load and score as before, though a ticket's score can move where
the new checks apply.

### Added

- `NormalizedIssue.ReleaseNotes` (Jira `customfield_10600`, plain text or ADF) and `NormalizedIssue.Resolution`
  (`fields.resolution.name`), read from the `releaseNotes` and `resolution` JSON keys.
- Built-in ticket-quality rubric: MS-only `ReleaseNotes` assessment (and `EpicReleaseNotes` in Epic Quality), weight 6,
  mandatory cap 0.79. It checks that the notes are present, not a placeholder or generic, at least 8 words, different
  from the summary, customer-facing (no ticket keys other than standards ids, file paths or code identifiers), free of
  em dashes and in British spelling. It does not apply to other projects or to undelivered resolutions.
- Built-in ticket-quality rubric: MS-only `TicketContract` assessment (not for Epics), weight 6, mandatory cap 0.79.
  Acceptance criteria must each be classified MUST, SHOULD or MAY, with at least one MUST. A criterion is classified
  when it starts with a keyword, contains exactly one distinct keyword (the RFC 2119 sentence form), or sits under a
  label line such as `*MUST*` or `MUST:`. Only capitals count. From Ready for Test onwards
  the ticket needs an `h2. Verification Evidence` section (optionally marked "(Retrospective)") with a criterion table,
  a Reachability block, a Not covered block and a Suggested QA route of A to D.
- New condition type `fieldsDiffer` and new measure `criteriaClassification`, for rubrics that need them.

### Fixed

- TicketContract: a Verification Evidence table headed Criterion | Evidence (not only Criterion | Test) is evidence, not restated criteria, so it no longer triggers the duplication cap; a bold sub-heading inside the evidence section (such as *MUST criteria*) is not counted as a criteria heading; and an acceptance-criteria item struck through in full (-text-, optionally followed by a note or moved to KEY) is no longer counted as a criterion of the ticket.

### Changed

- Documentation and Certifications accept a justified `No documentation change: <reason>` or
  `No certification question: <reason>` line, for all projects.

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
- `sectionPanelCount` measure and `IssueText.PanelLayoutOf`: how the `h2.` sections of Jira wiki text sit in
  `{panel}` blocks. A section with a table or code block cannot reasonably be panelled and is not counted.
- `Presentation` (and `EpicPresentation`) factor in the built-in ticket-quality rubric, weight 8: each section should
  sit in an untitled panel directly under its heading, because a panel title only repeats the heading. It applies to
  Jira tickets with at least two sections that could be panelled and is left out of the score otherwise.
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
