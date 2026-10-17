using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// Reads the structure of ticket text written in Jira wiki markup or Markdown: the body under a heading, the headings
/// that state acceptance criteria, and whether a section is only a placeholder.
/// </summary>
/// <remarks>
/// A port of the hardened readers in PanoramicData.Skills' <c>IssueQualityCore.ps1</c>, so that a rubric scored by this
/// library reads a ticket exactly as Yoda does. Jira returns descriptions with CRLF line endings, and every pattern
/// here accepts both CRLF and LF.
/// </remarks>
public static partial class IssueText
{
	/// <summary>
	/// Returns the body text under the best heading whose text matches <paramref name="headingKeywordPattern"/>, or
	/// null when there is no such heading.
	/// </summary>
	/// <param name="text">The ticket text.</param>
	/// <param name="headingKeywordPattern">A regular expression fragment matched against the heading text.</param>
	/// <returns>
	/// The section body, which may be empty when a heading has nothing under it; null when no heading matches. When
	/// several headings match, the longest body wins.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Recognises Jira wiki headings (<c>h1.</c> to <c>h6.</c>), Markdown ATX headings (<c>#</c> to <c>######</c>),
	/// single- or double-asterisk bold used as a heading, and a label line that is only the label and a colon
	/// (<c>Steps to reproduce:</c>).
	/// </para>
	/// <para>
	/// What closes a section depends on what opened it. A wiki or ATX heading closes at a heading of the same level or
	/// stronger, so sub-headings stay inside. In wiki text a line starting <c># </c> is a numbered list item, never a
	/// heading, and a <c>## </c> line continuing such a list is a nested item. Bold and label headings close at the
	/// next heading-like line.
	/// </para>
	/// </remarks>
	public static string? SectionBody(string? text, string headingKeywordPattern)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(headingKeywordPattern);
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}

		Regex heading = RegexCache.GetOrAdd(@"(?im)^[ \t]*(?:h[1-6]\.|#{1,6}[ \t]|\*{1,2}(?=[^\r\n]*\*{1,2}[ \t]*\r?$))[^\r\n]*" + headingKeywordPattern + @"[^\r\n]*");
		Regex label = RegexCache.GetOrAdd(@"(?im)^[ \t]*(?![hH][1-6]\.)(?=[A-Za-z])[^\r\n:#*]{0,60}?" + headingKeywordPattern + @"[^\r\n:]{0,60}:[ \t]*\r?$");

		IEnumerable<Match> openings = heading.Matches(text).Concat(label.Matches(text));

		// In wiki text a "#" line is a list item, so it cannot open a section; only a "##"-or-deeper line that does not
		// continue a "#" list may.
		if (WikiHeadingAnywhere.IsMatch(text))
		{
			openings = openings.Where(opening => IsWikiTextOpening(text, opening));
		}

		string? best = null;
		foreach (Match opening in openings)
		{
			string body = SectionAfter(opening.Value, text[(opening.Index + opening.Length)..]);
			if (best is null || body.Length > best.Length)
			{
				best = body;
			}
		}

		return best;
	}

	/// <summary>
	/// Returns the text of every heading that opens an acceptance-criteria section: a heading whose text mentions
	/// criteria, a definition of done or verification evidence.
	/// </summary>
	/// <param name="text">The ticket text.</param>
	/// <returns>The heading texts, in order, without markup.</returns>
	/// <remarks>
	/// Only headings count, never body text. A section holding a criterion-to-test mapping table is evidence rather than
	/// restated criteria, so its heading is not counted. In wiki text a single <c>#</c> line is a list item.
	/// </remarks>
	public static IReadOnlyList<string> CriteriaHeadings(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return [];
		}

		string[] lines = LineBreak.Split(text);
		List<(int Index, int Level, string Text)> headings = Headings(lines, WikiHeadingAnywhere.IsMatch(text));
		List<string> found = [];
		for (int current = 0; current < headings.Count; current++)
		{
			string headingText = headings[current].Text;
			if (CriteriaHeadingText.IsMatch(headingText) && !IsMappingTableSection(lines, headings, current))
			{
				found.Add(headingText);
			}
		}

		return found;
	}

	/// <summary>
	/// True when a section body is a placeholder (TBD, N/A, none, to do, pending, ...) or blank, rather than content.
	/// </summary>
	/// <param name="body">The section body.</param>
	/// <returns><see langword="true"/> for a blank or placeholder body.</returns>
	public static bool IsPlaceholder(string? body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return true;
		}

		// Remove list bullets, numbering and emphasis so "* TBD" reads as "TBD".
		string stripped = PlaceholderListMarker.Replace(body, string.Empty);
		stripped = PlaceholderPunctuation.Replace(stripped, " ").Trim();
		return PlaceholderWords.IsMatch(stripped);
	}

	/// <summary>
	/// The number of whitespace-separated words in <paramref name="text"/>.
	/// </summary>
	public static int WordCount(string? text)
		=> string.IsNullOrEmpty(text) ? 0 : Whitespace.Split(text).Count(word => word.Length > 0);

	/// <summary>
	/// The number of ASCII letters and digits in <paramref name="text"/>: a measure of how much a section actually says,
	/// ignoring markup and punctuation.
	/// </summary>
	public static int AlphanumericCount(string? text)
		=> string.IsNullOrEmpty(text) ? 0 : Alphanumeric.Count(text);

	/// <summary>
	/// The level of a heading line: 1 for <c>h1.</c> or <c>#</c>, up to 6; 7 for a line that is not a structural
	/// heading, which is weaker than every real level and so never closes a section by accident.
	/// </summary>
	public static int HeadingLevel(string? headingLine)
	{
		if (string.IsNullOrWhiteSpace(headingLine))
		{
			return 7;
		}

		Match wiki = WikiHeadingLevel.Match(headingLine);
		if (wiki.Success)
		{
			return int.Parse(wiki.Groups[1].Value, CultureInfo.InvariantCulture);
		}

		Match atx = AtxHeadingLevel.Match(headingLine);
		return atx.Success ? atx.Groups[1].Value.Length : 7;
	}
}
