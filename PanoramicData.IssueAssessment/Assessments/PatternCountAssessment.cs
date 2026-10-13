namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a regex pattern occurs in a selected string value a number of times within an inclusive range.
/// </summary>
public sealed record PatternCountAssessment : ValueAssessment, IRegexPatternAssessmentDefinition
{
	/// <summary>
	/// Gets the regex pattern.
	/// </summary>
	public required string Pattern { get; init; }

	/// <summary>
	/// Gets the inclusive minimum match count, or null for no minimum.
	/// </summary>
	public int? MinMatches { get; init; }

	/// <summary>
	/// Gets the inclusive maximum match count, or null for no maximum.
	/// </summary>
	public int? MaxMatches { get; init; }

	internal override string DescribeSettings()
		=> string.Create(CultureInfo.InvariantCulture, $"{Pattern}:{MinMatches}:{MaxMatches}");

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetString(value, out string text))
		{
			return NotFound();
		}

		int matchCount = RegexCache.GetOrAdd(Pattern).Count(text);
		bool isInRange = (!MinMatches.HasValue || matchCount >= MinMatches.Value) && (!MaxMatches.HasValue || matchCount <= MaxMatches.Value);
		string minimum = MinMatches?.ToString(CultureInfo.InvariantCulture) ?? "0";
		string maximum = MaxMatches?.ToString(CultureInfo.InvariantCulture) ?? "+inf";

		return Scored(isInRange, string.Create(
			CultureInfo.InvariantCulture,
			$"Found {matchCount} matches at '{JsonPath}', {(isInRange ? "within" : "outside")} expected range {minimum}-{maximum}."));
	}
}
