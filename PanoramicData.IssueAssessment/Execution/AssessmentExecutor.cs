namespace PanoramicData.IssueAssessment.Execution;

using PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Executes a single assessment with timeout and failure mapping.
/// </summary>
internal static class AssessmentExecutor
{
	/// <summary>
	/// Executes an assessment and returns normalized result artifacts.
	/// </summary>
	public static async Task<AssessmentExecutionItemResult> ExecuteAsync(
		string categoryName,
		Assessment assessment,
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(categoryName);
		ArgumentNullException.ThrowIfNull(assessment);

		(double minPossibleRaw, double maxPossibleRaw) = ScoreCalculator.CalculateAssessmentRawRange(assessment);
		AssessmentExecutionResult executionResult = await RunAsync(assessment, input, virtualExtension, cancellationToken)
			.ConfigureAwait(false);

		double? normalizedScore = executionResult.RawScore.HasValue
			? ScoreCalculator.Normalize(executionResult.RawScore.Value, minPossibleRaw, maxPossibleRaw)
			: null;

		AssessmentScore score = new(
			RawScore: executionResult.RawScore,
			NormalizedScore: normalizedScore,
			MinPossibleRaw: minPossibleRaw,
			MaxPossibleRaw: maxPossibleRaw);

		AssessmentResult assessmentResult = new(
			AssessmentName: assessment.Name,
			Score: score,
			ExplanationText: executionResult.ExplanationText ?? string.Empty,
			FailureReason: executionResult.FailureReason,
			InnerException: executionResult.InnerException);

		AssessmentFailure? failure = executionResult.FailureReason is null
			? null
			: new AssessmentFailure(
				CategoryName: categoryName,
				AssessmentName: assessment.Name,
				Reason: executionResult.FailureReason.Value,
				Message: executionResult.ExplanationText ?? "Assessment failed.",
				InnerException: executionResult.InnerException);

		return new AssessmentExecutionItemResult(
			AssessmentResult: assessmentResult,
			Failure: failure,
			VirtualDocument: executionResult.VirtualExtensionOutput);
	}

	/// <summary>
	/// Runs the definition, turning a cancellation into a timeout result and any other exception into a failure result.
	/// </summary>
	private static async Task<AssessmentExecutionResult> RunAsync(
		Assessment assessment,
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		try
		{
			return await ExecuteDefinitionAsync(assessment, input, virtualExtension, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new AssessmentExecutionResult(
				RawScore: null,
				ExplanationText: "Assessment execution was canceled due to timeout.",
				FailureReason: AssessmentFailureReason.TimeoutExceeded,
				InnerException: null,
				VirtualExtensionOutput: null);
		}
		catch (Exception exception)
		{
			return new AssessmentExecutionResult(
				RawScore: null,
				ExplanationText: "Assessment execution failed with an exception.",
				FailureReason: AssessmentFailureReason.ThrowingException,
				InnerException: exception,
				VirtualExtensionOutput: null);
		}
	}

	private static async Task<AssessmentExecutionResult> ExecuteDefinitionAsync(
		Assessment assessment,
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		if (!assessment.AssessmentTimeout.HasValue)
		{
			return await assessment.Definition.ExecuteAsync(input, virtualExtension, cancellationToken).ConfigureAwait(false);
		}

		using CancellationTokenSource timeoutCts = new(assessment.AssessmentTimeout.Value);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			return await assessment.Definition.ExecuteAsync(input, virtualExtension, linkedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			return new AssessmentExecutionResult(
				RawScore: null,
				ExplanationText: $"Assessment exceeded timeout of {assessment.AssessmentTimeout.Value}.",
				FailureReason: AssessmentFailureReason.TimeoutExceeded,
				InnerException: null,
				VirtualExtensionOutput: null);
		}
	}
}
