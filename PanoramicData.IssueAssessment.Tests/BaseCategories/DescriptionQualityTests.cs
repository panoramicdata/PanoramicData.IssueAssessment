using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="DescriptionQualityCategory"/>.
/// </summary>
public class DescriptionQualityTests : TestBase
{
	[Fact]
	public void Create_Always_ReturnsSeriesCategoryWithPresenceLengthAndWordCountAssessments()
	{
		AssessmentCategory category = DescriptionQualityCategory.Create();

		category.ShouldBeSeriesCategory("Description Quality", "Description Presence", "Description Length", "Description Word Count");
		category.Assessments[0].ShouldBePresence("$.description");
		category.Assessments[1].ShouldBeStringLength("$.description", 20, 10_000);
		category.Assessments[2].ShouldBeWordCount("$.description", 8, 1_500);
	}

	[Fact]
	public async Task Assess_GoodTicket_ScoresOne()
	{
		using JsonDocument issue = SampleIssuesFixture.GoodNormalized();

		CategoryResult result = await AssessAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
		result.Reason.Should().Be("Executed 3 assessment(s) in category 'Description Quality'.");
		result.Assessments[2].ExplanationText.Should().StartWith("Word count ").And.EndWith("at '$.description' is within range 8-1500.");
	}

	[Fact]
	public async Task Assess_MediocreTicketWithSevenWordDescription_ScoresTwoThirds()
	{
		using JsonDocument issue = SampleIssuesFixture.MediocreNormalized();

		CategoryResult result = await AssessAsync(issue);

		result.Score.NormalizedScore.Should().BeApproximately(2d / 3d, 1e-9);
		result.Assessments[1].Score.NormalizedScore.Should().Be(1d);
		result.Assessments[2].ExplanationText.Should().Be("Word count 7 at '$.description' is outside range 8-1500.");
	}

	[Fact]
	public async Task Assess_PoorTicketWithEmptyDescription_ScoresZeroWithoutFailures()
	{
		using JsonDocument issue = SampleIssuesFixture.PoorNormalized();

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(DescriptionQualityCategory.Create(), issue, CancellationToken);

		result.Categories[0].Score.NormalizedScore.Should().Be(0d);
		result.Categories[0].Assessments[0].ExplanationText.Should().Be("Value is missing for '$.description'.");
		result.Categories[0].Assessments[1].ExplanationText.Should().Be("String length 0 at '$.description' is outside range 20-10000.");
		result.Failures.Should().BeEmpty("an empty string is present, just too short");
	}

	[Fact]
	public async Task Assess_MissingDescription_ScoresZeroAndRecordsTwoFailures()
	{
		using JsonDocument issue = SampleIssuesFixture.Empty();

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(DescriptionQualityCategory.Create(), issue, CancellationToken);

		result.Categories[0].Score.NormalizedScore.Should().Be(0d);
		result.Failures.Select(failure => failure.AssessmentName).Should().Equal("Description Length", "Description Word Count");
		result.Failures.Should().OnlyContain(failure => failure.Reason == AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task Assess_VeryLongDescriptionWithPastedLogs_ScoresOneThird()
	{
		using JsonDocument issue = SampleIssuesFixture.With(("description", string.Concat(Enumerable.Repeat("ERROR timeout ", 1_000))));

		CategoryResult result = await AssessAsync(issue);

		result.Score.NormalizedScore.Should().BeApproximately(1d / 3d, 1e-9);
	}

	[Theory]
	[InlineData("ServiceNow", 1d)]
	[InlineData("Jira", 0d)]
	[InlineData("GitHub", 0d)]
	public async Task Assess_RawPlatformShapes_ScoreOnlyWhereTheFieldIsCalledDescription(string platform, double expected)
	{
		using JsonDocument issue = platform switch
		{
			"ServiceNow" => SampleIssuesFixture.RawServiceNow(),
			"Jira" => SampleIssuesFixture.RawJira(),
			_ => SampleIssuesFixture.RawGitHub(),
		};

		CategoryResult result = await AssessAsync(issue);

		result.Score.NormalizedScore.Should().Be(expected, "Jira uses fields.description and GitHub uses body");
	}

	private static Task<CategoryResult> AssessAsync(JsonDocument issue)
		=> BaseCategoryTestHelpers.AssessCategoryAsync(DescriptionQualityCategory.Create(), issue);
}
