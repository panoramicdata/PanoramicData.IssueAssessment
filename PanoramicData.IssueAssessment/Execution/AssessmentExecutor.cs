namespace PanoramicData.IssueAssessment.Execution;

using PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Executes a single assessment with timeout and failure mapping.
/// </summary>
internal static class AssessmentExecutor
{
	/// <summary>
	/// Executes an assessment against <paramref name="input"/> with no rubric conditions.
	/// </summary>
	public static Task<AssessmentExecutionItemResult> ExecuteAsync(
		string categoryName,
		Assessment assessment,
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);
		return ExecuteAsync(categoryName, assessment, new EvaluationContext(input), virtualExtension, cancellationToken);
	}

	/// <summary>
	/// Executes an assessment and returns normalized result artifacts. An assessment whose
	/// <see cref="Assessment.AppliesWhen"/> is false is not run and is reported as not applicable.
	/// </summary>
	public static async Task<AssessmentExecutionItemResult> ExecuteAsync(
		string categoryName,
		Assessment assessment,
		EvaluationContext context,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(categoryName);
		ArgumentNullException.ThrowIfNull(assessment);
		ArgumentNullException.ThrowIfNull(context);

		EvaluationContext scoped = context.WithVirtualExtension(virtualExtension);
		AssessmentExecutionResult? executionResult = await RunAsync(assessment, scoped, cancellationToken).ConfigureAwait(false);
		if (executionResult is null)
		{
			return NotApplicable(assessment);
		}

		(double minPossibleRaw, double maxPossibleRaw) = ScoreCalculator.CalculateAssessmentRawRange(assessment);
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
		if (assessmentResult.Outcome != AssessmentOutcome.Passed)
		{
			assessmentResult = assessmentResult with { Recommendation = executionResult.Recommendation ?? assessment.Recommendation };
		}

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

	private static AssessmentExecutionItemResult NotApplicable(Assessment assessment)
		=> new(
			AssessmentResult: new AssessmentResult(
				AssessmentName: assessment.Name,
				Score: new AssessmentScore(null, null, 0d, 0d),
				ExplanationText: assessment.NotApplicableExplanation ?? string.Empty,
				FailureReason: null,
				InnerException: null)
			{
				Outcome = AssessmentOutcome.NotApplicable,
			},
			Failure: null,
			VirtualDocument: null);

	/// <summary>
	/// Checks applicability, then runs the definition, turning a cancellation into a timeout result and any other
	/// exception into a failure result. Returns null when the assessment does not apply.
	/// </summary>
	private static async Task<AssessmentExecutionResult?> RunAsync(
		Assessment assessment,
		EvaluationContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			if (assessment.AppliesWhen is { } appliesWhen && !appliesWhen.Evaluate(context))
			{
				return null;
			}

			return await ExecuteDefinitionAsync(assessment, context, cancellationToken).ConfigureAwait(false);
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
		EvaluationContext context,
		CancellationToken cancellationToken)
	{
		if (!assessment.AssessmentTimeout.HasValue)
		{
			return await InvokeAsync(assessment.Definition, context, cancellationToken).ConfigureAwait(false);
		}

		using CancellationTokenSource timeoutCts = new(assessment.AssessmentTimeout.Value);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			return await InvokeAsync(assessment.Definition, context, linkedCts.Token).ConfigureAwait(false);
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

	private static Task<AssessmentExecutionResult> InvokeAsync(
		IAssessmentDefinition definition,
		EvaluationContext context,
		CancellationToken cancellationToken)
		=> definition is IContextualAssessmentDefinition contextual
			? contextual.ExecuteAsync(context, cancellationToken)
			: definition.ExecuteAsync(context.Issue, context.VirtualExtension, cancellationToken);
}
