using System.Text.Json.Serialization;
using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// A number measured from an issue, such as how many of a list of patterns its text matches. Measures feed
/// <see cref="MeasureCondition"/>s and the placeholders in tier findings.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PatternMatchCountMeasure), "patternMatchCount")]
[JsonDerivedType(typeof(OccurrenceCountMeasure), "occurrenceCount")]
[JsonDerivedType(typeof(WordCountMeasure), "wordCount")]
[JsonDerivedType(typeof(AlphanumericCountMeasure), "alphanumericCount")]
[JsonDerivedType(typeof(LengthMeasure), "length")]
[JsonDerivedType(typeof(ItemCountMeasure), "itemCount")]
[JsonDerivedType(typeof(NumberMeasure), "number")]
[JsonDerivedType(typeof(CriteriaHeadingCountMeasure), "criteriaHeadingCount")]
public interface IMeasure
{
	/// <summary>
	/// Measures the issue in <paramref name="context"/>.
	/// </summary>
	double Evaluate(EvaluationContext context);
}

/// <summary>
/// How many of <see cref="Patterns"/> match the text at least once, case-insensitively unless
/// <see cref="CaseSensitive"/> is set. Each pattern counts at most once, however often it occurs.
/// </summary>
public sealed record PatternMatchCountMeasure : IMeasure
{
	/// <summary>Gets the text to search.</summary>
	public required TextSource Source { get; init; }

	/// <summary>Gets the regular expressions to look for.</summary>
	public required ImmutableList<string> Patterns { get; init; }

	/// <summary>Gets a value indicating whether matching is case-sensitive. Default false.</summary>
	public bool CaseSensitive { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		string text = context.Text(Source) ?? string.Empty;
		return Patterns.Count(pattern => RuleRegex.Get(pattern, CaseSensitive).IsMatch(text));
	}
}

/// <summary>
/// How many times <see cref="Pattern"/> occurs in the text, case-insensitively unless <see cref="CaseSensitive"/> is set.
/// </summary>
public sealed record OccurrenceCountMeasure : IMeasure
{
	/// <summary>Gets the text to search; an absent section counts zero.</summary>
	public required TextSource Source { get; init; }

	/// <summary>Gets the regular expression to count.</summary>
	public required string Pattern { get; init; }

	/// <summary>Gets a value indicating whether matching is case-sensitive. Default false.</summary>
	public bool CaseSensitive { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return RuleRegex.Get(Pattern, CaseSensitive).Count(context.Text(Source) ?? string.Empty);
	}
}

/// <summary>
/// The number of whitespace-separated words in the text.
/// </summary>
public sealed record WordCountMeasure : IMeasure
{
	/// <summary>Gets the text to count; an absent section counts zero.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return IssueText.WordCount(context.Text(Source));
	}
}

/// <summary>
/// The number of ASCII letters and digits in the text: how much it says, ignoring markup.
/// </summary>
public sealed record AlphanumericCountMeasure : IMeasure
{
	/// <summary>Gets the text to count; an absent section counts zero.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return IssueText.AlphanumericCount(context.Text(Source));
	}
}

/// <summary>
/// The length of the text in characters.
/// </summary>
public sealed record LengthMeasure : IMeasure
{
	/// <summary>Gets the text to measure; an absent section measures zero.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Text(Source)?.Length ?? 0;
	}
}

/// <summary>
/// How many values are at a JSONPath: an array's length, 1 for a present scalar, 0 when missing or null.
/// </summary>
public sealed record ItemCountMeasure : IMeasure
{
	/// <summary>Gets the JSONPath to count.</summary>
	public required string Path { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Strings(Path).Count;
	}
}

/// <summary>
/// The number at a JSONPath (a JSON number or an invariant numeric string), or 0 when there is none.
/// </summary>
public sealed record NumberMeasure : IMeasure
{
	/// <summary>Gets the JSONPath to read.</summary>
	public required string Path { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Strings(Path) is [string text]
			&& double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
			&& double.IsFinite(value)
			? value
			: 0d;
	}
}

/// <summary>
/// How many headings in the text open an acceptance-criteria section (see
/// <see cref="IssueText.CriteriaHeadings"/>). More than one means the criteria are stated twice.
/// </summary>
public sealed record CriteriaHeadingCountMeasure : IMeasure
{
	/// <summary>Gets the text to search.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return IssueText.CriteriaHeadings(context.Text(Source)).Count;
	}
}
