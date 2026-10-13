using System.Globalization;
using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="DueDateTimelineCategory"/>.
/// </summary>
public class DueDateTimelineTests : TestBase
{
	[Fact]
	public void Create_Always_ReturnsSeriesCategoryWithPresenceAndWindowAssessments()
	{
		DateTime before = DateTime.UtcNow.Date;
		AssessmentCategory category = DueDateTimelineCategory.Create();
		DateTime after = DateTime.UtcNow.Date;

		category.Name.Should().Be("Due Date Timeline");
		category.Description.Should().NotBeNullOrWhiteSpace();
		category.AssessmentExecutionStrategy.Should().Be(AssessmentExecutionStrategy.Series);
		category.Assessments.Select(assessment => assessment.Name).Should().Equal("Due Date Presence", "Due Date Window");
		category.Assessments.Should().OnlyContain(assessment => assessment.Weight == 1d);

		PresenceAssessment presence = category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Subject;
		presence.JsonPath.Should().Be("$.dueDate");
		presence.PresentScore.Should().Be(1d);
		presence.AbsentScore.Should().Be(0d);

		DateRangeAssessment window = category.Assessments[1].Definition.Should().BeOfType<DateRangeAssessment>().Subject;
		window.JsonPath.Should().Be("$.dueDate");
		window.DateFormat.Should().BeEmpty("real due dates come in many ISO 8601 variants, so parsing is flexible");
		window.MinDate.Should().BeOneOf(before.AddDays(-1), after.AddDays(-1));
		window.MaxDate.Should().BeOneOf(before.AddYears(2), after.AddYears(2));
		window.SuccessScore.Should().Be(1d);
		window.FailureScore.Should().Be(0d);
	}

	[Fact]
	public async Task Assess_GoodTicket_ScoresOne()
	{
		using JsonDocument issue = SampleIssuesFixture.GoodNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
		result.Assessments[0].ExplanationText.Should().Be("Value is present for '$.dueDate'.");
	}

	[Theory]
	[InlineData("yyyy-MM-dd")]
	[InlineData("yyyy-MM-dd'T'HH:mm:ss'Z'")]
	[InlineData("yyyy-MM-dd'T'HH:mm:ss.fffK")]
	[InlineData("yyyy-MM-dd HH:mm:ss")]
	public async Task Assess_DueDateInsideWindowInCommonFormat_ScoresOne(string format)
	{
		string dueDate = SampleIssuesFixture.UtcToday.AddDays(20).AddHours(8).ToString(format, CultureInfo.InvariantCulture);
		using JsonDocument issue = SampleIssuesFixture.With(("dueDate", dueDate));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Assessments[1].Score.NormalizedScore.Should().Be(1d, $"'{dueDate}' is a valid due date three weeks out");
		result.Score.NormalizedScore.Should().Be(1d);
	}

	[Theory]
	[InlineData(-30)]
	[InlineData(-2)]
	[InlineData(366 * 3)]
	public async Task Assess_DueDateOutsideWindow_ScoresHalf(int daysFromToday)
	{
		string dueDate = SampleIssuesFixture.UtcToday.AddDays(daysFromToday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		using JsonDocument issue = SampleIssuesFixture.With(("dueDate", dueDate));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Assessments[0].Score.NormalizedScore.Should().Be(1d);
		result.Assessments[1].Score.NormalizedScore.Should().Be(0d);
		result.Score.NormalizedScore.Should().Be(0.5d);
	}

	[Fact]
	public async Task Assess_DueDateYesterday_IsStillInsideWindow()
	{
		string dueDate = SampleIssuesFixture.UtcToday.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		using JsonDocument issue = SampleIssuesFixture.With(("dueDate", dueDate));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task Assess_MediocreTicketOverdueByAMonth_ScoresHalf()
	{
		using JsonDocument issue = SampleIssuesFixture.MediocreNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(0.5d);
	}

	[Fact]
	public async Task Assess_UnparseableDueDate_ScoresHalfAndExplainsParseFailure()
	{
		using JsonDocument issue = SampleIssuesFixture.With(("dueDate", "next sprint"));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(0.5d);
		result.Assessments[1].ExplanationText.Should().Contain("'next sprint'").And.Contain("could not be parsed");
	}

	[Fact]
	public async Task Assess_PoorTicketWithNullDueDate_ScoresZeroAndRecordsFailure()
	{
		using JsonDocument issue = SampleIssuesFixture.PoorNormalized();
		IssueAssessor assessor = new(OptionsBuilderFixture.Options(DueDateTimelineCategory.Create()));

		IAssessmentResult result = await assessor.AssessAsync(issue, CancellationToken);

		CategoryResult category = result.Categories.Should().ContainSingle().Subject;
		category.Score.NormalizedScore.Should().Be(0d);
		category.Assessments[0].ExplanationText.Should().Be("Value is missing for '$.dueDate'.");
		result.Failures.Should().ContainSingle()
			.Which.Should().Match<AssessmentFailure>(failure =>
				failure.AssessmentName == "Due Date Window"
				&& failure.Reason == AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task Assess_RawJiraIssue_ScoresZeroBecauseDueDateIsUnderFields()
	{
		using JsonDocument issue = SampleIssuesFixture.RawJira();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(0d);
	}

	private static async Task<CategoryResult> AssessCategoryAsync(JsonDocument issue)
	{
		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(DueDateTimelineCategory.Create(), issue, CancellationToken);
		return result.Categories.Should().ContainSingle().Subject;
	}
}
