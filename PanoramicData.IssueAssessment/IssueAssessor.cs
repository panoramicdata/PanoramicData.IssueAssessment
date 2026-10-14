using System.Runtime.CompilerServices;
using PanoramicData.IssueAssessment.Aggregation;
using PanoramicData.IssueAssessment.Execution;

namespace PanoramicData.IssueAssessment;
/// <summary>
/// Main entry point for issue assessment execution.
/// </summary>
public class IssueAssessor
{
	private readonly IAssessmentOptions _options;
	private readonly IAssessmentCacheProvider? _cacheProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="IssueAssessor"/> class without a cache.
	/// </summary>
	/// <param name="options">Assessment options used by this assessor.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when options contain invalid values.</exception>
	/// <exception cref="InvalidOperationException">Thrown when option combinations are invalid.</exception>
	public IssueAssessor(IAssessmentOptions options)
	{
		OptionsValidator.Validate(options);
		_options = options;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IssueAssessor"/> class with a cache.
	/// </summary>
	/// <param name="options">Assessment options used by this assessor.</param>
	/// <param name="cacheProvider">Cache provider for storing and retrieving assessment results.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="cacheProvider"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when options contain invalid values.</exception>
	/// <exception cref="InvalidOperationException">Thrown when option combinations are invalid.</exception>
	public IssueAssessor(IAssessmentOptions options, IAssessmentCacheProvider cacheProvider)
		: this(options)
	{
		ArgumentNullException.ThrowIfNull(cacheProvider);
		_cacheProvider = cacheProvider;
	}

	/// <summary>
	/// Assesses a single issue.
	/// </summary>
	/// <param name="issue">The issue document to assess.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The assessment result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="issue"/> is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
	public async Task<IAssessmentResult> AssessAsync(JsonDocument issue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(issue);
		cancellationToken.ThrowIfCancellationRequested();

		if (_cacheProvider is not null
			&& await _cacheProvider.GetAsync(issue, _options, cancellationToken).ConfigureAwait(false) is { } cached)
		{
			return cached;
		}

		IAssessmentResult result = await ExecuteAsync(issue, cancellationToken).ConfigureAwait(false);

		if (_cacheProvider is not null)
		{
			_ = await _cacheProvider.SetAsync(issue, _options, result, cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	/// <summary>
	/// Assesses many issues from an asynchronous sequence.
	/// </summary>
	/// <param name="issues">The issue sequence.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>An asynchronous sequence of assessment results.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="issues"/> is null.</exception>
	public async IAsyncEnumerable<IAssessmentResult> AssessManyAsync(
		IAsyncEnumerable<JsonDocument> issues,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(issues);

		await foreach (JsonDocument issue in issues.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return await AssessAsync(issue, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task<IAssessmentResult> ExecuteAsync(JsonDocument issue, CancellationToken cancellationToken)
	{
		using CancellationTokenSource? globalTimeoutCts = _options.GlobalTimeout.HasValue
			? new CancellationTokenSource(_options.GlobalTimeout.Value)
			: null;
		using CancellationTokenSource linkedCts = globalTimeoutCts is null
			? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
			: CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalTimeoutCts.Token);

		EvaluationContext context = new(issue, _options.Conditions);
		ExecutionResult executionResult = _options.ExecutionStrategy == CategoryExecutionStrategy.Parallel
			? await ParallelExecutor.ExecuteAsync(_options, context, linkedCts.Token).ConfigureAwait(false)
			: await SeriesExecutor.ExecuteAsync(_options, context, linkedCts.Token).ConfigureAwait(false);

		// Executors record a cancelled assessment as a timeout failure so that the global timeout yields a partial
		// result. Cancellation requested by the caller is not a timeout: surface it rather than return that result.
		cancellationToken.ThrowIfCancellationRequested();

		return new IssueAssessmentResult
		{
			Categories = executionResult.Categories,
			Failures = executionResult.Failures,
			Aggregations = AggregationCalculator.Calculate(executionResult.Categories),
			VirtualDocument = _options.CaptureVirtualDocument ? executionResult.VirtualDocument : null,
			Overall = OverallCalculator.Calculate(_options, context, executionResult.Categories),
		};
	}
}
