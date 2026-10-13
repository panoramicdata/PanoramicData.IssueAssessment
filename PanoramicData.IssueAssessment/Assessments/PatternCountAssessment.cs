namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether regex pattern match count in a selected string value falls within an inclusive range.
/// </summary>
public sealed record PatternCountAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition, IRegexPatternAssessmentDefinition
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
	/// Gets the inclusive minimum match count.
	/// </summary>
	public int? MinMatches { get; init; }

	/// <summary>
	/// Gets the inclusive maximum match count.
	/// </summary>
	public int? MaxMatches { get; init; }

	/// <summary>
	/// Gets the score returned when match count is in range.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when match count is outside range.
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
		int matchCount = RegexCache.GetOrAdd(Pattern).Count(text);

		bool meetsMinimum = !MinMatches.HasValue || matchCount >= MinMatches.Value;
		bool meetsMaximum = !MaxMatches.HasValue || matchCount <= MaxMatches.Value;
		bool isInRange = meetsMinimum && meetsMaximum;

		AssessmentExecutionResult result = new(
			RawScore: isInRange ? SuccessScore : FailureScore,
			ExplanationText: BuildRangeExplanation(matchCount, isInRange),
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

	private string BuildRangeExplanation(int matchCount, bool isInRange)
	{
		string minimum = MinMatches?.ToString(CultureInfo.InvariantCulture) ?? "0";
		string maximum = MaxMatches?.ToString(CultureInfo.InvariantCulture) ?? "+inf";

		return ExplanationTemplate
			?? (isInRange
				? $"Found {matchCount} matches at '{JsonPath}', within expected range {minimum}-{maximum}."
				: $"Found {matchCount} matches at '{JsonPath}', outside expected range {minimum}-{maximum}.");
	}
}
