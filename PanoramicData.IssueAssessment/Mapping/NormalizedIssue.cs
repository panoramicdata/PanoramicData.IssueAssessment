using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Mapping;

/// <summary>
/// A ticket from any system in the shape the built-in categories and rubrics read. Build one with a mapper
/// (<see cref="JiraIssueMapper"/>, <see cref="GitHubIssueMapper"/>, <see cref="ServiceNowRecordMapper"/>) or directly,
/// then assess <see cref="ToJsonDocument"/>.
/// </summary>
/// <remarks>
/// JSON property names are camelCase (<c>$.title</c>, <c>$.issueType</c>, <c>$.components</c>). Unset values are left
/// out, so they read as missing; list properties are always written, so an empty list reads as present but empty.
/// </remarks>
public sealed partial record NormalizedIssue
{
	/// <summary>Gets the issue key, such as <c>MS-1234</c> or <c>#42</c>.</summary>
	public string? Key { get; init; }

	/// <summary>Gets the project key: set explicitly, or taken from a key such as <c>MS-1234</c>.</summary>
	public string? Project { get; init; }

	/// <summary>Gets the one-line summary.</summary>
	public string? Title { get; init; }

	/// <summary>Gets the description, as text (Jira wiki markup or Markdown).</summary>
	public string? Description { get; init; }

	/// <summary>Gets the issue type, such as Bug, Story, Epic or Change Request.</summary>
	public string? IssueType { get; init; }

	/// <summary>Gets the priority name.</summary>
	public string? Priority { get; init; }

	/// <summary>Gets the workflow status name.</summary>
	public string? Status { get; init; }

	/// <summary>Gets the assignee's name or login.</summary>
	public string? Assignee { get; init; }

	/// <summary>Gets the assignment state the built-in ownership category reads (Assigned, InProgress, Owned).</summary>
	public string? AssigneeState { get; init; }

	/// <summary>Gets the component names.</summary>
	public ImmutableList<string> Components { get; init; } = [];

	/// <summary>Gets the labels.</summary>
	public ImmutableList<string> Labels { get; init; } = [];

	/// <summary>Gets the fix version names.</summary>
	public ImmutableList<string> FixVersions { get; init; } = [];

	/// <summary>Gets the number of attachments.</summary>
	public int AttachmentsCount { get; init; }

	/// <summary>Gets whether the system has fix versions at all; null when not known.</summary>
	public bool? SupportsFixVersion { get; init; }

	/// <summary>Gets whether the project uses components; null to let the rubric decide.</summary>
	public bool? SupportsComponents { get; init; }

	/// <summary>Gets the due date, as text.</summary>
	public string? DueDate { get; init; }

	/// <summary>Gets acceptance criteria held in a field of their own, if the system has one.</summary>
	public string? AcceptanceCriteria { get; init; }

	/// <summary>Gets reproduction steps held in a field of their own, if the system has one.</summary>
	public string? ReproductionSteps { get; init; }

	/// <summary>Gets a link to the issue.</summary>
	public string? Url { get; init; }

	/// <summary>Gets the system the issue came from, such as Jira, GitHub or ServiceNow.</summary>
	public string? Platform { get; init; }

	/// <summary>
	/// The project key from an issue key such as <c>MS-1234</c>, or null.
	/// </summary>
	public static string? ProjectFromKey(string? key)
		=> key is null ? null : ProjectKeyPattern().Match(key) is { Success: true } match ? match.Groups[1].Value : null;

	/// <summary>
	/// Writes the issue as the JSON document assessments read.
	/// </summary>
	/// <returns>A new document; the caller disposes it.</returns>
	public JsonDocument ToJsonDocument() => JsonDocument.Parse(ToJsonObject().ToJsonString());

	/// <summary>
	/// Writes the issue as a mutable JSON object, for callers that add fields of their own.
	/// </summary>
	public JsonObject ToJsonObject()
	{
		JsonObject issue = [];
		Add(issue, "key", Key);
		Add(issue, "project", Project ?? ProjectFromKey(Key));
		Add(issue, "title", Title);
		Add(issue, "description", Description);
		Add(issue, "issueType", IssueType);
		Add(issue, "priority", Priority);
		Add(issue, "status", Status);
		Add(issue, "assignee", Assignee);
		Add(issue, "assigneeState", AssigneeState);
		issue["components"] = new JsonArray([.. Components.Select(value => (JsonNode?)value)]);
		issue["labels"] = new JsonArray([.. Labels.Select(value => (JsonNode?)value)]);
		issue["fixVersions"] = new JsonArray([.. FixVersions.Select(value => (JsonNode?)value)]);
		issue["attachmentsCount"] = AttachmentsCount;
		if (SupportsFixVersion is { } supportsFixVersion)
		{
			issue["supportsFixVersion"] = supportsFixVersion;
		}

		if (SupportsComponents is { } supportsComponents)
		{
			issue["supportsComponents"] = supportsComponents;
		}

		Add(issue, "dueDate", DueDate);
		Add(issue, "acceptanceCriteria", AcceptanceCriteria);
		Add(issue, "reproductionSteps", ReproductionSteps);
		Add(issue, "url", Url);
		Add(issue, "platform", Platform);
		return issue;
	}

	private static void Add(JsonObject issue, string name, string? value)
	{
		if (value is not null)
		{
			issue[name] = value;
		}
	}

	[GeneratedRegex(@"^([A-Za-z][A-Za-z0-9]+)-\d+$", RegexOptions.CultureInvariant, 1000)]
	private static partial Regex ProjectKeyPattern();
}
