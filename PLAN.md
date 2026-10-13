# PanoramicData.IssueAssessment — Implementation Plan

**Status:** Phases 0–7 complete; Phase 8 (publishing) waits on repository creation and nuget.org trusted publishing.
**Updated:** 2026-10-13

The design is in [DESIGN.md](DESIGN.md). This file tracks what has been done and what is left.

## Milestones

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository, build, CI/CD, governance files | ✅ Local. GitHub repository not yet created (see Phase 8) |
| 1 | Core interfaces, configuration and result records, construction-time validation | ✅ |
| 2 | Ten assessment types, JSONPath helper, regex cache, scoring, seven base categories | ✅ |
| 3 | Execution engine: assessment, category, series and parallel executors | ✅ |
| 4 | Aggregation and virtual document finalizer | ✅ |
| 5 | Caching: in-memory cache and cache keys | ✅ |
| 6 | Test suite | ✅ 600 tests; 100% line coverage, 99.8% branch coverage |
| 7 | Documentation, examples, calibration, release notes | ✅ |
| 8 | GitHub repository, Codacy, NuGet publishing | ⏳ Blocked: needs the repository created and trusted publishing configured |
| 9 | Consuming repositories | ⏳ Optional; after publishing |

## Phase 0: Repository

- [x] Solution layout: library, tests, examples (`PanoramicData.IssueAssessment.slnx`, with Solution Items)
- [x] `Directory.Build.props` (warnings as errors, nullable, XML docs, `latest-recommended` analyzers, code style
      enforced), central package management, `global.json` with the Microsoft Testing Platform runner
- [x] Nerdbank.GitVersioning (`version.json`, version line 1.0) and `Publish.ps1`
- [x] `ci.yml`: build, test with coverage, Codacy upload, pack, and trusted-publishing release on version tags
- [x] `codeql.yml`, `dependabot.yml`
- [x] SECURITY.md, CONTRIBUTING.md, CLAUDE.md, AGENTS.md and copilot-instructions.md from the NugetManagement
      standards; `.codacy.yml`, `.gitattributes`, `coverage.config`
- [x] Original package icon
- [ ] Create the public GitHub repository and push
- [ ] Remove superseded files: `.github/workflows/test.yml`, `.github/workflows/publish.yml`,
      `.github/instructions/repository.instructions.md`, `.vscode/`, the empty `PanoramicData.IssueAssessment.Docs/`
      project (already removed from the solution)

## Phases 1–5: Library

- [x] Immutable `AssessmentOptions`, `AssessmentCategory`, `Assessment`; result records
- [x] `OptionsValidator`: every rule in DESIGN.md, including regex and JSONPath compilation, duplicate category and
      virtual output names
- [x] Ten assessment types; JSONPath subset; regex cache with a match timeout
- [x] Score ranges, normalization, negative weights, weighted and exponential averages
- [x] Seven base categories, calibrated against real issues
- [x] Series and parallel execution; gates (`ScoreThresholdGate`, `NoFailuresGate` built in); FailFast and
      CollectAll at options and category level; assessment, category and global timeouts; caller cancellation
- [x] Virtual extensions stored under `VirtualOutputName` and readable by built-in assessments
- [x] Aggregations: count, minimum, maximum, average, standard deviation, percentiles, weighted average
- [x] `InMemoryAssessmentCache` (LRU, TTL, `TimeProvider`); culture-invariant cache keys; per-instance keys for
      delegates, gates and custom definitions

## Phase 6: Tests

- [x] xUnit v3 with AwesomeAssertions on the Microsoft Testing Platform; `failSkips`
- [x] Tests per assessment type, JSONPath and regex utilities
- [x] Integration tests: category execution, series, parallel, timeouts, failure handling, virtual documents,
      assessor entry points
- [x] Base category tests, including good, mediocre and poor sample tickets and raw platform shapes
- [x] Validation tests for every rejection
- [x] Scoring, aggregation and caching tests
- [x] Coverage measured with `coverage.config`: 100% of lines. The one unmeasured branch is the compiler-generated
      enumerator null check of `await foreach` in `AssessManyAsync`; replacing the loop by hand adds more such branches.
- [x] Suite run repeatedly to rule out timing flakiness

## Phase 7: Documentation and polish

- [x] XML documentation on every public member (CS1591 no longer suppressed)
- [x] README rewritten against the real API; code samples compiled and run
- [x] DESIGN.md updated to the implemented design, with calibration results and deliberate differences
- [x] Examples: Jira, GitHub and ServiceNow, each with a mapper to the normalized ticket shape
- [x] CHANGELOG.md
- [x] Performance baseline: median 0.22 ms, p99 0.95 ms per ticket with all seven categories (75 real issues ×40)
- [ ] API documentation site (DocFX): optional, not started

## Phase 8: Publishing

Needs, in order:

1. [ ] Create the repository: `gh repo create panoramicdata/PanoramicData.IssueAssessment --public --source . --push`
2. [ ] Enable private vulnerability reporting:
       `gh api -X PUT repos/panoramicdata/PanoramicData.IssueAssessment/private-vulnerability-reporting`
3. [ ] Add the repository to Codacy and set the `CODACY_PROJECT_TOKEN` secret (codacy skill: `add-repo`, then
       `create-repo-token --set-github-secret`)
4. [ ] Check CI and CodeQL are green
5. [ ] On nuget.org, add a trusted publishing policy: GitHub Actions, owner `panoramicdata`, repository
       `PanoramicData.IssueAssessment`, workflow `ci.yml`, no environment
6. [ ] Run the NugetManagement governance assessment and fix what it reports
7. [ ] `./Publish.ps1` on a clean, synced `main`; confirm the version on nuget.org

## Phase 9: Consumers (optional)

- [ ] Decide whether a PanoramicData.Skills skill or a consuming dashboard should use the package
