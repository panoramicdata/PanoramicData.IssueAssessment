namespace PanoramicData.IssueAssessment.Results;

/// <summary>
/// The overall verdict on an issue.
/// </summary>
/// <param name="Score">The weighted average of the applicable categories' normalized scores, in [0, 1], after score
/// caps; null when no category was scored.</param>
/// <param name="UncappedScore">The same average before score caps.</param>
/// <param name="Grade">The name of the highest grade band the score reaches, or null without bands or a score.</param>
/// <param name="Caps">The score caps that applied, in rubric order.</param>
/// <param name="Flags">The value of every named condition in the rubric.</param>
/// <param name="Classifications">The value each classification in the rubric assigned.</param>
public sealed record AssessmentOverall(
	double? Score,
	double? UncappedScore,
	string? Grade,
	ImmutableList<AppliedScoreCap> Caps,
	ImmutableDictionary<string, bool> Flags,
	ImmutableDictionary<string, string> Classifications)
{
	/// <summary>
	/// Gets the score on a 0-100 scale, rounded half to even; null without a score.
	/// </summary>
	public int? Score100 => Score is { } score ? (int)Math.Round(score * 100d, MidpointRounding.ToEven) : null;
}

/// <summary>
/// A score cap that applied to an issue.
/// </summary>
/// <param name="Name">The cap's name, for example the mandatory element that was missing.</param>
/// <param name="Group">The cap's group, for example "mandatory", or null.</param>
/// <param name="Cap">The highest score the cap allows, in [0, 1].</param>
public sealed record AppliedScoreCap(string Name, string? Group, double Cap);
