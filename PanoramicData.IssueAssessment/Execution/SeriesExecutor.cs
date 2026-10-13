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
					bool shouldContinue = await previousCategory.SeriesGateCondition
						.ShouldExecuteNextAsync(priorResult, failures.ToImmutableList(), cancellationToken)
						.ConfigureAwait(false);

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