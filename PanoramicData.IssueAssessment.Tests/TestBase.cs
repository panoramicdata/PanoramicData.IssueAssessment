using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests;

/// <summary>
/// Base test class
/// </summary>
public abstract class TestBase
{
	/// <summary>
	/// Provides a cancellation token for test methods, which can be used to handle timeouts or cancellations in asynchronous operations during testing.
	/// </summary>
	protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

	/// <summary>
	/// Assesses an in-progress Jira issue with the given fields.
	/// </summary>
	protected static async Task<AssessmentReport> AssessJiraAsync(IssueAssessor assessor, string project, string title, string description, string issueType, params string[] labels)
	{
		NormalizedIssue issue = new()
		{
			Key = project + "-1",
			Project = project,
			Title = title,
			Description = description,
			IssueType = issueType,
			Status = "In Progress",
			Platform = "Jira",
			Labels = [.. labels],
		};
		using JsonDocument document = issue.ToJsonDocument();
		return AssessmentReport.From(await assessor.AssessAsync(document, CancellationToken));
	}

	private static readonly IssueAssessor TicketQualityAssessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	/// <summary>
	/// Assesses a Jira-style example issue against the built-in ticket quality rubric.
	/// </summary>
	protected static async Task<AssessmentReport> AssessDescriptionAsync(string description, string platform = "Jira", string issueType = "Task")
	{
		using JsonDocument document = new NormalizedIssue
		{
			Key = "MS-1",
			Title = "Example",
			Description = description,
			IssueType = issueType,
			Status = "Open",
			Platform = platform,
		}.ToJsonDocument();

		return AssessmentReport.From(await TicketQualityAssessor.AssessAsync(document, CancellationToken));
	}
}