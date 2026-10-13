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
}
