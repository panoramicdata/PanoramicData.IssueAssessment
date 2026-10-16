using System.Text.RegularExpressions;
using static PanoramicData.IssueAssessment.Mapping.JsonReading;

namespace PanoramicData.IssueAssessment.Mapping;

/// <summary>
/// Maps an issue from the GitHub REST API (<c>GET /repos/{owner}/{repo}/issues/{number}</c>) to a
/// <see cref="NormalizedIssue"/>.
/// </summary>
/// <remarks>
/// GitHub has no issue type, priority, component or fix version fields, so they are read the way PanoramicData.Skills'
/// GitHub quality check reads them: the type from labels (<c>bug</c> is a Bug; enhancement, feature, improvement or story
/// is an Improvement; anything else a Task), the priority from a <c>priority:high</c>-style label, components from
/// <c>component:</c> or <c>area:</c> labels, and attachments from images in the body. Fix versions are marked
/// unsupported.
/// </remarks>
public static partial class GitHubIssueMapper
{
	/// <summary>
	/// Maps a GitHub REST issue.
	/// </summary>
	/// <param name="issue">The issue JSON.</param>
	/// <returns>The normalized issue.</returns>
	/// <exception cref="ArgumentException">Thrown when the JSON is not an object.</exception>
	public static NormalizedIssue FromRestJson(JsonElement issue)
	{
		if (issue.ValueKind != JsonValueKind.Object)
		{
			throw new ArgumentException("A GitHub issue must be an object.", nameof(issue));
		}

		ImmutableList<string> labels = Labels(issue);
		string body = StringAt(issue, "body") ?? string.Empty;

		return new NormalizedIssue
		{
			Key = Key(issue),
			Title = StringAt(issue, "title"),
			Description = body,
			IssueType = IssueType(labels),
			Priority = Priority(labels),
			Status = StringAt(issue, "state"),
			Assignee = FirstAssignee(issue),
			Components = Components(labels),
			Labels = labels,
			AttachmentsCount = MarkdownImage().Count(body) + ImageUrl().Count(body),
			SupportsFixVersion = false,
			DueDate = issue.TryGetProperty("milestone", out JsonElement milestone) ? StringAt(milestone, "due_on") : null,
			Url = StringAt(issue, "html_url"),
			Platform = "GitHub",
		};
	}

	private static ImmutableList<string> Labels(JsonElement issue)
		=> issue.TryGetProperty("labels", out JsonElement labels) && labels.ValueKind == JsonValueKind.Array
			? [.. labels.EnumerateArray().Select(label => label.ValueKind == JsonValueKind.String ? label.GetString() : StringAt(label, "name")).OfType<string>()]
			: [];

	private static string? Key(JsonElement issue)
		=> issue.TryGetProperty("number", out JsonElement number) && number.ValueKind == JsonValueKind.Number ? $"#{number.GetRawText()}" : null;

	private static string? FirstAssignee(JsonElement issue)
		=> issue.TryGetProperty("assignees", out JsonElement assignees) && assignees.ValueKind == JsonValueKind.Array && assignees.GetArrayLength() > 0
			? StringAt(assignees[0], "login")
			: null;

	private static string IssueType(ImmutableList<string> labels)
	{
		if (labels.Exists(label => label.Equals("bug", StringComparison.OrdinalIgnoreCase)))
		{
			return "Bug";
		}

		return labels.Exists(label => FeatureLabel().IsMatch(label)) ? "Improvement" : "Task";
	}

	private static string Priority(ImmutableList<string> labels)
	{
		Match? match = labels.Select(label => PriorityLabel().Match(label)).FirstOrDefault(found => found.Success);
		if (match is null)
		{
			return "Unknown";
		}

		string value = match.Groups[1].Value.ToLowerInvariant();
		return char.ToUpperInvariant(value[0]) + value[1..];
	}

	private static ImmutableList<string> Components(ImmutableList<string> labels)
		=> [.. labels
			.Select(label => ComponentLabel().Match(label))
			.Where(match => match.Success)
			.Select(match => match.Groups[2].Value.Trim())
			.Distinct(StringComparer.Ordinal)];


	[GeneratedRegex("enhancement|feature|improvement|story", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
	private static partial Regex FeatureLabel();

	[GeneratedRegex(@"priority[:\-]?(critical|high|major|medium|minor|low|trivial)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
	private static partial Regex PriorityLabel();

	[GeneratedRegex(@"^(component|area)[:\-](.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
	private static partial Regex ComponentLabel();

	[GeneratedRegex(@"!\[[^\]]*\]\(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
	private static partial Regex MarkdownImage();

	[GeneratedRegex(@"https?://\S+\.(png|jpg|jpeg|gif|webp|bmp)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
	private static partial Regex ImageUrl();
}
