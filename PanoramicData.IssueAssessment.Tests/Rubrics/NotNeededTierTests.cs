using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

public class NotNeededTierTests : TestBase
{
	private static readonly IssueAssessor Assessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	[Fact]
	public async Task NoDocumentationChangeWithReason_Scores100()
	{
		AssessmentReportItem item = await Item("Documentation", "No documentation change: internal refactor with no user-visible behaviour", "Improvement", "MS");
		item.RawScore.Should().Be(100);
		item.Finding.Should().Be("✓ Documentation: Not needed (reason given)");
	}

	[Fact]
	public async Task NoDocumentationChangeBare_Scores0()
		=> (await Item("Documentation", "No documentation change.", "Improvement", "MS")).Finding.Should().NotContain("Not needed");

	[Fact]
	public async Task NoDocumentationChangeNA_Scores0()
		=> (await Item("Documentation", "No documentation change: N/A", "Improvement", "MS")).Finding.Should().NotContain("Not needed");

	[Fact]
	public async Task NoCertificationQuestionWithReason_Scores100()
	{
		AssessmentReportItem item = await Item("Certifications", "No certification question: internal refactor with no user-visible behaviour", "Improvement", "MS");
		item.RawScore.Should().Be(100);
		item.Finding.Should().Be("✓ Certifications: Not needed (reason given)");
	}

	[Fact]
	public async Task NoCertificationQuestionBare_Scores0()
		=> (await Item("Certifications", "No certification question.", "Improvement", "MS")).Finding.Should().NotContain("Not needed");

	[Fact]
	public async Task NoCertificationQuestionNA_Scores0()
		=> (await Item("Certifications", "No certification question: N/A", "Improvement", "MS")).Finding.Should().NotContain("Not needed");

	[Fact]
	public async Task AppliesToOpsToo()
	{
		(await Item("Documentation", "No documentation change: internal refactor with no user-visible behaviour", "Improvement", "OPS")).RawScore.Should().Be(100);
		(await Item("Certifications", "No certification question: internal refactor with no user-visible behaviour", "Improvement", "OPS")).RawScore.Should().Be(100);
	}

	[Fact]
	public async Task BugCertifications_StillNotApplicable()
		=> (await Item("Certifications", "No certification question: internal refactor with no user-visible behaviour", "Bug", "MS")).Finding.Should().Be("○ Certifications: Not applicable for bugs");

	private static async Task<AssessmentReportItem> Item(string name, string description, string issueType, string project)
	{
		NormalizedIssue issue = new()
		{
			Key = project + "-1",
			Project = project,
			Title = "Tidy the schedule code",
			Description = description,
			IssueType = issueType,
			Status = "In Progress",
			Platform = "Jira",
			Labels = [],
		};
		using JsonDocument document = issue.ToJsonDocument();
		AssessmentReport report = AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));
		return report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == name);
	}
}
