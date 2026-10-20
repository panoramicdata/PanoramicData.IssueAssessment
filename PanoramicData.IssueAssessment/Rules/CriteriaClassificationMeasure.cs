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
/// item (<c>#</c>, <c>*</c>, <c>-</c> or <c>1.</c>) or a table row (<c>|</c>, not a <c>||</c> header). Keywords are
/// MUST, SHOULD and MAY as whole words in capitals. An absent section counts zero. An item struck through in full (<c>-text-</c>, optionally followed by a note) belongs to\n/// another ticket now and is not counted.
/// </summary>
/// <remarks>
/// A criterion (the item, or the row's first cell) is classified, in order, by: a keyword it starts with, optionally
/// after <c>*</c> or <c>(</c>; otherwise the one keyword it contains, when it contains exactly one distinct keyword;
/// otherwise, for a list item, the label line above it. A label line is a line that is not a criterion and starts
/// with a keyword, optionally bolded or bracketed (<c>*MUST*</c>, <c>MUST:</c>). Its scope runs to the next label line
/// or wiki heading.
/// </remarks>
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

		(int items, int classified, int must) = Tally(body);
		return Count switch
		{
			CriteriaClassificationKind.Unclassified => items - classified,
			CriteriaClassificationKind.Must => must,
			_ => items,
		};
	}

	private static (int Items, int Classified, int Must) Tally(string body)
	{
		int items = 0;
		int classified = 0;
		int must = 0;
		string? label = null;
		foreach (string line in LineBreak().Split(body))
		{
			if (Criterion(line) is not { } criterion)
			{
				label = LabelAfter(line, label);
				continue;
			}

			items++;
			if (Classify(criterion.Text, criterion.IsListItem ? label : null) is { } keyword)
			{
				classified++;
				must += keyword == "MUST" ? 1 : 0;
			}
		}

		return (items, classified, must);
	}

	/// <summary>The label in force after a line that is not a criterion: a label line sets it, a wiki heading clears it.</summary>
	private static string? LabelAfter(string line, string? label)
	{
		Match labelLine = LabelLine().Match(line);
		if (labelLine.Success)
		{
			return labelLine.Groups[1].Value;
		}

		return WikiHeading().IsMatch(line) ? null : label;
	}

	private static string? Classify(string criterion, string? label)
	{
		Match leading = Classification().Match(criterion);
		if (leading.Success)
		{
			return leading.Groups[1].Value;
		}

		List<string> keywords = [.. Keyword().Matches(criterion).Select(match => match.Value).Distinct(StringComparer.Ordinal)];
		return keywords.Count == 1 ? keywords[0] : label;
	}

	private static (string Text, bool IsListItem)? Criterion(string line)
	{
		(string Text, bool IsListItem)? found = Candidate(line);
		return found is { } candidate && StruckThrough().IsMatch(candidate.Text) ? null : found;
	}

	private static (string Text, bool IsListItem)? Candidate(string line)
	{
		Match item = ListItem().Match(line);
		if (item.Success)
		{
			return (item.Groups[1].Value, true);
		}

		Match row = TableRow().Match(line);
		return row.Success && !MarkdownTableRule().IsMatch(row.Groups[1].Value) ? (row.Groups[1].Value, false) : null;
	}

	// All of the text is struck through (-text-), then at most a "(note)", an "_note_" or "moved to KEY ..." remark.
	[GeneratedRegex(@"^[ \t]*-(?=\S)(?:(?!-(?:\s|$)).)+-(?:[ \t]*|[ \t]+(?:\(.*\)|_.*_|moved[ \t]+to[ \t]+[A-Z][A-Z0-9]*-\d+.*)[ \t]*)$", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex StruckThrough();

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

	[GeneratedRegex(@"\b(?:MUST|SHOULD|MAY)\b", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex Keyword();

	// No space after the marker, so a "** MUST ..." nested sub-point is not a label.
	[GeneratedRegex(@"^[ \t]*(?:\*{1,2}|_|\(|\[)?(MUST|SHOULD|MAY)\b", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex LabelLine();

	[GeneratedRegex(@"^[ \t]*h[1-6]\.", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex WikiHeading();
}
