using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="PresenceAssessment"/>.
/// </summary>
public class PresenceAssessmentTests
{
	private static PresenceAssessment Create() => new()
	{
		JsonPath = "$.assignee",
		PresentScore = 0.9d,
		AbsentScore = 0.2d,
	};

	[Theory]
	[InlineData("""{"assignee":"alex"}""")]
	[InlineData("""{"assignee":0}""")]
	[InlineData("""{"assignee":false}""")]
	[InlineData("""{"assignee":{}}""")]
	[InlineData("""{"assignee":[]}""")]
	public async Task ExecuteAsync_NonEmptyOrNonStringValue_IsPresent(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.RawScore.Should().Be(0.9d);
		result.FailureReason.Should().BeNull("absence is a score, not an execution failure");
		result.ExplanationText.Should().Be("Value is present for '$.assignee'.");
	}

	[Theory]
	[InlineData("""{}""")]
	[InlineData("""{"assignee":null}""")]
	[InlineData("""{"assignee":""}""")]
	[InlineData("""{"assignee":"   "}""")]
	[InlineData("""{"assignee":"\t\n"}""")]
	public async Task ExecuteAsync_MissingNullOrBlankValue_IsAbsentWithoutFailureReason(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be("Value is missing for '$.assignee'.");
	}

	[Fact]
	public async Task ExecuteAsync_NestedPath_FindsValue()
	{
		PresenceAssessment assessment = Create() with { JsonPath = "$.fields.assignee.displayName" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, """{"fields":{"assignee":{"displayName":"Alex"}}}""");

		result.RawScore.Should().Be(0.9d);
	}

	[Theory]
	[InlineData("""{"assignee":"alex"}""")]
	[InlineData("""{}""")]
	public async Task ExecuteAsync_ExplanationTemplate_ReplacesBothExplanations(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create() with { ExplanationTemplate = "custom" }, json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_Defaults_AreOneAndZero()
	{
		PresenceAssessment assessment = new() { JsonPath = "$.assignee" };

		(await ExecuteAsync(assessment, """{"assignee":"alex"}""")).RawScore.Should().Be(1d);
		(await ExecuteAsync(assessment, """{}""")).RawScore.Should().Be(0d);
	}

	[Fact]
	public async Task ExecuteAsync_CancelledToken_Throws()
	{
		Func<Task> act = () => ExecuteWithCancelledTokenAsync(Create(), "{}");

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_Throws()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create());

		await act.Should().ThrowAsync<ArgumentNullException>();
	}
}
