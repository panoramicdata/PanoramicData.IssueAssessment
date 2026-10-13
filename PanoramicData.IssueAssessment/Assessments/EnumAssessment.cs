namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value is contained in an allowed whitelist.
/// </summary>
public sealed record EnumAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the allowed string values.
	/// </summary>
	public required ImmutableHashSet<string> AllowedValues { get; init; }

	/// <summary>
	/// Gets the score returned when the value is allowed.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when the value is not allowed.
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

		string candidate = value.GetString() ?? string.Empty;
		bool isAllowed = AllowedValues.Contains(candidate);

		AssessmentExecutionResult result = new(
			RawScore: isAllowed ? SuccessScore : FailureScore,
			ExplanationText: BuildAllowedExplanation(candidate, isAllowed),
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

	private string BuildAllowedExplanation(string candidate, bool isAllowed)
	{
		return ExplanationTemplate
			?? (isAllowed
				? $"Value '{candidate}' at '{JsonPath}' is allowed."
				: $"Value '{candidate}' at '{JsonPath}' is not allowed.");
	}
}
