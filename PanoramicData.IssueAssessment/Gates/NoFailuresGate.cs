namespace PanoramicData.IssueAssessment.Gates;

/// <summary>
/// Continues a series only when the category it is attached to recorded no failures.
/// </summary>
/// <remarks>
/// Failures include values that were not found, so with the built-in categories this also stops the series when a
/// checked field is missing from the ticket.
/// </remarks>
public sealed record NoFailuresGate : ISeriesGateCondition
{
	/// <inheritdoc />
	public Task<bool> ShouldExecuteNextAsync(
		CategoryResult priorCategoryResult,
		ImmutableList<AssessmentFailure> priorFailures,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(priorCategoryResult);
		ArgumentNullException.ThrowIfNull(priorFailures);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(!priorFailures.Any(failure => failure.CategoryName == priorCategoryResult.CategoryName));
	}
}
