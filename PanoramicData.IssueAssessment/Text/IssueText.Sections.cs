using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// Section and heading helpers behind <see cref="SectionBody"/> and <see cref="CriteriaHeadings"/>.
/// </summary>
public static partial class IssueText
{
	/// <summary>
	/// Every heading line, with its index and level; in wiki text a single <c>#</c> line is a list item, not a heading.
	/// </summary>
	private static List<(int Index, int Level, string Text)> Headings(string[] lines, bool isWiki)
	{
		List<(int Index, int Level, string Text)> headings = [];
		for (int index = 0; index < lines.Length; index++)
		{
			Match match = CriteriaHeadingLine.Match(lines[index]);
			if (match.Success && !(isWiki && StartsWithSingleHashItem.IsMatch(lines[index])))
			{
				headings.Add((index, HeadingLevel(match), match.Groups[3].Value.Trim().Trim('*').Trim().TrimEnd(':').Trim()));
			}
		}

		return headings;
	}

	private static int HeadingLevel(Match criteriaHeading)
	{
		if (criteriaHeading.Groups[1].Success)
		{
			return int.Parse(criteriaHeading.Groups[1].Value, CultureInfo.InvariantCulture);
		}

		return criteriaHeading.Groups[2].Success ? criteriaHeading.Groups[2].Value.Length : 7;
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
			return LevelledSectionAfter(opener, rest, openedByWiki);
		}

		Regex closer = StartsWithDoubleAsterisk.IsMatch(opener) ? Bold2Next
			: StartsWithAsterisk.IsMatch(opener) ? Bold1Next
			: LabelNext;
		Match next = closer.Match(rest);
		return next.Success ? rest[..next.Index] : rest;
	}

	/// <summary>
	/// Cuts <paramref name="rest"/> at the next heading of the same level as <paramref name="opener"/> or stronger, never
	/// at a weaker one.
	/// </summary>
	private static string LevelledSectionAfter(string opener, string rest, bool openedByWiki)
	{
		int openLevel = HeadingLevel(opener);
		foreach (Match candidate in (openedByWiki ? WikiNext : AtxNext).Matches(rest))
		{
			// A "## " line continuing a "#" list is a nested wiki list item, not a Markdown heading.
			bool continuesWikiList = openedByWiki && StartsWithHash.IsMatch(candidate.Value) && PreviousLineIsHashList(rest[..candidate.Index]);
			if (!continuesWikiList && HeadingLevel(candidate.Value) <= openLevel)
			{
				return rest[..candidate.Index];
			}
		}

		return rest;
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
