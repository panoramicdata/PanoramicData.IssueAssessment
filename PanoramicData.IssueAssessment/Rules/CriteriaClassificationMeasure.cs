using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// Which criteria a <see cref="CriteriaClassificationMeasure"/> counts.
/// </summary>
public enum CriteriaClassificationKind
{
	/// <summary>Every criterion.</summary>
	Items,

	/// <summary>Criteria not classified MUST, SHOULD or MAY.</summary>
	Unclassified,

	/// <summary>Criteria classified MUST.</summary>
	Must,
}

/// <summary>
/// Counts the criteria under a section of the text, by their RFC 2119 classification. A criterion is a top-level list
/// item (<c>#</c>, <c>*</c>, <c>-</c> or <c>1.</c>) or a table row (<c>|</c>, not a <c>||</c> header). It is classified
/// when the item, or the row's first cell, starts with MUST, SHOULD or MAY in capitals, optionally after <c>*</c> or
/// <c>(</c>. An absent section counts zero.
/// </summary>
public sealed partial record CriteriaClassificationMeasure : IMeasure
{
	/// <summary>Gets the JSONPath of the text to read.</summary>
	public required string Path { get; init; }

	/// <summary>Gets the regular expression fragment matched against the heading of the section holding the criteria.</summary>
	public required string Section { get; init; }

	/// <summary>Gets which criteria to count.</summary>
	public required CriteriaClassificationKind Count { get; init; }

	/// <inheritdoc />
	public double Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		string? body = context.Text(new TextSource { Paths = [Path], Section = Section });
		if (body is null)
		{
			return 0;
		}

		int items = 0;
		int classified = 0;
		int must = 0;
		foreach (string line in LineBreak().Split(body))
		{
			if (CriterionText(line) is not { } criterion)
			{
				continue;
			}

			items++;
			Match classification = Classification().Match(criterion);
			if (classification.Success)
			{
				classified++;
				must += classification.Groups[1].Value == "MUST" ? 1 : 0;
			}
		}

		return Count switch
		{
			CriteriaClassificationKind.Unclassified => items - classified,
			CriteriaClassificationKind.Must => must,
			_ => items,
		};
	}

	private static string? CriterionText(string line)
	{
		Match item = ListItem().Match(line);
		if (item.Success)
		{
			return item.Groups[1].Value;
		}

		Match row = TableRow().Match(line);
		return row.Success && !MarkdownTableRule().IsMatch(row.Groups[1].Value) ? row.Groups[1].Value : null;
	}

	[GeneratedRegex(@"\r?\n", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex LineBreak();

	// More indentation, or a doubled wiki marker, is a sub-point of the criterion above it.
	[GeneratedRegex(@"^ ?(?:[#*-]|\d+\.)[ \t]+(\S.*)$", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex ListItem();

	[GeneratedRegex(@"^[ \t]*\|(?!\|)([^|]*)", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex TableRow();

	[GeneratedRegex(@"^[ \t]*:?-+:?[ \t]*$", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex MarkdownTableRule();

	[GeneratedRegex(@"^[ \t]*[*(]?[ \t]*(MUST|SHOULD|MAY)\b", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex Classification();
}
