namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value matches a regex pattern.
/// </summary>
public sealed record RegexAssessment : ValueAssessment, IRegexPatternAssessmentDefinition
{
	/// <summary>
	/// Gets the regex pattern.
	/// </summary>
	public required string Pattern { get; init; }

	internal override string DescribeSettings() => Pattern;

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetString(value, out string text))
		{
			return NotFound();
		}

		bool isMatch = RegexCache.GetOrAdd(Pattern).IsMatch(text);
		return Scored(isMatch, isMatch
			? $"Pattern '{Pattern}' matched value at '{JsonPath}'."
			: $"Pattern '{Pattern}' did not match value at '{JsonPath}'.");
	}
}
