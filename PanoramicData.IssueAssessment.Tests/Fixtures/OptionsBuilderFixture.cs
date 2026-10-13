namespace PanoramicData.IssueAssessment.Tests.Fixtures;

/// <summary>
/// Small factories for test categories and assessment options.
/// </summary>
internal static class OptionsBuilderFixture
{
	/// <summary>
	/// The category name used when a test does not supply one.
	/// </summary>
	public const string DefaultCategoryName = "Category";

	/// <summary>
	/// Creates a valid series category containing the given assessments, or one default assessment when none are given.
	/// </summary>
	public static AssessmentCategory Category(
		string name = DefaultCategoryName,
		AssessmentExecutionStrategy strategy = AssessmentExecutionStrategy.Series,
		params Assessment[] assessments)
	{
		return new AssessmentCategory
		{
			Name = name,
			Description = "Category description",
			AssessmentExecutionStrategy = strategy,
			Assessments = assessments.Length == 0
				? ImmutableList.Create(AssessmentBuilderFixture.Create())
				: ImmutableList.Create(assessments),
		};
	}

	/// <summary>
	/// Creates a valid category containing the single given assessment.
	/// </summary>
	public static AssessmentCategory CategoryWith(Assessment assessment, string name = DefaultCategoryName)
	{
		return Category(name, AssessmentExecutionStrategy.Series, assessment);
	}

	/// <summary>
	/// Creates series options containing the given categories, or one default category when none are given.
	/// </summary>
	public static AssessmentOptions Options(params AssessmentCategory[] categories)
	{
		return new AssessmentOptions
		{
			Categories = categories.Length == 0
				? ImmutableList.Create(Category())
				: ImmutableList.Create(categories),
			ExecutionStrategy = CategoryExecutionStrategy.Series,
		};
	}

	/// <summary>
	/// Creates options containing one default category whose single assessment is the given one.
	/// </summary>
	public static AssessmentOptions OptionsWith(Assessment assessment)
	{
		return Options(CategoryWith(assessment));
	}

	/// <summary>
	/// Creates options containing one default category whose single assessment uses the given definition.
	/// </summary>
	public static AssessmentOptions OptionsWith(IAssessmentDefinition definition)
	{
		return OptionsWith(AssessmentBuilderFixture.Create(definition));
	}

	/// <summary>
	/// Runs one category end to end through a new <see cref="IssueAssessor"/> and returns the full result.
	/// </summary>
	public static async Task<IAssessmentResult> AssessAsync(
		AssessmentCategory category,
		JsonDocument issue,
		CancellationToken cancellationToken)
	{
		IssueAssessor assessor = new(Options(category));
		return await assessor.AssessAsync(issue, cancellationToken);
	}
}
