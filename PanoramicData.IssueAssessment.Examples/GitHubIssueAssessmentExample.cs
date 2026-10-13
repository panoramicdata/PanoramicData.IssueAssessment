using System.Text.Json;
using PanoramicData.IssueAssessment.Core;

namespace PanoramicData.IssueAssessment.Examples;

/// <summary>
/// Scores a GitHub issue as returned by the GitHub REST API (<c>GET /repos/{owner}/{repo}/issues/{number}</c>).
/// </summary>
/// <remarks>
/// GitHub has no separate fields for reproduction steps or acceptance criteria, so they are read from the headed
/// sections of the body that issue templates produce. Template comments and collapsed system-information blocks are
/// removed first: without that, an unfilled template scores as a long, detailed description.
/// </remarks>
internal static class GitHubIssueAssessmentExample
{
	public static async Task RunAsync(CancellationToken cancellationToken)
	{
		using JsonDocument raw = JsonDocument.Parse(await File.ReadAllTextAsync(
			Path.Combine(AppContext.BaseDirectory, "Fixtures", "github-issue.json"),
			cancellationToken).ConfigureAwait(false));
		using JsonDocument issue = Normalize(raw);

		IAssessmentResult result = await BuiltInCategories.CreateAssessor().AssessAsync(issue, cancellationToken).ConfigureAwait(false);
		ResultPrinter.Print($"GitHub #{raw.RootElement.GetProperty("number").GetInt32()}", result);
	}

	public static JsonDocument Normalize(JsonDocument gitHubIssue)
	{
		JsonElement root = gitHubIssue.RootElement;
		string body = IssueText.WithoutBoilerplate(root.GetString("body") ?? string.Empty);

		return CanonicalIssue.Create(
			title: root.GetString("title"),
			description: body,
			acceptanceCriteria: IssueText.Section(body, "acceptance criteria|expected behaviou?r|expected result"),
			reproductionSteps: IssueText.Section(body, "steps to reproduce|reproduction steps|repro"),
			assignee: root.TryGetProperty("assignee", out JsonElement assignee) && assignee.ValueKind == JsonValueKind.Object
				? assignee.GetString("login")
				: null,
			dueDate: root.TryGetProperty("milestone", out JsonElement milestone) && milestone.ValueKind == JsonValueKind.Object
				? milestone.GetString("due_on")
				: null);
	}
}
