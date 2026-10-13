namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value word count falls within an inclusive range.
/// </summary>
/// <remarks>A word is a run of non-whitespace characters.</remarks>
public sealed record WordCountAssessment : ValueAssessment
{
	/// <summary>
	/// Gets the minimum allowed word count.
	/// </summary>
	public int MinimumWordCount { get; init; }

	/// <summary>
	/// Gets the maximum allowed word count.
	/// </summary>
	public int MaximumWordCount { get; init; }

	internal override string DescribeSettings()
		=> string.Create(CultureInfo.InvariantCulture, $"{MinimumWordCount}:{MaximumWordCount}");

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetString(value, out string text))
		{
			return NotFound();
		}

		int wordCount = CountWords(text);
		bool isInRange = wordCount >= MinimumWordCount && wordCount <= MaximumWordCount;
		return Scored(isInRange, RangeExplanation(
			string.Create(CultureInfo.InvariantCulture, $"Word count {wordCount}"),
			isInRange,
			MinimumWordCount.ToString(CultureInfo.InvariantCulture),
			MaximumWordCount.ToString(CultureInfo.InvariantCulture)));
	}

	private static int CountWords(string text)
	{
		bool inWord = false;
		int count = 0;

		foreach (char character in text)
		{
			if (char.IsWhiteSpace(character))
			{
				inWord = false;
			}
			else if (!inWord)
			{
				count++;
				inWord = true;
			}
		}

		return count;
	}
}
