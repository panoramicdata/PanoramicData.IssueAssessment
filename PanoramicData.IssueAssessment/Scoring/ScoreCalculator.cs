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
			RegexAssessment regex => ToRange(regex.SuccessScore, regex.FailureScore),
			StringLengthAssessment stringLength => ToRange(stringLength.SuccessScore, stringLength.FailureScore),
			WordCountAssessment wordCount => ToRange(wordCount.SuccessScore, wordCount.FailureScore),
			NumericRangeAssessment numericRange => ToRange(numericRange.SuccessScore, numericRange.FailureScore),
			DateRangeAssessment dateRange => ToRange(dateRange.SuccessScore, dateRange.FailureScore),
			EnumAssessment enumAssessment => ToRange(enumAssessment.SuccessScore, enumAssessment.FailureScore),
			PatternCountAssessment patternCount => ToRange(patternCount.SuccessScore, patternCount.FailureScore),
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

		if (minPossibleRaw == maxPossibleRaw)
		{
			return 1d;
		}

		double normalized = (rawScore - minPossibleRaw) / (maxPossibleRaw - minPossibleRaw);
		return Math.Clamp(normalized, 0d, 1d);
	}

	/// <summary>
	/// Calculates simple and exponential weighted averages from scored values.
	/// </summary>
	/// <param name="values">Input values, in logical execution order for exponential weighting.</param>
	/// <param name="exponentialDecay">
	/// Decay factor for exponential weighting, where 0 favors earlier entries and 1 keeps all entries equal.
	/// </param>
	/// <returns>Simple and exponential weighted averages plus summed absolute weight.</returns>
	public static (double? SimpleWeightedAverage, double? ExponentialWeightedAverage, double WeightSum) CalculateWeightedAverages(
		IEnumerable<(double? Score, double Weight)> values,
		double exponentialDecay = 0.5d)
	{
		ArgumentNullException.ThrowIfNull(values);

		if (exponentialDecay < 0d || exponentialDecay > 1d)
		{
			throw new ArgumentOutOfRangeException(nameof(exponentialDecay), "Exponential decay must be between 0 and 1.");
		}

		List<(double Score, double Weight)> normalized = values
			.Where(value => value.Score.HasValue)
			.Select(value => (value.Score!.Value, value.Weight))
			.Where(value => value.Weight != 0d)
			.ToList();

		if (normalized.Count == 0)
		{
			return (null, null, 0d);
		}

		double simpleWeightSum = normalized.Sum(value => value.Weight);
		double absoluteWeightSum = normalized.Sum(value => Math.Abs(value.Weight));

		double? simpleAverage = simpleWeightSum == 0d
			? null
			: normalized.Sum(value => value.Score * value.Weight) / simpleWeightSum;

		double exponentialNumerator = 0d;
		double exponentialDenominator = 0d;

		for (int index = 0; index < normalized.Count; index++)
		{
			int reverseIndex = normalized.Count - 1 - index;
			double decayFactor = Math.Pow(exponentialDecay, reverseIndex);
			double effectiveWeight = normalized[index].Weight * decayFactor;

			exponentialNumerator += normalized[index].Score * effectiveWeight;
			exponentialDenominator += effectiveWeight;
		}

		double? exponentialAverage = exponentialDenominator == 0d
			? null
			: exponentialNumerator / exponentialDenominator;

		return (simpleAverage, exponentialAverage, absoluteWeightSum);
	}

	private static (double Minimum, double Maximum) ToRange(double first, double second)
	{
		return (Math.Min(first, second), Math.Max(first, second));
	}
}
