namespace PanoramicData.IssueAssessment.Aggregation;

using PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Calculates aggregate metrics for issue assessment results.
/// </summary>
public static class AggregationCalculator
{
	/// <summary>
	/// Calculates assessment aggregations from category results.
	/// </summary>
	/// <param name="categories">Category results.</param>
	/// <returns>Calculated aggregation metrics.</returns>
	public static AssessmentAggregations Calculate(ImmutableList<CategoryResult> categories)
	{
		ArgumentNullException.ThrowIfNull(categories);

		ImmutableList<double> allScores = categories
			.SelectMany(category => category.Assessments)
			.Select(result => result.Score.NormalizedScore)
			.Where(score => score.HasValue)
			.Select(score => score!.Value)
			.OrderBy(score => score)
			.ToImmutableList();

		AggregationMetrics all = BuildMetrics(allScores);

		ImmutableDictionary<string, AggregationMetrics> byCategory = categories
			.ToImmutableDictionary(
				category => category.CategoryName,
				category => BuildMetrics(
					category.Assessments
						.Select(result => result.Score.NormalizedScore)
						.Where(score => score.HasValue)
						.Select(score => score!.Value)
						.OrderBy(score => score)
						.ToImmutableList()));

		PercentileMetrics percentiles = new(
			P50: Percentile(allScores, 0.50d),
			P90: Percentile(allScores, 0.90d),
			P95: Percentile(allScores, 0.95d),
			P99: Percentile(allScores, 0.99d));

		(double? weightedAverage, _, double weightSum) = ScoreCalculator.CalculateWeightedAverages(
			categories
				.Select(category => (Score: category.Score.NormalizedScore, Weight: 1.0d))
				.ToList());

		WeightedAggregationMetrics weighted = new(weightedAverage, weightSum);

		return new AssessmentAggregations(all, byCategory, percentiles, weighted);
	}

	private static AggregationMetrics BuildMetrics(ImmutableList<double> values)
	{
		if (values.Count == 0)
		{
			return new AggregationMetrics(0, null, null, null, null);
		}

		double sum = values.Sum();
		double mean = sum / values.Count;
		double variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Count;

		return new AggregationMetrics(
			Count: values.Count,
			Minimum: values[0],
			Maximum: values[^1],
			Average: mean,
			StandardDeviation: Math.Sqrt(variance));
	}

	private static double? Percentile(ImmutableList<double> sortedValues, double quantile)
	{
		if (sortedValues.Count == 0)
		{
			return null;
		}

		double index = (sortedValues.Count - 1) * quantile;
		int lower = (int)Math.Floor(index);
		int upper = (int)Math.Ceiling(index);

		if (lower == upper)
		{
			return sortedValues[lower];
		}

		double fraction = index - lower;
		return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
	}
}
