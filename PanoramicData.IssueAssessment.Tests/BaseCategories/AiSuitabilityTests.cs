using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="AiSuitabilityCategory"/>.
/// </summary>
public class AiSuitabilityTests : TestBase
{
	[Fact]
	public void Create_Always_ChecksTitleAndDescriptionDetail()
	{
		AssessmentCategory category = AiSuitabilityCategory.Create();

		category.Name.Should().Be("AI Suitability");
		category.Assessments.Select(assessment => assessment.Name).Should().Equal("AI Title Presence", "AI Description Word Count");
		category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Which.JsonPath.Should().Be("$.title");
		WordCountAssessment words = category.Assessments[1].Definition.Should().BeOfType<WordCountAssessment>().Subject;
		words.JsonPath.Should().Be("$.description");
		words.MinimumWordCount.Should().Be(15);
		words.MaximumWordCount.Should().Be(1_500);
	}

	[Theory]
	[InlineData(nameof(SampleIssuesFixture.GoodNormalized), 1d)]
	[InlineData(nameof(SampleIssuesFixture.MediocreNormalized), 0.5d)]
	[InlineData(nameof(SampleIssuesFixture.PoorNormalized), 0d)]
	public async Task Assess_SampleTickets_ScoreByQuality(string sample, double expected)
	{
		using JsonDocument issue = BaseCategoryTestHelpers.Sample(sample);

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(AiSuitabilityCategory.Create(), issue, CancellationToken);

		result.Categories.Should().ContainSingle().Which.Score.NormalizedScore.Should().Be(expected);
	}
}
