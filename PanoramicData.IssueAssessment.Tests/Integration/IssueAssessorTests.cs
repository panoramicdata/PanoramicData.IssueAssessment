using static PanoramicData.IssueAssessment.Tests.Integration.IntegrationTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Integration;

/// <summary>
/// Tests for the <see cref="IssueAssessor"/> entry points: <see cref="IssueAssessor.AssessAsync"/>,
/// <see cref="IssueAssessor.AssessManyAsync"/> and the cache provider contract.
/// </summary>
public class IssueAssessorTests : TestBase
{
	private static AssessmentOptions Options(IAssessmentDefinition? definition = null)
		=> CreateOptions(CategoryExecutionStrategy.Series, CreateSeriesCategory("C", CreateAssessment("A", definition ?? new StubDefinition())));

	[Fact]
	public async Task AssessAsync_NullIssue_Throws()
	{
		Func<Task> act = () => new IssueAssessor(Options()).AssessAsync(null!, CancellationToken);

		await act.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task AssessAsync_ComputesAggregationsFromCategoryResults()
	{
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(Options(new StubDefinition { RawScore = 0.25d })).AssessAsync(issue, CancellationToken);

		result.Aggregations.AllAssessments.Count.Should().Be(1);
		result.Aggregations.AllAssessments.Average.Should().Be(0.25d);
		result.Aggregations.ByCategory.Should().ContainKey("C");
		result.Aggregations.Weighted.WeightedAverage.Should().Be(0.25d);
	}

	[Fact]
	public async Task AssessAsync_CacheHit_ReturnsCachedResultWithoutExecuting()
	{
		StubDefinition definition = new();
		IssueAssessmentResult cached = CreateEmptyResult();
		RecordingCacheProvider cache = new(cached);
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(Options(definition), cache).AssessAsync(issue, CancellationToken);

		result.Should().BeSameAs(cached);
		definition.ExecutionCount.Should().Be(0);
	}

	[Fact]
	public async Task AssessAsync_CacheMiss_ExecutesAndStoresTheResult()
	{
		RecordingCacheProvider cache = new();
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(Options(), cache).AssessAsync(issue, CancellationToken);

		cache.SetCount.Should().Be(1);
		cache.LastStored.Should().BeSameAs(result);
	}

	[Fact]
	public async Task AssessManyAsync_AssessesEachIssueInOrder()
	{
		IssueAssessor assessor = new(Options(new DelegateDefinition((input, _, _) =>
			Task.FromResult(new AssessmentExecutionResult(input.RootElement.GetProperty("n").GetDouble(), null, null, null, null)))));

		List<double?> scores = [];
		await foreach (IAssessmentResult result in assessor.AssessManyAsync(Issues(3), CancellationToken))
		{
			scores.Add(result.Categories[0].Score.RawScore);
		}

		scores.Should().Equal(0d, 1d, 2d);
	}

	[Fact]
	public async Task AssessManyAsync_NullSequence_Throws()
	{
		Func<Task> act = async () =>
		{
			await foreach (IAssessmentResult _ in new IssueAssessor(Options()).AssessManyAsync(null!, CancellationToken))
			{
			}
		};

		await act.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task AssessManyAsync_Cancelled_StopsEnumerating()
	{
		using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		IssueAssessor assessor = new(Options());
		int seen = 0;

		Func<Task> act = async () =>
		{
			await foreach (IAssessmentResult _ in assessor.AssessManyAsync(Issues(10), cancellation.Token))
			{
				seen++;
				await cancellation.CancelAsync();
			}
		};

		await act.Should().ThrowAsync<OperationCanceledException>();
		seen.Should().Be(1);
	}

	private static async IAsyncEnumerable<JsonDocument> Issues(int count)
	{
		for (int index = 0; index < count; index++)
		{
			await Task.Yield();
			yield return JsonDocument.Parse($$"""{"n":{{index}}}""");
		}
	}
}
