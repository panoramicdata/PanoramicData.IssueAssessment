using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="TitleQualityCategory"/>.
/// </summary>
public class TitleQualityTests : TestBase
{
	[Fact]
	public void Create_Always_ReturnsSeriesCategoryWithPresenceAndLengthAssessments()
	{
		AssessmentCategory category = TitleQualityCategory.Create();

		category.Name.Should().Be("Title Quality");
		category.Description.Should().NotBeNullOrWhiteSpace();
		category.AssessmentExecutionStrategy.Should().Be(AssessmentExecutionStrategy.Series);
		category.Assessments.Select(assessment => assessment.Name).Should().Equal("Title Presence", "Title Length");
		category.Assessments.Should().OnlyContain(assessment => assessment.Weight == 1d && !string.IsNullOrWhiteSpace(assessment.Description));

		PresenceAssessment presence = category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Subject;
		presence.JsonPath.Should().Be("$.title");
		presence.PresentScore.Should().Be(1d);
		presence.AbsentScore.Should().Be(0d);

		StringLengthAssessment length = category.Assessments[1].Definition.Should().BeOfType<StringLengthAssessment>().Subject;
		length.JsonPath.Should().Be("$.title");
		length.MinimumLength.Should().Be(5);
		length.MaximumLength.Should().Be(150);
		length.SuccessScore.Should().Be(1d);
		length.FailureScore.Should().Be(0d);
	}

	[Fact]
	public void Create_CalledTwice_ReturnsEqualButIndependentCategories()
	{
		AssessmentCategory first = TitleQualityCategory.Create();
		AssessmentCategory second = TitleQualityCategory.Create();

		second.Should().NotBeSameAs(first);
		second.Assessments.Select(assessment => assessment.Definition).Should().Equal(first.Assessments.Select(assessment => assessment.Definition));
	}

	[Fact]
	public async Task Assess_GoodTicket_ScoresOneWithExplanations()
	{
		using JsonDocument issue = SampleIssuesFixture.GoodNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
		result.Reason.Should().Be("Executed 2 assessment(s) in category 'Title Quality'.");
		result.Assessments[0].ExplanationText.Should().Be("Value is present for '$.title'.");
		result.Assessments[1].ExplanationText.Should().Be(
			$"String length {SampleIssuesFixture.GoodTitle.Length} at '$.title' is within range 5-150.");
	}

	[Fact]
	public async Task Assess_MediocreTicketWithThreeLetterTitle_ScoresHalf()
	{
		using JsonDocument issue = SampleIssuesFixture.MediocreNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(0.5d);
		result.Assessments[1].ExplanationText.Should().Be("String length 3 at '$.title' is outside range 5-150.");
	}

	[Fact]
	public async Task Assess_PoorTicketWithBlankTitle_ScoresZero()
	{
		using JsonDocument issue = SampleIssuesFixture.PoorNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(0d);
		result.Assessments[0].ExplanationText.Should().Be("Value is missing for '$.title'.");
	}

	[Fact]
	public async Task Assess_MissingTitle_ScoresZeroAndRecordsLengthFailure()
	{
		using JsonDocument issue = SampleIssuesFixture.Empty();
		IssueAssessor assessor = new(OptionsBuilderFixture.Options(TitleQualityCategory.Create()));

		IAssessmentResult result = await assessor.AssessAsync(issue, CancellationToken);

		result.Categories[0].Score.NormalizedScore.Should().Be(0d);
		result.Failures.Should().ContainSingle()
			.Which.Should().Match<AssessmentFailure>(failure =>
				failure.CategoryName == "Title Quality"
				&& failure.AssessmentName == "Title Length"
				&& failure.Reason == AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData(4, 0.5d)]
	[InlineData(5, 1d)]
	[InlineData(150, 1d)]
	[InlineData(151, 0.5d)]
	public async Task Assess_TitleLengthAtBoundary_ScoresAsExpected(int length, double expected)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("title", new string('a', length)));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(expected);
	}

	[Fact]
	public async Task Assess_RawGitHubIssue_ScoresOneBecauseGitHubUsesTitle()
	{
		using JsonDocument issue = SampleIssuesFixture.RawGitHub();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task Assess_RawJiraAndServiceNowIssues_ScoreZeroWithoutNormalization()
	{
		using JsonDocument jira = SampleIssuesFixture.RawJira();
		using JsonDocument serviceNow = SampleIssuesFixture.RawServiceNow();

		CategoryResult jiraResult = await AssessCategoryAsync(jira);
		CategoryResult serviceNowResult = await AssessCategoryAsync(serviceNow);

		jiraResult.Score.NormalizedScore.Should().Be(0d, "Jira keeps the title in fields.summary");
		serviceNowResult.Score.NormalizedScore.Should().Be(0d, "ServiceNow keeps the title in short_description");
	}

	private static async Task<CategoryResult> AssessCategoryAsync(JsonDocument issue)
	{
		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(TitleQualityCategory.Create(), issue, CancellationToken);
		return result.Categories.Should().ContainSingle().Subject;
	}
}
