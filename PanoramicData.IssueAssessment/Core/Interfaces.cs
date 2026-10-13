namespace PanoramicData.IssueAssessment.Core;
/// <summary>
/// Defines immutable options used to configure issue assessments.
/// </summary>
public interface IAssessmentOptions
{
	/// <summary>
	/// Gets the configured assessment categories.
	/// </summary>
	ImmutableList<AssessmentCategory> Categories { get; }

	/// <summary>
	/// Gets the strategy used to execute categories.
	/// </summary>
	CategoryExecutionStrategy ExecutionStrategy { get; }

	/// <summary>
	/// Gets an optional global timeout for a full assessment run.
	/// </summary>
	TimeSpan? GlobalTimeout { get; }

	/// <summary>
	/// Gets the failure handling mode used during execution.
	/// </summary>
	FailureHandlingStrategy FailureHandling { get; }

	/// <summary>
	/// Gets a value indicating whether the final virtual document should be captured in results.
	/// </summary>
	bool CaptureVirtualDocument { get; }

	/// <summary>
	/// Gets named conditions that rules refer to with a <see cref="ConditionReference"/>, and that are reported as the
	/// result's flags.
	/// </summary>
	ImmutableDictionary<string, ICondition> Conditions => ImmutableDictionary<string, ICondition>.Empty;

	/// <summary>
	/// Gets caps on the overall score, applied when their condition holds: for example, a missing mandatory element
	/// holds the score below the top grade.
	/// </summary>
	ImmutableList<ScoreCap> ScoreCaps => [];

	/// <summary>
	/// Gets the grade bands the overall score is graded by.
	/// </summary>
	ImmutableList<GradeBand> GradeBands => [];

	/// <summary>
	/// Gets classifications: for each name, rules evaluated in order whose first match gives the value.
	/// </summary>
	ImmutableDictionary<string, ImmutableList<ClassificationRule>> Classifications
		=> ImmutableDictionary<string, ImmutableList<ClassificationRule>>.Empty;
}

/// <summary>
/// Defines an executable assessment implementation.
/// </summary>
public interface IAssessmentDefinition
{
	/// <summary>
	/// Executes assessment logic against the provided input issue and optional virtual extension document.
	/// </summary>
	/// <param name="input">The source issue document.</param>
	/// <param name="virtualExtension">The optional virtual extension document produced by prior series assessments.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The execution result.</returns>
	Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken);
}

/// <summary>
/// An assessment definition that can use the <see cref="EvaluationContext"/>: the rubric's named conditions and the
/// cached text of the issue. The assessor calls this overload instead of
/// <see cref="IAssessmentDefinition.ExecuteAsync(JsonDocument, JsonDocument?, CancellationToken)"/>.
/// </summary>
public interface IContextualAssessmentDefinition
{
	/// <summary>
	/// Executes the assessment against the issue and virtual extension in <paramref name="context"/>.
	/// </summary>
	/// <param name="context">The evaluation context.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The execution result.</returns>
	Task<AssessmentExecutionResult> ExecuteAsync(EvaluationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Marker interface for assessment definitions that operate on a JSONPath selector.
/// </summary>
public interface IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	string JsonPath { get; }
}

/// <summary>
/// Marker interface for assessment definitions that include a regex pattern.
/// </summary>
public interface IRegexPatternAssessmentDefinition
{
	/// <summary>
	/// Gets the regex pattern.
	/// </summary>
	string Pattern { get; }
}

/// <summary>
/// Defines a gate condition that controls whether the next series category should execute.
/// </summary>
public interface ISeriesGateCondition
{
	/// <summary>
	/// Determines whether execution should continue to the next category.
	/// </summary>
	/// <param name="priorCategoryResult">The prior category result.</param>
	/// <param name="priorFailures">Failures collected so far.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> when execution should continue; otherwise <see langword="false"/>.</returns>
	Task<bool> ShouldExecuteNextAsync(
		CategoryResult priorCategoryResult,
		ImmutableList<AssessmentFailure> priorFailures,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides optional cache storage for assessment results.
/// </summary>
public interface IAssessmentCacheProvider
{
	/// <summary>
	/// Attempts to get a cached assessment result for a given issue and options combination.
	/// </summary>
	Task<IAssessmentResult?> GetAsync(
		JsonDocument issue,
		IAssessmentOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stores an assessment result in cache.
	/// </summary>
	Task<bool> SetAsync(
		JsonDocument issue,
		IAssessmentOptions options,
		IAssessmentResult result,
		CancellationToken cancellationToken);

	/// <summary>
	/// Clears all cached results.
	/// </summary>
	Task ClearAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Represents the final output of an issue assessment run.
/// </summary>
public interface IAssessmentResult
{
	/// <summary>
	/// Gets per-category results.
	/// </summary>
	ImmutableList<CategoryResult> Categories { get; }

	/// <summary>
	/// Gets failures collected during assessment execution.
	/// </summary>
	ImmutableList<AssessmentFailure> Failures { get; }

	/// <summary>
	/// Gets roll-up aggregation metrics.
	/// </summary>
	AssessmentAggregations Aggregations { get; }

	/// <summary>
	/// Gets the final virtual document, if captured.
	/// </summary>
	JsonDocument? VirtualDocument { get; }

	/// <summary>
	/// Gets the overall verdict: the weighted score across categories after any score caps, its grade, and the
	/// rubric's flags and classifications. Null for results produced before 1.1 or by custom implementations.
	/// </summary>
	AssessmentOverall? Overall => null;
}

/// <summary>
/// Defines how categories should be executed.
/// </summary>
public enum CategoryExecutionStrategy
{
	/// <summary>
	/// Execute categories in parallel.
	/// </summary>
	Parallel,

	/// <summary>
	/// Execute categories in sequence.
	/// </summary>
	Series,
}

/// <summary>
/// Defines how assessments in a category should be executed.
/// </summary>
public enum AssessmentExecutionStrategy
{
	/// <summary>
	/// Execute assessments in parallel.
	/// </summary>
	Parallel,

	/// <summary>
	/// Execute assessments in sequence.
	/// </summary>
	Series,
}

/// <summary>
/// Defines how failures should be handled during execution.
/// </summary>
public enum FailureHandlingStrategy
{
	/// <summary>
	/// Stop execution on first failure.
	/// </summary>
	FailFast,

	/// <summary>
	/// Continue execution and collect all failures.
	/// </summary>
	CollectAll,
}

/// <summary>
/// Identifies a failure reason for an assessment.
/// </summary>
public enum AssessmentFailureReason
{
	/// <summary>
	/// The JSONPath did not resolve to a value.
	/// </summary>
	JsonPathNotFound,

	/// <summary>
	/// The regex pattern was invalid.
	/// </summary>
	InvalidRegex,

	/// <summary>
	/// The JSONPath expression was invalid.
	/// </summary>
	InvalidJsonPath,

	/// <summary>
	/// A timeout was exceeded.
	/// </summary>
	TimeoutExceeded,

	/// <summary>
	/// An exception was thrown by assessment logic.
	/// </summary>
	ThrowingException,

	/// <summary>
	/// A virtual extension was attempted from a parallel execution context.
	/// </summary>
	VirtualExtensionAttemptedInParallel,

	/// <summary>
	/// A configuration error was detected.
	/// </summary>
	ConfigurationError,
}
