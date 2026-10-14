namespace PanoramicData.IssueAssessment.Tests.Integration;

using PanoramicData.IssueAssessment.Execution;
using static PanoramicData.IssueAssessment.Tests.Integration.IntegrationTestHelpers;

/// <summary>
/// Tests for executing a single category: assessment ordering, scoring and argument validation.
/// </summary>
public class CategoryExecutionTests : TestBase
{
	[Fact]
	public async Task ExecuteAsync_NullCategory_ThrowsArgumentNullException()
	{
		using JsonDocument issue = ParseIssue();

		Func<Task> act = () => CategoryExecutor.ExecuteAsync(null!, issue, null, FailureHandlingStrategy.CollectAll, CancellationToken);

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("category");
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_ThrowsArgumentNullException()
	{
		AssessmentCategory category = CreateSeriesCategory("Category", CreateAssessment("Assessment", new StubDefinition()));

		Func<Task> act = () => CategoryExecutor.ExecuteAsync(category, (JsonDocument)null!, null, FailureHandlingStrategy.CollectAll, CancellationToken);

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}

	[Theory]
	[InlineData(AssessmentExecutionStrategy.Series)]
	[InlineData(AssessmentExecutionStrategy.Parallel)]
	public async Task ExecuteAsync_AllAssessmentsScored_ReturnsWeightedCategoryScore(AssessmentExecutionStrategy strategy)
	{
		AssessmentCategory category = CreateCategory(
			"Category",
			strategy,
			CreateAssessment("A", new StubDefinition { RawScore = 1d }, weight: 3d),
			CreateAssessment("B", new StubDefinition { RawScore = 0d }, weight: 1d));
		(CategoryResult result, ImmutableList<AssessmentFailure> failures, JsonDocument? virtualDocument) = await ExecuteCategoryAsync(category);

		failures.Should().BeEmpty();
		virtualDocument.Should().BeNull();
		result.CategoryName.Should().Be("Category");
		result.Assessments.Select(assessment => assessment.AssessmentName).Should().Equal("A", "B");
		result.Score.RawScore.Should().Be(3d);
		result.Score.MinPossibleRaw.Should().Be(0d);
		result.Score.MaxPossibleRaw.Should().Be(4d);
		result.Score.NormalizedScore.Should().Be(0.75d);
		result.Reason.Should().Be("Executed 2 assessment(s) in category 'Category'.");
	}

	[Fact]
	public async Task ExecuteAsync_AssessmentWithNullRawScore_LeavesCategoryUnscored()
	{
		AssessmentCategory category = CreateSeriesCategory(
			"Category",
			CreateAssessment("Scored", new StubDefinition { RawScore = 1d }),
			CreateAssessment("Unscored", new StubDefinition { RawScore = null }));
		(CategoryResult result, ImmutableList<AssessmentFailure> failures, _) = await ExecuteCategoryAsync(category);

		failures.Should().BeEmpty("a null raw score without a failure reason is not a failure");
		result.Assessments[1].Score.RawScore.Should().BeNull();
		result.Assessments[1].Score.NormalizedScore.Should().BeNull();
		result.Score.RawScore.Should().BeNull();
		result.Score.NormalizedScore.Should().BeNull();
		result.Score.MaxPossibleRaw.Should().Be(2d, "the bounds still describe the configured assessments");
	}

	[Fact]
	public async Task ExecuteAsync_NegativeWeight_InvertsContributionAndBounds()
	{
		AssessmentCategory category = CreateSeriesCategory(
			"Category",
			CreateAssessment("Positive", new StubDefinition { RawScore = 1d }, weight: 2d),
			CreateAssessment("Negative", new StubDefinition { RawScore = 1d }, weight: -1d));
		(CategoryResult result, _, _) = await ExecuteCategoryAsync(category);

		result.Score.RawScore.Should().Be(1d);
		result.Score.MinPossibleRaw.Should().Be(-1d);
		result.Score.MaxPossibleRaw.Should().Be(2d);
		result.Score.NormalizedScore.Should().BeApproximately(2d / 3d, 1e-12);
	}

	[Fact]
	public async Task ExecuteAsync_AllWeightsZero_CollapsedBoundsNormalizeToOne()
	{
		AssessmentCategory category = CreateSeriesCategory(
			"Category",
			CreateAssessment("Zero", new StubDefinition { RawScore = 0d }, weight: 0d));
		(CategoryResult result, _, _) = await ExecuteCategoryAsync(category);

		result.Score.RawScore.Should().Be(0d);
		result.Score.MinPossibleRaw.Should().Be(0d);
		result.Score.MaxPossibleRaw.Should().Be(0d);
		result.Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task ExecuteAsync_CategoryWithoutAssessments_ReturnsUnscoredResult()
	{
		// The options validator rejects an empty category; the executor still copes with one.
		AssessmentCategory category = CreateSeriesCategory("Empty");
		(CategoryResult result, ImmutableList<AssessmentFailure> failures, _) = await ExecuteCategoryAsync(category);

		failures.Should().BeEmpty();
		result.Assessments.Should().BeEmpty();
		result.Score.RawScore.Should().BeNull();
		result.Reason.Should().Be("Executed 0 assessment(s) in category 'Empty'.");
	}

	[Fact]
	public async Task ExecuteAsync_SeriesStrategy_RunsAssessmentsInOrder()
	{
		List<string> order = [];
		AssessmentCategory category = CreateSeriesCategory(
			"Category",
			CreateAssessment("First", Recording(order, "First")),
			CreateAssessment("Second", Recording(order, "Second")),
			CreateAssessment("Third", Recording(order, "Third")));
		using JsonDocument issue = ParseIssue();

		_ = await CategoryExecutor.ExecuteAsync(category, issue, null, FailureHandlingStrategy.CollectAll, CancellationToken);

		order.Should().Equal("start First", "end First", "start Second", "end Second", "start Third", "end Third");
	}

	[Fact]
	public async Task ExecuteAsync_ParallelStrategy_StartsAllAssessmentsBeforeAnyCompletes()
	{
		TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		int started = 0;
		DelegateDefinition definition = new(async (_, _, token) =>
		{
			if (Interlocked.Increment(ref started) == 2)
			{
				release.SetResult();
			}

			await release.Task.WaitAsync(SafetyNet, token);
			return new AssessmentExecutionResult(1d, "Released.", null, null, null);
		});
		AssessmentCategory category = CreateParallelCategory(
			"Category",
			CreateAssessment("A", definition),
			CreateAssessment("B", definition));
		(CategoryResult result, ImmutableList<AssessmentFailure> failures, _) = await ExecuteCategoryAsync(category);

		failures.Should().BeEmpty("a series run would have waited for the first assessment forever");
		result.Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task ExecuteAsync_ExplanationIsNull_UsesEmptyExplanation()
	{
		AssessmentCategory category = CreateSeriesCategory(
			"Category",
			CreateAssessment("Silent", new StubDefinition { Explanation = null }));
		(CategoryResult result, _, _) = await ExecuteCategoryAsync(category);

		result.Assessments[0].ExplanationText.Should().BeEmpty();
	}

	[Fact]
	public async Task ExecuteAsync_BuiltInDefinition_NormalizesAgainstDefinitionScores()
	{
		AssessmentCategory category = CreateSeriesCategory(
			"Category",
			CreateAssessment("Presence", new PresenceAssessment { JsonPath = "$.title", PresentScore = 10d, AbsentScore = 2d }));
		(CategoryResult result, _, _) = await ExecuteCategoryAsync(category);

		AssessmentScore score = result.Assessments[0].Score;
		score.RawScore.Should().Be(10d);
		score.MinPossibleRaw.Should().Be(2d);
		score.MaxPossibleRaw.Should().Be(10d);
		score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task ExecuteAsync_NullArguments_AssessmentExecutorThrows()
	{
		Assessment assessment = CreateAssessment("Assessment", new StubDefinition());
		using JsonDocument issue = ParseIssue();

		Func<Task> nullCategoryName = () => AssessmentExecutor.ExecuteAsync(null!, assessment, issue, null, CancellationToken);
		Func<Task> nullAssessment = () => AssessmentExecutor.ExecuteAsync("Category", null!, issue, null, CancellationToken);

		(await nullCategoryName.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("categoryName");
		(await nullAssessment.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("assessment");
	}

	private static DelegateDefinition Recording(List<string> order, string name)
		=> new(async (_, _, token) =>
		{
			lock (order)
			{
				order.Add($"start {name}");
			}

			// Pause so that an accidental parallel run would interleave the start and end entries.
			await Task.Delay(TimeSpan.FromMilliseconds(5), token);
			lock (order)
			{
				order.Add($"end {name}");
			}

			return new AssessmentExecutionResult(1d, name, null, null, null);
		});
	/// <summary>
	/// Runs <paramref name="category"/> with CollectAll against a ticket with only a title.
	/// </summary>
	private static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteCategoryAsync(
		AssessmentCategory category)
	{
		using JsonDocument issue = ParseIssue();
		return await CategoryExecutor.ExecuteAsync(category, issue, null, FailureHandlingStrategy.CollectAll, TestContext.Current.CancellationToken);
	}
}
