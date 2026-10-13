namespace PanoramicData.IssueAssessment.Assessments;


/// <summary>
/// Assesses whether a selected date value falls within an inclusive date range.
/// </summary>
public sealed record DateRangeAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the inclusive minimum date.
	/// </summary>
	public DateTime? MinDate { get; init; }

	/// <summary>
	/// Gets the inclusive maximum date.
	/// </summary>
	public DateTime? MaxDate { get; init; }

	/// <summary>
	/// Gets the expected date format for parsing source values.
	/// </summary>
	public string DateFormat { get; init; } = "O";

	/// <summary>
	/// Gets the score returned when the date is in range.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when the date is out of range.
	/// </summary>
	public double FailureScore { get; init; }

	/// <summary>
	/// Gets an optional explanation template.
	/// </summary>
	public string? ExplanationTemplate { get; init; }

	/// <inheritdoc />
	public Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);
		cancellationToken.ThrowIfCancellationRequested();

		if (!JsonPathHelper.TrySelect(input, JsonPath, out JsonElement value) || value.ValueKind != JsonValueKind.String)
		{
			AssessmentExecutionResult missingResult = new(
				RawScore: FailureScore,
				ExplanationText: BuildMissingExplanation(),
				FailureReason: AssessmentFailureReason.JsonPathNotFound,
				InnerException: null,
				VirtualExtensionOutput: null);

			return Task.FromResult(missingResult);
		}

		string text = value.GetString() ?? string.Empty;
		if (!TryParseDate(text, out DateTime actualDate))
		{
			AssessmentExecutionResult parseFailureResult = new(
				RawScore: FailureScore,
				ExplanationText: BuildParseFailureExplanation(text),
				FailureReason: null,
				InnerException: null,
				VirtualExtensionOutput: null);

			return Task.FromResult(parseFailureResult);
		}

		bool meetsMinimum = !MinDate.HasValue || actualDate >= MinDate.Value;
		bool meetsMaximum = !MaxDate.HasValue || actualDate <= MaxDate.Value;
		bool isInRange = meetsMinimum && meetsMaximum;

		AssessmentExecutionResult result = new(
			RawScore: isInRange ? SuccessScore : FailureScore,
			ExplanationText: BuildRangeExplanation(actualDate, isInRange),
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: null);

		return Task.FromResult(result);
	}

	private bool HasFormat => !string.IsNullOrWhiteSpace(DateFormat);

	/// <summary>
	/// The format used to write dates into explanations: the configured format, or the sortable format when parsing is
	/// flexible.
	/// </summary>
	private string DisplayFormat => HasFormat ? DateFormat : "s";

	private bool TryParseDate(string value, out DateTime date)
	{
		bool parsed = HasFormat
			? DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)
			: DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);

		// RoundtripKind converts a value carrying an offset (+01:30) to the machine's local time. Compare it as UTC
		// instead, so the result does not depend on the time zone of the machine running the assessment.
		if (parsed && date.Kind == DateTimeKind.Local)
		{
			date = date.ToUniversalTime();
		}

		return parsed;
	}

	private string BuildMissingExplanation()
	{
		return ExplanationTemplate
			?? $"Date value was not found at '{JsonPath}'.";
	}

	private string BuildParseFailureExplanation(string value)
	{
		return ExplanationTemplate
			?? (HasFormat
				? $"Date value '{value}' at '{JsonPath}' could not be parsed with format '{DateFormat}'."
				: $"Date value '{value}' at '{JsonPath}' could not be parsed as a date.");
	}

	private string BuildRangeExplanation(DateTime date, bool isInRange)
	{
		string minimum = MinDate?.ToString(DisplayFormat, CultureInfo.InvariantCulture) ?? "-inf";
		string maximum = MaxDate?.ToString(DisplayFormat, CultureInfo.InvariantCulture) ?? "+inf";
		string actual = date.ToString(DisplayFormat, CultureInfo.InvariantCulture);

		return ExplanationTemplate
			?? (isInRange
				? $"Date {actual} at '{JsonPath}' is within range {minimum}-{maximum}."
				: $"Date {actual} at '{JsonPath}' is outside range {minimum}-{maximum}.");
	}
}
