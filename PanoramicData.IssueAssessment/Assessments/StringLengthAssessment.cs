namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value length falls within an inclusive range.
/// </summary>
public sealed record StringLengthAssessment : ValueAssessment
{
	/// <summary>
	/// Gets the minimum allowed string length.
	/// </summary>
	public int MinimumLength { get; init; }

	/// <summary>
	/// Gets the maximum allowed string length.
	/// </summary>
	public int MaximumLength { get; init; }

	internal override string DescribeSettings()
		=> string.Create(CultureInfo.InvariantCulture, $"{MinimumLength}:{MaximumLength}");

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetString(value, out string text))
		{
			return NotFound();
		}

		bool isInRange = text.Length >= MinimumLength && text.Length <= MaximumLength;
		return Scored(isInRange, RangeExplanation(
			string.Create(CultureInfo.InvariantCulture, $"String length {text.Length}"),
			isInRange,
			MinimumLength.ToString(CultureInfo.InvariantCulture),
			MaximumLength.ToString(CultureInfo.InvariantCulture)));
	}
}
