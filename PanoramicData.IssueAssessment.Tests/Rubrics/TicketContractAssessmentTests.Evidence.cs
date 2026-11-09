using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

public partial class TicketContractAssessmentTests
{
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

	[Fact]
	public async Task LeadingMinusNumericItem_IsStillACriterion()
		=> ContractItem(await AssessAsync(Summary + "h2. Acceptance Criteria\r\n# MUST: a schedule keeps its run time.\r\n# -1 offset must be rejected\r\n"))
			.Finding.Should().Be("✗ Ticket Contract: 1 criteria not classified MUST/SHOULD/MAY");

	[Theory]
	[InlineData("# MUST: show -5 when negative")]
	[InlineData("# -1 offset MUST be rejected - (see MS-123)")]
	public async Task LeadingMinusClassifiedItem_IsStillACriterion(string item)
		=> ContractItem(await AssessAsync(Summary + $"h2. Acceptance Criteria\r\n# SHOULD: the list shows the offset.\r\n{item}\r\n"))
			.RawScore.Should().Be(100);

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
}
