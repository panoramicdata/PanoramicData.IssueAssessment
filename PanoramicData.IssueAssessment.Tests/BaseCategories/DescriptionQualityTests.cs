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

		category.Name.Should().Be("Description Quality");
		category.Description.Should().NotBeNullOrWhiteSpace();
		category.AssessmentExecutionStrategy.Should().Be(AssessmentExecutionStrategy.Series);
		category.Assessments.Select(assessment => assessment.Name)
			.Should().Equal("Description Presence", "Description Length", "Description Word Count");
		category.Assessments.Should().OnlyContain(assessment => assessment.Weight == 1d && !string.IsNullOrWhiteSpace(assessment.Description));

		PresenceAssessment presence = category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Subject;
		presence.JsonPath.Should().Be("$.description");
		presence.PresentScore.Should().Be(1d);
		presence.AbsentScore.Should().Be(0d);

		StringLengthAssessment length = category.Assessments[1].Definition.Should().BeOfType<StringLengthAssessment>().Subject;
		length.JsonPath.Should().Be("$.description");
		length.MinimumLength.Should().Be(20);
		length.MaximumLength.Should().Be(10_000);
		length.SuccessScore.Should().Be(1d);
		length.FailureScore.Should().Be(0d);

		WordCountAssessment words = category.Assessments[2].Definition.Should().BeOfType<WordCountAssessment>().Subject;
		words.JsonPath.Should().Be("$.description");
		words.MinimumWordCount.Should().Be(8);
		words.MaximumWordCount.Should().Be(1_500);
		words.SuccessScore.Should().Be(1d);
		words.FailureScore.Should().Be(0d);
	}

	[Fact]
	public async Task Assess_GoodTicket_ScoresOne()
	{
		using JsonDocument issue = SampleIssuesFixture.GoodNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
		result.Reason.Should().Be("Executed 3 assessment(s) in category 'Description Quality'.");
		result.Assessments[2].ExplanationText.Should().StartWith("Word count ").And.EndWith("at '$.description' is within range 8-1500.");
	}

	[Fact]
	public async Task Assess_MediocreTicketWithSevenWordDescription_ScoresTwoThirds()
	{
		using JsonDocument issue = SampleIssuesFixture.MediocreNormalized();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().BeApproximately(2d / 3d, 1e-9);
		result.Assessments[1].Score.NormalizedScore.Should().Be(1d);
		result.Assessments[2].ExplanationText.Should().Be("Word count 7 at '$.description' is outside range 8-1500.");
	}

	[Fact]
	public async Task Assess_PoorTicketWithEmptyDescription_ScoresZeroWithoutFailures()
	{
		using JsonDocument issue = SampleIssuesFixture.PoorNormalized();
		IssueAssessor assessor = new(OptionsBuilderFixture.Options(DescriptionQualityCategory.Create()));

		IAssessmentResult result = await assessor.AssessAsync(issue, CancellationToken);

		result.Categories[0].Score.NormalizedScore.Should().Be(0d);
		result.Categories[0].Assessments[0].ExplanationText.Should().Be("Value is missing for '$.description'.");
		result.Categories[0].Assessments[1].ExplanationText.Should().Be("String length 0 at '$.description' is outside range 20-10000.");
		result.Failures.Should().BeEmpty("an empty string is present, just too short");
	}

	[Fact]
	public async Task Assess_MissingDescription_ScoresZeroAndRecordsTwoFailures()
	{
		using JsonDocument issue = SampleIssuesFixture.Empty();
		IssueAssessor assessor = new(OptionsBuilderFixture.Options(DescriptionQualityCategory.Create()));

		IAssessmentResult result = await assessor.AssessAsync(issue, CancellationToken);

		result.Categories[0].Score.NormalizedScore.Should().Be(0d);
		result.Failures.Select(failure => failure.AssessmentName).Should().Equal("Description Length", "Description Word Count");
		result.Failures.Should().OnlyContain(failure => failure.Reason == AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task Assess_VeryLongDescriptionWithPastedLogs_ScoresOneThird()
	{
		string description = string.Concat(Enumerable.Repeat("ERROR timeout ", 1_000));
		using JsonDocument issue = SampleIssuesFixture.With(("description", description));

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().BeApproximately(1d / 3d, 1e-9);
	}

	[Fact]
	public async Task Assess_RawServiceNowIncident_ScoresOneBecauseServiceNowUsesDescription()
	{
		using JsonDocument issue = SampleIssuesFixture.RawServiceNow();

		CategoryResult result = await AssessCategoryAsync(issue);

		result.Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task Assess_RawJiraAndGitHubIssues_ScoreZeroWithoutNormalization()
	{
		using JsonDocument jira = SampleIssuesFixture.RawJira();
		using JsonDocument gitHub = SampleIssuesFixture.RawGitHub();

		CategoryResult jiraResult = await AssessCategoryAsync(jira);
		CategoryResult gitHubResult = await AssessCategoryAsync(gitHub);

		jiraResult.Score.NormalizedScore.Should().Be(0d, "Jira keeps the description in fields.description");
		gitHubResult.Score.NormalizedScore.Should().Be(0d, "GitHub keeps the description in body");
	}

	private static async Task<CategoryResult> AssessCategoryAsync(JsonDocument issue)
	{
		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(DescriptionQualityCategory.Create(), issue, CancellationToken);
		return result.Categories.Should().ContainSingle().Subject;
	}
}
