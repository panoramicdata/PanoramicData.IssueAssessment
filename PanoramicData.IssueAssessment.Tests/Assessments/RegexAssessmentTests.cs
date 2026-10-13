using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="RegexAssessment"/>.
/// </summary>
public class RegexAssessmentTests : TestBase
{
	private static RegexAssessment Create(string pattern = @"^BUG-\d+$", string jsonPath = "$.title") => new()
	{
		JsonPath = jsonPath,
		Pattern = pattern,
		SuccessScore = 0.9d,
		FailureScore = 0.2d,
	};

	[Fact]
	public async Task ExecuteAsync_PatternMatches_ReturnsSuccessScoreAndMatchedExplanation()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"title":"BUG-123"}""");

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be(@"Pattern '^BUG-\d+$' matched value at '$.title'.");
		result.FailureReason.Should().BeNull();
		result.InnerException.Should().BeNull();
		result.VirtualExtensionOutput.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_PatternDoesNotMatch_ReturnsFailureScoreWithoutFailureReason()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"title":"Something is broken"}""");

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be(@"Pattern '^BUG-\d+$' did not match value at '$.title'.");
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_PathMissing_ReturnsFailureScoreAndJsonPathNotFound()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"summary":"BUG-123"}""");

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be("String value was not found at '$.title'.");
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("""{"title":123}""")]
	[InlineData("""{"title":null}""")]
	[InlineData("""{"title":true}""")]
	[InlineData("""{"title":{"text":"BUG-1"}}""")]
	[InlineData("""{"title":["BUG-1"]}""")]
	public async Task ExecuteAsync_ValueIsNotString_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task ExecuteAsync_EmptyString_IsEvaluatedAgainstPattern()
	{
		AssessmentExecutionResult matchesEmpty = await ExecuteAsync(Create("^$"), """{"title":""}""");
		AssessmentExecutionResult requiresText = await ExecuteAsync(Create(@"\S"), """{"title":""}""");

		matchesEmpty.RawScore.Should().Be(0.9d);
		matchesEmpty.FailureReason.Should().BeNull();
		requiresText.RawScore.Should().Be(0.2d);
		requiresText.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_PatternIsCaseSensitive_DoesNotMatchDifferentCase()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"title":"bug-123"}""");

		result.RawScore.Should().Be(0.2d);
	}

	[Fact]
	public async Task ExecuteAsync_InlineCaseInsensitiveOption_Matches()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(@"(?i)^bug-\d+$"), """{"title":"BUG-7"}""");

		result.RawScore.Should().Be(0.9d);
	}

	[Fact]
	public async Task ExecuteAsync_NestedArrayPath_SelectsIndexedValue()
	{
		AssessmentExecutionResult result = await ExecuteAsync(
			Create(jsonPath: "$.issue.links[1].key"),
			"""{"issue":{"links":[{"key":"nope"},{"key":"BUG-42"}]}}""");

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be(@"Pattern '^BUG-\d+$' matched value at '$.issue.links[1].key'.");
	}

	[Fact]
	public async Task ExecuteAsync_ArrayIndexOutOfRange_ReturnsJsonPathNotFound()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(jsonPath: "$.keys[5]"), """{"keys":["BUG-1"]}""");

		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("""{"title":"BUG-1"}""")]
	[InlineData("""{"title":"nope"}""")]
	[InlineData("""{"other":"BUG-1"}""")]
	public async Task ExecuteAsync_ExplanationTemplateSet_OverridesEveryExplanation(string json)
	{
		RegexAssessment assessment = Create() with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_InvalidPattern_ThrowsRegexParseException()
	{
		Func<Task> act = () => ExecuteAsync(Create("("), """{"title":"BUG-1"}""");

		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task ExecuteAsync_InvalidJsonPath_ThrowsInvalidJsonPathException()
	{
		Func<Task> act = () => ExecuteAsync(Create(jsonPath: "title"), """{"title":"BUG-1"}""");

		await act.Should().ThrowAsync<InvalidJsonPathException>();
	}

	[Fact]
	public async Task ExecuteAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException()
	{
		Func<Task> act = () => ExecuteWithCancelledTokenAsync(Create(), """{"title":"BUG-1"}""");

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_ThrowsArgumentNullException()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create());

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}

	[Fact]
	public async Task ExecuteAsync_VirtualExtensionSupplied_IsIgnored()
	{
		using JsonDocument input = JsonDocument.Parse("""{"title":"BUG-1"}""");
		using JsonDocument extension = JsonDocument.Parse("""{"title":"nope"}""");

		AssessmentExecutionResult result = await Create().ExecuteAsync(input, extension, CancellationToken);

		result.RawScore.Should().Be(0.9d);
		result.VirtualExtensionOutput.Should().BeNull();
	}
}
