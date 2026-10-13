namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in title quality category.
/// </summary>
public static class TitleQualityCategory
{
	/// <summary>
	/// Creates an assessment category for title quality.
	/// </summary>
	/// <returns>Configured title quality category.</returns>
	public static AssessmentCategory Create()
	{
		return new AssessmentCategory
		{
			Name = "Title Quality",
			Description = "Validates that the issue title is present, meaningful, and properly formatted.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "Title Presence",
					Description = "Ensures a title exists.",
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
					Name = "Title Length",
					Description = "Checks title length is within expected range.",
					Weight = 1.0d,
					Definition = new StringLengthAssessment
					{
						JsonPath = "$.title",
						MinimumLength = 5,
						MaximumLength = 150,
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}
