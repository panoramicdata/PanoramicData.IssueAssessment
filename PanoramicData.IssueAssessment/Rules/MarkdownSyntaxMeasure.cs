using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// How many Markdown constructs the text holds that Jira's wiki markup renders wrongly (see <see cref="MarkdownSyntax"/>).
/// Described as the distinct kinds found, separated by semicolons.
/// </summary>
public sealed record MarkdownSyntaxMeasure : IMeasure, IDescribedMeasure
{
	/// <summary>Gets the text to read.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return MarkdownSyntax.Constructs(context.Text(Source)).Count;
	}

	/// <inheritdoc />
	public string Describe(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return string.Join("; ", MarkdownSyntax.Constructs(context.Text(Source)).Distinct(StringComparer.Ordinal));
	}
}
