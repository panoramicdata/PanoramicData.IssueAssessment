namespace PanoramicData.IssueAssessment.Tests;

using PanoramicData.IssueAssessment.Caching;

/// <summary>
/// Tests for in-memory assessment cache behaviors.
/// </summary>
public class CachingTests
{
	[Fact]
	public async Task InMemoryCache_SetThenGet_ReturnsStoredResult()
	{
		InMemoryAssessmentCache cache = new();
		AssessmentOptions options = CreateOptions();
		IssueAssessmentResult expected = CreateResult();
		using JsonDocument issue = JsonDocument.Parse("{\"id\":1,\"title\":\"Issue\"}");

		bool stored = await cache.SetAsync(issue, options, expected, TestContext.Current.CancellationToken);
		IAssessmentResult? cached = await cache.GetAsync(issue, options, TestContext.Current.CancellationToken);

		stored.Should().BeTrue();
		cached.Should().NotBeNull();
		ReferenceEquals(cached, expected).Should().BeTrue();
	}

	[Fact]
	public async Task InMemoryCache_WithTtl_ExpiresEntry()
	{
		InMemoryAssessmentCache cache = new(ttl: TimeSpan.FromMilliseconds(30));
		AssessmentOptions options = CreateOptions();
		IssueAssessmentResult expected = CreateResult();
		using JsonDocument issue = JsonDocument.Parse("{\"id\":2,\"title\":\"Issue\"}");

		_ = await cache.SetAsync(issue, options, expected, TestContext.Current.CancellationToken);
		await Task.Delay(60, TestContext.Current.CancellationToken);

		IAssessmentResult? cached = await cache.GetAsync(issue, options, TestContext.Current.CancellationToken);

		cached.Should().BeNull();
	}

	[Fact]
	public async Task InMemoryCache_WhenCapacityExceeded_EvictsLeastRecentlyUsed()
	{
		InMemoryAssessmentCache cache = new(maxEntries: 1);
		AssessmentOptions options = CreateOptions();

		using JsonDocument issue1 = JsonDocument.Parse("{\"id\":1,\"title\":\"One\"}");
		using JsonDocument issue2 = JsonDocument.Parse("{\"id\":2,\"title\":\"Two\"}");

		_ = await cache.SetAsync(issue1, options, CreateResult(), TestContext.Current.CancellationToken);
		_ = await cache.SetAsync(issue2, options, CreateResult(), TestContext.Current.CancellationToken);

		IAssessmentResult? first = await cache.GetAsync(issue1, options, TestContext.Current.CancellationToken);
		IAssessmentResult? second = await cache.GetAsync(issue2, options, TestContext.Current.CancellationToken);

		first.Should().BeNull();
		second.Should().NotBeNull();
	}

	private static AssessmentOptions CreateOptions()
	{
		return new AssessmentOptions
		{
			ExecutionStrategy = CategoryExecutionStrategy.Series,
			Categories = ImmutableList.Create(
				new AssessmentCategory
				{
					Name = "Category",
					Description = "Category",
					AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
					Assessments = ImmutableList.Create(
						new Assessment
						{
							Name = "Assessment",
							Description = "Assessment",
							Definition = new PresenceAssessment { JsonPath = "$.title" },
						}),
				}),
		};
	}

	private static IssueAssessmentResult CreateResult()
	{
		return new IssueAssessmentResult
		{
			Categories = ImmutableList<CategoryResult>.Empty,
			Failures = ImmutableList<AssessmentFailure>.Empty,
			Aggregations = new AssessmentAggregations(
				AllAssessments: new AggregationMetrics(0, null, null, null, null),
				ByCategory: ImmutableDictionary<string, AggregationMetrics>.Empty,
				Percentiles: new PercentileMetrics(null, null, null, null),
				Weighted: new WeightedAggregationMetrics(null, 0d)),
			VirtualDocument = null,
		};
	}
}
