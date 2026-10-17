namespace PanoramicData.IssueAssessment.Execution;

using PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Executes a category and materializes category-level scoring.
/// </summary>
internal static class CategoryExecutor
{
	/// <summary>
	/// Executes category assessments against <paramref name="input"/> with no rubric conditions.
	/// </summary>
	public static Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteAsync(
		AssessmentCategory category,
		JsonDocument input,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy defaultFailureHandling,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);
		return ExecuteAsync(category, new EvaluationContext(input), incomingVirtualDocument, defaultFailureHandling, cancellationToken);
	}

	/// <summary>
	/// Executes category assessments and returns category output. A category whose
	/// <see cref="AssessmentCategory.AppliesWhen"/> is false is not run and is reported as not applicable.
	/// </summary>
	public static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteAsync(
		AssessmentCategory category,
		EvaluationContext context,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy defaultFailureHandling,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(category);
		ArgumentNullException.ThrowIfNull(context);

		if (category.AppliesWhen is { } appliesWhen && !appliesWhen.Evaluate(context))
		{
			return (NotApplicable(category), [], incomingVirtualDocument);
		}

		FailureHandlingStrategy effectiveFailureHandling = category.CategoryFailureHandling ?? defaultFailureHandling;

		// AssessmentExecutor records a cancelled assessment as a TimeoutExceeded result rather than throwing, so a category
		// timeout surfaces as a timeout failure on each assessment it cut short and needs no handler of its own here.
		using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		if (category.CategoryTimeout is { } timeout)
		{
			timeoutSource.CancelAfter(timeout);
		}

		return category.AssessmentExecutionStrategy == AssessmentExecutionStrategy.Parallel
			? await ExecuteParallelAsync(category, context, incomingVirtualDocument, effectiveFailureHandling, timeoutSource.Token)
				.ConfigureAwait(false)
			: await ExecuteSeriesAsync(category, context, incomingVirtualDocument, effectiveFailureHandling, timeoutSource.Token)
				.ConfigureAwait(false);
	}

	private static CategoryResult NotApplicable(AssessmentCategory category)
		=> new(category.Name, new AssessmentScore(null, null, 0d, 0d), $"Category '{category.Name}' does not apply.", [])
		{
			Outcome = AssessmentOutcome.NotApplicable,
			Weight = category.Weight,
		};

	private static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteSeriesAsync(
		AssessmentCategory category,
		EvaluationContext context,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy failureHandling,
		CancellationToken cancellationToken)
	{
		List<AssessmentResult> assessmentResults = new(category.Assessments.Count);
		List<AssessmentFailure> failures = [];
		JsonDocument? virtualDocument = incomingVirtualDocument;

		foreach (Assessment assessment in category.Assessments)
		{
			AssessmentExecutionItemResult item = await AssessmentExecutor.ExecuteAsync(
				category.Name,
				assessment,
				context,
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
				// The output is stored under the assessment's VirtualOutputName, so later assessments read it as
				// $.<VirtualOutputName>. The validator guarantees names are unique, so outputs never collide.
				using JsonDocument named = VirtualDocumentFinalizer.Wrap(assessment.VirtualOutputName!, item.VirtualDocument);
				virtualDocument = VirtualDocumentFinalizer.Merge(virtualDocument, named);
			}
		}

		CategoryResult categoryResult = BuildCategoryResult(category, assessmentResults);
		return (categoryResult, [.. failures], virtualDocument);
	}

	private static async Task<(CategoryResult Category, ImmutableList<AssessmentFailure> Failures, JsonDocument? VirtualDocument)> ExecuteParallelAsync(
		AssessmentCategory category,
		EvaluationContext context,
		JsonDocument? incomingVirtualDocument,
		FailureHandlingStrategy failureHandling,
		CancellationToken cancellationToken)
	{
		Task<AssessmentExecutionItemResult>[] tasks = [.. category.Assessments
			.Select(assessment => AssessmentExecutor.ExecuteAsync(category.Name, assessment, context, incomingVirtualDocument, cancellationToken))];

		AssessmentExecutionItemResult[] executed = await Task.WhenAll(tasks).ConfigureAwait(false);

		List<AssessmentFailure> failures = [.. executed.Where(item => item.Failure is not null).Select(item => item.Failure!)];
		if (failureHandling == FailureHandlingStrategy.FailFast && failures.Count > 1)
		{
			failures = [failures[0]];
		}

		CategoryResult categoryResult = BuildCategoryResult(category, executed.Select(item => item.AssessmentResult));
		return (categoryResult, [.. failures], incomingVirtualDocument);
	}

	/// <summary>
	/// Scores the category over the assessments that applied: those reported not applicable are left out of both the
	/// weighted raw score and its range, so the remaining weights are renormalised.
	/// </summary>
	private static CategoryResult BuildCategoryResult(AssessmentCategory category, IEnumerable<AssessmentResult> assessmentResults)
	{
		ImmutableList<AssessmentResult> resultList = [.. assessmentResults];
		HashSet<int> notApplicable = [.. resultList
			.Select((result, index) => (result, index))
			.Where(pair => pair.result.Outcome == AssessmentOutcome.NotApplicable)
			.Select(pair => pair.index)];

		if (resultList.Count > 0 && notApplicable.Count == resultList.Count && resultList.Count == category.Assessments.Count)
		{
			return NotApplicable(category) with
			{
				Reason = $"No assessment in category '{category.Name}' applies.",
				Assessments = resultList,
			};
		}

		(double minPossibleRaw, double maxPossibleRaw) = ScoreCalculator.CalculateCategoryRawRange(
			category.Assessments.Where((_, index) => !notApplicable.Contains(index)));

		List<(AssessmentResult Result, Assessment Assessment)> applicable = [.. resultList
			.Zip(category.Assessments)
			.Where(pair => pair.First.Outcome != AssessmentOutcome.NotApplicable)];

		double? rawScore = WeightedRawScore(applicable);
		double? normalizedScore = rawScore.HasValue
			? ScoreCalculator.Normalize(rawScore.Value, minPossibleRaw, maxPossibleRaw)
			: null;

		AssessmentScore score = new(
			RawScore: rawScore,
			NormalizedScore: normalizedScore,
			MinPossibleRaw: minPossibleRaw,
			MaxPossibleRaw: maxPossibleRaw);

		string reason = $"Executed {resultList.Count} assessment(s) in category '{category.Name}'.";
		return new CategoryResult(category.Name, score, reason, resultList) { Weight = category.Weight };
	}

	/// <summary>
	/// The weighted sum of the applicable assessments' raw scores; null when none applies or any of them has no score.
	/// </summary>
	private static double? WeightedRawScore(List<(AssessmentResult Result, Assessment Assessment)> applicable)
		=> applicable.Count > 0 && applicable.TrueForAll(pair => pair.Result.Score.RawScore.HasValue)
			? applicable.Sum(pair => pair.Result.Score.RawScore!.Value * pair.Assessment.Weight)
			: null;
}
