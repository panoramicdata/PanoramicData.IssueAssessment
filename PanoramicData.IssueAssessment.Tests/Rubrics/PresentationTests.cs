using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;
using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// The Presentation factor: each section sits in an untitled panel directly under its h2 heading, except where it holds
/// a table or code block. The expected panel form is the one hand-edited onto the acceptance criteria of MS-27513.
/// </summary>
public class PresentationTests : TestBase
{
	private static readonly IssueAssessor Assessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	private const string Untitled = "{panel:bgColor=#F4F5F7}";

	[Fact]
	public void PanelLayoutOf_UntitledPanelsUnderEachHeading_AllPanelledNoneTitled()
	{
		PanelLayout layout = IssueText.PanelLayoutOf(Description(("Summary", Untitled), ("Scope", Untitled)));

		layout.Panelable.Should().Equal("Summary", "Scope");
		layout.Unpanelled.Should().BeEmpty();
		layout.Titled.Should().BeEmpty();
		layout.Panelled.Should().Be(2);
	}

	[Fact]
	public void PanelLayoutOf_TitledPanel_IsReportedAsRepeatingItsHeading()
	{
		PanelLayout layout = IssueText.PanelLayoutOf(Description(("Summary", "{panel:title=Summary|bgColor=#F4F5F7}"), ("Scope", Untitled)));

		layout.Titled.Should().Equal("Summary");
		layout.Unpanelled.Should().BeEmpty();
	}

	[Fact]
	public void PanelLayoutOf_PlainSection_IsUnpanelled()
	{
		PanelLayout layout = IssueText.PanelLayoutOf("h2. Summary\r\nSome words here.\r\n\r\nh2. Scope\r\n{panel}\r\nMore words.\r\n{panel}");

		layout.Panelable.Should().Equal("Summary", "Scope");
		layout.Unpanelled.Should().Equal("Summary");
	}

	[Theory]
	[InlineData("||Name||Value||\r\n|a|b|")]
	[InlineData("{code:c#}\r\nvar x = 1;\r\n{code}")]
	[InlineData("{noformat}\r\nlog line\r\n{noformat}")]
	[InlineData("")]
	[InlineData("TBD")]
	public void PanelLayoutOf_TableCodeOrEmptySection_IsNotPanelable(string body)
	{
		PanelLayout layout = IssueText.PanelLayoutOf($"h2. Evidence\r\n{body}\r\n\r\nh2. Summary\r\n{Untitled}\r\nWords.\r\n{{panel}}");

		layout.Panelable.Should().Equal("Summary");
	}

	[Fact]
	public void PanelLayoutOf_SubHeadingsStayInsideTheirSection()
	{
		PanelLayout layout = IssueText.PanelLayoutOf($"h2. Scope\r\n{Untitled}\r\nh3. In scope\r\nWords.\r\n{{panel}}\r\n\r\nh2. Notes\r\nMore words.");

		layout.Panelable.Should().Equal("Scope", "Notes");
		layout.Unpanelled.Should().Equal("Notes");
	}

	[Fact]
	public void PanelLayoutOf_NoText_IsEmpty() => IssueText.PanelLayoutOf(null).Panelable.Should().BeEmpty();

	[Fact]
	public async Task Assess_AllSectionsInUntitledPanels_ScoresFull()
	{
		AssessmentReport report = await AssessAsync(Description(("Summary", Untitled), ("Scope", Untitled), ("Risks", Untitled)));

		report.Findings.Should().Contain("✓ Presentation: 3 sections in untitled panels");
		report.Recommendations.Should().NotContain(recommendation => recommendation.Contains("panel"));
	}

	[Fact]
	public async Task Assess_PanelTitlesRepeatTheHeading_ScoresLowerAndSaysWhich()
	{
		AssessmentReport report = await AssessAsync(Description(("Summary", "{panel:title=Summary|bgColor=#F4F5F7}"), ("Scope", "{panel:title=Scope}"), ("Risks", Untitled)));

		report.Findings.Should().Contain("⚠ Presentation: 2 panel titles repeat their heading (Summary; Scope)");
		report.Recommendations.Should().Contain(recommendation => recommendation.StartsWith("Remove the title from each panel", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Assess_SomeSectionsNotPanelled_NamesThem()
	{
		AssessmentReport report = await AssessAsync($"h2. Summary\r\n{Untitled}\r\nWords here.\r\n{{panel}}\r\n\r\nh2. Scope\r\nWords here.\r\n\r\nh2. Risks\r\nWords here.");

		report.Findings.Should().Contain("⚠ Presentation: 2 of 3 sections are not in a panel (Scope; Risks)");
	}

	[Fact]
	public async Task Assess_NoPanelsAtAll_IsTheLowestTier()
	{
		AssessmentReport report = await AssessAsync("h2. Summary\r\nWords here.\r\n\r\nh2. Scope\r\nWords here.");

		report.Findings.Should().Contain("✗ Presentation: None of 2 sections is in a panel (Summary; Scope)");
	}

	[Fact]
	public async Task Assess_TableSectionsAreExempt()
	{
		string description = $"h2. Summary\r\n{Untitled}\r\nWords here.\r\n{{panel}}\r\n\r\nh2. Scope\r\n{Untitled}\r\nWords here.\r\n{{panel}}\r\n\r\nh2. Evidence\r\n||Name||Value||\r\n|a|b|";

		(await AssessAsync(description)).Findings.Should().Contain("✓ Presentation: 2 sections in untitled panels");
	}

	[Fact]
	public async Task Assess_FewerThanTwoPanelableSections_LeavesPresentationOut()
	{
		AssessmentReport report = await AssessAsync("h2. Summary\r\nWords here.");

		report.Findings.Should().NotContain(finding => finding.Contains("Presentation"));
	}

	[Fact]
	public async Task Assess_NotJira_LeavesPresentationOut()
	{
		AssessmentReport report = await AssessAsync("h2. Summary\r\nWords here.\r\n\r\nh2. Scope\r\nWords here.", platform: "GitHub");

		report.Findings.Should().NotContain(finding => finding.Contains("Presentation"));
	}

	[Fact]
	public async Task Assess_Epic_IsScoredOnPresentationToo()
	{
		AssessmentReport report = await AssessAsync(Description(("Summary", Untitled), ("Scope", Untitled)), issueType: "Epic");

		report.Findings.Should().Contain("✓ Presentation: 2 sections in untitled panels");
	}

	private static string Description(params (string Heading, string PanelOpening)[] sections)
		=> string.Join("\r\n\r\n", sections.Select(section => $"h2. {section.Heading}\r\n{section.PanelOpening}\r\nSome words about {section.Heading}.\r\n{{panel}}"));

	private static async Task<AssessmentReport> AssessAsync(string description, string platform = "Jira", string issueType = "Task")
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

		return AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));
	}
}
