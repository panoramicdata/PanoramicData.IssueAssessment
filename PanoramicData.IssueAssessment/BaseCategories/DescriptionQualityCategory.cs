namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in description quality category.
/// </summary>
public static class DescriptionQualityCategory
{
	/// <summary>
	/// Creates an assessment category for description quality.
	/// </summary>
	/// <returns>Configured description quality category.</returns>
	public static AssessmentCategory Create()
	{
		return new AssessmentCategory
		{
			Name = "Description Quality",
			Description = "Validates issue description completeness and useful detail.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "Description Presence",
					Description = "Ensures a description exists.",
					Weight = 1.0d,
					Definition = new PresenceAssessment
					{
						JsonPath = "$.description",
						PresentScore = 1.0d,
						AbsentScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "Description Length",
					Description = "Checks description length is substantial.",
					Weight = 1.0d,
					Definition = new StringLengthAssessment
					{
						JsonPath = "$.description",
						MinimumLength = 20,
						MaximumLength = 10_000,
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "Description Word Count",
					Description = "Checks description contains enough words.",
					Weight = 1.0d,
					Definition = new WordCountAssessment
					{
						JsonPath = "$.description",
						MinimumWordCount = 8,
						MaximumWordCount = 1_500,
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}