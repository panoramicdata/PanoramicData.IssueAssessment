using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// The MS-only TicketContract assessment: criteria are classified MUST/SHOULD/MAY, and from Ready for Test on a
/// Verification Evidence section is present. A failing tier caps the score.
/// </summary>
public class TicketContractAssessmentTests : TestBase
{
	private const string CapName = "Ticket contract";
	private static readonly IssueAssessor Assessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	private const string Summary = "h2. Summary\r\nRun times drift when the tenant timezone changes.\r\n\r\n";
	private const string ClassifiedCriteria = "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its local run time after a timezone change.\r\n# SHOULD: the schedule list shows the new offset.\r\n# MAY: the audit log records the change.\r\n\r\n";

	private const string FullEvidence = """
		h2. Verification Evidence

		*Automated coverage of the MUST criteria*
		|| Criterion || Test || Location ||
		| MUST 1 | GitFileSystemProvider_RecoversFromMissingIndex | MagicSuite.SystemAccess.Test/Git/GitFileSystemProviderTests.cs |
		| MUST 2 | (none - not reachable from a test) | - |

		*Reachability - how to make the covered code actually run*
		Files > any Git-backed connection > open any file. The recovery path runs on the
		first read after the clone is corrupted.

		*Not covered by automated tests, and why*
		MUST 2 needs a corrupted clone on a shared RWX volume; not reproducible in-process.

		*Suggested QA route:* B (regression suite)
		""";

	[Fact]
	public async Task NoCriteriaSection_Fails()
	{
		AssessmentReport report = await AssessAsync(Summary + "h2. Notes\r\nNothing else to say.");

		ContractItem(report).RawScore.Should().Be(0);
		ContractItem(report).Finding.Should().Be("✗ Ticket Contract: No acceptance criteria section");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
		report.NormalizedScore.Should().BeLessThanOrEqualTo(0.79);
	}

	[Fact]
	public async Task EmptyCriteriaSection_Fails()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\nTo be agreed with the team.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: Acceptance criteria section has no criteria");

	[Fact]
	public async Task OneUnclassified_Fails()
	{
		AssessmentReport report = await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n# the schedule list shows the new offset.\r\n");

		ContractItem(report).Finding.Should().Be("✗ Ticket Contract: 1 criteria not classified MUST/SHOULD/MAY");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Fact]
	public async Task AllShouldNoMust_Fails()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# SHOULD: a schedule keeps its run time.\r\n# SHOULD: the list shows the offset.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: No MUST criterion");

	[Fact]
	public async Task ClassifiedInProgress_NoEvidenceNeeded_Passes()
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria);

		ContractItem(report).RawScore.Should().Be(100);
		ContractItem(report).Finding.Should().Be("✓ Ticket Contract: Criteria classified");
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Theory]
	[InlineData("Ready for Test")]
	[InlineData("Ready for Customer")]
	[InlineData("With Customer")]
	public async Task EvidenceDue_NoEvidence_Fails(string status)
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria, status: status);

		ContractItem(report).Finding.Should().Be("✗ Ticket Contract: No Verification Evidence section");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Fact]
	public async Task ReadyForTest_EvidenceMissingRoute_Fails()
	{
		string evidence = FullEvidence.Replace("*Suggested QA route:* B (regression suite)", string.Empty, StringComparison.Ordinal);

		ContractItem(await AssessAsync(Summary + ClassifiedCriteria + evidence, status: "Ready for Test"))
			.Finding.Should().Be("✗ Ticket Contract: Verification Evidence incomplete");
	}

	[Theory]
	[InlineData("Ready for Test")]
	[InlineData("In Test")]
	[InlineData("Ready for Customer")]
	[InlineData("With Customer")]
	[InlineData("Closed")]
	public async Task ReadyForTest_FullEvidence_Passes(string status)
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria + FullEvidence, status: status);

		ContractItem(report).RawScore.Should().Be(100);
		ContractItem(report).Finding.Should().Be("✓ Ticket Contract: Criteria classified; evidence present");
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Fact]
	public async Task RetrospectiveHeading_Passes()
	{
		string evidence = FullEvidence.Replace("h2. Verification Evidence", "h2. Verification Evidence (Retrospective)", StringComparison.Ordinal);

		ContractItem(await AssessAsync(Summary + ClassifiedCriteria + evidence, status: "Ready for Test")).RawScore.Should().Be(100);
	}

	[Theory]
	[InlineData("# MUST: x")]
	[InlineData("# *MUST* x")]
	[InlineData("* (SHOULD) x")]
	[InlineData("| MAY 1 | x |")]
	[InlineData("1. MUST - x")]
	[InlineData("# *MUST* - PASS when a reference is struck through on its page")]
	public async Task FormsAccepted(string criterion)
	{
		string criteria = $"h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n{criterion}\r\n";

		AssessmentReport report = await AssessAsync(Summary + criteria);

		ContractItem(report).RawScore.Should().Be(100, ContractItem(report).Finding);
	}

	[Fact]
	public async Task InlineRfc2119Criteria_Ms27557Form_Pass()
	{
		const string criteria = "h2. Acceptance Criteria\r\n# SECTIONPAGES in a header or footer MUST show the page count of its section\r\n# NUMPAGES SHOULD still show the page count of the whole document\r\n";

		ContractItem(await AssessAsync(Summary + criteria)).Finding.Should().Be("✓ Ticket Contract: Criteria classified");
	}

	[Fact]
	public async Task LabelThenUnlabelledItems_Ms27609Form_Pass()
	{
		const string criteria = "h2. Acceptance Criteria\r\n*MUST*\r\n# A value encrypted under the old key is readable after rotation\r\n# The rotation is recorded in the audit log\r\n*SHOULD*\r\n# The admin is told when the rotation finishes\r\n";

		ContractItem(await AssessAsync(Summary + criteria)).Finding.Should().Be("✓ Ticket Contract: Criteria classified");
	}

	[Fact]
	public async Task MixedMustAndMayItem_IsNotClassified()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n# the list MUST refresh and MAY animate.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: 1 criteria not classified MUST/SHOULD/MAY");

	[Fact]
	public async Task ShouldAndMayLabelsOnly_FailNoMust()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n*SHOULD*\r\n# a schedule keeps its run time.\r\n*MAY*\r\n# the list shows the offset.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: No MUST criterion");

	[Fact]
	public async Task LowercaseMay_IsNotAClassification()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n# may show the offset.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: 1 criteria not classified MUST/SHOULD/MAY");

	[Fact]
	public async Task BoldGroupLabelLine_IsNotACriterion()
	{
		const string criteria = "h2. Acceptance Criteria\r\n*MUST* (the ticket cannot pass without these)\r\n# *MUST* A value encrypted under the old key is readable\r\n# *SHOULD* Something else\r\n";

		AssessmentReport report = await AssessAsync(Summary + criteria);

		ContractItem(report).RawScore.Should().Be(100, ContractItem(report).Finding);
		ContractItem(report).Finding.Should().Be("✓ Ticket Contract: Criteria classified");
	}

	[Fact]
	public async Task NestedSubPointsAndTableRules_AreNotCriteria()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n## including daylight saving changes\r\n  - and leap days\r\n| MAY | x |\r\n|---|---|\r\n"))
			.RawScore.Should().Be(100);

	[Fact]
	public async Task EvidenceTableDoesNotTriggerDuplicationCap()
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria + FullEvidence, status: "Ready for Test");

		report.Caps.Select(cap => cap.Name).Should().NotContain("Criteria duplication");
		report.Findings.Should().NotContain(finding => finding.StartsWith("✗ Duplication", StringComparison.Ordinal));
	}


	private const string PanelledPlainEvidence = """
		h2. Verification Evidence
		{panel:title=Verification Evidence|titleBGColor=#E3FCEF|bgColor=#F3FFF9}
		*Automated coverage of the MUST criteria*
		|| Criterion || Test || Location ||
		| MUST 1 (accumulated per tenant per month) | MerlinAllowanceStoreTests.TheMonthsFigure_IsReadable | EfCore.IntegrationTest/Merlin/MerlinAllowanceStoreTests.cs |

		*Reachability - how to make the covered code actually run*
		* Refusal: the chat as a user of a tenant on the Free plan.

		*Not covered by automated tests, and why*
		* The Admin markup has no component test.

		*Suggested QA route:* C (token-billing boundary, never Route A).
		{panel}
		""";

	private const string EvidenceWithEvidenceColumn = """
		h2. Verification Evidence

		*MUST criteria*
		|| Criterion || Evidence || Where ||
		| MUST 1 | Every image ends with a non-root user | root Dockerfile |

		*Reachability:* every pod on the next build.

		*Not covered by automated tests, and why:* container start-up has no harness here.

		*Suggested QA route:* D (infrastructure).
		""";

	private const string EvidenceWithBulletsUnderBoldHeading = """
		h2. Verification Evidence

		*Coverage*
		|| Criterion || Test || Location ||
		| MUST 1 | ParserTests.ReadsTheSection | Parser/ParserTests.cs |

		*MUST criteria*
		* MUST 1 is covered by the parser tests.

		*Reachability:* the import command.

		*Not covered by automated tests, and why:* nothing is uncovered.

		*Suggested QA route:* A
		""";

	[Fact]
	public async Task PlainEvidenceHeading_InsideAPanel_Passes()
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria + PanelledPlainEvidence, status: "Ready for Test");

		ContractItem(report).Finding.Should().Be("✓ Ticket Contract: Criteria classified; evidence present");
		report.Caps.Select(cap => cap.Name).Should().NotContain("Criteria duplication");
	}

	[Theory]
	[InlineData("h2. Verification Evidence")]
	[InlineData("h2. Verification Evidence (Retrospective)")]
	public async Task EvidenceTableWithEvidenceColumn_IsNotRestatedCriteria(string heading)
	{
		string evidence = EvidenceWithEvidenceColumn.Replace("h2. Verification Evidence", heading, StringComparison.Ordinal);

		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria + evidence, status: "Ready for Test");

		ContractItem(report).RawScore.Should().Be(100, ContractItem(report).Finding);
		report.Caps.Select(cap => cap.Name).Should().NotContain("Criteria duplication");
	}

	[Fact]
	public async Task BoldSubHeadingInsideEvidence_IsNotACriteriaHeading()
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria + EvidenceWithBulletsUnderBoldHeading, status: "Ready for Test");

		report.Caps.Select(cap => cap.Name).Should().NotContain("Criteria duplication");
	}

	[Fact]
	public async Task CriteriaStatedAgainOutsideEvidence_StillTriggersDuplicationCap()
	{
		const string again = "h2. Definition of Done\r\n# MUST: a schedule keeps its local run time.\r\n";

		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria + again);

		report.Caps.Select(cap => cap.Name).Should().Contain("Criteria duplication");
	}

	[Theory]
	[InlineData("# -MUST: export the report as PDF- (moved to MS-27650 by Jane Smith)")]
	[InlineData("# -Export the report as PDF- moved to MS-27650")]
	[InlineData("# -MUST: export the report as PDF-")]
	[InlineData("# -MUST: export the report as PDF- _Moved to MS-27650 by Jane Smith, 2026-10-19: waits on Legal._")]
	[InlineData("* -SHOULD: export the report as PDF- (moved to MS-27650)")]
	public async Task StruckThroughCriterion_IsNotACriterionOfThisTicket(string struck)
	{
		string criteria = $"h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n{struck}\r\n# SHOULD: the list shows the offset.\r\n";

		AssessmentReport report = await AssessAsync(Summary + criteria);

		ContractItem(report).RawScore.Should().Be(100, ContractItem(report).Finding);
	}

	[Fact]
	public async Task StruckThroughMust_DoesNotCountAsTheOnlyMust()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# -MUST: export the report as PDF- (moved to MS-27650)\r\n# SHOULD: the list shows the offset.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: No MUST criterion");

	[Fact]
	public async Task StruckThroughOnly_LeavesNoCriteria()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# -MUST: export the report as PDF- (moved to MS-27650)\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: Acceptance criteria section has no criteria");

	[Theory]
	[InlineData("# MUST: export the report as -PDF- only")]
	[InlineData("# -export- the report as PDF")]
	[InlineData("# -MUST: export- the report as PDF, and keep it -ready-")]
	[InlineData("# -the old wording- replaced by a live statement of the rule")]
	public async Task PartialStrike_IsStillACriterion_AndStaysUnclassifiedWhenNoKeyword(string item)
	{
		string criteria = $"h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n{item}\r\n";

		AssessmentReport report = await AssessAsync(Summary + criteria);

		bool hasKeyword = item.Contains("MUST", StringComparison.Ordinal);
		ContractItem(report).RawScore.Should().Be(hasKeyword ? 100 : 0, ContractItem(report).Finding);
	}
	[Fact]
	public async Task Epic_NotApplicable()
	{
		AssessmentReport report = await AssessAsync(Summary + "h2. Notes\r\nNo criteria here.", issueType: "Epic");

		report.Categories.SelectMany(category => category.Assessments)
			.Where(item => item.Name == "TicketContract")
			.Should().OnlyContain(item => item.Outcome == AssessmentOutcome.NotApplicable);
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Fact]
	public async Task Bug_RequiresCriteriaHeading()
	{
		const string bug = "h2. Steps to Reproduce\r\n# Open a schedule.\r\n# Change the tenant timezone.\r\n\r\nh2. Current Behaviour\r\nThe run time moves.\r\n\r\nh2. Expected Behaviour\r\nThe run time stays.\r\n";

		AssessmentReport report = await AssessAsync(bug, issueType: "Bug");

		ContractItem(report).Finding.Should().Be("✗ Ticket Contract: No acceptance criteria section");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Fact]
	public async Task OpsProject_NotApplicable()
	{
		NormalizedIssue ops = Issue(Summary, "Improvement", "In Progress", null) with { Key = "OPS-1", Project = "OPS" };

		AssessmentReport report = await ReportAsync(ops);

		report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == "TicketContract").Outcome.Should().Be(AssessmentOutcome.NotApplicable);
		report.Findings.Should().NotContain(finding => finding.Contains("Ticket Contract", StringComparison.Ordinal));
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Fact]
	public async Task Undelivered_NotApplicable()
	{
		AssessmentReport report = await AssessAsync(Summary, resolution: "Won't Do");

		report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == "TicketContract").Outcome.Should().Be(AssessmentOutcome.NotApplicable);
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	private static AssessmentReportItem ContractItem(AssessmentReport report)
		=> report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == "TicketContract" && item.Outcome != AssessmentOutcome.NotApplicable);

	private static NormalizedIssue Issue(string description, string issueType, string status, string? resolution)
		=> new()
		{
			Key = "MS-1",
			Project = "MS",
			Title = "Keep schedule run times across timezone changes",
			Description = description,
			IssueType = issueType,
			Status = status,
			Platform = "Jira",
			ReleaseNotes = "Reports now keep their scheduled run time after the tenant timezone changes",
			Resolution = resolution,
			Labels = [],
		};

	private static async Task<AssessmentReport> ReportAsync(NormalizedIssue issue)
	{
		using JsonDocument document = issue.ToJsonDocument();
		return AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));
	}

	private static Task<AssessmentReport> AssessAsync(string description, string issueType = "Improvement", string status = "In Progress", string? resolution = null)
		=> ReportAsync(Issue(description, issueType, status, resolution));
}
