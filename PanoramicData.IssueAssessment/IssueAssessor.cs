
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
	/// Initializes a new instance of the <see cref="IssueAssessor"/> class.
	/// </summary>
	/// <param name="options">Assessment options used by this assessor.</param>
	/// <param name="cacheProvider">Optional cache provider for storing and retrieving assessment results.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when options contain invalid values.</exception>
	/// <exception cref="InvalidOperationException">Thrown when option combinations are invalid.</exception>
	public IssueAssessor(IAssessmentOptions options, IAssessmentCacheProvider? cacheProvider = null)
	{
		OptionsValidator.Validate(options);
		_options = options;
		_cacheProvider = cacheProvider;
	}

	/// <summary>
	/// Assesses a single issue.
	/// </summary>
	/// <param name="issue">The issue document to assess.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The assessment result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="issue"/> is null.</exception>
	public async Task<IAssessmentResult> AssessAsync(JsonDocument issue, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(issue);
		cancellationToken.ThrowIfCancellationRequested();

		if (_cacheProvider is not null)
		{
			IAssessmentResult? cached = await _cacheProvider.GetAsync(issue, _options, cancellationToken).ConfigureAwait(false);
			if (cached is not null)
			{
				return cached;
			}
		}

		using CancellationTokenSource? globalTimeoutCts = _options.GlobalTimeout.HasValue
			? new CancellationTokenSource(_options.GlobalTimeout.Value)
			: null;
		using CancellationTokenSource linkedCts = globalTimeoutCts is null
			? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
			: CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalTimeoutCts.Token);

		ExecutionResult executionResult = _options.ExecutionStrategy == CategoryExecutionStrategy.Parallel
			? await ParallelExecutor.ExecuteAsync(_options, issue, linkedCts.Token).ConfigureAwait(false)
			: await SeriesExecutor.ExecuteAsync(_options, issue, linkedCts.Token).ConfigureAwait(false);

		AssessmentAggregations aggregations = AggregationCalculator.Calculate(executionResult.Categories);
		JsonDocument? virtualDocument = _options.CaptureVirtualDocument ? executionResult.VirtualDocument : null;

		IssueAssessmentResult result = new()
		{
			Categories = executionResult.Categories,
			Failures = executionResult.Failures,
			Aggregations = aggregations,
			VirtualDocument = virtualDocument,
		};

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
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(issues);

		await foreach (JsonDocument issue in issues.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return await AssessAsync(issue, cancellationToken).ConfigureAwait(false);
		}
	}
}
