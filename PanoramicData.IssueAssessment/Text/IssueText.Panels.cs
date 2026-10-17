using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// Reads how Jira wiki sections are laid out in panels.
/// </summary>
public static partial class IssueText
{
	/// <summary>
	/// Reads how the <c>h2.</c> sections of Jira wiki text are laid out in panels.
	/// </summary>
	/// <param name="text">The ticket text.</param>
	/// <returns>The headings of the sections that could be panelled, are not, or carry a repeated panel title.</returns>
	/// <remarks>
	/// A section is panelled when its first line is <c>{panel}</c>. It could be panelled when it has content and no table
	/// or code block, since those do not sit well in a panel. A panel directly under a heading should carry no
	/// <c>title=</c>, because the title bar only repeats the heading above it.
	/// </remarks>
	public static PanelLayout PanelLayoutOf(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return PanelLayout.Empty;
		}

		List<string> panelable = [];
		List<string> unpanelled = [];
		List<string> titled = [];
		string? heading = null;
		List<string> body = [];
		foreach (string line in LineBreak.Split(text))
		{
			Match opening = PanelSectionHeading.Match(line);
			if (opening.Success)
			{
				Classify(heading, body, panelable, unpanelled, titled);
				heading = opening.Groups[1].Value == "2" ? opening.Groups[2].Value.Trim() : null;
				body = [];
			}
			else if (heading is not null)
			{
				body.Add(line);
			}
		}

		Classify(heading, body, panelable, unpanelled, titled);
		return new PanelLayout(panelable, unpanelled, titled);
	}

	private static void Classify(string? heading, List<string> body, List<string> panelable, List<string> unpanelled, List<string> titled)
	{
		if (heading is null)
		{
			return;
		}

		string? first = body.Find(line => line.Trim().Length > 0);
		Match panel = first is null ? Match.Empty : PanelOpening.Match(first);
		if (panel.Success)
		{
			panelable.Add(heading);
			AddIf(titled, heading, panel.Groups[1].Value.Contains("title=", StringComparison.OrdinalIgnoreCase));
			return;
		}

		bool hasContent = first is not null && !IsPlaceholder(string.Join('\n', body));
		if (hasContent && !body.Exists(line => PanelBlocker.IsMatch(line)))
		{
			panelable.Add(heading);
			unpanelled.Add(heading);
		}
	}

	private static void AddIf(List<string> list, string item, bool condition)
	{
		if (condition)
		{
			list.Add(item);
		}
	}
}
