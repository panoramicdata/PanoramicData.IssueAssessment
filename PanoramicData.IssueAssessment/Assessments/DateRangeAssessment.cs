namespace PanoramicData.IssueAssessment.Assessments;

using System.Globalization;

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
	public double FailureScore { get; init; } = 0d;

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

	private bool TryParseDate(string value, out DateTime date)
	{
		if (string.IsNullOrWhiteSpace(DateFormat))
		{
			return DateTime.TryParse(
				value,
				CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind,
				out date);
		}

		return DateTime.TryParseExact(
			value,
			DateFormat,
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind,
			out date);
	}

	private string BuildMissingExplanation()
	{
		return ExplanationTemplate
			?? $"Date value was not found at '{JsonPath}'.";
	}

	private string BuildParseFailureExplanation(string value)
	{
		return ExplanationTemplate
			?? $"Date value '{value}' at '{JsonPath}' could not be parsed with format '{DateFormat}'.";
	}

	private string BuildRangeExplanation(DateTime date, bool isInRange)
	{
		string minimum = MinDate?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? "-inf";
		string maximum = MaxDate?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? "+inf";
		string actual = date.ToString(DateFormat, CultureInfo.InvariantCulture);

		return ExplanationTemplate
			?? (isInRange
				? $"Date {actual} at '{JsonPath}' is within range {minimum}-{maximum}."
				: $"Date {actual} at '{JsonPath}' is outside range {minimum}-{maximum}.");
	}
}