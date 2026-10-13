using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Examples;

/// <summary>
/// Text helpers shared by the platform mappers: finding a titled section in a ticket body, and removing
/// boilerplate that would otherwise inflate length- and word-based scores.
/// </summary>
internal static partial class IssueText
{
	/// <summary>
	/// Returns the text under the first heading that matches <paramref name="headingPattern"/>, up to the next heading,
	/// or null when there is no such heading or the section is empty. Recognises Markdown headings (### Steps) and
	/// Jira wiki headings (h3. Steps).
	/// </summary>
	public static string? Section(string body, string headingPattern)
	{
		Regex heading = new($@"^\s*(?:#{{1,6}}\s*|h[1-6]\.\s*)(?:{headingPattern})", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
		string[] lines = body.ReplaceLineEndings("\n").Split('\n');

		for (int index = 0; index < lines.Length; index++)
		{
			if (!heading.IsMatch(lines[index]))
			{
				continue;
			}

			IEnumerable<string> sectionLines = lines
				.Skip(index + 1)
				.TakeWhile(line => !AnyHeading().IsMatch(line));
			string text = string.Join('\n', sectionLines).Trim();
			return text.Length == 0 ? null : text;
		}

		return null;
	}

	/// <summary>
	/// Removes HTML comments (issue-template instructions) and collapsed details blocks (system information tables),
	/// which say nothing about the problem but would count towards a description's length.
	/// </summary>
	public static string WithoutBoilerplate(string body)
		=> DetailsBlock().Replace(HtmlComment().Replace(body, string.Empty), string.Empty).Trim();

	/// <summary>
	/// Converts Jira wiki numbered and bulleted list items (# item, * item) to Markdown (1. item, - item), so list
	/// detection works the same for every platform.
	/// </summary>
	public static string JiraListsToMarkdown(string text)
		=> JiraBullet().Replace(JiraNumbered().Replace(text, "1. "), "- ");

	[GeneratedRegex(@"^\s*(?:#{1,6}\s|h[1-6]\.\s)", RegexOptions.None, 1000)]
	private static partial Regex AnyHeading();

	[GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline, 1000)]
	private static partial Regex HtmlComment();

	[GeneratedRegex(@"<details>.*?</details>", RegexOptions.Singleline | RegexOptions.IgnoreCase, 1000)]
	private static partial Regex DetailsBlock();

	[GeneratedRegex(@"^#\s+", RegexOptions.Multiline, 1000)]
	private static partial Regex JiraNumbered();

	[GeneratedRegex(@"^\*\s+", RegexOptions.Multiline, 1000)]
	private static partial Regex JiraBullet();
}
