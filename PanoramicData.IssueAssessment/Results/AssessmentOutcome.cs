namespace PanoramicData.IssueAssessment.Results;

/// <summary>
/// How an assessment or category came out.
/// </summary>
public enum AssessmentOutcome
{
	/// <summary>Scored its maximum.</summary>
	Passed,

	/// <summary>Scored between its minimum and maximum.</summary>
	Partial,

	/// <summary>Scored its minimum. A missing value is a failure, not an error.</summary>
	Failed,

	/// <summary>Did not apply to this issue, so it was not scored and does not count.</summary>
	NotApplicable,

	/// <summary>Could not be scored: it timed out, threw, or produced no score.</summary>
	Error,
}

/// <summary>
/// Derives an <see cref="AssessmentOutcome"/> from a score.
/// </summary>
public static class AssessmentOutcomes
{
	private const double Tolerance = 1e-9;

	/// <summary>
	/// The outcome a score represents: <see cref="AssessmentOutcome.Error"/> without a score or after a timeout or
	/// exception; otherwise passed, partial or failed by the normalized score.
	/// </summary>
	/// <param name="score">The score.</param>
	/// <param name="failureReason">The failure reason, if any. <see cref="AssessmentFailureReason.JsonPathNotFound"/> is
	/// a scored failure, not an error.</param>
	/// <returns>The outcome.</returns>
	public static AssessmentOutcome FromScore(AssessmentScore score, AssessmentFailureReason? failureReason)
	{
		ArgumentNullException.ThrowIfNull(score);
		if (failureReason is { } reason && reason != AssessmentFailureReason.JsonPathNotFound)
		{
			return AssessmentOutcome.Error;
		}

		return score.NormalizedScore switch
		{
			null => AssessmentOutcome.Error,
			>= 1d - Tolerance => AssessmentOutcome.Passed,
			<= Tolerance => AssessmentOutcome.Failed,
			_ => AssessmentOutcome.Partial,
		};
	}
}
