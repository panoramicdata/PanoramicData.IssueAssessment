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
			ValidateAssessment(category, assessment);
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

	private static void ValidateAssessment(AssessmentCategory category, Assessment assessment)
	{
		ArgumentNullException.ThrowIfNull(assessment);

		if (string.IsNullOrWhiteSpace(assessment.Name))
		{
			throw new ArgumentException($"Category '{category.Name}' contains an assessment with no name.", nameof(assessment));
		}

		Exception? innerException = null;
		string? error = AssessmentError(assessment);
		if (error is null)
		{
			error = SelectorError(assessment.Definition, out innerException)
				?? DefinitionError(assessment.Definition)
				?? SettingsError(assessment);
		}

		if (error is not null)
		{
			throw new ArgumentException(
				$"Assessment '{assessment.Name}' in category '{category.Name}' {error}",
				nameof(assessment),
				innerException);
		}
	}

	private static string? AssessmentError(Assessment assessment)
	{
		if (string.IsNullOrWhiteSpace(assessment.Description))
		{
			return "must include a description.";
		}

		return assessment.Definition is null ? "must include a definition." : null;
	}

	/// <summary>
	/// Compiles the definition's JSONPath and regex, reporting the parse error as the inner exception.
	/// </summary>
	private static string? SelectorError(IAssessmentDefinition definition, out Exception? innerException)
	{
		innerException = null;
		try
		{
			if (definition is IJsonPathAssessmentDefinition jsonPathDefinition)
			{
				JsonPathHelper.Validate(jsonPathDefinition.JsonPath);
			}
		}
		catch (InvalidJsonPathException exception)
		{
			innerException = exception;
			return $"has invalid JSONPath '{((IJsonPathAssessmentDefinition)definition).JsonPath}'.";
		}

		try
		{
			if (definition is IRegexPatternAssessmentDefinition regexDefinition)
			{
				_ = RegexCache.GetOrAdd(regexDefinition.Pattern);
			}
		}
		catch (ArgumentException exception)
		{
			innerException = exception;
			return $"has invalid regex pattern '{((IRegexPatternAssessmentDefinition)definition).Pattern}'.";
		}

		return null;
	}

	private static string? DefinitionError(IAssessmentDefinition definition) => definition switch
	{
		StringLengthAssessment value => RangeError(
			value.MinimumLength,
			value.MaximumLength,
			nameof(StringLengthAssessment.MinimumLength),
			nameof(StringLengthAssessment.MaximumLength)),
		WordCountAssessment value => RangeError(
			value.MinimumWordCount,
			value.MaximumWordCount,
			nameof(WordCountAssessment.MinimumWordCount),
			nameof(WordCountAssessment.MaximumWordCount)),
		PatternCountAssessment value => RangeError(
			value.MinMatches,
			value.MaxMatches,
			nameof(PatternCountAssessment.MinMatches),
			nameof(PatternCountAssessment.MaxMatches)),
		NumericRangeAssessment value when value.MinValue > value.MaxValue => "has MinValue greater than MaxValue.",
		DateRangeAssessment value when value.MinDate > value.MaxDate => "has MinDate greater than MaxDate.",
		_ => MissingPartError(definition),
	};

	private static string? MissingPartError(IAssessmentDefinition definition) => definition switch
	{
		EnumAssessment { AllowedValues.Count: 0 } => "must define at least one allowed value.",
		JsonPathSelectorAssessment { Evaluator: null } => "must define an evaluator.",
		CustomCallbackAssessment { Callback: null } => "must define a callback.",
		_ => null,
	};

	/// <summary>
	/// Checks an inclusive range whose bounds are optional: a minimum cannot be negative or above the maximum.
	/// </summary>
	private static string? RangeError(int? minimum, int? maximum, string minimumName, string maximumName)
	{
		if (minimum < 0)
		{
			return string.Create(CultureInfo.InvariantCulture, $"has invalid {minimumName} {minimum}.");
		}

		return maximum < minimum ? $"has {maximumName} less than {minimumName}." : null;
	}

	private static string? SettingsError(Assessment assessment)
	{
		if (assessment.Weight is < MinimumWeight or > MaximumWeight)
		{
			return string.Create(
				CultureInfo.InvariantCulture,
				$"has invalid weight {assessment.Weight}. Weight must be between {MinimumWeight} and {MaximumWeight}.");
		}

		if (assessment.AssessmentTimeout <= TimeSpan.Zero)
		{
			return "timeout must be greater than zero.";
		}

		return assessment.AllowVirtualExtension && string.IsNullOrWhiteSpace(assessment.VirtualOutputName)
			? "enables virtual extension but does not define VirtualOutputName."
			: null;
	}
}
