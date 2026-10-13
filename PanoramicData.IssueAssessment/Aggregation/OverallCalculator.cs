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
		List<CategoryResult> scored = [.. categories.Where(category =>
			category.Outcome != AssessmentOutcome.NotApplicable && category.Score.NormalizedScore.HasValue)];
		double weightSum = scored.Sum(category => category.Weight);
		double? uncapped = weightSum > 0d
			? scored.Sum(category => category.Weight * category.Score.NormalizedScore!.Value) / weightSum
			: null;

		Dictionary<string, AssessmentResult> results = new(StringComparer.Ordinal);
		foreach (AssessmentResult result in categories.SelectMany(category => category.Assessments))
		{
			results[result.AssessmentName] = result;
		}

		EvaluationContext scoring = context.WithAssessmentResults(results);
		ImmutableList<AppliedScoreCap> caps = [.. options.ScoreCaps
			.Where(cap => cap.When.Evaluate(scoring))
			.Select(cap => new AppliedScoreCap(cap.Name, cap.Group, cap.Cap))];

		double? score = uncapped is { } value ? caps.Aggregate(value, (current, cap) => Math.Min(current, cap.Cap)) : null;
		string? grade = score is { } graded
			? options.GradeBands
				.OrderByDescending(band => band.MinimumScore)
				.FirstOrDefault(band => graded >= band.MinimumScore - Tolerance)?.Name
			: null;

		ImmutableDictionary<string, string> classifications = options.Classifications
			.Select(classification => (classification.Key, Rule: classification.Value.Find(rule => rule.When is null || rule.When.Evaluate(scoring))))
			.Where(pair => pair.Rule is not null)
			.ToImmutableDictionary(pair => pair.Key, pair => pair.Rule!.Value, StringComparer.Ordinal);

		return new AssessmentOverall(score, uncapped, grade, caps, context.AllConditions(), classifications);
	}
}
