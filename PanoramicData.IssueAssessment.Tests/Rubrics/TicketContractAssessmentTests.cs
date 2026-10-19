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

	[Fact]
	public async Task ReadyForTest_NoEvidence_Fails()
	{
		AssessmentReport report = await AssessAsync(Summary + ClassifiedCriteria, status: "Ready for Test");

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
	public async Task FormsAccepted(string criterion)
	{
		string criteria = $"h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n{criterion}\r\n";

		AssessmentReport report = await AssessAsync(Summary + criteria);

		ContractItem(report).RawScore.Should().Be(100, ContractItem(report).Finding);
	}

	[Fact]
	public async Task LowercaseMay_IsNotAClassification()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n# may show the offset.\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: 1 criteria not classified MUST/SHOULD/MAY");

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
