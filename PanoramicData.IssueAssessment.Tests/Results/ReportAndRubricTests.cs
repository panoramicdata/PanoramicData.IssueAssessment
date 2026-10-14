using PanoramicData.IssueAssessment.Serialization;

namespace PanoramicData.IssueAssessment.Tests.Results;

/// <summary>
/// Tests for <see cref="AssessmentReport"/>, <see cref="AssessmentRubric"/>, not-applicable categories and the default
/// members of <see cref="IAssessmentOptions"/> and <see cref="IAssessmentResult"/>.
/// </summary>
public class ReportAndRubricTests : TestBase
{
	private const string Rubric = """
		{
			"executionStrategy": "series",
			"conditions": { "bug": { "type": "fieldEquals", "path": "$.type", "values": [ "Bug" ] } },
			"categories": [
				{
					"name": "Bugs", "description": "d", "assessmentExecutionStrategy": "series",
					"assessments": [
						{ "name": "Steps", "description": "d", "appliesWhen": { "type": "ref", "name": "bug" },
							"definition": { "type": "tiered", "tiers": [ { "score": 0, "finding": "none" } ] } },
						{ "name": "Env", "description": "d", "appliesWhen": { "type": "ref", "name": "bug" }, "notApplicableExplanation": "Not a bug: {$.type}.",
							"definition": { "type": "tiered", "tiers": [ { "score": 0, "finding": "none" } ] } }
					]
				},
				{
					"name": "General", "description": "d", "assessmentExecutionStrategy": "parallel",
					"assessments": [
						{ "name": "Title", "description": "d", "recommendation": "Write a title.",
							"definition": { "type": "tiered", "tiers": [ { "when": { "type": "fieldPresent", "path": "$.title" }, "score": 100, "finding": "ok" }, { "score": 0, "finding": "No title." } ] } }
					]
				}
			],
			"scoreCaps": [ { "name": "No steps", "cap": 0.5, "when": { "type": "assessmentScore", "assessment": "Steps", "lessThan": 50 } } ],
			"gradeBands": [ { "name": "GOOD", "minimumScore": 0.5 }, { "name": "POOR", "minimumScore": 0 } ]
		}
		""";

	[Fact]
	public async Task Category_WhoseAssessmentsAllDoNotApply_IsNotApplicable()
	{
		IssueAssessor assessor = new(AssessmentRubric.Load(Rubric));
		using JsonDocument issue = JsonDocument.Parse("""{ "type": "Task", "title": "t" }""");

		IAssessmentResult result = await assessor.AssessAsync(issue, CancellationToken);
		AssessmentReport report = AssessmentReport.From(result);

		result.Categories[0].Outcome.Should().Be(AssessmentOutcome.NotApplicable);
		result.Categories[0].Reason.Should().Be("No assessment in category 'Bugs' applies.");
		report.Findings.Should().Equal("Not a bug: Task.", "ok");
		report.Caps.Should().BeEmpty("the capped assessment did not apply");
		report.Score.Should().Be(100);
		report.Grade.Should().Be("GOOD");
		report.ToJson().Should().Contain("\"outcome\": \"notApplicable\"");
	}

	[Fact]
	public async Task Cap_OnAnAssessmentInACategoryThatDoesNotApply_DoesNotApply()
	{
		AssessmentOptions options = AssessmentRubric.Load(Rubric);
		options = options with
		{
			Categories = [options.Categories[0] with { AppliesWhen = new ConditionReference { Name = "bug" } }, options.Categories[1]],
		};
		using JsonDocument issue = JsonDocument.Parse("""{ "type": "Task", "title": "t" }""");

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories[0].Assessments.Should().BeEmpty();
		result.Overall!.Caps.Should().BeEmpty();
	}

	[Fact]
	public async Task Report_RecommendsAndCaps()
	{
		IssueAssessor assessor = new(AssessmentRubric.Load(Rubric));
		using JsonDocument issue = JsonDocument.Parse("""{ "type": "Bug" }""");

		AssessmentReport report = AssessmentReport.From(await assessor.AssessAsync(issue, CancellationToken));

		report.Recommendations.Should().Equal("Write a title.");
		report.Caps.Select(cap => cap.Name).Should().Equal("No steps");
		report.Score.Should().Be(0);
	}

	[Fact]
	public void Report_FromAResultWithoutAnOverall_HasNoScoreAndListsFailures()
	{
		AssessmentResult failed = new("A", new AssessmentScore(null, null, 0, 1), "boom", AssessmentFailureReason.ThrowingException, new InvalidOperationException("inner"));
		PlainResult result = new(
			[new CategoryResult("C", new AssessmentScore(null, null, 0, 1), "r", [failed])],
			[new AssessmentFailure("C", "A", AssessmentFailureReason.ThrowingException, "boom", new InvalidOperationException("inner")), new AssessmentFailure("C", "B", AssessmentFailureReason.ThrowingException, "b", null)]);

		AssessmentReport report = AssessmentReport.From(result);

		((IAssessmentResult)result).Overall.Should().BeNull();
		report.Score.Should().BeNull();
		report.Grade.Should().BeNull();
		report.Flags.Should().BeEmpty();
		report.Classifications.Should().BeEmpty();
		report.Failures.Select(failure => failure.Error).Should().Equal("inner", null);
		report.Categories[0].Assessments[0].Error.Should().Be("inner");
	}

	[Fact]
	public void Overall_WithoutAScore_HasNoScore100()
	{
		new AssessmentOverall(null, null, null, [], ImmutableDictionary<string, bool>.Empty, ImmutableDictionary<string, string>.Empty)
			.Score100.Should().BeNull();
	}

	[Fact]
	public async Task Options_ImplementedWithoutRubricMembers_UseEmptyDefaults()
	{
		AssessmentOptions loaded = AssessmentRubric.Load(Rubric);
		IAssessmentOptions options = new PlainOptions([loaded.Categories[1]]);
		using JsonDocument issue = JsonDocument.Parse("""{ "title": "t" }""");

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		options.Conditions.Should().BeEmpty();
		options.ScoreCaps.Should().BeEmpty();
		options.GradeBands.Should().BeEmpty();
		options.Classifications.Should().BeEmpty();
		result.Overall!.Grade.Should().BeNull();
		result.Overall.Score100.Should().Be(100);
	}

	[Fact]
	public void Load_FromAStream_ReadsTheRubric()
	{
		using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(Rubric));

		AssessmentRubric.Load(stream).Categories.Should().HaveCount(2);
	}

	[Fact]
	public void Load_NullJson_Throws()
	{
		Action act = () => AssessmentRubric.Load("null");

		act.Should().Throw<JsonException>().WithMessage("The rubric JSON is null.");
	}

	[Fact]
	public void Load_NamedDefinitions_ReplacesOnlyRegisteredNames()
	{
		const string json = """
			{
				"executionStrategy": "series",
				"categories": [ { "name": "C", "description": "d", "assessmentExecutionStrategy": "series", "assessments": [
					{ "name": "A", "description": "d", "definition": { "type": "named", "name": "known" } },
					{ "name": "B", "description": "d", "definition": { "type": "named", "name": "unknown" } },
					{ "name": "C", "description": "d", "definition": { "type": "tiered", "tiers": [ { "score": 0, "finding": "x" } ] } }
				] } ]
			}
			""";
		TieredAssessment known = new() { Tiers = [new Tier { Score = 1, Finding = "k" }] };

		AssessmentOptions options = AssessmentRubric.Load(json, new Dictionary<string, IAssessmentDefinition> { ["known"] = known });

		options.Categories[0].Assessments.Select(assessment => assessment.Definition).Should().SatisfyRespectively(
			definition => definition.Should().BeSameAs(known),
			definition => definition.Should().BeOfType<NamedAssessmentDefinition>(),
			definition => definition.Should().BeOfType<TieredAssessment>());
	}

	[Fact]
	public void ToJson_RoundTrips()
	{
		AssessmentOptions options = AssessmentRubric.Load(Rubric);

		AssessmentRubric.ToJson(AssessmentRubric.Load(AssessmentRubric.ToJson(options))).Should().Be(AssessmentRubric.ToJson(options));
	}

	private sealed record PlainResult(ImmutableList<CategoryResult> Categories, ImmutableList<AssessmentFailure> Failures) : IAssessmentResult
	{
		public AssessmentAggregations Aggregations => null!;

		public JsonDocument? VirtualDocument => null;
	}

	private sealed record PlainOptions(ImmutableList<AssessmentCategory> Categories) : IAssessmentOptions
	{
		public CategoryExecutionStrategy ExecutionStrategy => CategoryExecutionStrategy.Series;

		public TimeSpan? GlobalTimeout => null;

		public FailureHandlingStrategy FailureHandling => FailureHandlingStrategy.CollectAll;

		public bool CaptureVirtualDocument => false;
	}
}
