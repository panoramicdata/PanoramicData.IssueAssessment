namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in reproducibility category.
/// </summary>
public static class ReproducibilityCategory
{
	/// <summary>
	/// Creates an assessment category for issue reproducibility quality.
	/// </summary>
	/// <returns>Configured reproducibility category.</returns>
	public static AssessmentCategory Create()
	{
		return new AssessmentCategory
		{
			Name = "Reproducibility",
			Description = "Validates that repro steps are present and structured.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "Repro Steps Presence",
					Description = "Ensures reproduction steps text exists.",
					Weight = 1.0d,
					Definition = new PresenceAssessment
					{
						JsonPath = "$.reproductionSteps",
						PresentScore = 1.0d,
						AbsentScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "Repro Steps Structure",
					Description = "Checks repro steps include at least one ordered or bullet step marker.",
					Weight = 1.0d,
					Definition = new PatternCountAssessment
					{
						JsonPath = "$.reproductionSteps",
						Pattern = "(?im)^\\s*(?:\\d+\\.|[-*])\\s+",
						MinMatches = 1,
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}