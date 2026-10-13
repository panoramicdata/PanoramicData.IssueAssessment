using PanoramicData.IssueAssessment.Serialization;

namespace PanoramicData.IssueAssessment.Results;

/// <summary>
/// A plain, serialisable summary of an <see cref="IAssessmentResult"/>: no exceptions or JSON documents, only values,
/// so it can be written as JSON, stored, or passed to another process.
/// </summary>
public sealed record AssessmentReport
{
	/// <summary>Gets the overall score on a 0-100 scale, after caps; null when nothing was scored.</summary>
	public int? Score { get; init; }

	/// <summary>Gets the overall score in [0, 1], after caps.</summary>
	public double? NormalizedScore { get; init; }

	/// <summary>Gets the grade, if the rubric defines grade bands.</summary>
	public string? Grade { get; init; }

	/// <summary>Gets every non-empty finding, in rubric order.</summary>
	public required ImmutableList<string> Findings { get; init; }

	/// <summary>Gets every recommendation, in rubric order, without duplicates.</summary>
	public required ImmutableList<string> Recommendations { get; init; }

	/// <summary>Gets the score caps that applied.</summary>
	public required ImmutableList<AppliedScoreCap> Caps { get; init; }

	/// <summary>Gets the value of each named condition.</summary>
	public required ImmutableDictionary<string, bool> Flags { get; init; }

	/// <summary>Gets the value of each classification.</summary>
	public required ImmutableDictionary<string, string> Classifications { get; init; }

	/// <summary>Gets each category's result.</summary>
	public required ImmutableList<CategoryReport> Categories { get; init; }

	/// <summary>Gets the failures, with exception messages in place of exceptions.</summary>
	public required ImmutableList<FailureReport> Failures { get; init; }

	/// <summary>
	/// Builds the report for <paramref name="result"/>.
	/// </summary>
	/// <param name="result">The assessment result.</param>
	/// <returns>The report.</returns>
	public static AssessmentReport From(IAssessmentResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		AssessmentOverall? overall = result.Overall;
		List<AssessmentResult> assessments = [.. result.Categories.SelectMany(category => category.Assessments)];

		return new AssessmentReport
		{
			Score = overall?.Score100,
			NormalizedScore = overall?.Score,
			Grade = overall?.Grade,
			Findings = [.. assessments.Select(assessment => assessment.ExplanationText).Where(text => !string.IsNullOrWhiteSpace(text))],
			Recommendations = [.. assessments.Select(assessment => assessment.Recommendation).OfType<string>().Distinct(StringComparer.Ordinal)],
			Caps = overall?.Caps ?? [],
			Flags = overall?.Flags ?? ImmutableDictionary<string, bool>.Empty,
			Classifications = overall?.Classifications ?? ImmutableDictionary<string, string>.Empty,
			Categories = [.. result.Categories.Select(CategoryReport.From)],
			Failures = [.. result.Failures.Select(failure => new FailureReport(
				failure.CategoryName,
				failure.AssessmentName,
				failure.Reason,
				failure.Message,
				failure.InnerException?.Message))],
		};
	}

	/// <summary>
	/// Writes the report as indented JSON with camelCase names and enum names as strings.
	/// </summary>
	/// <returns>The JSON.</returns>
	public string ToJson() => JsonSerializer.Serialize(this, AssessmentRubric.SerializerOptions);
}

/// <summary>
/// One category in an <see cref="AssessmentReport"/>.
/// </summary>
/// <param name="Name">The category name.</param>
/// <param name="Outcome">How the category came out.</param>
/// <param name="Weight">The category's weight in the overall score.</param>
/// <param name="NormalizedScore">The category's normalized score, if scored.</param>
/// <param name="Assessments">The category's assessments.</param>
public sealed record CategoryReport(
	string Name,
	AssessmentOutcome Outcome,
	double Weight,
	double? NormalizedScore,
	ImmutableList<AssessmentReportItem> Assessments)
{
	internal static CategoryReport From(CategoryResult category)
		=> new(
			category.CategoryName,
			category.Outcome,
			category.Weight,
			category.Score.NormalizedScore,
			[.. category.Assessments.Select(assessment => new AssessmentReportItem(
				assessment.AssessmentName,
				assessment.Outcome,
				assessment.Score.RawScore,
				assessment.Score.NormalizedScore,
				assessment.ExplanationText,
				assessment.Recommendation,
				assessment.FailureReason,
				assessment.InnerException?.Message))]);
}

/// <summary>
/// One assessment in an <see cref="AssessmentReport"/>.
/// </summary>
/// <param name="Name">The assessment name.</param>
/// <param name="Outcome">How the assessment came out.</param>
/// <param name="RawScore">The raw score, if scored.</param>
/// <param name="NormalizedScore">The normalized score, if scored.</param>
/// <param name="Finding">The finding (explanation).</param>
/// <param name="Recommendation">The recommendation, if any.</param>
/// <param name="FailureReason">The failure reason, if any.</param>
/// <param name="Error">The exception message, if one was thrown.</param>
public sealed record AssessmentReportItem(
	string Name,
	AssessmentOutcome Outcome,
	double? RawScore,
	double? NormalizedScore,
	string Finding,
	string? Recommendation,
	AssessmentFailureReason? FailureReason,
	string? Error);

/// <summary>
/// One failure in an <see cref="AssessmentReport"/>.
/// </summary>
/// <param name="CategoryName">The category name.</param>
/// <param name="AssessmentName">The assessment name.</param>
/// <param name="Reason">The failure reason.</param>
/// <param name="Message">The failure message.</param>
/// <param name="Error">The exception message, if one was thrown.</param>
public sealed record FailureReport(
	string CategoryName,
	string AssessmentName,
	AssessmentFailureReason Reason,
	string Message,
	string? Error);
