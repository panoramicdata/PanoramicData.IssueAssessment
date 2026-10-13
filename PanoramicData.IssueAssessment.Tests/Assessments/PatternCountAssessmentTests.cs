using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="PatternCountAssessment"/>.
/// </summary>
public class PatternCountAssessmentTests
{
	private static PatternCountAssessment Create() => new()
	{
		JsonPath = "$.steps",
		Pattern = @"(?m)^\d+\.",
		MinMatches = 2,
		MaxMatches = 4,
		SuccessScore = 0.9d,
		FailureScore = 0.2d,
	};

	private static string Steps(int count)
		=> JsonSerializer.Serialize(new { steps = string.Join('\n', Enumerable.Range(1, count).Select(index => $"{index}. step")) });

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public async Task ExecuteAsync_CountWithinRange_ReturnsSuccessScore(int count)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), Steps(count));

		result.RawScore.Should().Be(0.9d);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be($"Found {count} matches at '$.steps', within expected range 2-4.");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	public async Task ExecuteAsync_CountOutsideRange_ReturnsFailureScore(int count)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), Steps(count));

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be($"Found {count} matches at '$.steps', outside expected range 2-4.");
	}

	[Fact]
	public async Task ExecuteAsync_NoBounds_AlwaysInRangeAndExplainsOpenRange()
	{
		PatternCountAssessment assessment = Create() with { MinMatches = null, MaxMatches = null };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Steps(0));

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be("Found 0 matches at '$.steps', within expected range 0-+inf.");
	}

	[Fact]
	public async Task ExecuteAsync_MinimumOnly_RejectsTooFewAndAcceptsMany()
	{
		PatternCountAssessment assessment = Create() with { MaxMatches = null };

		(await ExecuteAsync(assessment, Steps(1))).RawScore.Should().Be(0.2d);
		(await ExecuteAsync(assessment, Steps(50))).RawScore.Should().Be(0.9d);
	}

	[Fact]
	public async Task ExecuteAsync_MaximumOnly_RejectsTooManyAndAcceptsNone()
	{
		PatternCountAssessment assessment = Create() with { MinMatches = null };

		(await ExecuteAsync(assessment, Steps(0))).RawScore.Should().Be(0.9d);
		(await ExecuteAsync(assessment, Steps(5))).RawScore.Should().Be(0.2d);
	}

	[Theory]
	[InlineData("""{}""")]
	[InlineData("""{"steps":null}""")]
	[InlineData("""{"steps":12}""")]
	public async Task ExecuteAsync_MissingOrNonStringValue_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
		result.ExplanationText.Should().Be("String value was not found at '$.steps'.");
	}

	[Theory]
	[InlineData(3)]
	[InlineData(0)]
	public async Task ExecuteAsync_ExplanationTemplate_ReplacesRangeExplanation(int count)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create() with { ExplanationTemplate = "custom" }, Steps(count));

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_ExplanationTemplate_ReplacesMissingExplanation()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create() with { ExplanationTemplate = "custom" }, "{}");

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_Defaults_AreOneAndZero()
	{
		PatternCountAssessment assessment = new() { JsonPath = "$.steps", Pattern = "x", MinMatches = 1 };

		(await ExecuteAsync(assessment, """{"steps":"x"}""")).RawScore.Should().Be(1d);
		(await ExecuteAsync(assessment, """{"steps":"y"}""")).RawScore.Should().Be(0d);
	}

	[Fact]
	public async Task ExecuteAsync_CancelledToken_Throws()
	{
		Func<Task> act = () => ExecuteWithCancelledTokenAsync(Create(), Steps(2));

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_Throws()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create());

		await act.Should().ThrowAsync<ArgumentNullException>();
	}
}
