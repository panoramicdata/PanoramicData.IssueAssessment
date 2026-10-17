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
		return AdfFlattener.Flatten(document);
	}

	private static string? Description(JsonElement fields)
		=> fields.TryGetProperty("description", out JsonElement description) ? description.ValueKind switch
		{
			JsonValueKind.String => description.GetString(),
			JsonValueKind.Object => FlattenAdf(description),
			_ => null,
		} : null;

	private static ImmutableList<string> Names(JsonElement fields, string property)
		=> fields.TryGetProperty(property, out JsonElement values) && values.ValueKind == JsonValueKind.Array
			? [.. values.EnumerateArray().Select(value => StringAt(value, "name")).OfType<string>()]
			: [];

	private static ImmutableList<string> Strings(JsonElement fields, string property)
		=> fields.TryGetProperty(property, out JsonElement values) && values.ValueKind == JsonValueKind.Array
			? [.. values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!)]
			: [];
}
