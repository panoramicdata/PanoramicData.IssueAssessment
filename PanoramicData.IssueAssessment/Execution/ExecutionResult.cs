namespace PanoramicData.IssueAssessment.Execution;

/// <summary>
/// Represents the outcome of category execution.
/// </summary>
/// <param name="Categories">Executed category results.</param>
/// <param name="Failures">Collected execution failures.</param>
/// <param name="VirtualDocument">Final virtual document, if any.</param>
internal sealed record ExecutionResult(
	ImmutableList<CategoryResult> Categories,
	ImmutableList<AssessmentFailure> Failures,
	JsonDocument? VirtualDocument);