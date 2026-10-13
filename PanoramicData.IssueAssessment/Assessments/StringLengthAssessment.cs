namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value length falls within an inclusive range.
/// </summary>
public sealed record StringLengthAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the minimum allowed string length.
	/// </summary>
	public int MinimumLength { get; init; }

	/// <summary>
	/// Gets the maximum allowed string length.
	/// </summary>
	public int MaximumLength { get; init; }

	/// <summary>
	/// Gets the score returned when the value length falls within range.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when the value length falls outside the range.
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

		if (!JsonPathHelper.TrySelect(input, virtualExtension, JsonPath, out JsonElement value) || value.ValueKind != JsonValueKind.String)
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
		bool isInRange = text.Length >= MinimumLength && text.Length <= MaximumLength;

		AssessmentExecutionResult result = new(
			RawScore: isInRange ? SuccessScore : FailureScore,
			ExplanationText: BuildRangeExplanation(text.Length, isInRange),
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: null);

		return Task.FromResult(result);
	}

	private string BuildMissingExplanation()
	{
		return ExplanationTemplate
			?? $"String value was not found at '{JsonPath}'.";
	}

	private string BuildRangeExplanation(int length, bool isInRange)
	{
		return ExplanationTemplate
			?? (isInRange
				? $"String length {length} at '{JsonPath}' is within range {MinimumLength}-{MaximumLength}."
				: $"String length {length} at '{JsonPath}' is outside range {MinimumLength}-{MaximumLength}.");
	}
}
