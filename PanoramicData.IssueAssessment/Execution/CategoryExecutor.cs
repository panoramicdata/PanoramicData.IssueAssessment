namespace PanoramicData.IssueAssessment.Execution;

using PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Executes a category and materializes category-level scoring.
/// </summary>
internal static class CategoryExecutor
{
	/// <summary>
	/// Executes category assessments and returns category output.
	/// </summary>
	public static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteAsync(
		AssessmentCategory category,
		JsonDocument input,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy defaultFailureHandling,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(category);
		ArgumentNullException.ThrowIfNull(input);

		FailureHandlingStrategy effectiveFailureHandling = category.CategoryFailureHandling;
		if (effectiveFailureHandling != FailureHandlingStrategy.FailFast && effectiveFailureHandling != FailureHandlingStrategy.CollectAll)
		{
			effectiveFailureHandling = defaultFailureHandling;
		}

		if (!category.CategoryTimeout.HasValue)
		{
			if (category.AssessmentExecutionStrategy == AssessmentExecutionStrategy.Parallel)
			{
				return await ExecuteParallelAsync(category, input, incomingVirtualDocument, effectiveFailureHandling, cancellationToken)
					.ConfigureAwait(false);
			}

			return await ExecuteSeriesAsync(category, input, incomingVirtualDocument, effectiveFailureHandling, cancellationToken)
				.ConfigureAwait(false);
		}

		using CancellationTokenSource timeoutCts = new(category.CategoryTimeout.Value);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			if (category.AssessmentExecutionStrategy == AssessmentExecutionStrategy.Parallel)
			{
				return await ExecuteParallelAsync(category, input, incomingVirtualDocument, effectiveFailureHandling, linkedCts.Token)
					.ConfigureAwait(false);
			}

			return await ExecuteSeriesAsync(category, input, incomingVirtualDocument, effectiveFailureHandling, linkedCts.Token)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			AssessmentFailure failure = new(
				CategoryName: category.Name,
				AssessmentName: "CategoryTimeout",
				Reason: AssessmentFailureReason.TimeoutExceeded,
				Message: $"Category '{category.Name}' exceeded timeout of {category.CategoryTimeout.Value}.",
				InnerException: null);

			CategoryResult timeoutResult = BuildCategoryResult(category, ImmutableList<AssessmentResult>.Empty);
			return (timeoutResult, ImmutableList.Create(failure), incomingVirtualDocument);
		}
	}

	private static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteSeriesAsync(
		AssessmentCategory category,
		JsonDocument input,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy failureHandling,
		CancellationToken cancellationToken)
	{
		List<AssessmentResult> assessmentResults = new(category.Assessments.Count);
		List<AssessmentFailure> failures = new();
		JsonDocument? virtualDocument = incomingVirtualDocument;

		foreach (Assessment assessment in category.Assessments)
		{
			AssessmentExecutionItemResult item = await AssessmentExecutor.ExecuteAsync(
				category.Name,
				assessment,
				input,
				virtualDocument,
				cancellationToken).ConfigureAwait(false);

			assessmentResults.Add(item.AssessmentResult);
			if (item.Failure is not null)
			{
				failures.Add(item.Failure);
				if (failureHandling == FailureHandlingStrategy.FailFast)
				{
					break;
				}
			}

			if (assessment.AllowVirtualExtension && item.VirtualDocument is not null)
			{
				virtualDocument = VirtualDocumentFinalizer.Merge(virtualDocument, item.VirtualDocument);
			}
		}

		CategoryResult categoryResult = BuildCategoryResult(category, assessmentResults);
		return (categoryResult, failures.ToImmutableList(), virtualDocument);
	}

	private static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteParallelAsync(
		AssessmentCategory category,
		JsonDocument input,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy failureHandling,
		CancellationToken cancellationToken)
	{
		Task<AssessmentExecutionItemResult>[] tasks = category.Assessments
			.Select(assessment => AssessmentExecutor.ExecuteAsync(category.Name, assessment, input, incomingVirtualDocument, cancellationToken))
			.ToArray();

		AssessmentExecutionItemResult[] executed = await Task.WhenAll(tasks).ConfigureAwait(false);

		List<AssessmentFailure> failures = executed
			.Where(item => item.Failure is not null)
			.Select(item => item.Failure!)
			.ToList();

		ImmutableList<AssessmentResult> assessmentResults = executed
			.Select(item => item.AssessmentResult)
			.ToImmutableList();

		if (failureHandling == FailureHandlingStrategy.FailFast && failures.Count > 1)
		{
			failures = new List<AssessmentFailure> { failures[0] };
		}

		CategoryResult categoryResult = BuildCategoryResult(category, assessmentResults);
		return (categoryResult, failures.ToImmutableList(), incomingVirtualDocument);
	}

	private static CategoryResult BuildCategoryResult(AssessmentCategory category, IEnumerable<AssessmentResult> assessmentResults)
	{
		ImmutableList<AssessmentResult> resultList = assessmentResults.ToImmutableList();
		(double minPossibleRaw, double maxPossibleRaw) = ScoreCalculator.CalculateCategoryRawRange(category);

		double? rawScore = null;
		if (resultList.Count > 0 && resultList.All(result => result.Score.RawScore.HasValue))
		{
			rawScore = resultList
				.Zip(category.Assessments, (result, assessment) => result.Score.RawScore!.Value * assessment.Weight)
				.Sum();
		}

		double? normalizedScore = rawScore.HasValue
			? ScoreCalculator.Normalize(rawScore.Value, minPossibleRaw, maxPossibleRaw)
			: null;

		AssessmentScore score = new(
			RawScore: rawScore,
			NormalizedScore: normalizedScore,
			MinPossibleRaw: minPossibleRaw,
			MaxPossibleRaw: maxPossibleRaw);

		string reason = $"Executed {resultList.Count} assessment(s) in category '{category.Name}'.";
		return new CategoryResult(category.Name, score, reason, resultList);
	}
}
