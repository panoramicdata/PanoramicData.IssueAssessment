using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// Finds Markdown that Jira's wiki markup renders wrongly. Only the unambiguous constructs are reported: an ATX heading
/// (<c>## Heading</c> is a nested numbered list in wiki markup), a backtick code span or fence, double-asterisk bold, a
/// Markdown link and a table separator row. A line holding only <c>1.</c> style numbers reads acceptably and is not reported.
/// Text inside <c>{code}</c>, <c>{noformat}</c> and <c>{{monospace}}</c> is shown literally by Jira, so it is skipped.
/// </summary>
/// <remarks>
/// The rules mirror PanoramicData.Skills' <c>Find-JiraMarkdownSyntax</c>, each checked against Jira's own renderer. A
/// <c>##</c> line directly under a <c>#</c> or <c>*</c> list line of enough depth is a legitimate nested item and is not reported.
/// </remarks>
public static partial class MarkdownSyntax
{
	private static readonly (string Kind, Func<string, bool> Found)[] _lineRules =
	[
		("backtick code span", line => CodeSpan().IsMatch(line)),
		("double-asterisk bold", line => DoubleAsteriskBold().IsMatch(line)),
		("Markdown link", line => MarkdownLink().IsMatch(line)),
		("Markdown table separator", line => TableSeparator().IsMatch(line)),
	];

	/// <summary>
	/// The kind of every Markdown construct in <paramref name="text"/>, one entry per construct, in the order found.
	/// </summary>
	/// <param name="text">The wiki-markup text to read; null or blank has none.</param>
	public static IReadOnlyList<string> Constructs(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return [];
		}

		string[] lines = LineBreak().Split(Mask(text));
		List<string> found = [];
		for (int index = 0; index < lines.Length; index++)
		{
			found.AddRange(ConstructsOnLine(lines[index], index > 0 ? lines[index - 1] : string.Empty));
		}

		return found;
	}

	private static List<string> ConstructsOnLine(string line, string previous)
	{
		if (CodeFence().IsMatch(line))
		{
			return ["code fence"];
		}

		List<string> found = [];
		if (IsMarkdownHeading(line, previous))
		{
			found.Add("Markdown heading");
		}

		found.AddRange(_lineRules.Where(rule => rule.Found(line)).Select(rule => rule.Kind));
		return found;
	}

	private static bool IsMarkdownHeading(string line, string previous)
	{
		Match heading = AtxHeading().Match(line);
		if (!heading.Success)
		{
			return false;
		}

		Match list = ListMarker().Match(previous);
		return !(list.Success && list.Groups[1].Length >= heading.Groups[1].Length - 1);
	}

	/// <summary>Blanks the regions Jira shows literally, keeping every line break so line positions stay true.</summary>
	private static string Mask(string text)
	{
		MatchEvaluator blank = Blank;
		string masked = CodeBlock().Replace(text, blank);
		masked = NoFormatBlock().Replace(masked, blank);
		return Monospace().Replace(masked, blank);
	}

	private static string Blank(Match match) => NotLineBreak().Replace(match.Value, " ");

	[GeneratedRegex(@"\r?\n", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex LineBreak();

	[GeneratedRegex(@"[^\r\n]", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex NotLineBreak();

	// The lookarounds stop "{{code}}" (monospace text that says "code") being read as a code-block delimiter.
	[GeneratedRegex(@"(?<!\{)\{code(?::[^}]*)?\}(?!\}).*?(?<!\{)\{code\}(?!\})", RegexOptions.CultureInvariant | RegexOptions.Singleline, 1000)]
	private static partial Regex CodeBlock();

	[GeneratedRegex(@"\{noformat(?::[^}]*)?\}.*?\{noformat\}", RegexOptions.CultureInvariant | RegexOptions.Singleline, 1000)]
	private static partial Regex NoFormatBlock();

	[GeneratedRegex(@"\{\{[^\r\n]*?\}\}", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex Monospace();

	[GeneratedRegex(@"^\s*```", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex CodeFence();

	[GeneratedRegex(@"^(#{2,6})\s+\S", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex AtxHeading();

	[GeneratedRegex(@"^\s*([#*]+)\s+\S", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex ListMarker();

	// Not glued to a letter or digit outside, so a .NET "IEnumerable`1 source, Func`2" is not a code span.
	[GeneratedRegex(@"(?<![`A-Za-z0-9])`[^`\r\n]+`(?![`A-Za-z0-9])", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex CodeSpan();

	[GeneratedRegex(@"(?<!\*)\*\*(?=\S)[^*\r\n]+?(?<=\S)\*\*(?!\*)", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex DoubleAsteriskBold();

	[GeneratedRegex(@"\[[^\]\r\n]+\]\((?:https?://|mailto:|\.{0,2}/|#)[^)\s]*\)", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex MarkdownLink();

	[GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex TableSeparator();
}
