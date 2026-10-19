namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// True when the text at <see cref="Left"/> differs from the text at <see cref="Right"/> after trimming, ignoring case
/// and removing <see cref="IgnorePrefixPattern"/> from the start of each. A missing value reads as empty.
/// </summary>
public sealed record FieldsDifferCondition : ICondition
{
	/// <summary>Gets the JSONPath of the first text.</summary>
	public required string Left { get; init; }

	/// <summary>Gets the JSONPath of the second text.</summary>
	public required string Right { get; init; }

	/// <summary>Gets an optional regular expression removed from the start of each text before comparing.</summary>
	public string? IgnorePrefixPattern { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return !string.Equals(Normalise(context, Left), Normalise(context, Right), StringComparison.Ordinal);
	}

	private string Normalise(EvaluationContext context, string path)
	{
		IReadOnlyList<string> values = context.Strings(path);
		string text = (values.Count > 0 ? values[0] : string.Empty).Trim();
		if (IgnorePrefixPattern is { } prefix)
		{
			text = RuleRegex.Get(prefix, caseSensitive: true).Replace(text, string.Empty, 1).Trim();
		}

		return text.ToLowerInvariant();
	}
}
