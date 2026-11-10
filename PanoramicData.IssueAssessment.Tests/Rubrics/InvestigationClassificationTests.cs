using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// Issue #7: an investigation is named by its title or a label, never by one word in the description.
/// </summary>
public class InvestigationClassificationTests : TestBase
{
	private static readonly IssueAssessor Assessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	private const string ImprovementWithBusinessCase =
		"h2. Summary\r\nShow run durations on the schedule list.\r\n\r\n"
		+ "h2. Business case\r\nSupport cannot assess where the time goes without opening each run.\r\n\r\n"
		+ "h2. Current behaviour\r\nDurations are only on the run page.\r\n\r\n"
		+ "h2. Requirements\r\n# Add a duration column.\r\n";

	[Fact]
	public async Task InvestigationWordOnlyInDescription_IsNotAnInvestigation()
		=> (await IsInvestigationAsync("Show run durations on the schedule list", ImprovementWithBusinessCase, [])).Should().BeFalse();

	[Theory]
	[InlineData("investigation")]
	[InlineData("research")]
	[InlineData("spike")]
	public async Task InvestigationLabel_IsStillAnInvestigation(string label)
		=> (await IsInvestigationAsync("Show run durations on the schedule list", ImprovementWithBusinessCase, [label])).Should().BeTrue();

	[Fact]
	public async Task InvestigationWordInTitle_IsStillAnInvestigation()
		=> (await IsInvestigationAsync("Investigate why run durations drift", ImprovementWithBusinessCase, [])).Should().BeTrue();

	private static async Task<bool> IsInvestigationAsync(string title, string description, List<string> labels)
	{
		NormalizedIssue issue = new()
		{
			Key = "MS-1",
			Project = "MS",
			Title = title,
			Description = description,
			IssueType = "Improvement",
			Status = "In Progress",
			Platform = "Jira",
			Labels = [.. labels],
		};
		using JsonDocument document = issue.ToJsonDocument();
		AssessmentReport report = AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));
		return report.Flags["isInvestigation"];
	}
}
