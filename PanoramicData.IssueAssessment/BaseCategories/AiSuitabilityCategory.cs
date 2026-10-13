namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in AI suitability category.
/// </summary>
public static class AiSuitabilityCategory
{
	/// <summary>
	/// Creates an assessment category for AI suitability signals.
	/// </summary>
	/// <returns>Configured AI suitability category.</returns>
	public static AssessmentCategory Create()
	{
		return new AssessmentCategory
		{
			Name = "AI Suitability",
			Description = "Validates issue content sufficiency for AI-assisted analysis and planning.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "AI Title Presence",
					Description = "Ensures title exists for contextual grounding.",
					Weight = 1.0d,
					Definition = new PresenceAssessment
					{
						JsonPath = "$.title",
						PresentScore = 1.0d,
						AbsentScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "AI Description Word Count",
					Description = "Ensures description includes enough detail for model reasoning.",
					Weight = 1.0d,
					Definition = new WordCountAssessment
					{
						JsonPath = "$.description",
						MinimumWordCount = 15,
						MaximumWordCount = 1_500,
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}
