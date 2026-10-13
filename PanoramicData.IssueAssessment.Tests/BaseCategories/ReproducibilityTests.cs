using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="ReproducibilityCategory"/>.
/// </summary>
public class ReproducibilityTests : TestBase
{
	[Fact]
	public void Create_Always_ChecksStepsArePresentAndListed()
	{
		AssessmentCategory category = ReproducibilityCategory.Create();

		category.Name.Should().Be("Reproducibility");
		category.Assessments.Select(assessment => assessment.Name).Should().Equal("Repro Steps Presence", "Repro Steps Structure");
		category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Which.JsonPath.Should().Be("$.reproductionSteps");
		PatternCountAssessment structure = category.Assessments[1].Definition.Should().BeOfType<PatternCountAssessment>().Subject;
		structure.JsonPath.Should().Be("$.reproductionSteps");
		structure.MinMatches.Should().Be(1);
	}

	[Theory]
	[InlineData(nameof(SampleIssuesFixture.GoodNormalized), 1d)]
	[InlineData(nameof(SampleIssuesFixture.MediocreNormalized), 0.5d)]
	[InlineData(nameof(SampleIssuesFixture.PoorNormalized), 0d)]
	public async Task Assess_SampleTickets_ScoreByQuality(string sample, double expected)
	{
		using JsonDocument issue = BaseCategoryTestHelpers.Sample(sample);

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(ReproducibilityCategory.Create(), issue, CancellationToken);

		result.Categories.Should().ContainSingle().Which.Score.NormalizedScore.Should().Be(expected);
	}

	[Theory]
	[InlineData("1. Open the report\n2. Export", 1d)]
	[InlineData("- Open the report\n- Export", 1d)]
	[InlineData("* Open the report", 1d)]
	[InlineData("   3. Indented step", 1d)]
	[InlineData("Open the report and export it.", 0d)]
	[InlineData("Version 1.2 fails", 0d)]
	public async Task Assess_StepStructure_RecognisesNumberedAndBulletedLists(string steps, double expected)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("reproductionSteps", steps));

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(ReproducibilityCategory.Create(), issue, CancellationToken);

		result.Categories[0].Assessments[1].Score.NormalizedScore.Should().Be(expected);
	}
}
