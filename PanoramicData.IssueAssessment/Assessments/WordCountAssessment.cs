namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value word count falls within an inclusive range.
/// </summary>
public sealed record WordCountAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the minimum allowed word count.
	/// </summary>
	public int MinimumWordCount { get; init; }

	/// <summary>
	/// Gets the maximum allowed word count.
	/// </summary>
	public int MaximumWordCount { get; init; }

	/// <summary>
	/// Gets the score returned when the value word count falls within range.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when the value word count falls outside the range.
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
		int wordCount = CountWords(text);
		bool isInRange = wordCount >= MinimumWordCount && wordCount <= MaximumWordCount;

		AssessmentExecutionResult result = new(
			RawScore: isInRange ? SuccessScore : FailureScore,
			ExplanationText: BuildRangeExplanation(wordCount, isInRange),
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

	private string BuildRangeExplanation(int wordCount, bool isInRange)
	{
		return ExplanationTemplate
			?? (isInRange
				? $"Word count {wordCount} at '{JsonPath}' is within range {MinimumWordCount}-{MaximumWordCount}."
				: $"Word count {wordCount} at '{JsonPath}' is outside range {MinimumWordCount}-{MaximumWordCount}.");
	}

	private static int CountWords(string text)
	{
		ReadOnlySpan<char> span = text.AsSpan();
		bool inWord = false;
		int count = 0;

		foreach (char character in span)
		{
			if (char.IsWhiteSpace(character))
			{
				inWord = false;
				continue;
			}

			if (!inWord)
			{
				count++;
				inWord = true;
			}
		}

		return count;
	}
}
