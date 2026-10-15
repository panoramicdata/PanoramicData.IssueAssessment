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
[JsonDerivedType(typeof(SectionPanelCountMeasure), "sectionPanelCount")]
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
/// <see cref="IssueText.CriteriaHeadings"/>). More than one means the criteria are stated twice. Described as the
/// headings' text, separated by semicolons.
/// </summary>
public sealed record CriteriaHeadingCountMeasure : IMeasure, IDescribedMeasure
{
	/// <summary>Gets the text to search.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return IssueText.CriteriaHeadings(context.Text(Source)).Count;
	}

	/// <inheritdoc />
	public string Describe(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return string.Join("; ", IssueText.CriteriaHeadings(context.Text(Source)));
	}
}

/// <summary>
/// Which sections a <see cref="SectionPanelCountMeasure"/> counts.
/// </summary>
public enum SectionPanelKind
{
	/// <summary>Sections that are panelled, or could be.</summary>
	Panelable,

	/// <summary>Sections that are panelled.</summary>
	Panelled,

	/// <summary>Sections that could be panelled but are not.</summary>
	Unpanelled,

	/// <summary>Sections whose panel carries a title that repeats the heading above it.</summary>
	Titled,
}

/// <summary>
/// How many <c>h2.</c> sections of the text are laid out a given way in panels (see <see cref="IssueText.PanelLayoutOf"/>).
/// Described as the headings counted, separated by semicolons.
/// </summary>
public sealed record SectionPanelCountMeasure : IMeasure, IDescribedMeasure
{
	/// <summary>Gets the text to read.</summary>
	public required TextSource Source { get; init; }

	/// <summary>Gets which sections to count.</summary>
	public required SectionPanelKind Count { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		PanelLayout layout = IssueText.PanelLayoutOf(context.Text(Source));
		return Count == SectionPanelKind.Panelled ? layout.Panelled : Headings(layout).Count;
	}

	/// <inheritdoc />
	public string Describe(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return string.Join("; ", Headings(IssueText.PanelLayoutOf(context.Text(Source))));
	}

	private IReadOnlyList<string> Headings(PanelLayout layout) => Count switch
	{
		SectionPanelKind.Panelable => layout.Panelable,
		SectionPanelKind.Unpanelled => layout.Unpanelled,
		SectionPanelKind.Titled => layout.Titled,
		_ => [.. layout.Panelable.Except(layout.Unpanelled)],
	};
}

/// <summary>
/// A measure that can also describe what it measured, for <c>{name:text}</c> placeholders in tier findings.
/// </summary>
public interface IDescribedMeasure
{
	/// <summary>
	/// Describes what was measured for the issue in <paramref name="context"/>.
	/// </summary>
	string Describe(EvaluationContext context);
}
