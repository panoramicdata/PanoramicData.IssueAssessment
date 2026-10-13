namespace PanoramicData.IssueAssessment.Execution;

/// <summary>
/// Represents assessment execution output and optional derived failure.
/// </summary>
/// <param name="AssessmentResult">Materialized assessment result.</param>
/// <param name="Failure">Optional failure entry.</param>
/// <param name="VirtualDocument">Virtual extension produced by the assessment.</param>
internal sealed record AssessmentExecutionItemResult(
	AssessmentResult AssessmentResult,
	AssessmentFailure? Failure,
	JsonDocument? VirtualDocument);