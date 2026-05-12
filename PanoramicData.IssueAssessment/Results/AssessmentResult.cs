namespace PanoramicData.IssueAssessment.Results;

/// <summary>
/// Represents the result of one assessment execution.
/// </summary>
/// <param name="AssessmentName">Assessment name.</param>
/// <param name="Score">Assessment score details.</param>
/// <param name="ExplanationText">Human-readable explanation text.</param>
/// <param name="FailureReason">Optional failure reason.</param>
/// <param name="InnerException">Optional exception captured during execution.</param>
public sealed record AssessmentResult(
	string AssessmentName,
	AssessmentScore Score,
	string ExplanationText,
	AssessmentFailureReason? FailureReason,
	Exception? InnerException);

/// <summary>
/// Represents the aggregated result for one category.
/// </summary>
/// <param name="CategoryName">Category name.</param>
/// <param name="Score">Category score details.</param>
/// <param name="Reason">Reason text for category score.</param>
/// <param name="Assessments">Assessment results in this category.</param>
public sealed record CategoryResult(
	string CategoryName,
	AssessmentScore Score,
	string Reason,
	ImmutableList<AssessmentResult> Assessments);

/// <summary>
/// Represents score information including normalization bounds.
/// </summary>
/// <param name="RawScore">Raw score value before normalization.</param>
/// <param name="NormalizedScore">Score normalized to a comparable range.</param>
/// <param name="MinPossibleRaw">Minimum possible raw score for context.</param>
/// <param name="MaxPossibleRaw">Maximum possible raw score for context.</param>
public sealed record AssessmentScore(
	double? RawScore,
	double? NormalizedScore,
	double MinPossibleRaw,
	double MaxPossibleRaw);

/// <summary>
/// Represents the low-level output of an assessment definition execution.
/// </summary>
/// <param name="RawScore">Raw score from the assessment.</param>
/// <param name="ExplanationText">Explanation text from the assessment.</param>
/// <param name="FailureReason">Optional failure reason.</param>
/// <param name="InnerException">Optional exception captured during execution.</param>
/// <param name="VirtualExtensionOutput">Optional virtual extension document produced by the assessment.</param>
public sealed record AssessmentExecutionResult(
	double? RawScore,
	string? ExplanationText,
	AssessmentFailureReason? FailureReason,
	Exception? InnerException,
	JsonDocument? VirtualExtensionOutput);

/// <summary>
/// Represents a failure captured during execution.
/// </summary>
/// <param name="CategoryName">Category name.</param>
/// <param name="AssessmentName">Assessment name.</param>
/// <param name="Reason">Failure reason.</param>
/// <param name="Message">Failure message.</param>
/// <param name="InnerException">Optional inner exception.</param>
public sealed record AssessmentFailure(
	string CategoryName,
	string AssessmentName,
	AssessmentFailureReason Reason,
	string Message,
	Exception? InnerException);

/// <summary>
/// Represents aggregate metrics for a group of scores.
/// </summary>
/// <param name="Count">Count of values included.</param>
/// <param name="Minimum">Minimum value.</param>
/// <param name="Maximum">Maximum value.</param>
/// <param name="Average">Average value.</param>
/// <param name="StandardDeviation">Standard deviation value.</param>
public sealed record AggregationMetrics(
	int Count,
	double? Minimum,
	double? Maximum,
	double? Average,
	double? StandardDeviation);

/// <summary>
/// Represents percentile metrics.
/// </summary>
/// <param name="P50">50th percentile value.</param>
/// <param name="P90">90th percentile value.</param>
/// <param name="P95">95th percentile value.</param>
/// <param name="P99">99th percentile value.</param>
public sealed record PercentileMetrics(
	double? P50,
	double? P90,
	double? P95,
	double? P99);

/// <summary>
/// Represents weighted aggregate metrics.
/// </summary>
/// <param name="WeightedAverage">Weighted average value.</param>
/// <param name="WeightSum">Sum of weights.
/// </param>
public sealed record WeightedAggregationMetrics(
	double? WeightedAverage,
	double WeightSum);

/// <summary>
/// Represents all aggregate views generated for an assessment result.
/// </summary>
/// <param name="AllAssessments">Aggregate metrics across all assessments.</param>
/// <param name="ByCategory">Per-category aggregate metrics.</param>
/// <param name="Percentiles">Percentile metrics across normalized scores.</param>
/// <param name="Weighted">Weighted aggregate metrics.</param>
public sealed record AssessmentAggregations(
	AggregationMetrics AllAssessments,
	ImmutableDictionary<string, AggregationMetrics> ByCategory,
	PercentileMetrics Percentiles,
	WeightedAggregationMetrics Weighted);

/// <summary>
/// Default immutable implementation of <see cref="IAssessmentResult"/>.
/// </summary>
public sealed record IssueAssessmentResult : IAssessmentResult
{
	/// <summary>
	/// Gets per-category results.
	/// </summary>
	public required ImmutableList<CategoryResult> Categories { get; init; }

	/// <summary>
	/// Gets execution failures.
	/// </summary>
	public required ImmutableList<AssessmentFailure> Failures { get; init; }

	/// <summary>
	/// Gets aggregation summaries.
	/// </summary>
	public required AssessmentAggregations Aggregations { get; init; }

	/// <summary>
	/// Gets optional captured virtual document.
	/// </summary>
	public JsonDocument? VirtualDocument { get; init; }
}
