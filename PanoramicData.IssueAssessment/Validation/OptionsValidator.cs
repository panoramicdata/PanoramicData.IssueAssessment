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

		ValidateDefinitionSpecificConfiguration(category, assessment);

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

	private static void ValidateDefinitionSpecificConfiguration(AssessmentCategory category, Assessment assessment)
	{
		switch (assessment.Definition)
		{
			case StringLengthAssessment stringLengthAssessment:
				ValidateInclusiveRange(
					category,
					assessment,
					stringLengthAssessment.MinimumLength,
					stringLengthAssessment.MaximumLength,
					allowNegativeMinimum: false,
					minimumName: nameof(StringLengthAssessment.MinimumLength),
					maximumName: nameof(StringLengthAssessment.MaximumLength));
				break;

			case WordCountAssessment wordCountAssessment:
				ValidateInclusiveRange(
					category,
					assessment,
					wordCountAssessment.MinimumWordCount,
					wordCountAssessment.MaximumWordCount,
					allowNegativeMinimum: false,
					minimumName: nameof(WordCountAssessment.MinimumWordCount),
					maximumName: nameof(WordCountAssessment.MaximumWordCount));
				break;

			case NumericRangeAssessment numericRangeAssessment when numericRangeAssessment.MinValue.HasValue && numericRangeAssessment.MaxValue.HasValue && numericRangeAssessment.MinValue.Value > numericRangeAssessment.MaxValue.Value:
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' has MinValue greater than MaxValue.",
					nameof(assessment));

			case DateRangeAssessment dateRangeAssessment when dateRangeAssessment.MinDate.HasValue && dateRangeAssessment.MaxDate.HasValue && dateRangeAssessment.MinDate.Value > dateRangeAssessment.MaxDate.Value:
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' has MinDate greater than MaxDate.",
					nameof(assessment));

			case PatternCountAssessment patternCountAssessment:
				ValidateOptionalInclusiveRange(
					category,
					assessment,
					patternCountAssessment.MinMatches,
					patternCountAssessment.MaxMatches,
					allowNegativeMinimum: false,
					minimumName: nameof(PatternCountAssessment.MinMatches),
					maximumName: nameof(PatternCountAssessment.MaxMatches));
				break;

			case EnumAssessment enumAssessment when enumAssessment.AllowedValues.Count == 0:
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' must define at least one allowed value.",
					nameof(assessment));

			case JsonPathSelectorAssessment jsonPathSelectorAssessment when jsonPathSelectorAssessment.Evaluator is null:
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' must define an evaluator.",
					nameof(assessment));

			case CustomCallbackAssessment customCallbackAssessment when customCallbackAssessment.Callback is null:
				throw new ArgumentException(
					$"Assessment '{assessment.Name}' in category '{category.Name}' must define a callback.",
					nameof(assessment));
		}
	}

	private static void ValidateInclusiveRange(
		AssessmentCategory category,
		Assessment assessment,
		int minimum,
		int maximum,
		bool allowNegativeMinimum,
		string minimumName,
		string maximumName)
	{
		if (!allowNegativeMinimum && minimum < 0)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' has invalid {minimumName} {minimum}.",
				nameof(assessment));
		}

		if (maximum < minimum)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' has {maximumName} less than {minimumName}.",
				nameof(assessment));
		}
	}

	private static void ValidateOptionalInclusiveRange(
		AssessmentCategory category,
		Assessment assessment,
		int? minimum,
		int? maximum,
		bool allowNegativeMinimum,
		string minimumName,
		string maximumName)
	{
		if (minimum.HasValue && !allowNegativeMinimum && minimum.Value < 0)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' has invalid {minimumName} {minimum.Value}.",
				nameof(assessment));
		}

		if (minimum.HasValue && maximum.HasValue && maximum.Value < minimum.Value)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' has {maximumName} less than {minimumName}.",
				nameof(assessment));
		}
	}
}
