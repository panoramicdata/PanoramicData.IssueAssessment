using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// The MS-only ReleaseNotes assessment: customer-facing release notes are mandatory from Ready for Test, so a failing tier caps the score from then on.
/// </summary>
public class ReleaseNotesAssessmentTests : TestBase
{
	private const string CapName = "Release notes";
	private const string GoodNotes = "Reports now keep their scheduled run time after the tenant timezone changes";
	private static readonly IssueAssessor Assessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	private const string Description = "h2. Summary\r\nRun times drift when the tenant timezone changes.\r\n\r\nh2. Acceptance Criteria\r\n# MUST: a schedule keeps its local run time after a timezone change.\r\n# SHOULD: the schedule list shows the new offset.\r\n\r\nh2. Business Case\r\nCustomers rely on reports arriving at the agreed time, which has a direct business value.";

	[Fact]
	public async Task Empty_FailsAndCapsAt79()
	{
		AssessmentReport report = await AssessAsync(notes: null);

		NotesItem(report).RawScore.Should().Be(0);
		NotesItem(report).Finding.Should().Be("✗ Release Notes: Empty");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
		report.NormalizedScore.Should().BeLessThanOrEqualTo(0.79);
	}

	[Theory]
	[InlineData("TBD")]
	[InlineData("None")]
	[InlineData("See the ticket")]
	[InlineData("---")]
	public async Task Placeholder_Fails(string notes)
		=> NotesItem(await AssessAsync(notes)).Finding.Should().Be("✗ Release Notes: Placeholder");

	[Fact]
	public async Task Placeholder_TBD_Fails()
	{
		AssessmentReport report = await AssessAsync("TBD");

		NotesItem(report).Finding.Should().Be("✗ Release Notes: Placeholder");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Fact]
	public async Task SevenWords_Fails()
		=> NotesItem(await AssessAsync("Schedules now keep their run time changes")).Finding.Should().Be("✗ Release Notes: Under 8 words");

	[Fact]
	public async Task SameAsSummaryIgnoringPrefix_Fails()
		=> NotesItem(await AssessAsync("Add X to Y report for every tenant", title: "ReportMagic: Add X to Y report for every tenant"))
			.Finding.Should().Be("✗ Release Notes: Same as the summary");

	[Theory]
	[InlineData("Fixes the issue raised in MS-12345 for all users today")]
	[InlineData("Updated ReportJob.cs so that every scheduled report runs on time")]
	[InlineData("Changed the file under C:\\inetpub\\reports so that every report loads")]
	[InlineData("Updated the files in the repo/src/Reports folder so reports load faster")]
	public async Task TicketKey_Fails(string notes)
		=> NotesItem(await AssessAsync(notes)).Finding.Should().StartWith("✗ Release Notes: Not customer-facing");

	[Theory]
	[InlineData("Schedules now read and write UTF-8 files correctly for every tenant")]
	[InlineData("Passwords are now hashed with SHA-256 before they are stored for tenants")]
	[InlineData("Reports now meet the ISO-27001 audit trail wording for every customer")]
	[InlineData("The library update addresses CVE-2026-12345 for every hosted customer")]
	public async Task StandardsIdentifiers_Pass(string notes)
		=> NotesItem(await AssessAsync(notes)).RawScore.Should().Be(100);

	[Fact]
	public async Task FilePath_Fails()
		=> NotesItem(await AssessAsync("The settings now load from appsettings.json when the service starts up")).Finding.Should().StartWith("✗ Release Notes: Not customer-facing");

	[Fact]
	public async Task ServiceIdentifier_Fails()
		=> NotesItem(await AssessAsync("ReportJobService now retries failed jobs three times before giving up")).Finding.Should().StartWith("✗ Release Notes: Not customer-facing");

	[Fact]
	public async Task ProductNames_Pass()
	{
		AssessmentReport report = await AssessAsync("ReportMagic and ConnectMagic now show the DataMagic sync time on every dashboard page");

		NotesItem(report).RawScore.Should().Be(100);
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Fact]
	public async Task SentenceInitialCapital_Passes()
		=> NotesItem(await AssessAsync("Schedules now keep their local run time after the timezone changes")).RawScore.Should().Be(100);

	[Fact]
	public async Task EmDash_Fails()
		=> NotesItem(await AssessAsync("Schedules now keep their run time \u2014 even when the timezone changes")).Finding.Should().Be("✗ Release Notes: Contains an em dash");

	[Fact]
	public async Task UsSpelling_Fails()
		=> NotesItem(await AssessAsync("Schedules now show the correct behavior when the timezone changes for tenants")).Finding.Should().Be("✗ Release Notes: US spelling");

	[Theory]
	[InlineData("Fixed bug.")]
	[InlineData("Various fixes")]
	public async Task GenericFixedBug_Fails(string notes)
	{
		AssessmentReport report = await AssessAsync(notes);

		NotesItem(report).Finding.Should().Be("✗ Release Notes: Generic");
	}

	public static TheoryData<string, string?, string, string> FailingTiers => new()
	{
		{ "Improvement", null, "✗ Release Notes: Empty", EmptyAdvice },
		{ "Epic", null, "✗ Release Notes: Empty", EmptyAdvice },
		{ "Improvement", "TBD", "✗ Release Notes: Placeholder", PlaceholderAdvice },
		{ "Epic", "TBD", "✗ Release Notes: Placeholder", PlaceholderAdvice },
		{ "Improvement", "Various fixes", "✗ Release Notes: Generic", GenericAdvice },
		{ "Epic", "Various fixes", "✗ Release Notes: Generic", GenericAdvice },
		{ "Improvement", "Schedules now keep their run time changes", "✗ Release Notes: Under 8 words", ShortAdvice },
		{ "Epic", "Schedules now keep their run time changes", "✗ Release Notes: Under 8 words", ShortAdvice },
		{ "Improvement", "Schedules: Keep schedule run times across timezone changes", "✗ Release Notes: Same as the summary", SameAsSummaryAdvice },
		{ "Epic", "Schedules: Keep schedule run times across timezone changes", "✗ Release Notes: Same as the summary", SameAsSummaryAdvice },
		{ "Improvement", "Fixes the issue raised in MS-12345 for all users today", "✗ Release Notes: Not customer-facing (ticket key, file path or code identifier)", NotCustomerFacingAdvice },
		{ "Epic", "Fixes the issue raised in MS-12345 for all users today", "✗ Release Notes: Not customer-facing (ticket key, file path or code identifier)", NotCustomerFacingAdvice },
		{ "Improvement", $"Schedules now keep their run time {EmDash} even when the timezone changes", "✗ Release Notes: Contains an em dash", EmDashAdvice },
		{ "Epic", $"Schedules now keep their run time {EmDash} even when the timezone changes", "✗ Release Notes: Contains an em dash", EmDashAdvice },
		{ "Improvement", "Schedules now show the correct behavior when the timezone changes for tenants", "✗ Release Notes: US spelling", UsSpellingAdvice },
		{ "Epic", "Schedules now show the correct behavior when the timezone changes for tenants", "✗ Release Notes: US spelling", UsSpellingAdvice },
	};

	private static readonly string EmDash = ((char)0x2014).ToString();
	private const string EmptyAdvice = "Fill in the Release Notes field with one or two customer-facing sentences of at least 8 words saying what changed for the customer";
	private const string PlaceholderAdvice = "Replace the placeholder in the Release Notes field with one or two sentences saying what changed for the customer";
	private const string GenericAdvice = "Say what changed and for whom: a generic note such as 'Fixed bug' tells the customer nothing";
	private const string ShortAdvice = "Expand the Release Notes to at least 8 words, saying what changed and who it affects";
	private const string SameAsSummaryAdvice = "Describe the outcome for the customer rather than repeating the ticket summary";
	private const string NotCustomerFacingAdvice = "Remove ticket keys, file paths and code identifiers, and describe the change as the customer sees it";
	private const string EmDashAdvice = "Replace the em dash with a comma, colon or full stop";
	private const string UsSpellingAdvice = "Use British spelling, for example behaviour, colour, organisation, centre, favourite and cancelled";

	[Theory]
	[MemberData(nameof(FailingTiers))]
	public async Task FailingTier_RecommendsAFixForItsOwnRule(string issueType, string? notes, string finding, string recommendation)
	{
		AssessmentReportItem item = NotesItem(await AssessAsync(notes, issueType: issueType));

		item.Finding.Should().Be(finding);
		item.Recommendation.Should().Be(recommendation);
		item.Recommendation.Should().NotContain(EmDash);
	}

	[Fact]
	public async Task Good_Scores100_NoCap()
	{
		AssessmentReport report = await AssessAsync(GoodNotes);

		NotesItem(report).Finding.Should().Be("✓ Release Notes: Present and customer-facing");
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Fact]
	public async Task InternalLabel_StillRequiresNotes()
	{
		AssessmentReport report = await AssessAsync(notes: null, labels: ["internal"]);

		NotesItem(report).RawScore.Should().Be(0);
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Fact]
	public async Task Epic_IsAssessed()
	{
		AssessmentReport report = await AssessAsync(notes: null, issueType: "Epic");

		report.Categories.Single(category => category.Name == "Epic Quality").Assessments
			.Single(item => item.Name.EndsWith("ReleaseNotes", StringComparison.Ordinal)).RawScore.Should().Be(0);
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Fact]
	public async Task OpsProject_NotApplicable_ScoreUnchanged()
	{
		NormalizedIssue ops = Issue(notes: null) with { Key = "OPS-1", Project = "OPS", Description = Description };
		IssueAssessor previous = new(Serialization.AssessmentRubric.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Rubrics", "ticket-quality-1.1.6.json"))));

		AssessmentReport now = await ReportAsync(Assessor, ops);
		AssessmentReport before = await ReportAsync(previous, ops);

		now.Score.Should().Be(before.Score);
		now.Findings.Should().NotContain(finding => finding.Contains("Release Notes", StringComparison.Ordinal));
		now.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
		now.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == "ReleaseNotes").Outcome.Should().Be(AssessmentOutcome.NotApplicable);
	}

	[Theory]
	[InlineData("Won't Do")]
	[InlineData("Duplicate")]
	public async Task ResolutionWontDo_NotApplicable_NoCap(string resolution)
	{
		AssessmentReport report = await AssessAsync(notes: null, resolution: resolution);

		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
		report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == "ReleaseNotes").Outcome.Should().Be(AssessmentOutcome.NotApplicable);
	}

	[Theory]
	[InlineData("Fixed")]
	[InlineData("Done")]
	public async Task ResolutionFixed_IsAssessed(string resolution)
	{
		AssessmentReport report = await AssessAsync(notes: null, resolution: resolution);

		NotesItem(report).RawScore.Should().Be(0);
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	[Theory]
	[InlineData("Improvement", "Ready for Progress")]
	[InlineData("Improvement", "In Progress")]
	[InlineData("Epic", "In Progress")]
	public async Task EmptyBeforeReadyForTest_NotRequiredYet_NoCap(string issueType, string status)
	{
		AssessmentReport report = await AssessAsync(notes: null, issueType: issueType, status: status);

		NotesItem(report).RawScore.Should().Be(100);
		NotesItem(report).Finding.Should().Be($"○ Release Notes: Not required yet (status: {status})");
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Fact]
	public async Task FlawedBeforeReadyForTest_StillReported_NoCap()
	{
		AssessmentReport report = await AssessAsync("TBD", status: "Ready for Progress");

		NotesItem(report).Finding.Should().Be("✗ Release Notes: Placeholder");
		report.Caps.Select(cap => cap.Name).Should().NotContain(CapName);
	}

	[Theory]
	[InlineData("In Test")]
	[InlineData("Closed")]
	public async Task EmptyFromReadyForTestOn_Caps(string status)
	{
		AssessmentReport report = await AssessAsync(notes: null, status: status);

		NotesItem(report).Finding.Should().Be("✗ Release Notes: Empty");
		report.Caps.Select(cap => cap.Name).Should().Contain(CapName);
	}

	private static AssessmentReportItem NotesItem(AssessmentReport report)
		=> report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name.EndsWith("ReleaseNotes", StringComparison.Ordinal) && item.Outcome != AssessmentOutcome.NotApplicable);

	private static NormalizedIssue Issue(string? notes, string title = "Keep schedule run times across timezone changes", string issueType = "Improvement", string? resolution = null, ImmutableList<string>? labels = null, string status = "Ready for Test")
		=> new()
		{
			Key = "MS-1",
			Project = "MS",
			Title = title,
			Description = Description,
			IssueType = issueType,
			Status = status,
			Platform = "Jira",
			ReleaseNotes = notes,
			Resolution = resolution,
			Labels = labels ?? [],
		};

	private static async Task<AssessmentReport> ReportAsync(IssueAssessor assessor, NormalizedIssue issue)
	{
		using JsonDocument document = issue.ToJsonDocument();
		return AssessmentReport.From(await assessor.AssessAsync(document, CancellationToken));
	}

	private static Task<AssessmentReport> AssessAsync(string? notes, string title = "Keep schedule run times across timezone changes", string issueType = "Improvement", string? resolution = null, ImmutableList<string>? labels = null, string status = "Ready for Test")
		=> ReportAsync(Assessor, Issue(notes, title, issueType, resolution, labels, status));
}
