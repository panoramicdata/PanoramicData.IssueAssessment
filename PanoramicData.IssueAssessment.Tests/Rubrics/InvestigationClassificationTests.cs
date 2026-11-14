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
	{
		string description = ImprovementWithBusinessCase;
		bool isInvestigation = await FlagAsync("Show run durations on the schedule list", description, [], "Improvement");
		isInvestigation.Should().BeFalse();
	}

	[Theory]
	[InlineData("investigation")]
	[InlineData("research")]
	[InlineData("spike")]
	public async Task InvestigationLabel_IsStillAnInvestigation(string label)
	{
		string description = ImprovementWithBusinessCase;
		bool isInvestigation = await FlagAsync("Show run durations on the schedule list", description, [label], "Improvement");
		isInvestigation.Should().BeTrue();
	}

	[Fact]
	public async Task InvestigationWordInTitle_IsStillAnInvestigation()
	{
		string description = ImprovementWithBusinessCase;
		bool isInvestigation = await FlagAsync("Investigate why run durations drift", description, [], "Improvement");
		isInvestigation.Should().BeTrue();
	}

	[Theory]
	[InlineData("CaseMagic: MQL should support JQL functions, and refuse clearly the ones CaseMagic cannot evaluate")]
	[InlineData("Assess the run durations on the schedule list")]
	[InlineData("Study mode for the schedule list")]
	[InlineData("Explore button on the schedule list")]
	[InlineData("Analysis column on the schedule list")]
	public async Task AmbiguousWordInAnImprovementTitle_IsNotAnInvestigation(string title)
	{
		string description = ImprovementWithBusinessCase;
		bool isInvestigation = await FlagAsync(title, description, [], "Improvement");
		isInvestigation.Should().BeFalse();
	}

	[Theory]
	[InlineData("Evaluate options for the schedule list")]
	[InlineData("Analysis of run duration drift")]
	public async Task AmbiguousWordInATaskTitle_IsStillAnInvestigation(string title)
	{
		string description = ImprovementWithBusinessCase;
		bool isInvestigation = await FlagAsync(title, description, [], "Task");
		isInvestigation.Should().BeTrue();
	}

	private static async Task<bool> FlagAsync(string title, string description, List<string> labels, string issueType)
	{
		NormalizedIssue issue = new()
		{
			Key = "MS-1",
			Project = "MS",
			Title = title,
			Description = description,
			IssueType = issueType,
			Status = "In Progress",
			Platform = "Jira",
			Labels = [.. labels],
		};
		using JsonDocument document = issue.ToJsonDocument();
		AssessmentReport report = AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));
		return report.Flags["isInvestigation"];
	}
}
