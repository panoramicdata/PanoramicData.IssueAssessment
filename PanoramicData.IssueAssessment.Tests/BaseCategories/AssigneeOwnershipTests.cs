using PanoramicData.IssueAssessment.BaseCategories;
using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

/// <summary>
/// Tests for <see cref="AssigneeOwnershipCategory"/>.
/// </summary>
public class AssigneeOwnershipTests : TestBase
{
	[Fact]
	public void Create_Always_ChecksAssigneeAndOwnershipState()
	{
		AssessmentCategory category = AssigneeOwnershipCategory.Create();

		category.Name.Should().Be("Assignee Ownership");
		category.Assessments.Select(assessment => assessment.Name).Should().Equal("Assignee Presence", "Assignee State");
		category.Assessments[0].Definition.Should().BeOfType<PresenceAssessment>().Which.JsonPath.Should().Be("$.assignee");
		EnumAssessment state = category.Assessments[1].Definition.Should().BeOfType<EnumAssessment>().Subject;
		state.JsonPath.Should().Be("$.assigneeState");
		state.AllowedValues.Should().BeEquivalentTo("Assigned", "InProgress", "Owned");
	}

	[Theory]
	[InlineData(nameof(SampleIssuesFixture.GoodNormalized), 1d)]
	[InlineData(nameof(SampleIssuesFixture.MediocreNormalized), 0.5d)]
	[InlineData(nameof(SampleIssuesFixture.PoorNormalized), 0d)]
	public async Task Assess_SampleTickets_ScoreByQuality(string sample, double expected)
	{
		using JsonDocument issue = BaseCategoryTestHelpers.Sample(sample);

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(AssigneeOwnershipCategory.Create(), issue, CancellationToken);

		result.Categories.Should().ContainSingle().Which.Score.NormalizedScore.Should().Be(expected);
	}

	[Theory]
	[InlineData("Assigned", 1d)]
	[InlineData("Owned", 1d)]
	[InlineData("Unassigned", 0d)]
	public async Task Assess_AssigneeState_IsCheckedAgainstOwnershipStates(string state, double expected)
	{
		using JsonDocument issue = SampleIssuesFixture.With(("assignee", "Alex"), ("assigneeState", state));

		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(AssigneeOwnershipCategory.Create(), issue, CancellationToken);

		result.Categories[0].Assessments[1].Score.NormalizedScore.Should().Be(expected);
	}
}
