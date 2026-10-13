namespace PanoramicData.IssueAssessment.Execution;

/// <summary>
/// Executes categories in sequence.
/// </summary>
internal static class SeriesExecutor
{
	/// <summary>
	/// Executes categories in series and returns execution artifacts.
	/// </summary>
	public static async Task<ExecutionResult> ExecuteAsync(
		IAssessmentOptions options,
		JsonDocument input,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(input);

		List<CategoryResult> categories = new(options.Categories.Count);
		List<AssessmentFailure> failures = [];
		JsonDocument? virtualDocument = null;

		for (int index = 0; index < options.Categories.Count; index++)
		{
			AssessmentCategory category = options.Categories[index];
			(CategoryResult categoryResult, ImmutableList<AssessmentFailure> categoryFailures, JsonDocument? categoryVirtualDocument) =
				await CategoryExecutor.ExecuteAsync(category, input, virtualDocument, options.FailureHandling, cancellationToken)
					.ConfigureAwait(false);

			categories.Add(categoryResult);
			failures.AddRange(categoryFailures);
			virtualDocument = categoryVirtualDocument;

			bool isLast = index == options.Categories.Count - 1;
			bool stopOnFailure = options.FailureHandling == FailureHandlingStrategy.FailFast && categoryFailures.Count > 0;
			if (isLast || stopOnFailure || !await ShouldContinueAsync(category, categoryResult, failures, cancellationToken).ConfigureAwait(false))
			{
				break;
			}
		}

		return new ExecutionResult([.. categories], [.. failures], virtualDocument);
	}

	/// <summary>
	/// Asks the category's gate, if it has one, whether the remaining categories should run. A gate that is cancelled
	/// or throws is recorded as a failure and stops the series.
	/// </summary>
	private static async Task<bool> ShouldContinueAsync(
		AssessmentCategory category,
		CategoryResult categoryResult,
		List<AssessmentFailure> failures,
		CancellationToken cancellationToken)
	{
		if (category.SeriesGateCondition is null)
		{
			return true;
		}

		try
		{
			return await category.SeriesGateCondition
				.ShouldExecuteNextAsync(categoryResult, [.. failures], cancellationToken)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Like a cancelled assessment, a cancelled gate is recorded as a timeout so a global timeout yields a partial
			// result; the assessor rethrows when the caller asked for the cancellation.
			failures.Add(GateFailure(category, AssessmentFailureReason.TimeoutExceeded, "was canceled due to timeout", null));
			return false;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// A gate that throws is recorded like an assessment that throws; the series stops, as nothing says whether
			// it should have continued.
			failures.Add(GateFailure(category, AssessmentFailureReason.ThrowingException, "threw an exception", exception));
			return false;
		}
	}

	private static AssessmentFailure GateFailure(
		AssessmentCategory category,
		AssessmentFailureReason reason,
		string what,
		Exception? exception)
		=> new(
			CategoryName: category.Name,
			AssessmentName: nameof(AssessmentCategory.SeriesGateCondition),
			Reason: reason,
			Message: $"Series gate condition of category '{category.Name}' {what}.",
			InnerException: exception);
}
