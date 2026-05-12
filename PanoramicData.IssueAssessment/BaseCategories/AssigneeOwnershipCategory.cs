namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in assignee ownership category.
/// </summary>
public static class AssigneeOwnershipCategory
{
	/// <summary>
	/// Creates an assessment category for assignee ownership quality.
	/// </summary>
	/// <returns>Configured assignee ownership category.</returns>
	public static AssessmentCategory Create()
	{
		return new AssessmentCategory
		{
			Name = "Assignee Ownership",
			Description = "Validates assignee presence and avoids unassigned ownership states.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "Assignee Presence",
					Description = "Ensures an assignee value exists.",
					Weight = 1.0d,
					Definition = new PresenceAssessment
					{
						JsonPath = "$.assignee",
						PresentScore = 1.0d,
						AbsentScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "Assignee State",
					Description = "Checks assignee state is in an accepted ownership state.",
					Weight = 1.0d,
					Definition = new EnumAssessment
					{
						JsonPath = "$.assigneeState",
						AllowedValues = ImmutableHashSet.Create("Assigned", "InProgress", "Owned"),
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}