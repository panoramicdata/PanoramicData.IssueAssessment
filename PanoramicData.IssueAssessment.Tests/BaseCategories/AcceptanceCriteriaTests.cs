using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="AcceptanceCriteriaCategory"/>.
/// </summary>
public class AcceptanceCriteriaTests : TestBase
{
	[Fact]
	public void Create_Always_ChecksPresenceAndLengthOfAcceptanceCriteria()
	{
		AssessmentCategory category = AcceptanceCriteriaCategory.Create();

		category.Name.Should().Be("Acceptance Criteria");
		category.AssessmentExecutionStrategy.Should().Be(AssessmentExecutionStrategy.Series);
		category.Assessments.Select(assessment => assessment.Name).Should().Equal("Acceptance Criteria Presence", "Acceptance Criteria Length");
		category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Which.JsonPath.Should().Be("$.acceptanceCriteria");
		StringLengthAssessment length = category.Assessments[1].Definition.Should().BeOfType<StringLengthAssessment>().Subject;
		length.JsonPath.Should().Be("$.acceptanceCriteria");
		length.MinimumLength.Should().Be(50);
		length.MaximumLength.Should().Be(8_000);
	}

	[Theory]
	[InlineData(nameof(SampleIssuesFixture.GoodNormalized), 1d)]
	[InlineData(nameof(SampleIssuesFixture.MediocreNormalized), 0.5d)]
	[InlineData(nameof(SampleIssuesFixture.PoorNormalized), 0d)]
	public async Task Assess_SampleTickets_ScoreByQuality(string sample, double expected)
	{
		using JsonDocument issue = BaseCategoryTestHelpers.Sample(sample);

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(AcceptanceCriteriaCategory.Create(), issue, CancellationToken);

		result.Categories.Should().ContainSingle().Which.Score.NormalizedScore.Should().Be(expected);
	}
}
