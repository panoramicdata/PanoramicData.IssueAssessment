namespace PanoramicData.IssueAssessment.Validation;
/// <summary>
/// Validates <see cref="IAssessmentOptions"/> at construction time.
/// </summary>
public static class OptionsValidator
{
	private const double MinimumWeight = -100d;
	private const double MaximumWeight = 100d;

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

		if (options.GlobalTimeout is { } globalTimeout && globalTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException("GlobalTimeout must be greater than zero.", nameof(options));
		}

		foreach (AssessmentCategory category in options.Categories)
		{
			ValidateCategory(category);
		}
	}

	private static void ValidateCategory(AssessmentCategory category)
	{
		ArgumentNullException.ThrowIfNull(category);

		if (string.IsNullOrWhiteSpace(category.Name))
		{
			throw new ArgumentException("Category name is required.", nameof(category));
		}

		if (string.IsNullOrWhiteSpace(category.Description))
		{
			throw new ArgumentException($"Category '{category.Name}' description is required.", nameof(category));
		}

		if (category.Assessments.Count == 0)
		{
			throw new ArgumentException($"Category '{category.Name}' must include at least one assessment.", nameof(category));
		}

		if (category.CategoryTimeout is { } categoryTimeout && categoryTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException($"Category '{category.Name}' timeout must be greater than zero.", nameof(category));
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
			ValidateAssessment(category, assessment);
		}
	}

	private static void ValidateAssessment(AssessmentCategory category, Assessment assessment)
	{
		ArgumentNullException.ThrowIfNull(assessment);

		if (string.IsNullOrWhiteSpace(assessment.Name))
		{
			throw new ArgumentException($"Category '{category.Name}' contains an assessment with no name.", nameof(assessment));
		}

		if (string.IsNullOrWhiteSpace(assessment.Description))
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' must include a description.",
				nameof(assessment));
		}

		if (assessment.Definition is null)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' must include a definition.",
				nameof(assessment));
		}

		if (assessment.Definition is IJsonPathAssessmentDefinition jsonPathDefinition)
		{
			try
			{
				JsonPathHelper.Validate(jsonPathDefinition.JsonPath);
			}
			catch (InvalidJsonPathException exception)
			{
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' has invalid JSONPath "
					+ $"'{jsonPathDefinition.JsonPath}'.",
					nameof(assessment),
					exception);
			}
		}

		if (assessment.Definition is IRegexPatternAssessmentDefinition regexDefinition)
		{
			try
			{
				_ = RegexCache.GetOrAdd(regexDefinition.Pattern);
			}
			catch (ArgumentException exception)
			{
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' has invalid regex pattern "
					+ $"'{regexDefinition.Pattern}'.",
					nameof(assessment),
					exception);
			}
		}

		if (assessment.Weight < MinimumWeight || assessment.Weight > MaximumWeight)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' has invalid weight {assessment.Weight}. "
				+ $"Weight must be between {MinimumWeight} and {MaximumWeight}.",
				nameof(assessment));
		}

		if (assessment.AssessmentTimeout is { } assessmentTimeout && assessmentTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' timeout must be greater than zero.",
				nameof(assessment));
		}

		if (assessment.AllowVirtualExtension && string.IsNullOrWhiteSpace(assessment.VirtualOutputName))
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' enables virtual extension but does not "
				+ "define VirtualOutputName.",
				nameof(assessment));
		}
	}
}
