using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Gates;
using static PanoramicData.IssueAssessment.Tests.Integration.IntegrationTestHelpers;
using StubDefinition = PanoramicData.IssueAssessment.Tests.Integration.StubDefinition;

namespace PanoramicData.IssueAssessment.Tests.Gates;

/// <summary>
/// Tests for <see cref="ScoreThresholdGate"/> and <see cref="NoFailuresGate"/>.
/// </summary>
public class GateTests : TestBase
{
	private static CategoryResult Scored(string name, double? normalized)
		=> new(name, new AssessmentScore(normalized, normalized, 0d, 1d), "reason", []);

	private static AssessmentFailure FailureIn(string category)
		=> new(category, "A", AssessmentFailureReason.JsonPathNotFound, "missing", null);

	[Theory]
	[InlineData(0.6d, true)]
	[InlineData(0.5d, true)]
	[InlineData(0.49d, false)]
	[InlineData(null, false)]
	public async Task ScoreThresholdGate_ComparesNormalizedScoreInclusively(double? score, bool expected)
	{
		ScoreThresholdGate gate = new() { MinimumScore = 0.5d };

		(await gate.ShouldExecuteNextAsync(Scored("C", score), [], CancellationToken)).Should().Be(expected);
	}

	[Fact]
	public async Task NoFailuresGate_ContinuesOnlyWhenTheCategoryHasNoFailures()
	{
		NoFailuresGate gate = new();

		(await gate.ShouldExecuteNextAsync(Scored("C", 1d), [], CancellationToken)).Should().BeTrue();
		(await gate.ShouldExecuteNextAsync(Scored("C", 1d), [FailureIn("Earlier")], CancellationToken))
			.Should().BeTrue("failures in earlier categories are not this gate's concern");
		(await gate.ShouldExecuteNextAsync(Scored("C", 1d), [FailureIn("C")], CancellationToken)).Should().BeFalse();
	}

	[Fact]
	public async Task Gates_NullArgumentsOrCancelledToken_Throw()
	{
		using CancellationTokenSource cancelled = new();
		await cancelled.CancelAsync();
		ScoreThresholdGate threshold = new() { MinimumScore = 0d };
		NoFailuresGate noFailures = new();

		Func<Task> thresholdNull = () => threshold.ShouldExecuteNextAsync(null!, [], CancellationToken);
		Func<Task> thresholdCancelled = () => threshold.ShouldExecuteNextAsync(Scored("C", 1d), [], cancelled.Token);
		Func<Task> noFailuresNullResult = () => noFailures.ShouldExecuteNextAsync(null!, [], CancellationToken);
		Func<Task> noFailuresNullList = () => noFailures.ShouldExecuteNextAsync(Scored("C", 1d), null!, CancellationToken);
		Func<Task> noFailuresCancelled = () => noFailures.ShouldExecuteNextAsync(Scored("C", 1d), [], cancelled.Token);

		await thresholdNull.Should().ThrowAsync<ArgumentNullException>();
		await thresholdCancelled.Should().ThrowAsync<OperationCanceledException>();
		await noFailuresNullResult.Should().ThrowAsync<ArgumentNullException>();
		await noFailuresNullList.Should().ThrowAsync<ArgumentNullException>();
		await noFailuresCancelled.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ScoreThresholdGate_InASeries_SkipsLaterCategoriesForAPoorTitle()
	{
		StubDefinition expensive = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			TitleQualityCategory.Create() with { SeriesGateCondition = new ScoreThresholdGate { MinimumScore = 0.5d } },
			CreateSeriesCategory("Expensive", CreateAssessment("LLM review", expensive)));
		using JsonDocument issue = JsonDocument.Parse("""{"title":"x"}""");

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories.Should().ContainSingle();
		expensive.ExecutionCount.Should().Be(0);
	}
}
