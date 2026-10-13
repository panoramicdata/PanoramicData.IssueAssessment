namespace PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Provides score range and normalization utilities for assessment execution and aggregation.
/// </summary>
public static class ScoreCalculator
{
	private const double DefaultUnknownMinimum = 0d;
	private const double DefaultUnknownMaximum = 1d;

	/// <summary>
	/// Calculates the minimum and maximum raw score bounds for an assessment configuration.
	/// </summary>
	/// <param name="assessment">Assessment configuration.</param>
	/// <returns>The raw score bounds for the assessment definition.</returns>
	public static (double MinPossibleRaw, double MaxPossibleRaw) CalculateAssessmentRawRange(Assessment assessment)
	{
		ArgumentNullException.ThrowIfNull(assessment);

		return assessment.Definition switch
		{
			PresenceAssessment presence => ToRange(presence.PresentScore, presence.AbsentScore),
			ValueAssessment value => ToRange(value.SuccessScore, value.FailureScore),
			JsonPathSelectorAssessment selector => ToRange(selector.SuccessScore, selector.FailureScore),
			// Callbacks and custom definitions declare no bounds, so their raw scores are taken to lie in [0, 1].
			_ => (DefaultUnknownMinimum, DefaultUnknownMaximum),
		};
	}

	/// <summary>
	/// Calculates the weighted minimum and maximum raw score bounds for an assessment category.
	/// </summary>
	/// <param name="category">Category configuration.</param>
	/// <returns>The weighted raw score bounds.</returns>
	public static (double MinPossibleRaw, double MaxPossibleRaw) CalculateCategoryRawRange(AssessmentCategory category)
	{
		ArgumentNullException.ThrowIfNull(category);

		double min = 0d;
		double max = 0d;

		foreach (Assessment assessment in category.Assessments)
		{
			(double assessmentMin, double assessmentMax) = CalculateAssessmentRawRange(assessment);
			double weight = assessment.Weight;

			if (weight >= 0d)
			{
				min += weight * assessmentMin;
				max += weight * assessmentMax;
			}
			else
			{
				min += weight * assessmentMax;
				max += weight * assessmentMin;
			}
		}

		return (min, max);
	}

	/// <summary>
	/// Normalizes a raw score using known minimum and maximum bounds.
	/// </summary>
	/// <param name="rawScore">Raw score to normalize.</param>
	/// <param name="minPossibleRaw">Minimum raw bound.</param>
	/// <param name="maxPossibleRaw">Maximum raw bound.</param>
	/// <returns>Normalized score in range [0, 1], or 1.0 when bounds collapse.</returns>
	public static double Normalize(double rawScore, double minPossibleRaw, double maxPossibleRaw)
	{
		if (minPossibleRaw > maxPossibleRaw)
		{
			throw new ArgumentException("Minimum raw score cannot be greater than maximum raw score.");
		}

		// The range is never negative here, so a negligible width means the bounds have collapsed.
		if (IsNegligible(maxPossibleRaw - minPossibleRaw))
		{
			return 1d;
		}

		double normalized = (rawScore - minPossibleRaw) / (maxPossibleRaw - minPossibleRaw);
		return Math.Clamp(normalized, 0d, 1d);
	}

	/// <summary>
	/// Calculates simple and exponential weighted averages from scored values, with an exponential decay of 0.5.
	/// </summary>
	/// <param name="values">Input values, oldest first.</param>
	/// <returns>Simple and exponential weighted averages plus summed absolute weight.</returns>
	public static (double? SimpleWeightedAverage, double? ExponentialWeightedAverage, double WeightSum) CalculateWeightedAverages(
		IEnumerable<(double? Score, double Weight)> values)
		=> CalculateWeightedAverages(values, 0.5d);

	/// <summary>
	/// Calculates simple and exponential weighted averages from scored values.
	/// </summary>
	/// <param name="values">Input values, oldest first.</param>
	/// <param name="exponentialDecay">
	/// Decay factor for exponential weighting, in [0, 1]: each step back from the latest entry multiplies its weight by
	/// this factor, so 0 uses only the latest entry and 1 weights every entry equally.
	/// </param>
	/// <returns>
	/// Simple and exponential weighted averages (null when there is nothing to average, or when the weights cancel out)
	/// plus the summed absolute weight.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="exponentialDecay"/> is outside [0, 1].</exception>
	public static (double? SimpleWeightedAverage, double? ExponentialWeightedAverage, double WeightSum) CalculateWeightedAverages(
		IEnumerable<(double? Score, double Weight)> values,
		double exponentialDecay)
	{
		ArgumentNullException.ThrowIfNull(values);

		if (exponentialDecay is < 0d or > 1d)
		{
			throw new ArgumentOutOfRangeException(nameof(exponentialDecay), "Exponential decay must be between 0 and 1.");
		}

		List<(double Score, double Weight)> scored = [.. values
			.Where(value => value.Score.HasValue && !IsNegligible(value.Weight))
			.Select(value => (value.Score!.Value, value.Weight))];

		if (scored.Count == 0)
		{
			return (null, null, 0d);
		}

		double absoluteWeightSum = scored.Sum(value => Math.Abs(value.Weight));
		double? simpleAverage = WeightedAverage(scored.Select(value => (value.Score, value.Weight)));
		double? exponentialAverage = WeightedAverage(scored.Select((value, index) =>
			(value.Score, value.Weight * Math.Pow(exponentialDecay, scored.Count - 1 - index))));

		return (simpleAverage, exponentialAverage, absoluteWeightSum);
	}

	/// <summary>
	/// Σ(score × weight) / Σ(weight), or null when the weights sum to (effectively) zero.
	/// </summary>
	private static double? WeightedAverage(IEnumerable<(double Score, double Weight)> values)
	{
		double numerator = 0d;
		double denominator = 0d;
		foreach ((double score, double weight) in values)
		{
			numerator += score * weight;
			denominator += weight;
		}

		return IsNegligible(denominator) ? null : numerator / denominator;
	}

	/// <summary>
	/// True for values indistinguishable from zero after floating-point rounding, such as weights that cancel out.
	/// </summary>
	private static bool IsNegligible(double value) => Math.Abs(value) < 1e-12d;

	private static (double Minimum, double Maximum) ToRange(double first, double second)
	{
		return (Math.Min(first, second), Math.Max(first, second));
	}
}
