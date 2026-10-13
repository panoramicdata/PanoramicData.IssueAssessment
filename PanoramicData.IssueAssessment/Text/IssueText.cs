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
public static class IssueText
{
	private const RegexOptions Options = RegexOptions.CultureInvariant;
	private static readonly TimeSpan Timeout = RegexCache.MatchTimeout;

	private static readonly Regex WikiHeadingAnywhere = new(@"(?im)^[ \t]*h[1-6]\.", Options, Timeout);
	private static readonly Regex WikiHeadingLevel = new(@"(?im)^[ \t]*h([1-6])\.", Options, Timeout);
	private static readonly Regex AtxHeadingLevel = new(@"(?im)^[ \t]*(#{1,6})[ \t]", Options, Timeout);
	private static readonly Regex StartsWithHash = new(@"^[ \t]*#", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithSingleHashItem = new(@"^[ \t]*#[ \t]", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex HashListLine = new(@"^[ \t]*#+[ \t]", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithWiki = new(@"^[ \t]*h[1-6]\.", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithDoubleAsterisk = new(@"^[ \t]*\*\*", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithAsterisk = new(@"^[ \t]*\*", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex LineBreak = new(@"\r?\n", Options, Timeout);

	private static readonly Regex WikiNext = new(@"(?im)^[ \t]*(?:h[1-6]\.|#{2,6}[ \t])", Options, Timeout);
	private static readonly Regex AtxNext = new(@"(?im)^[ \t]*(?:h[1-6]\.|#{1,6}[ \t])", Options, Timeout);
	private static readonly Regex Bold2Next = new(@"(?im)^[ \t]*(?:h[1-6]\.|#{1,6}[ \t]|\*{1,2}[^\r\n*]+\*{1,2}[ \t]*\r?$)", Options, Timeout);
	private static readonly Regex Bold1Next = new(@"(?im)^[ \t]*(?:h[1-6]\.|\*{1,2}[^\r\n*]+\*{1,2}[ \t]*\r?$)", Options, Timeout);
	private static readonly Regex LabelNext = new(@"(?im)^[ \t]*(?:h[1-6]\.|\*{1,2}[^\r\n*]+\*{1,2}[ \t]*\r?$|(?=[A-Za-z])[^\r\n:#*]{1,60}:[ \t]*\r?$)", Options, Timeout);

	private static readonly Regex CriteriaHeadingLine = new(@"^[ \t]*(?:h([1-6])\.|(#{1,6})[ \t]|\*{1,2}(?=[^*\r\n]+\*{1,2}[ \t]*:?[ \t]*$))(.*)$", Options, Timeout);
	private static readonly Regex CriteriaHeadingText = new(@"(?i)criteria|definition\s+of\s+done|verification\s+evidence", Options, Timeout);
	private static readonly Regex CriteriaMappingTable = new(@"(?im)^[ \t]*\|.*criteri.*\|.*\btests?\b", Options, Timeout);

	private static readonly Regex PlaceholderListMarker = new(@"(?m)^[ \t]*(?:[-*#>]+|\d+\.)[ \t]*", Options, Timeout);
	private static readonly Regex PlaceholderPunctuation = new(@"[\*_`{}\[\]\(\)\|:;,\.\-–—/\\]", Options, Timeout);
	private static readonly Regex PlaceholderWords = new(@"^\s*(tbd|to\s*be\s*(done|decided|determined|confirmed|advised)|n\s*/?\s*a|none|nil|todo|to\s*do|pending|unknown|see\s+above)\s*$", Options | RegexOptions.IgnoreCase, Timeout);

	private static readonly Regex Whitespace = new(@"\s+", Options, Timeout);
	private static readonly Regex Alphanumeric = new("[A-Za-z0-9]", Options, Timeout);

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

		bool isWiki = WikiHeadingAnywhere.IsMatch(text);
		string[] lines = LineBreak.Split(text);
		List<(int Index, int Level, string Text)> headings = [];
		for (int index = 0; index < lines.Length; index++)
		{
			Match match = CriteriaHeadingLine.Match(lines[index]);
			if (match.Success && !(isWiki && StartsWithSingleHashItem.IsMatch(lines[index])))
			{
				int level = match.Groups[1].Success
					? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
					: match.Groups[2].Success ? match.Groups[2].Value.Length : 7;
				headings.Add((index, level, match.Groups[3].Value.Trim().Trim('*').Trim().TrimEnd(':').Trim()));
			}
		}

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

	private static bool IsWikiTextOpening(string text, Match opening)
	{
		if (!StartsWithHash.IsMatch(opening.Value))
		{
			return true;
		}

		return !StartsWithSingleHashItem.IsMatch(opening.Value) && !PreviousLineIsHashList(text[..opening.Index]);
	}

	/// <summary>
	/// Cuts <paramref name="rest"/> at the line that closes the section <paramref name="opener"/> opened.
	/// </summary>
	private static string SectionAfter(string opener, string rest)
	{
		bool openedByWiki = StartsWithWiki.IsMatch(opener);
		if (openedByWiki || StartsWithHash.IsMatch(opener))
		{
			// Levelled: close at a heading of the same level or stronger, never a weaker one.
			int openLevel = HeadingLevel(opener);
			foreach (Match candidate in (openedByWiki ? WikiNext : AtxNext).Matches(rest))
			{
				if (openedByWiki && StartsWithHash.IsMatch(candidate.Value) && PreviousLineIsHashList(rest[..candidate.Index]))
				{
					// A "## " line continuing a "#" list is a nested wiki list item, not a Markdown heading.
					continue;
				}

				if (HeadingLevel(candidate.Value) <= openLevel)
				{
					return rest[..candidate.Index];
				}
			}

			return rest;
		}

		Regex closer = StartsWithDoubleAsterisk.IsMatch(opener) ? Bold2Next
			: StartsWithAsterisk.IsMatch(opener) ? Bold1Next
			: LabelNext;
		Match next = closer.Match(rest);
		return next.Success ? rest[..next.Index] : rest;
	}

	private static bool PreviousLineIsHashList(string before)
	{
		string? previous = LineBreak.Split(before).LastOrDefault(line => line.Trim().Length > 0);
		return previous is not null && HashListLine.IsMatch(previous);
	}

	private static bool IsMappingTableSection(string[] lines, List<(int Index, int Level, string Text)> headings, int current)
	{
		(int index, int level, _) = headings[current];

		// The section runs to the next heading of the same or a higher level.
		int end = lines.Length;
		for (int next = current + 1; next < headings.Count; next++)
		{
			if (headings[next].Level <= level)
			{
				end = headings[next].Index;
				break;
			}
		}

		return end > index + 1 && CriteriaMappingTable.IsMatch(string.Join('\n', lines[(index + 1)..end]));
	}
}
