using static PanoramicData.IssueAssessment.Tests.Integration.IntegrationTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Integration;

/// <summary>
/// Parallel category execution and FailFast semantics when work runs concurrently.
/// </summary>
public class ParallelExecutionTests : TestBase
{
	private static StubDefinition Failing() => new() { RawScore = 0d, FailureReason = AssessmentFailureReason.JsonPathNotFound };

	private static async Task<IAssessmentResult> AssessAsync(AssessmentOptions options)
	{
		using JsonDocument issue = ParseIssue();
		return await new IssueAssessor(options).AssessAsync(issue, CancellationToken);
	}

	[Fact]
	public async Task ParallelCategories_AllRunAndKeepConfiguredOrder()
	{
		StubDefinition[] definitions = [new(), new(), new()];
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			[.. definitions.Select((definition, index) => CreateSeriesCategory($"C{index}", CreateAssessment($"A{index}", definition)))]);

		IAssessmentResult result = await AssessAsync(options);

		result.Categories.Select(category => category.CategoryName).Should().Equal("C0", "C1", "C2");
		definitions.Should().OnlyContain(definition => definition.ExecutionCount == 1);
	}

	[Fact]
	public async Task ParallelCategories_GatesAreNotConsulted()
	{
		RecordingGate gate = new(shouldContinue: false);
		StubDefinition second = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			CreateSeriesCategory("First", CreateAssessment("A", new StubDefinition())) with { SeriesGateCondition = gate },
			CreateSeriesCategory("Second", CreateAssessment("B", second)));

		IAssessmentResult result = await AssessAsync(options);

		gate.Calls.Should().BeEmpty();
		second.ExecutionCount.Should().Be(1);
		result.Categories.Should().HaveCount(2);
	}

	[Fact]
	public async Task ParallelCategories_CollectAll_ReportsEveryFailure()
	{
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			CreateSeriesCategory("One", CreateAssessment("A", Failing())),
			CreateSeriesCategory("Two", CreateAssessment("B", Failing())));

		IAssessmentResult result = await AssessAsync(options);

		result.Failures.Select(failure => failure.AssessmentName).Should().Equal("A", "B");
	}

	[Fact]
	public async Task ParallelCategories_FailFast_ReportsOnlyTheFirstFailureButReturnsEveryCategory()
	{
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			CreateSeriesCategory("One", CreateAssessment("A", Failing())),
			CreateSeriesCategory("Two", CreateAssessment("B", Failing()))) with
		{ FailureHandling = FailureHandlingStrategy.FailFast };

		IAssessmentResult result = await AssessAsync(options);

		result.Failures.Should().ContainSingle().Which.AssessmentName.Should().Be("A");
		result.Categories.Should().HaveCount(2, "work already running in parallel cannot be stopped");
	}

	[Fact]
	public async Task ParallelAssessments_FailFast_ReportsOnlyTheFirstFailure()
	{
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateParallelCategory("One", CreateAssessment("A", Failing()), CreateAssessment("B", Failing()), CreateAssessment("C", new StubDefinition())) with
			{
				CategoryFailureHandling = FailureHandlingStrategy.FailFast,
			});

		IAssessmentResult result = await AssessAsync(options);

		result.Failures.Should().ContainSingle().Which.AssessmentName.Should().Be("A");
		result.Categories[0].Assessments.Should().HaveCount(3);
	}

	[Fact]
	public async Task ParallelAssessments_SingleFailureUnderFailFast_IsReported()
	{
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			CreateParallelCategory("One", CreateAssessment("A", Failing()), CreateAssessment("B", new StubDefinition()))) with
		{
			FailureHandling = FailureHandlingStrategy.FailFast,
		};

		IAssessmentResult result = await AssessAsync(options);

		result.Failures.Should().ContainSingle().Which.AssessmentName.Should().Be("A");
	}
}
