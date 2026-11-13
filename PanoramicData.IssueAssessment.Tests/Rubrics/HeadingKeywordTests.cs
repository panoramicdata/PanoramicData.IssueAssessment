using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// MS-23199 Section headings are credited whatever their level (h1 to h4), case or wording variant.
/// </summary>
public class HeadingKeywordTests : TestBase
{
	private static readonly IssueAssessor Assessor = new(BuiltInRubrics.PanoramicDataTicketQuality);

	[Theory]
	[InlineData("Current Behavior", "Expected Behavior")]
	[InlineData("Current Behaviour", "Expected Behaviour")]
	[InlineData("Actual Behavior", "Expected Behavior")]
	[InlineData("Actual Result", "Expected Result")]
	[InlineData("Current Behavior", "Desired Behavior")]
	[InlineData("current behavior", "EXPECTED BEHAVIOR")]
	public async Task CurrentAndExpectedHeadings_ScoreFull_WhateverTheVariantOrCase(string current, string expected)
	{
		int score = await RawScore("CurrentVsExpected", $"h2. {current}\r\nRows with line breaks are missing.\r\n\r\nh2. {expected}\r\nEvery row is exported.");

		score.Should().Be(100);
	}

	[Theory]
	[InlineData("h1.")]
	[InlineData("h2.")]
	[InlineData("h3.")]
	[InlineData("h4.")]
	public async Task CurrentAndExpectedHeadings_ScoreFull_AtEveryJiraHeadingLevel(string marker)
	{
		int score = await RawScore("CurrentVsExpected", $"{marker} Current Behavior\r\nRows are missing.\r\n\r\n{marker} Expected Behavior\r\nEvery row is exported.");

		score.Should().Be(100);
	}

	[Fact]
	public async Task CurrentHeadingAlone_StillScoresHalf()
	{
		int score = await RawScore("CurrentVsExpected", "h2. Actual Result\r\nRows are missing.");

		score.Should().Be(50);
	}

	[Theory]
	[InlineData("Acceptance Criteria")]
	[InlineData("Success Criteria")]
	[InlineData("Definition of Done")]
	[InlineData("ACCEPTANCE CRITERIA")]
	public async Task CriteriaHeadings_RaiseTestabilityAboveADescriptionWithNone(string heading)
	{
		const string Body = "Rows with line breaks are missing from the export.";

		int without = await RawScore("Testability", Body);
		int with = await RawScore("Testability", $"{Body}\r\n\r\nh2. {heading}\r\n# Every row is exported\r\n# Line breaks are preserved");

		with.Should().BeGreaterThan(without);
	}

	private static async Task<int> RawScore(string name, string description)
	{
		NormalizedIssue issue = new()
		{
			Key = "OPS-1",
			Project = "OPS",
			Title = "Export to CSV drops rows that contain line breaks",
			Description = description,
			IssueType = "Bug",
			Status = "In Progress",
			Platform = "Jira",
			Labels = [],
		};
		using JsonDocument document = issue.ToJsonDocument();
		AssessmentReport report = AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));
		return (int)report.Categories.SelectMany(category => category.Assessments).Single(item => item.Name == name).RawScore!.Value;
	}
}
