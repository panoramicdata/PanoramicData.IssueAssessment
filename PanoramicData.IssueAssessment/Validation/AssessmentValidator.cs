namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// Validates a single <see cref="Assessment"/>: its identity, its JSONPath and regex, the settings specific to its
/// definition type, and its weight, timeout and virtual extension settings.
/// </summary>
internal static class AssessmentValidator
{
	private const double MinimumWeight = -100d;
	private const double MaximumWeight = 100d;

	internal static void Validate(AssessmentCategory category, Assessment assessment)
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
