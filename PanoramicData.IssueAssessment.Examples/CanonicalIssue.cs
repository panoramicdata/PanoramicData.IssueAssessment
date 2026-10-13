using System.Text.Json;
using System.Text.Json.Nodes;

namespace PanoramicData.IssueAssessment.Examples;

/// <summary>
/// Builds the normalized ticket shape the built-in categories read:
/// <c>title</c>, <c>description</c>, <c>acceptanceCriteria</c>, <c>reproductionSteps</c>, <c>assignee</c>,
/// <c>assigneeState</c> and <c>dueDate</c>. Absent or blank values are left out, so they score as missing.
/// </summary>
internal static class CanonicalIssue
{
	public static JsonDocument Create(
		string? title,
		string? description,
		string? acceptanceCriteria,
		string? reproductionSteps,
		string? assignee,
		string? dueDate)
	{
		JsonObject issue = [];
		Add(issue, "title", title);
		Add(issue, "description", description);
		Add(issue, "acceptanceCriteria", acceptanceCriteria);
		Add(issue, "reproductionSteps", reproductionSteps);
		Add(issue, "assignee", assignee);
		if (!string.IsNullOrWhiteSpace(assignee))
		{
			issue["assigneeState"] = "Assigned";
		}

		Add(issue, "dueDate", dueDate);
		return JsonDocument.Parse(issue.ToJsonString());
	}

	private static void Add(JsonObject issue, string name, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			issue[name] = value.Trim();
		}
	}
}
