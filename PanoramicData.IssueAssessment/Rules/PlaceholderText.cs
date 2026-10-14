using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// Fills the placeholders in findings, recommendations and not-applicable explanations.
/// </summary>
/// <remarks>
/// <c>{$.path}</c> becomes the values at a JSONPath, comma-separated; <c>{$.path~pattern}</c> the first of them that
/// matches the pattern; <c>{name}</c> a measure's value; and <c>{name:text}</c> a described measure's text.
/// </remarks>
internal static partial class PlaceholderText
{
	/// <summary>
	/// The keys of the placeholders in <paramref name="text"/>.
	/// </summary>
	public static IEnumerable<string> Keys(string? text)
		=> text is null ? [] : Pattern().Matches(text).Select(match => match.Groups[1].Value);

	/// <summary>
	/// Fills the placeholders in <paramref name="text"/>, resolving measure names through
	/// <paramref name="measures"/>.
	/// </summary>
	public static string Format(string text, EvaluationContext context, ImmutableDictionary<string, IMeasure> measures)
		=> Pattern().Replace(text, match => Resolve(match.Groups[1].Value, context, measures));

	private static string Resolve(string key, EvaluationContext context, ImmutableDictionary<string, IMeasure> measures)
	{
		if (key.StartsWith('$'))
		{
			int tilde = key.IndexOf('~', StringComparison.Ordinal);
			return tilde < 0
				? string.Join(", ", context.Strings(key))
				: context.Strings(key[..tilde]).FirstOrDefault(value => RuleRegex.Get(key[(tilde + 1)..], false).IsMatch(value)) ?? string.Empty;
		}

		return key.EndsWith(":text", StringComparison.Ordinal)
			&& measures.TryGetValue(key[..^":text".Length], out IMeasure? measure)
			&& measure is IDescribedMeasure described
			? described.Describe(context)
			: context.Measure(key).ToString(CultureInfo.InvariantCulture);
	}

	[GeneratedRegex(@"\{([^{}\s]+)\}", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex Pattern();
}
