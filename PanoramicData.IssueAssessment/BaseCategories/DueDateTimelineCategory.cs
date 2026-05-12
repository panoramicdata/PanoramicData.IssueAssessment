namespace PanoramicData.IssueAssessment.BaseCategories;

using PanoramicData.IssueAssessment.Assessments;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

/// <summary>
/// Factory for the built-in due date timeline category.
/// </summary>
public static class DueDateTimelineCategory
{
	/// <summary>
	/// Creates an assessment category for due date reasonableness.
	/// </summary>
	/// <returns>Configured due date timeline category.</returns>
	public static AssessmentCategory Create()
	{
		DateTime utcToday = DateTime.UtcNow.Date;

		return new AssessmentCategory
		{
			Name = "Due Date Timeline",
			Description = "Validates due date presence and realistic date range.",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "Due Date Presence",
					Description = "Ensures a due date value exists.",
					Weight = 1.0d,
					Definition = new PresenceAssessment
					{
						JsonPath = "$.dueDate",
						PresentScore = 1.0d,
						AbsentScore = 0.0d,
					},
				},
				new Assessment
				{
					Name = "Due Date Window",
					Description = "Checks due date is not in the distant past or unrealistically far future.",
					Weight = 1.0d,
					Definition = new DateRangeAssessment
					{
						JsonPath = "$.dueDate",
						DateFormat = "O",
						MinDate = utcToday.AddDays(-1),
						MaxDate = utcToday.AddYears(2),
						SuccessScore = 1.0d,
						FailureScore = 0.0d,
					},
				}),
		};
	}
}