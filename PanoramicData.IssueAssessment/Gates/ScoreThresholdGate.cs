namespace PanoramicData.IssueAssessment.Gates;

/// <summary>
/// Continues a series only when the category it is attached to scored at least <see cref="MinimumScore"/>.
/// </summary>
/// <remarks>
/// Attach it to a category with <see cref="AssessmentCategory.SeriesGateCondition"/>; the categories after it run only
/// when that category's normalized score meets the threshold. An unscored category (one whose assessments failed to
/// produce a score) does not meet any threshold.
/// </remarks>
/// <example>
/// <code>
/// TitleQualityCategory.Create() with { SeriesGateCondition = new ScoreThresholdGate { MinimumScore = 0.5 } }
/// </code>
/// </example>
public sealed record ScoreThresholdGate : ISeriesGateCondition
{
	/// <summary>
	/// Gets the lowest normalized score, in [0, 1], that lets the series continue.
	/// </summary>
	public required double MinimumScore { get; init; }

	/// <inheritdoc />
	public Task<bool> ShouldExecuteNextAsync(
		CategoryResult priorCategoryResult,
		ImmutableList<AssessmentFailure> priorFailures,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(priorCategoryResult);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(priorCategoryResult.Score.NormalizedScore >= MinimumScore);
	}
}
