using System.Collections.Frozen;
using System.Text;
using static PanoramicData.IssueAssessment.Mapping.JsonReading;

namespace PanoramicData.IssueAssessment.Mapping;

/// <summary>
/// Flattens Atlassian Document Format (the description format of Jira REST API v3) to wiki-style text: headings become
/// <c>h2.</c> lines and list items <c>*</c> or <c>#</c> lines, so section rules read v2 and v3 descriptions the same way.
/// </summary>
internal static class AdfFlattener
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

	public static string Flatten(JsonElement document)
	{
		StringBuilder text = new();
		AppendBlock(document, text, string.Empty);
		return text.ToString().TrimEnd();
	}

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
}
