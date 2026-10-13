using PanoramicData.IssueAssessment.Tests.Fixtures;

namespace PanoramicData.IssueAssessment.Tests.BaseCategories;

internal static class BaseCategoryTestHelpers
{
	/// <summary>
	/// Returns the named sample ticket from <see cref="SampleIssuesFixture"/>, so theories can take the sample's name.
	/// </summary>
	public static JsonDocument Sample(string name) => name switch
	{
		nameof(SampleIssuesFixture.GoodNormalized) => SampleIssuesFixture.GoodNormalized(),
		nameof(SampleIssuesFixture.MediocreNormalized) => SampleIssuesFixture.MediocreNormalized(),
		nameof(SampleIssuesFixture.PoorNormalized) => SampleIssuesFixture.PoorNormalized(),
		_ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown sample."),
	};

	/// <summary>
	/// Assesses <paramref name="issue"/> with <paramref name="category"/> alone and returns its single category result.
	/// </summary>
	public static async Task<CategoryResult> AssessCategoryAsync(AssessmentCategory category, JsonDocument issue)
	{
		IAssessmentResult result = await OptionsBuilderFixture.AssessAsync(category, issue, TestContext.Current.CancellationToken);
		return result.Categories.Should().ContainSingle().Subject;
	}

	/// <summary>
	/// Asserts the parts every built-in category shares: its name, series execution, its assessments' names in order,
	/// and that every assessment has weight 1 and a description.
	/// </summary>
	public static void ShouldBeSeriesCategory(this AssessmentCategory category, string name, params string[] assessmentNames)
	{
		category.Name.Should().Be(name);
		category.Description.Should().NotBeNullOrWhiteSpace();
		category.AssessmentExecutionStrategy.Should().Be(AssessmentExecutionStrategy.Series);
		category.Assessments.Select(assessment => assessment.Name).Should().Equal(assessmentNames);
		category.Assessments.Should().OnlyContain(assessment => assessment.Weight == 1d && !string.IsNullOrWhiteSpace(assessment.Description));
	}

	public static void ShouldBePresence(this Assessment assessment, string jsonPath)
	{
		PresenceAssessment presence = assessment.Definition.Should().BeOfType<PresenceAssessment>().Subject;
		presence.JsonPath.Should().Be(jsonPath);
		presence.PresentScore.Should().Be(1d);
		presence.AbsentScore.Should().Be(0d);
	}

	public static void ShouldBeStringLength(this Assessment assessment, string jsonPath, int minimum, int maximum)
	{
		StringLengthAssessment length = assessment.Definition.Should().BeOfType<StringLengthAssessment>().Subject;
		(length.JsonPath, length.MinimumLength, length.MaximumLength, length.SuccessScore, length.FailureScore)
			.Should().Be((jsonPath, minimum, maximum, 1d, 0d));
	}

	public static void ShouldBeWordCount(this Assessment assessment, string jsonPath, int minimum, int maximum)
	{
		WordCountAssessment words = assessment.Definition.Should().BeOfType<WordCountAssessment>().Subject;
		(words.JsonPath, words.MinimumWordCount, words.MaximumWordCount, words.SuccessScore, words.FailureScore)
			.Should().Be((jsonPath, minimum, maximum, 1d, 0d));
	}
}
