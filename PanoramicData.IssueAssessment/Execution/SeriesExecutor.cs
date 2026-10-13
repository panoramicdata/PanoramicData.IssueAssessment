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
		List<AssessmentFailure> failures = new();
		JsonDocument? virtualDocument = null;

		for (int index = 0; index < options.Categories.Count; index++)
		{
			AssessmentCategory category = options.Categories[index];

			if (index > 0)
			{
				AssessmentCategory previousCategory = options.Categories[index - 1];
				if (previousCategory.SeriesGateCondition is not null)
				{
					CategoryResult priorResult = categories[index - 1];
					bool shouldContinue;
					try
					{
						shouldContinue = await previousCategory.SeriesGateCondition
							.ShouldExecuteNextAsync(priorResult, failures.ToImmutableList(), cancellationToken)
							.ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
						// Like a cancelled assessment, a cancelled gate is recorded as a timeout so a global timeout yields a
						// partial result; the assessor rethrows when the caller asked for the cancellation.
						failures.Add(new AssessmentFailure(
							CategoryName: previousCategory.Name,
							AssessmentName: nameof(AssessmentCategory.SeriesGateCondition),
							Reason: AssessmentFailureReason.TimeoutExceeded,
							Message: $"Series gate condition of category '{previousCategory.Name}' was canceled due to timeout.",
							InnerException: null));
						break;
					}
					catch (Exception exception) when (exception is not OperationCanceledException)
					{
						// A gate that throws is recorded like an assessment that throws; the series stops, as nothing says
						// whether it should have continued.
						failures.Add(new AssessmentFailure(
							CategoryName: previousCategory.Name,
							AssessmentName: nameof(AssessmentCategory.SeriesGateCondition),
							Reason: AssessmentFailureReason.ThrowingException,
							Message: $"Series gate condition of category '{previousCategory.Name}' threw an exception.",
							InnerException: exception));
						break;
					}

					if (!shouldContinue)
					{
						break;
					}
				}
			}

			(CategoryResult categoryResult, ImmutableList<AssessmentFailure> categoryFailures, JsonDocument? categoryVirtualDocument) =
				await CategoryExecutor.ExecuteAsync(category, input, virtualDocument, options.FailureHandling, cancellationToken)
					.ConfigureAwait(false);

			categories.Add(categoryResult);
			failures.AddRange(categoryFailures);
			virtualDocument = categoryVirtualDocument;

			if (options.FailureHandling == FailureHandlingStrategy.FailFast && categoryFailures.Count > 0)
			{
				break;
			}
		}

		return new ExecutionResult(categories.ToImmutableList(), failures.ToImmutableList(), virtualDocument);
	}
}
