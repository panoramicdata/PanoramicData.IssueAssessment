namespace PanoramicData.IssueAssessment.Tests.Integration;

using System.Collections.Concurrent;

/// <summary>
/// Builders shared by the execution engine tests.
/// </summary>
internal static class IntegrationTestHelpers
{
	/// <summary>
	/// The longest any test definition or gate waits for cancellation before giving up, so a regression fails rather than hangs.
	/// </summary>
	public static readonly TimeSpan SafetyNet = TimeSpan.FromSeconds(30);

	public static Assessment CreateAssessment(
		string name,
		IAssessmentDefinition definition,
		double weight = 1d,
		TimeSpan? timeout = null,
		string? virtualOutputName = null)
		=> new()
		{
			Name = name,
			Description = $"{name} description",
			Definition = definition,
			Weight = weight,
			AssessmentTimeout = timeout,
			AllowVirtualExtension = virtualOutputName is not null,
			VirtualOutputName = virtualOutputName,
		};

	public static AssessmentCategory CreateCategory(
		string name,
		AssessmentExecutionStrategy strategy,
		params Assessment[] assessments)
		=> new()
		{
			Name = name,
			Description = $"{name} description",
			AssessmentExecutionStrategy = strategy,
			Assessments = [.. assessments],
		};

	public static AssessmentCategory CreateSeriesCategory(string name, params Assessment[] assessments)
		=> CreateCategory(name, AssessmentExecutionStrategy.Series, assessments);

	public static AssessmentCategory CreateParallelCategory(string name, params Assessment[] assessments)
		=> CreateCategory(name, AssessmentExecutionStrategy.Parallel, assessments);

	public static AssessmentOptions CreateOptions(CategoryExecutionStrategy strategy, params AssessmentCategory[] categories)
		=> new()
		{
			ExecutionStrategy = strategy,
			Categories = [.. categories],
		};

	public static JsonDocument ParseIssue(string json = "{\"title\":\"Issue title\"}")
		=> JsonDocument.Parse(json);

	public static IssueAssessmentResult CreateEmptyResult()
		=> new()
		{
			Categories = [],
			Failures = [],
			Aggregations = new AssessmentAggregations(
				AllAssessments: new AggregationMetrics(0, null, null, null, null),
				ByCategory: ImmutableDictionary<string, AggregationMetrics>.Empty,
				Percentiles: new PercentileMetrics(null, null, null, null),
				Weighted: new WeightedAggregationMetrics(null, 0d)),
			VirtualDocument = null,
		};
}

/// <summary>
/// An assessment definition that returns a configured score, failure and virtual output, or throws a configured exception.
/// </summary>
internal sealed class StubDefinition : IAssessmentDefinition
{
	private int _executionCount;

	public double? RawScore { get; init; } = 1d;

	public string? Explanation { get; init; } = "Stub executed.";

	public AssessmentFailureReason? FailureReason { get; init; }

	public Exception? ExceptionToThrow { get; init; }

	/// <summary>
	/// Gets JSON returned as the virtual extension output; a fresh document is parsed for every execution.
	/// </summary>
	public string? VirtualOutputJson { get; init; }

	public int ExecutionCount => Volatile.Read(ref _executionCount);

	/// <summary>
	/// Gets the raw text of each virtual extension received, in call order (null when none was passed).
	/// </summary>
	public ConcurrentQueue<string?> ReceivedVirtualExtensions { get; } = new();

	public Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref _executionCount);
		ReceivedVirtualExtensions.Enqueue(virtualExtension?.RootElement.GetRawText());

		if (ExceptionToThrow is not null)
		{
			throw ExceptionToThrow;
		}

		JsonDocument? virtualOutput = VirtualOutputJson is null ? null : JsonDocument.Parse(VirtualOutputJson);
		return Task.FromResult(new AssessmentExecutionResult(RawScore, Explanation, FailureReason, null, virtualOutput));
	}
}

/// <summary>
/// An assessment definition that waits for its token to be cancelled, so it only finishes through a timeout or cancellation.
/// </summary>
internal sealed class DelayingDefinition : IAssessmentDefinition
{
	private int _cancellationObserved;

	public bool CancellationObserved => Volatile.Read(ref _cancellationObserved) == 1;

	public async Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(IntegrationTestHelpers.SafetyNet, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Volatile.Write(ref _cancellationObserved, 1);
			throw;
		}

		return new AssessmentExecutionResult(1d, "Delay completed without cancellation.", null, null, null);
	}
}

/// <summary>
/// An assessment definition whose behaviour is supplied by the test.
/// </summary>
internal sealed class DelegateDefinition(
	Func<JsonDocument, JsonDocument?, CancellationToken, Task<AssessmentExecutionResult>> execute) : IAssessmentDefinition
{
	public Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
		=> execute(input, virtualExtension, cancellationToken);
}

/// <summary>
/// A series gate condition that returns a configured decision and records what it was given.
/// </summary>
internal sealed class RecordingGate(bool shouldContinue) : ISeriesGateCondition
{
	public List<(CategoryResult PriorCategoryResult, ImmutableList<AssessmentFailure> PriorFailures)> Calls { get; } = [];

	public Task<bool> ShouldExecuteNextAsync(
		CategoryResult priorCategoryResult,
		ImmutableList<AssessmentFailure> priorFailures,
		CancellationToken cancellationToken)
	{
		Calls.Add((priorCategoryResult, priorFailures));
		return Task.FromResult(shouldContinue);
	}
}

/// <summary>
/// A series gate condition that honours its token: it waits for cancellation and then throws.
/// </summary>
internal sealed class CancellationObservingGate : ISeriesGateCondition
{
	public async Task<bool> ShouldExecuteNextAsync(
		CategoryResult priorCategoryResult,
		ImmutableList<AssessmentFailure> priorFailures,
		CancellationToken cancellationToken)
	{
		await Task.Delay(IntegrationTestHelpers.SafetyNet, cancellationToken);
		return true;
	}
}

/// <summary>
/// A cache provider that returns a configured result and records every call.
/// </summary>
internal sealed class RecordingCacheProvider(IAssessmentResult? cachedResult = null) : IAssessmentCacheProvider
{
	public int GetCount { get; private set; }

	public int SetCount { get; private set; }

	public IAssessmentResult? LastStored { get; private set; }

	public Task<IAssessmentResult?> GetAsync(JsonDocument issue, IAssessmentOptions options, CancellationToken cancellationToken)
	{
		GetCount++;
		return Task.FromResult(cachedResult);
	}

	public Task<bool> SetAsync(JsonDocument issue, IAssessmentOptions options, IAssessmentResult result, CancellationToken cancellationToken)
	{
		SetCount++;
		LastStored = result;
		return Task.FromResult(true);
	}

	public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
