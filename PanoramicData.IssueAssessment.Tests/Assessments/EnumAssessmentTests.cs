using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="EnumAssessment"/>.
/// </summary>
public class EnumAssessmentTests
{
	private static EnumAssessment Create() => new()
	{
		JsonPath = "$.state",
		AllowedValues = ["Assigned", "InProgress"],
		SuccessScore = 0.9d,
		FailureScore = 0.2d,
	};

	[Theory]
	[InlineData("Assigned")]
	[InlineData("InProgress")]
	public async Task ExecuteAsync_AllowedValue_ReturnsSuccessScore(string state)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), $$"""{"state":"{{state}}"}""");

		result.RawScore.Should().Be(0.9d);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be($"Value '{state}' at '$.state' is allowed.");
	}

	[Theory]
	[InlineData("Closed")]
	[InlineData("assigned")]
	[InlineData("")]
	public async Task ExecuteAsync_ValueNotAllowed_ReturnsFailureScoreWithoutFailureReason(string state)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), $$"""{"state":"{{state}}"}""");

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be($"Value '{state}' at '$.state' is not allowed.");
	}

	[Fact]
	public async Task ExecuteAsync_CaseInsensitiveComparer_AllowsDifferentCase()
	{
		EnumAssessment assessment = Create() with
		{
			AllowedValues = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Assigned"),
		};

		AssessmentExecutionResult result = await ExecuteAsync(assessment, """{"state":"ASSIGNED"}""");

		result.RawScore.Should().Be(0.9d);
	}

	[Theory]
	[InlineData("""{}""")]
	[InlineData("""{"state":null}""")]
	[InlineData("""{"state":3}""")]
	[InlineData("""{"state":["Assigned"]}""")]
	public async Task ExecuteAsync_MissingOrNonStringValue_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
		result.ExplanationText.Should().Be("String value was not found at '$.state'.");
	}

	[Theory]
	[InlineData("""{"state":"Assigned"}""")]
	[InlineData("""{"state":"Closed"}""")]
	[InlineData("""{}""")]
	public async Task ExecuteAsync_ExplanationTemplate_ReplacesEveryExplanation(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create() with { ExplanationTemplate = "custom" }, json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_Defaults_AreOneAndZero()
	{
		EnumAssessment assessment = new() { JsonPath = "$.state", AllowedValues = ["Open"] };

		(await ExecuteAsync(assessment, """{"state":"Open"}""")).RawScore.Should().Be(1d);
		(await ExecuteAsync(assessment, """{"state":"Shut"}""")).RawScore.Should().Be(0d);
	}

	[Fact]
	public async Task ExecuteAsync_CancelledToken_Throws()
	{
		Func<Task> act = () => ExecuteWithCancelledTokenAsync(Create(), """{"state":"Assigned"}""");

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_Throws()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create());

		await act.Should().ThrowAsync<ArgumentNullException>();
	}
}
