namespace PanoramicData.IssueAssessment.Tests.Caching;

/// <summary>
/// Shared helpers for the caching tests.
/// </summary>
internal static class CachingTestHelpers
{
	public static AssessmentOptions Options(params IAssessmentDefinition[] definitions) => new()
	{
		Categories =
		[
			new AssessmentCategory
			{
				Name = "Category",
				Description = "Category",
				AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
				Assessments = [.. definitions.Select((definition, index) => new Assessment
				{
					Name = $"Assessment {index}",
					Description = "Assessment",
					Definition = definition,
				})],
			},
		],
		ExecutionStrategy = CategoryExecutionStrategy.Series,
	};

	public static AssessmentOptions Options() => Options(new PresenceAssessment { JsonPath = "$.title" });

	public static IAssessmentResult Result(string marker) => new IssueAssessmentResult
	{
		Categories = [new CategoryResult(marker, new AssessmentScore(1d, 1d, 0d, 1d), marker, [])],
		Failures = [],
		Aggregations = new AssessmentAggregations(
			new AggregationMetrics(0, null, null, null, null),
			ImmutableDictionary<string, AggregationMetrics>.Empty,
			new PercentileMetrics(null, null, null, null),
			new WeightedAggregationMetrics(null, 0d)),
	};

	/// <summary>
	/// A clock the test moves by hand.
	/// </summary>
	internal sealed class ManualTimeProvider : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _now;

		public void Advance(TimeSpan by) => _now += by;
	}
}
