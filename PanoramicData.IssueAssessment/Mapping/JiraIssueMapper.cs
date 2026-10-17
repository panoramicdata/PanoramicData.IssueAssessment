using System.Collections.Frozen;
using System.Text;
using static PanoramicData.IssueAssessment.Mapping.JsonReading;

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
	// ADF list types, and the wiki marker each nesting level adds.
	private static readonly FrozenDictionary<string, string> ListMarkers = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["bulletList"] = "*",
		["orderedList"] = "#",
	}.ToFrozenDictionary(StringComparer.Ordinal);

	// ADF nodes written as one line of inline text.
	private static readonly FrozenSet<string> LineBlockTypes = FrozenSet.Create(
		StringComparer.Ordinal,
		"paragraph", "blockquote", "panel", "text", "mention", "inlineCard", "hardBreak");

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
		JsonElement fields = Fields(issue);
		string? key = StringAt(issue, "key");
		return new NormalizedIssue
		{
			Key = key,
			Project = StringAt(fields, "project", "key"),
			Title = StringAt(fields, "summary"),
			Description = Description(fields),
			IssueType = StringAt(fields, "issuetype", "name"),
			Priority = StringAt(fields, "priority", "name"),
			Status = StringAt(fields, "status", "name"),
			Assignee = StringAt(fields, "assignee", "displayName"),
			Components = Names(fields, "components"),
			Labels = Strings(fields, "labels"),
			FixVersions = Names(fields, "fixVersions"),
			AttachmentsCount = ArrayLength(fields, "attachment"),
			DueDate = StringAt(fields, "duedate"),
			Url = BrowseUrl(baseUrl, key),
			Platform = "Jira",
		};
	}

	private static JsonElement Fields(JsonElement issue)
	{
		if (issue.ValueKind == JsonValueKind.Object
			&& issue.TryGetProperty("fields", out JsonElement fields)
			&& fields.ValueKind == JsonValueKind.Object)
		{
			return fields;
		}

		throw new ArgumentException("A Jira issue must be an object with a 'fields' object.", nameof(issue));
	}

	private static int ArrayLength(JsonElement element, string name)
		=> element.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array ? array.GetArrayLength() : 0;

	private static string? BrowseUrl(Uri? baseUrl, string? key)
		=> baseUrl is null || key is null ? null : new Uri(baseUrl, $"browse/{key}").ToString();

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
		string type = StringAt(node, "type") ?? string.Empty;
		if (ListMarkers.TryGetValue(type, out string? listMarker))
		{
			AppendList(node, text, listPrefix + listMarker);
		}
		else if (type == "heading")
		{
			text.Append(CultureInfo.InvariantCulture, $"h{AdfHeadingLevel(node)}. ").Append(Inline(node)).Append('\n');
		}
		else if (type == "codeBlock")
		{
			text.Append("{code}\n").Append(Inline(node)).Append("\n{code}\n");
		}
		else if (LineBlockTypes.Contains(type))
		{
			text.Append(Inline(node)).Append('\n');
		}
		else
		{
			foreach (JsonElement child in Children(node))
			{
				AppendBlock(child, text, listPrefix);
			}
		}
	}

	private static void AppendList(JsonElement list, StringBuilder text, string marker)
	{
		foreach (JsonElement item in Children(list))
		{
			AppendListItem(item, text, marker);
		}
	}

	private static int AdfHeadingLevel(JsonElement heading)
		=> heading.TryGetProperty("attrs", out JsonElement attrs) && attrs.TryGetProperty("level", out JsonElement value) && value.TryGetInt32(out int level)
			? level
			: 2;

	private static void AppendListItem(JsonElement item, StringBuilder text, string marker)
	{
		bool first = true;
		foreach (JsonElement child in Children(item))
		{
			if (StringAt(child, "type") is "bulletList" or "orderedList")
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

	private static string Inline(JsonElement node) => StringAt(node, "type") switch
	{
		"text" => StringAt(node, "text") ?? string.Empty,
		"hardBreak" => "\n",
		"mention" or "emoji" => node.TryGetProperty("attrs", out JsonElement attrs) ? StringAt(attrs, "text") ?? string.Empty : string.Empty,
		"inlineCard" => node.TryGetProperty("attrs", out JsonElement attrs) ? StringAt(attrs, "url") ?? string.Empty : string.Empty,
		_ => string.Concat(Children(node).Select(Inline)),
	};

	private static JsonElement[] Children(JsonElement node)
		=> node.ValueKind == JsonValueKind.Object && node.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array
			? [.. content.EnumerateArray()]
			: [];


	private static ImmutableList<string> Names(JsonElement fields, string property)
		=> fields.TryGetProperty(property, out JsonElement values) && values.ValueKind == JsonValueKind.Array
			? [.. values.EnumerateArray().Select(value => StringAt(value, "name")).OfType<string>()]
			: [];

	private static ImmutableList<string> Strings(JsonElement fields, string property)
		=> fields.TryGetProperty(property, out JsonElement values) && values.ValueKind == JsonValueKind.Array
			? [.. values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!)]
			: [];
}
