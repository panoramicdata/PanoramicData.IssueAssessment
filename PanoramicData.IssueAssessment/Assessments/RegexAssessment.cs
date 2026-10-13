namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value matches a regex pattern.
/// </summary>
public sealed record RegexAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition, IRegexPatternAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the regex pattern.
	/// </summary>
	public required string Pattern { get; init; }

	/// <summary>
	/// Gets the score returned on a successful match.
	/// </summary>
	public required double SuccessScore { get; init; }

	/// <summary>
	/// Gets the score returned when the value does not match.
	/// </summary>
	public required double FailureScore { get; init; }

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
				ExplanationText: BuildExplanation(false),
				FailureReason: AssessmentFailureReason.JsonPathNotFound,
				InnerException: null,
				VirtualExtensionOutput: null);

			return Task.FromResult(missingResult);
		}

		string text = value.GetString()!;
		bool isMatch = RegexCache.GetOrAdd(Pattern).IsMatch(text);

		AssessmentExecutionResult result = new(
			RawScore: isMatch ? SuccessScore : FailureScore,
			ExplanationText: BuildExplanation(isMatch),
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: null);

		return Task.FromResult(result);
	}

	private string BuildExplanation(bool isMatch)
	{
		return ExplanationTemplate
			?? (isMatch
				? $"Pattern '{Pattern}' matched value at '{JsonPath}'."
				: $"Pattern '{Pattern}' did not match value at '{JsonPath}'.");
	}
}
