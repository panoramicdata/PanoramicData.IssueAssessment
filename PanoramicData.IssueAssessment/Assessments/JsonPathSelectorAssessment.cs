namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses a JSONPath-selected value using a consumer-supplied evaluator callback.
/// </summary>
public sealed record JsonPathSelectorAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the evaluator callback that determines pass/fail for the selected value.
	/// </summary>
	public required Func<JsonElement, CancellationToken, Task<bool>> Evaluator { get; init; }

	/// <summary>
	/// Gets the score returned when evaluator succeeds.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when evaluator fails.
	/// </summary>
	public double FailureScore { get; init; }

	/// <summary>
	/// Gets an optional explanation template.
	/// </summary>
	public string? ExplanationTemplate { get; init; }

	/// <inheritdoc />
	public async Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);
		cancellationToken.ThrowIfCancellationRequested();

		if (!JsonPathHelper.TrySelect(input, JsonPath, out JsonElement value))
		{
			return new AssessmentExecutionResult(
				RawScore: FailureScore,
				ExplanationText: BuildMissingExplanation(),
				FailureReason: AssessmentFailureReason.JsonPathNotFound,
				InnerException: null,
				VirtualExtensionOutput: null);
		}

		try
		{
			bool isSuccess = await Evaluator(value, cancellationToken).ConfigureAwait(false);

			return new AssessmentExecutionResult(
				RawScore: isSuccess ? SuccessScore : FailureScore,
				ExplanationText: BuildEvaluationExplanation(isSuccess),
				FailureReason: null,
				InnerException: null,
				VirtualExtensionOutput: null);
		}
		catch (Exception exception)
		{
			return new AssessmentExecutionResult(
				RawScore: FailureScore,
				ExplanationText: BuildEvaluatorExceptionExplanation(),
				FailureReason: AssessmentFailureReason.ThrowingException,
				InnerException: exception,
				VirtualExtensionOutput: null);
		}
	}

	private string BuildMissingExplanation()
	{
		return ExplanationTemplate
			?? $"Value was not found at '{JsonPath}'.";
	}

	private string BuildEvaluationExplanation(bool isSuccess)
	{
		return ExplanationTemplate
			?? (isSuccess
				? $"Evaluator returned success for '{JsonPath}'."
				: $"Evaluator returned failure for '{JsonPath}'.");
	}

	private string BuildEvaluatorExceptionExplanation()
	{
		return ExplanationTemplate
			?? $"Evaluator threw an exception for '{JsonPath}'.";
	}
}
