using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Configuration;
using PanoramicData.IssueAssessment.Core;

namespace PanoramicData.IssueAssessment.Examples;

internal static class BuiltInCategories
{
	/// <summary>
	/// Creates an assessor that runs all seven built-in categories in parallel.
	/// </summary>
	public static IssueAssessor CreateAssessor()
		=> new(new AssessmentOptions
		{
			Categories =
			[
				TitleQualityCategory.Create(),
				DescriptionQualityCategory.Create(),
				AcceptanceCriteriaCategory.Create(),
				AssigneeOwnershipCategory.Create(),
				DueDateTimelineCategory.Create(),
				ReproducibilityCategory.Create(),
				AiSuitabilityCategory.Create(),
			],
			ExecutionStrategy = CategoryExecutionStrategy.Parallel,
		});
}
