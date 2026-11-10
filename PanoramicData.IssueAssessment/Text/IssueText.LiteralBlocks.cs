using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// Keeps text quoted in <c>{noformat}</c> and <c>{code}</c> blocks from being read as the ticket's own structure.
/// </summary>
public static partial class IssueText
{
	/// <summary>
	/// Blanks the first visible character of every line inside a literal block, so headings, list items and table rows
	/// quoted there are not parsed. Length and positions are unchanged, so indexes still map onto the original text.
	/// </summary>
	private static string MaskLiteralBlocks(string text)
	{
		if (!LiteralBlockToken.IsMatch(text))
		{
			return text;
		}

		char[] masked = text.ToCharArray();
		string? openKind = null;
		int lineStart = 0;
		while (lineStart < text.Length)
		{
			int lineEnd = text.IndexOf('\n', lineStart);
			if (lineEnd < 0)
			{
				lineEnd = text.Length;
			}

			string line = text[lineStart..lineEnd];
			int firstVisible = line.Length - line.TrimStart().Length;
			if (openKind is not null && firstVisible < line.Length && line[firstVisible] != '{')
			{
				masked[lineStart + firstVisible] = ' ';
			}

			openKind = LiteralStateAfter(line, openKind);
			lineStart = lineEnd + 1;
		}

		return new string(masked);
	}

	/// <summary>
	/// The open block kind after <paramref name="line"/>: <c>{noformat}</c> closes only <c>{noformat}</c>, and inside
	/// a <c>{code}</c> block only <c>{code}</c> closes it.
	/// </summary>
	private static string? LiteralStateAfter(string line, string? openKind)
	{
		foreach (Match token in LiteralBlockToken.Matches(line))
		{
			string kind = token.Groups[1].Value.ToLowerInvariant();
			if (openKind is null)
			{
				openKind = kind;
			}
			else if (openKind == kind && !token.Value.Contains(':', StringComparison.Ordinal))
			{
				openKind = null;
			}
		}

		return openKind;
	}
}
