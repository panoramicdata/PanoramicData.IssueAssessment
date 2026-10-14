namespace PanoramicData.IssueAssessment.Execution;

/// <summary>
/// Executes categories in parallel.
/// </summary>
internal static class ParallelExecutor
{
	/// <summary>
	/// Executes categories in parallel and returns execution artifacts.
	/// </summary>
	public static async Task<ExecutionResult> ExecuteAsync(
		IAssessmentOptions options,
		EvaluationContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(context);

		Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)>[] tasks = options.Categories
			.Select(category => CategoryExecutor.ExecuteAsync(category, context, null, options.FailureHandling, cancellationToken))
			.ToArray();

		(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)[] executed =
			await Task.WhenAll(tasks).ConfigureAwait(false);

		ImmutableList<CategoryResult> categories = executed.Select(item => item.Category).ToImmutableList();
		ImmutableList<AssessmentFailure> failures = executed.SelectMany(item => item.Failures).ToImmutableList();

		if (options.FailureHandling == FailureHandlingStrategy.FailFast && failures.Count > 1)
		{
			failures = ImmutableList.Create(failures[0]);
		}

		return new ExecutionResult(categories, failures, null);
	}
}
