namespace PanoramicData.IssueAssessment.Validation;
/// <summary>
/// Validates <see cref="IAssessmentOptions"/> at construction time.
/// </summary>
public static class OptionsValidator
{

	/// <summary>
	/// Validates assessment options and throws when configuration is invalid.
	/// </summary>
	/// <param name="options">Options to validate.</param>
	/// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
	/// <exception cref="ArgumentException">Thrown when values are invalid.</exception>
	/// <exception cref="InvalidOperationException">Thrown when configuration combinations are invalid.</exception>
	public static void Validate(IAssessmentOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.Categories.Count == 0)
		{
			throw new ArgumentException("At least one assessment category is required.", nameof(options));
		}

		if (options.GlobalTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException("GlobalTimeout must be greater than zero.", nameof(options));
		}

		foreach (AssessmentCategory category in options.Categories)
		{
			ValidateCategory(category);
		}

		ValidateUniqueNames(options);
	}

	/// <summary>
	/// Results are keyed by category name, and virtual outputs by VirtualOutputName: a duplicate would collide at
	/// assessment time, so reject it here.
	/// </summary>
	private static void ValidateUniqueNames(IAssessmentOptions options)
	{
		string? duplicateCategory = FirstDuplicate(options.Categories.Select(category => category.Name));
		if (duplicateCategory is not null)
		{
			throw new ArgumentException($"Category name '{duplicateCategory}' is used more than once.", nameof(options));
		}

		string? duplicateOutput = FirstDuplicate(options.Categories
			.SelectMany(category => category.Assessments)
			.Where(assessment => assessment.AllowVirtualExtension)
			.Select(assessment => assessment.VirtualOutputName!));
		if (duplicateOutput is not null)
		{
			throw new ArgumentException($"VirtualOutputName '{duplicateOutput}' is used by more than one assessment.", nameof(options));
		}
	}

	private static string? FirstDuplicate(IEnumerable<string> names)
		=> names.GroupBy(name => name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;

	private static void ValidateCategory(AssessmentCategory category)
	{
		ArgumentNullException.ThrowIfNull(category);

		string? error = CategoryError(category);
		if (error is not null)
		{
			throw new ArgumentException(error, nameof(category));
		}

		if (
			category.AssessmentExecutionStrategy == AssessmentExecutionStrategy.Parallel
			&& category.Assessments.Any(assessment => assessment.AllowVirtualExtension)
		)
		{
			throw new InvalidOperationException(
				$"Category '{category.Name}' cannot allow virtual extension when assessments execute in parallel.");
		}

		foreach (Assessment assessment in category.Assessments)
		{
			AssessmentValidator.Validate(category, assessment);
		}
	}

	private static string? CategoryError(AssessmentCategory category)
	{
		if (string.IsNullOrWhiteSpace(category.Name))
		{
			return "Category name is required.";
		}

		if (string.IsNullOrWhiteSpace(category.Description))
		{
			return $"Category '{category.Name}' description is required.";
		}

		if (category.Assessments.Count == 0)
		{
			return $"Category '{category.Name}' must include at least one assessment.";
		}

		return category.CategoryTimeout <= TimeSpan.Zero
			? $"Category '{category.Name}' timeout must be greater than zero."
			: null;
	}
}
