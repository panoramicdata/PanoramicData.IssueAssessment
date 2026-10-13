using System.Text.Json;
using PanoramicData.IssueAssessment.Core;

namespace PanoramicData.IssueAssessment.Examples;

/// <summary>
/// Scores a ServiceNow incident as returned by the Table API (<c>GET /api/now/table/incident/{sys_id}</c>).
/// </summary>
/// <remarks>
/// The fixture is a deliberately poor ticket: a two-word summary, a one-line description, no assignee and no due
/// date. It shows what a low score and its explanations look like.
/// </remarks>
internal static class ServiceNowTicketAssessmentExample
{
	public static async Task RunAsync(CancellationToken cancellationToken)
	{
		using JsonDocument raw = JsonDocument.Parse(await File.ReadAllTextAsync(
			Path.Combine(AppContext.BaseDirectory, "Fixtures", "servicenow-incident.json"),
			cancellationToken).ConfigureAwait(false));
		using JsonDocument issue = Normalize(raw);

		IAssessmentResult result = await BuiltInCategories.CreateAssessor().AssessAsync(issue, cancellationToken).ConfigureAwait(false);
		ResultPrinter.Print($"ServiceNow {raw.RootElement.GetProperty("result").GetString("number")}", result);
	}

	public static JsonDocument Normalize(JsonDocument serviceNowRecord)
	{
		JsonElement record = serviceNowRecord.RootElement.GetProperty("result");
		string description = record.GetString("description") ?? string.Empty;

		return CanonicalIssue.Create(
			title: record.GetString("short_description"),
			description: description,
			acceptanceCriteria: IssueText.Section(description, "acceptance criteria|expected"),
			reproductionSteps: IssueText.Section(description, "steps to reproduce|repro"),
			assignee: record.GetString("assigned_to"),
			dueDate: record.GetString("due_date"));
	}
}
