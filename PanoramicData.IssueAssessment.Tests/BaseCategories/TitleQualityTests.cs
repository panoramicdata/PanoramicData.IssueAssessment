using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="TitleQualityCategory"/>.
/// </summary>
public class TitleQualityTests : TestBase
{
	[Fact]
	public void Create_Always_ReturnsSeriesCategoryWithPresenceLengthAndWordCountAssessments()
	{
		AssessmentCategory category = TitleQualityCategory.Create();

		category.ShouldBeSeriesCategory("Title Quality", "Title Presence", "Title Length", "Title Word Count");
		category.Assessments[0].ShouldBePresence("$.title");
		category.Assessments[1].ShouldBeStringLength("$.title", 5, 150);
		category.Assessments[2].ShouldBeWordCount("$.title", 3, 30);
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
		result.Reason.Should().Be("Executed 3 assessment(s) in category 'Title Quality'.");
		result.Assessments[0].ExplanationText.Should().Be("Value is present for '$.title'.");
		result.Assessments[1].ExplanationText.Should().Be(
			$"String length {SampleIssuesFixture.GoodTitle.Length} at '$.title' is within range 5-150.");
	}

	[Fact]
	public async Task Assess_MediocreTicketWithOneWordThreeLetterTitle_ScoresOneThird()
	{
		using JsonDocument issue = SampleIssuesFixture.MediocreNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().BeApproximately(1d / 3d, 1e-12);
		result.Assessments[1].ExplanationText.Should().Be("String length 3 at '$.title' is outside range 5-150.");
		result.Assessments[2].ExplanationText.Should().Be("Word count 1 at '$.title' is outside range 3-30.");
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
		result.Failures.Select(failure => failure.AssessmentName).Should().Equal("Title Length", "Title Word Count");
		result.Failures.Should().OnlyContain(failure =>
			failure.CategoryName == "Title Quality" && failure.Reason == AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("ab c", false)]
	[InlineData("ab cd", true)]
	public async Task Assess_TitleLengthAtLowerBoundary_ScoresLengthAsExpected(string title, bool lengthPasses)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("title", title));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Assessments[1].Score.NormalizedScore.Should().Be(lengthPasses ? 1d : 0d);
	}

	[Theory]
	[InlineData(150, 1d)]
	[InlineData(151, 0d)]
	public async Task Assess_TitleLengthAtUpperBoundary_ScoresLengthAsExpected(int length, double expected)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("title", TitleOfLength(length)));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Assessments[1].Score.NormalizedScore.Should().Be(expected);
		result.Assessments[2].Score.NormalizedScore.Should().Be(1d);
	}

	[Theory]
	[InlineData("Problems", 0d)]
	[InlineData("Doesn't work", 0d)]
	[InlineData("Export drops rows", 1d)]
	public async Task Assess_TitleWordCount_PenalisesPlaceholderTitles(string title, double expectedWordScore)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("title", title));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Assessments[2].Score.NormalizedScore.Should().Be(expectedWordScore);
	}

	[Theory]
	[InlineData(30, 1d)]
	[InlineData(31, 0d)]
	public async Task Assess_TitleWordCountAtUpperBoundary_ScoresAsExpected(int words, double expected)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("title", string.Join(' ', Enumerable.Repeat("a", words))));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Assessments[2].Score.NormalizedScore.Should().Be(expected);
	}

	/// <summary>
	/// A title of exactly <paramref name="length"/> characters made of long words, so it always passes the
	/// word-count check.
	/// </summary>
	private static string TitleOfLength(int length)
		=> string.Concat(Enumerable.Repeat("component ", (length / 10) + 1))[..length].TrimEnd().PadRight(length, 'x');

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

	private static Task<CategoryResult> AssessCategoryAsync(JsonDocument issue)
		=> BaseCategoryTestHelpers.AssessCategoryAsync(TitleQualityCategory.Create(), issue);
}
