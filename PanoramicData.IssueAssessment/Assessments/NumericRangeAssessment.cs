namespace PanoramicData.IssueAssessment.Assessments;

using System.Globalization;

/// <summary>
/// Assesses whether a selected numeric value falls within an inclusive range.
/// </summary>
public sealed record NumericRangeAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the inclusive minimum allowed value.
	/// </summary>
	public double? MinValue { get; init; }

	/// <summary>
	/// Gets the inclusive maximum allowed value.
	/// </summary>
	public double? MaxValue { get; init; }

	/// <summary>
	/// Gets the score returned when the value falls within range.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when the value falls outside the range.
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

		if (!JsonPathHelper.TrySelect(input, virtualExtension, JsonPath, out JsonElement value) || !TryGetNumericValue(value, out double actualValue))
		{
			AssessmentExecutionResult missingResult = new(
				RawScore: FailureScore,
				ExplanationText: BuildMissingExplanation(),
				FailureReason: AssessmentFailureReason.JsonPathNotFound,
				InnerException: null,
				VirtualExtensionOutput: null);

			return Task.FromResult(missingResult);
		}

		bool meetsMinimum = !MinValue.HasValue || actualValue >= MinValue.Value;
		bool meetsMaximum = !MaxValue.HasValue || actualValue <= MaxValue.Value;
		bool isInRange = meetsMinimum && meetsMaximum;

		AssessmentExecutionResult result = new(
			RawScore: isInRange ? SuccessScore : FailureScore,
			ExplanationText: BuildRangeExplanation(actualValue, isInRange),
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: null);

		return Task.FromResult(result);
	}

	private string BuildMissingExplanation()
	{
		return ExplanationTemplate
			?? $"Numeric value was not found at '{JsonPath}'.";
	}

	private string BuildRangeExplanation(double actualValue, bool isInRange)
	{
		string minimum = MinValue?.ToString(CultureInfo.InvariantCulture) ?? "-inf";
		string maximum = MaxValue?.ToString(CultureInfo.InvariantCulture) ?? "+inf";
		string valueText = actualValue.ToString(CultureInfo.InvariantCulture);

		return ExplanationTemplate
			?? (isInRange
				? $"Value {valueText} at '{JsonPath}' is within range {minimum}-{maximum}."
				: $"Value {valueText} at '{JsonPath}' is outside range {minimum}-{maximum}.");
	}

	private static bool TryGetNumericValue(JsonElement value, out double actualValue)
	{
		if (value.ValueKind == JsonValueKind.Number)
		{
			// A JSON number always converts; one too large for a double becomes infinity, which is rejected.
			actualValue = value.GetDouble();
			return double.IsFinite(actualValue);
		}

		if (value.ValueKind == JsonValueKind.String)
		{
			// "NaN", "Infinity" and out-of-range values parse, but are not numbers a range can meaningfully contain.
			return double.TryParse(
				value.GetString(),
				NumberStyles.Float | NumberStyles.AllowThousands,
				CultureInfo.InvariantCulture,
				out actualValue)
				&& double.IsFinite(actualValue);
		}

		actualValue = default;
		return false;
	}
}
