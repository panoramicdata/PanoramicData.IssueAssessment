namespace PanoramicData.IssueAssessment.Tests;

using PanoramicData.IssueAssessment.BaseCategories;

/// <summary>
/// Tests for built-in base category factory definitions.
/// </summary>
public class BaseCategoriesTests
{
	[Fact]
	public void TitleQualityCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = TitleQualityCategory.Create();

		AssertCategory(category, "Title Quality");
	}

	[Fact]
	public void DescriptionQualityCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = DescriptionQualityCategory.Create();

		AssertCategory(category, "Description Quality");
	}

	[Fact]
	public void AcceptanceCriteriaCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = AcceptanceCriteriaCategory.Create();

		AssertCategory(category, "Acceptance Criteria");
	}

	[Fact]
	public void AssigneeOwnershipCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = AssigneeOwnershipCategory.Create();

		AssertCategory(category, "Assignee Ownership");
	}

	[Fact]
	public void DueDateTimelineCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = DueDateTimelineCategory.Create();

		AssertCategory(category, "Due Date Timeline");
	}

	[Fact]
	public void ReproducibilityCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = ReproducibilityCategory.Create();

		AssertCategory(category, "Reproducibility");
	}

	[Fact]
	public void AiSuitabilityCategory_Create_ReturnsConfiguredCategory()
	{
		AssessmentCategory category = AiSuitabilityCategory.Create();

		AssertCategory(category, "AI Suitability");
	}

	private static void AssertCategory(AssessmentCategory category, string expectedName)
	{
		category.Name.Should().Be(expectedName);
		category.Description.Should().NotBeNullOrWhiteSpace();
		category.AssessmentExecutionStrategy.Should().Be(AssessmentExecutionStrategy.Series);
		category.Assessments.Should().NotBeEmpty();
	}
}
