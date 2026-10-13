namespace PanoramicData.IssueAssessment.Tests;

/// <summary>
/// Integration tests for the execution engine wiring through <see cref="IssueAssessor"/>.
/// </summary>
public class ExecutionEngineTests
{
	[Fact]
	public async Task AssessAsync_WithParallelCategories_ReturnsAllCategoryResults()
	{
		AssessmentOptions options = new()
		{
			ExecutionStrategy = CategoryExecutionStrategy.Parallel,
			Categories = ImmutableList.Create(
				CreateCategory("A", new PresenceAssessment { JsonPath = "$.title" }),
				CreateCategory("B", new PresenceAssessment { JsonPath = "$.description" })),
		};

		IssueAssessor assessor = new(options);
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"One\",\"description\":\"Two\"}");

		IAssessmentResult result = await assessor.AssessAsync(issue, TestContext.Current.CancellationToken);

		result.Categories.Should().HaveCount(2);
		result.Failures.Should().BeEmpty();
	}

	[Fact]
	public async Task AssessAsync_WithFailFast_StopsAfterFirstFailingCategory()
	{
		bool secondCategoryExecuted = false;

		AssessmentOptions options = new()
		{
			ExecutionStrategy = CategoryExecutionStrategy.Series,
			FailureHandling = FailureHandlingStrategy.FailFast,
			Categories = ImmutableList.Create(
				CreateCategory("Failing", new FailingDefinition()),
				CreateCategory(
					"Second",
					new CustomCallbackAssessment
					{
						Callback = (_, _, _) =>
						{
							secondCategoryExecuted = true;
							return Task.FromResult<(double?, string, Exception?)>((1d, "ok", null));
						},
					})),
		};

		IssueAssessor assessor = new(options);
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"One\"}");

		IAssessmentResult result = await assessor.AssessAsync(issue, TestContext.Current.CancellationToken);

		result.Categories.Should().HaveCount(1);
		result.Failures.Should().HaveCount(1);
		secondCategoryExecuted.Should().BeFalse();
	}

	[Fact]
	public async Task AssessAsync_WithCategoryTimeout_CollectsTimeoutFailure()
	{
		AssessmentCategory timedCategory = CreateCategory(
			"Timed",
			new SlowDefinition()) with
		{
			CategoryTimeout = TimeSpan.FromMilliseconds(20),
		};

		AssessmentOptions options = new()
		{
			ExecutionStrategy = CategoryExecutionStrategy.Series,
			FailureHandling = FailureHandlingStrategy.CollectAll,
			Categories = ImmutableList.Create(timedCategory),
		};

		IssueAssessor assessor = new(options);
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"One\"}");

		IAssessmentResult result = await assessor.AssessAsync(issue, TestContext.Current.CancellationToken);

		result.Failures.Should().HaveCount(1);
		result.Failures[0].Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
	}

	[Fact]
	public async Task AssessAsync_WhenCacheProviderReturnsResult_SkipsExecution()
	{
		IssueAssessmentResult cached = new()
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

		FakeCacheProvider cacheProvider = new(cached);
		AssessmentOptions options = new()
		{
			ExecutionStrategy = CategoryExecutionStrategy.Series,
			Categories = ImmutableList.Create(
				CreateCategory("WillNotRun", new ThrowingDefinition())),
		};

		IssueAssessor assessor = new(options, cacheProvider);
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"One\"}");

		IAssessmentResult result = await assessor.AssessAsync(issue, TestContext.Current.CancellationToken);

		ReferenceEquals(result, cached).Should().BeTrue();
	}

	private static AssessmentCategory CreateCategory(string name, IAssessmentDefinition definition)
	{
		return new AssessmentCategory
		{
			Name = name,
			Description = name,
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = $"{name}-Assessment",
					Description = $"{name}-Assessment",
					Definition = definition,
				}),
		};
	}

	private sealed class FailingDefinition : IAssessmentDefinition
	{
		public Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
		{
			return Task.FromResult(new AssessmentExecutionResult(
				RawScore: 0d,
				ExplanationText: "Failed",
				FailureReason: AssessmentFailureReason.ThrowingException,
				InnerException: null,
				VirtualExtensionOutput: null));
		}
	}

	private sealed class SlowDefinition : IAssessmentDefinition
	{
		public async Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
		{
			// Waits until cancelled, so only the timeout under test can end it; a fixed delay races the timeout under load.
			await Task.Delay(Timeout.Infinite, cancellationToken);
			return new AssessmentExecutionResult(1d, "ok", null, null, null);
		}
	}

	private sealed class ThrowingDefinition : IAssessmentDefinition
	{
		public Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Should not execute when cache hit occurs.");
		}
	}

	private sealed class FakeCacheProvider : IAssessmentCacheProvider
	{
		private readonly IAssessmentResult _cached;

		public FakeCacheProvider(IAssessmentResult cached)
		{
			_cached = cached;
		}

		public Task ClearAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task<IAssessmentResult?> GetAsync(JsonDocument issue, IAssessmentOptions options, CancellationToken cancellationToken)
		{
			return Task.FromResult<IAssessmentResult?>(_cached);
		}

		public Task<bool> SetAsync(JsonDocument issue, IAssessmentOptions options, IAssessmentResult result, CancellationToken cancellationToken)
		{
			return Task.FromResult(true);
		}
	}
}
