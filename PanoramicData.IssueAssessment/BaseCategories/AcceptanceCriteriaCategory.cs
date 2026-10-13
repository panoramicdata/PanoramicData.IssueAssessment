namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in acceptance criteria category.
/// </summary>
public static class AcceptanceCriteriaCategory
{
	/// <summary>
	/// Creates an assessment category for acceptance criteria quality.
	/// </summary>
	/// <returns>Configured acceptance criteria category.</returns>
	public static AssessmentCategory Create()
	{
		return new AssessmentCategory
		{
			Name = "Acceptance Criteria",
			Description = "Validates acceptance criteria presence and actionable structure.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "Acceptance Criteria Presence",
					Description = "Ensures acceptance criteria text exists.",
					Weight = 1.0d,
					Definition = new PresenceAssessment
					{
						JsonPath = "$.acceptanceCriteria",
						PresentScore = 1.0d,
						AbsentScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "Acceptance Criteria Length",
					Description = "Checks acceptance criteria is sufficiently detailed.",
					Weight = 1.0d,
					Definition = new StringLengthAssessment
					{
						JsonPath = "$.acceptanceCriteria",
						MinimumLength = 50,
						MaximumLength = 8_000,
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}
