namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected value is present and non-empty.
/// </summary>
public sealed record PresenceAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the score returned when a value is present.
	/// </summary>
	public double PresentScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when a value is absent.
	/// </summary>
	public double AbsentScore { get; init; }

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

		bool found = JsonPathHelper.TrySelect(input, JsonPath, out JsonElement value);
		bool isPresent = found && !IsMissingValue(value);
		double score = isPresent ? PresentScore : AbsentScore;

		AssessmentExecutionResult result = new(
			RawScore: score,
			ExplanationText: BuildExplanation(isPresent),
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: null);

		return Task.FromResult(result);
	}

	private string BuildExplanation(bool isPresent)
	{
		return ExplanationTemplate
			?? (isPresent
				? $"Value is present for '{JsonPath}'."
				: $"Value is missing for '{JsonPath}'.");
	}

	private static bool IsMissingValue(JsonElement element)
	{
		if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
		{
			return true;
		}

		if (element.ValueKind == JsonValueKind.String)
		{
			return string.IsNullOrWhiteSpace(element.GetString());
		}

		return false;
	}
}
