namespace PanoramicData.IssueAssessment.Tests.Validation;

/// <summary>
/// Builders for the validation tests: one valid assessment, category and options, and an action that constructs an
/// <see cref="IssueAssessor"/> from them.
/// </summary>
internal static class ValidationTestHelpers
{
	public static readonly IAssessmentDefinition Valid = new PresenceAssessment { JsonPath = "$.title" };

	public static Assessment Assessment(IAssessmentDefinition? definition = null)
		=> new() { Name = "Check", Description = "Check", Definition = definition ?? Valid };

	public static AssessmentCategory Category(params Assessment[] assessments) => new()
	{
		Name = "Quality",
		Description = "Quality",
		AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
		Assessments = assessments.Length == 0 ? [Assessment()] : [.. assessments],
	};

	public static AssessmentOptions Options(params AssessmentCategory[] categories) => new()
	{
		Categories = categories.Length == 0 ? [Category()] : [.. categories],
		ExecutionStrategy = CategoryExecutionStrategy.Series,
	};

	public static Action Construct(AssessmentOptions options) => () => _ = new IssueAssessor(options);

	public static Action Construct(Assessment assessment) => Construct(Options(Category(assessment)));
}
