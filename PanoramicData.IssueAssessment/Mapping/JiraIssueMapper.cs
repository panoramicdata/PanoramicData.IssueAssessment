using System.Text;

namespace PanoramicData.IssueAssessment.Mapping;

/// <summary>
/// Maps an issue from the Jira REST API (<c>GET /rest/api/2/issue/{key}</c> or <c>/rest/api/3/issue/{key}</c>) to a
/// <see cref="NormalizedIssue"/>.
/// </summary>
/// <remarks>
/// API v2 returns the description as wiki markup, which is used as it is. API v3 returns Atlassian Document Format,
/// which is flattened to wiki-style text: headings become <c>h2.</c> lines and list items <c>*</c> or <c>#</c> lines,
/// so section rules read both the same way.
/// </remarks>
public static class JiraIssueMapper
{
	/// <summary>
	/// Maps a Jira REST issue.
	/// </summary>
	/// <param name="issue">The issue JSON: an object with <c>key</c> and <c>fields</c>.</param>
	/// <returns>The normalized issue.</returns>
	/// <exception cref="ArgumentException">Thrown when the JSON has no <c>fields</c> object.</exception>
	public static NormalizedIssue FromRestJson(JsonElement issue)
		=> FromRestJson(issue, null);

	/// <summary>
	/// Maps a Jira REST issue, with a link built from <paramref name="baseUrl"/>.
	/// </summary>
	/// <param name="issue">The issue JSON: an object with <c>key</c> and <c>fields</c>.</param>
	/// <param name="baseUrl">The Jira site, such as <c>https://example.atlassian.net</c>, or null for no link.</param>
	/// <returns>The normalized issue.</returns>
	/// <exception cref="ArgumentException">Thrown when the JSON has no <c>fields</c> object.</exception>
	public static NormalizedIssue FromRestJson(JsonElement issue, Uri? baseUrl)
	{
		if (issue.ValueKind != JsonValueKind.Object
			|| !issue.TryGetProperty("fields", out JsonElement fields)
			|| fields.ValueKind != JsonValueKind.Object)
		{
			throw new ArgumentException("A Jira issue must be an object with a 'fields' object.", nameof(issue));
		}

		string? key = Text(issue, "key");
		return new NormalizedIssue
		{
			Key = key,
			Project = Text(fields, "project", "key"),
			Title = Text(fields, "summary"),
			Description = Description(fields),
			IssueType = Text(fields, "issuetype", "name"),
			Priority = Text(fields, "priority", "name"),
			Status = Text(fields, "status", "name"),
			Assignee = Text(fields, "assignee", "displayName"),
			Components = Names(fields, "components"),
			Labels = Strings(fields, "labels"),
			FixVersions = Names(fields, "fixVersions"),
			AttachmentsCount = fields.TryGetProperty("attachment", out JsonElement attachments) && attachments.ValueKind == JsonValueKind.Array
				? attachments.GetArrayLength()
				: 0,
			DueDate = Text(fields, "duedate"),
			Url = baseUrl is null || key is null ? null : new Uri(baseUrl, $"browse/{key}").ToString(),
			Platform = "Jira",
		};
	}

	/// <summary>
	/// Flattens an Atlassian Document Format node to wiki-style text.
	/// </summary>
	/// <param name="document">An ADF document or node.</param>
	/// <returns>The text.</returns>
	public static string FlattenAdf(JsonElement document)
	{
		StringBuilder text = new();
		AppendBlock(document, text, string.Empty);
		return text.ToString().TrimEnd();
	}

	private static string? Description(JsonElement fields)
		=> fields.TryGetProperty("description", out JsonElement description) ? description.ValueKind switch
		{
			JsonValueKind.String => description.GetString(),
			JsonValueKind.Object => FlattenAdf(description),
			_ => null,
		} : null;

	private static void AppendBlock(JsonElement node, StringBuilder text, string listPrefix)
	{
		string type = Text(node, "type") ?? string.Empty;
		switch (type)
		{
			case "heading":
				int level = node.TryGetProperty("attrs", out JsonElement attrs) && attrs.TryGetProperty("level", out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : 2;
				text.Append(CultureInfo.InvariantCulture, $"h{level}. ").Append(Inline(node)).Append('\n');
				break;
			case "bulletList" or "orderedList":
				string marker = listPrefix + (type == "bulletList" ? "*" : "#");
				foreach (JsonElement item in Children(node))
				{
					AppendListItem(item, text, marker);
				}

				break;
			case "codeBlock":
				text.Append("{code}\n").Append(Inline(node)).Append("\n{code}\n");
				break;
			case "paragraph" or "blockquote" or "panel" or "text" or "mention" or "inlineCard" or "hardBreak":
				text.Append(Inline(node)).Append('\n');
				break;
			default:
				foreach (JsonElement child in Children(node))
				{
					AppendBlock(child, text, listPrefix);
				}

				break;
		}
	}

	private static void AppendListItem(JsonElement item, StringBuilder text, string marker)
	{
		bool first = true;
		foreach (JsonElement child in Children(item))
		{
			if (Text(child, "type") is "bulletList" or "orderedList")
			{
				AppendBlock(child, text, marker);
			}
			else
			{
				text.Append(first ? marker + " " : string.Empty).Append(Inline(child)).Append('\n');
				first = false;
			}
		}
	}

	private static string Inline(JsonElement node) => Text(node, "type") switch
	{
		"text" => Text(node, "text") ?? string.Empty,
		"hardBreak" => "\n",
		"mention" or "emoji" => node.TryGetProperty("attrs", out JsonElement attrs) ? Text(attrs, "text") ?? string.Empty : string.Empty,
		"inlineCard" => node.TryGetProperty("attrs", out JsonElement attrs) ? Text(attrs, "url") ?? string.Empty : string.Empty,
		_ => string.Concat(Children(node).Select(Inline)),
	};

	private static JsonElement[] Children(JsonElement node)
		=> node.ValueKind == JsonValueKind.Object && node.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array
			? [.. content.EnumerateArray()]
			: [];

	private static string? Text(JsonElement element, string property)
		=> element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static string? Text(JsonElement element, string property, string nested)
		=> element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) ? Text(value, nested) : null;

	private static ImmutableList<string> Names(JsonElement fields, string property)
		=> fields.TryGetProperty(property, out JsonElement values) && values.ValueKind == JsonValueKind.Array
			? [.. values.EnumerateArray().Select(value => Text(value, "name")).OfType<string>()]
			: [];

	private static ImmutableList<string> Strings(JsonElement fields, string property)
		=> fields.TryGetProperty(property, out JsonElement values) && values.ValueKind == JsonValueKind.Array
			? [.. values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!)]
			: [];
}
