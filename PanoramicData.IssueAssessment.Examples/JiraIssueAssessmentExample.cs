using System.Text.Json;
using PanoramicData.IssueAssessment.Core;

namespace PanoramicData.IssueAssessment.Examples;

/// <summary>
/// Scores a Jira issue as returned by the Jira REST API v2 (<c>GET /rest/api/2/issue/{key}</c>).
/// </summary>
/// <remarks>
/// Jira keeps acceptance criteria in a site-specific custom field; set <see cref="AcceptanceCriteriaField"/> to yours.
/// The v2 API returns the description as wiki markup; API v3 returns Atlassian Document Format, which you would
/// flatten to text first.
/// </remarks>
internal static class JiraIssueAssessmentExample
{
	private const string AcceptanceCriteriaField = "customfield_10034";

	public static async Task RunAsync(CancellationToken cancellationToken)
	{
		using JsonDocument raw = JsonDocument.Parse(await File.ReadAllTextAsync(
			Path.Combine(AppContext.BaseDirectory, "Fixtures", "jira-issue.json"),
			cancellationToken).ConfigureAwait(false));
		using JsonDocument issue = Normalize(raw);

		IAssessmentResult result = await BuiltInCategories.CreateAssessor().AssessAsync(issue, cancellationToken).ConfigureAwait(false);
		ResultPrinter.Print($"Jira {raw.RootElement.GetProperty("key").GetString()}", result);
	}

	public static JsonDocument Normalize(JsonDocument jiraIssue)
	{
		JsonElement fields = jiraIssue.RootElement.GetProperty("fields");
		string description = IssueText.JiraListsToMarkdown(fields.GetString("description") ?? string.Empty);

		return CanonicalIssue.Create(
			title: fields.GetString("summary"),
			description: description,
			acceptanceCriteria: fields.GetString(AcceptanceCriteriaField)
				?? IssueText.Section(description, "acceptance criteria|expected"),
			reproductionSteps: IssueText.Section(description, "steps to reproduce|repro"),
			assignee: fields.TryGetProperty("assignee", out JsonElement assignee) && assignee.ValueKind == JsonValueKind.Object
				? assignee.GetString("displayName")
				: null,
			dueDate: fields.GetString("duedate"));
	}
}
