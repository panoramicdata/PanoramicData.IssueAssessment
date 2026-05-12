namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses an issue using a consumer-supplied asynchronous callback.
/// </summary>
public sealed record CustomCallbackAssessment : IAssessmentDefinition
{
	/// <summary>
	/// Gets the callback used to execute custom scoring logic.
	/// </summary>
	public required Func<JsonDocument, JsonDocument?, CancellationToken, Task<(double? Score, string Explanation, Exception? Exception)>> Callback { get; init; }

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

		try
		{
			(double? score, string explanation, Exception? exception) = await Callback(input, virtualExtension, cancellationToken)
				.ConfigureAwait(false);

			return new AssessmentExecutionResult(
				RawScore: score,
				ExplanationText: BuildExplanation(explanation),
				FailureReason: exception is null ? null : AssessmentFailureReason.ThrowingException,
				InnerException: exception,
				VirtualExtensionOutput: null);
		}
		catch (Exception exception)
		{
			return new AssessmentExecutionResult(
				RawScore: null,
				ExplanationText: BuildExceptionExplanation(),
				FailureReason: AssessmentFailureReason.ThrowingException,
				InnerException: exception,
				VirtualExtensionOutput: null);
		}
	}

	private string BuildExplanation(string callbackExplanation)
	{
		if (!string.IsNullOrWhiteSpace(ExplanationTemplate))
		{
			return ExplanationTemplate;
		}

		return string.IsNullOrWhiteSpace(callbackExplanation)
			? "Custom callback completed."
			: callbackExplanation;
	}

	private string BuildExceptionExplanation()
	{
		return ExplanationTemplate
			?? "Custom callback threw an exception.";
	}
}