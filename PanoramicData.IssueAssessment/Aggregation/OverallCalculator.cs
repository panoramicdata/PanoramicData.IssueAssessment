namespace PanoramicData.IssueAssessment.Aggregation;

/// <summary>
/// Computes the overall verdict on an issue from its category results and the rubric.
/// </summary>
internal static class OverallCalculator
{
	/// <summary>
	/// Scores are compared with grade bands after a division, so 80/100 can arrive as 0.79999999999999993; a band is
	/// reached within this tolerance.
	/// </summary>
	private const double Tolerance = 1e-9;

	public static AssessmentOverall Calculate(
		IAssessmentOptions options,
		EvaluationContext context,
		ImmutableList<CategoryResult> categories)
	{
		double? uncapped = WeightedAverage(categories);
		EvaluationContext scoring = context.WithAssessmentResults(ResultsByName(categories));
		ImmutableList<AppliedScoreCap> caps = [.. options.ScoreCaps
			.Where(cap => cap.When.Evaluate(scoring))
			.Select(cap => new AppliedScoreCap(cap.Name, cap.Group, cap.Cap))];

		double? score = uncapped is { } value ? caps.Aggregate(value, (current, cap) => Math.Min(current, cap.Cap)) : null;
		return new AssessmentOverall(score, uncapped, Grade(options, score), caps, context.AllConditions(), Classify(options, scoring));
	}

	/// <summary>
	/// The weighted average of the scored categories, leaving out those that do not apply; null when none is scored.
	/// </summary>
	private static double? WeightedAverage(ImmutableList<CategoryResult> categories)
	{
		List<CategoryResult> scored = [.. categories.Where(category =>
			category.Outcome != AssessmentOutcome.NotApplicable && category.Score.NormalizedScore.HasValue)];
		double weightSum = scored.Sum(category => category.Weight);
		return weightSum > 0d
			? scored.Sum(category => category.Weight * category.Score.NormalizedScore!.Value) / weightSum
			: null;
	}

	private static Dictionary<string, AssessmentResult> ResultsByName(ImmutableList<CategoryResult> categories)
	{
		Dictionary<string, AssessmentResult> results = new(StringComparer.Ordinal);
		foreach (AssessmentResult result in categories.SelectMany(category => category.Assessments))
		{
			results[result.AssessmentName] = result;
		}

		return results;
	}

	private static string? Grade(IAssessmentOptions options, double? score)
		=> score is { } graded
			? options.GradeBands
				.OrderByDescending(band => band.MinimumScore)
				.FirstOrDefault(band => graded >= band.MinimumScore - Tolerance)?.Name
			: null;

	private static ImmutableDictionary<string, string> Classify(IAssessmentOptions options, EvaluationContext scoring)
		=> options.Classifications
			.Select(classification => (classification.Key, Rule: classification.Value.Find(rule => rule.When is null || rule.When.Evaluate(scoring))))
			.Where(pair => pair.Rule is not null)
			.ToImmutableDictionary(pair => pair.Key, pair => pair.Rule!.Value, StringComparer.Ordinal);
}
