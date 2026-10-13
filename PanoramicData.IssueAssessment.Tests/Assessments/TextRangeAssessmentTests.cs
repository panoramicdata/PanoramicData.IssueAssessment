using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="StringLengthAssessment"/> and <see cref="WordCountAssessment"/>, which share their shape:
/// an inclusive range over a string value.
/// </summary>
public class TextRangeAssessmentTests
{
	private static StringLengthAssessment Length() => new()
	{
		JsonPath = "$.text",
		MinimumLength = 3,
		MaximumLength = 6,
		SuccessScore = 0.9d,
		FailureScore = 0.2d,
	};

	private static WordCountAssessment Words() => new()
	{
		JsonPath = "$.text",
		MinimumWordCount = 2,
		MaximumWordCount = 3,
		SuccessScore = 0.9d,
		FailureScore = 0.2d,
	};

	private static string Text(string value) => JsonSerializer.Serialize(new { text = value });

	[Theory]
	[InlineData("ab", 0.2d, "outside")]
	[InlineData("abc", 0.9d, "within")]
	[InlineData("abcdef", 0.9d, "within")]
	[InlineData("abcdefg", 0.2d, "outside")]
	public async Task StringLength_AtBoundaries_ScoresInclusively(string value, double expected, string relation)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Length(), Text(value));

		result.RawScore.Should().Be(expected);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be($"String length {value.Length} at '$.text' is {relation} range 3-6.");
	}

	[Fact]
	public async Task StringLength_CountsWhitespaceAndUnicodeCharacters()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Length(), Text("  é  "));

		result.ExplanationText.Should().Be("String length 5 at '$.text' is within range 3-6.");
	}

	[Theory]
	[InlineData("one", 0.2d, 1, "outside")]
	[InlineData("one two", 0.9d, 2, "within")]
	[InlineData("  one\ttwo\nthree  ", 0.9d, 3, "within")]
	[InlineData("one two three four", 0.2d, 4, "outside")]
	[InlineData("", 0.2d, 0, "outside")]
	[InlineData("   ", 0.2d, 0, "outside")]
	public async Task WordCount_CountsWhitespaceSeparatedWordsInclusively(string value, double expected, int words, string relation)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Words(), Text(value));

		result.RawScore.Should().Be(expected);
		result.FailureReason.Should().BeNull();
		result.ExplanationText.Should().Be($"Word count {words} at '$.text' is {relation} range 2-3.");
	}

	public static TheoryData<string> MissingValues => ["""{}""", """{"text":null}""", """{"text":42}""", """{"text":{"a":"b"}}"""];

	[Theory]
	[MemberData(nameof(MissingValues))]
	public async Task StringLength_MissingOrNonString_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Length(), json);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
		result.ExplanationText.Should().Be("String value was not found at '$.text'.");
	}

	[Theory]
	[MemberData(nameof(MissingValues))]
	public async Task WordCount_MissingOrNonString_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Words(), json);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
		result.ExplanationText.Should().Be("String value was not found at '$.text'.");
	}

	[Theory]
	[InlineData("""{"text":"abcd"}""")]
	[InlineData("""{"text":"a"}""")]
	[InlineData("""{}""")]
	public async Task ExplanationTemplate_ReplacesEveryExplanation(string json)
	{
		(await ExecuteAsync(Length() with { ExplanationTemplate = "custom" }, json)).ExplanationText.Should().Be("custom");
		(await ExecuteAsync(Words() with { ExplanationTemplate = "custom" }, json)).ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task Defaults_AreOneAndZero()
	{
		StringLengthAssessment length = new() { JsonPath = "$.text", MinimumLength = 1, MaximumLength = 2 };
		WordCountAssessment words = new() { JsonPath = "$.text", MinimumWordCount = 1, MaximumWordCount = 1 };

		(await ExecuteAsync(length, Text("a"))).RawScore.Should().Be(1d);
		(await ExecuteAsync(length, Text("abc"))).RawScore.Should().Be(0d);
		(await ExecuteAsync(words, Text("a"))).RawScore.Should().Be(1d);
		(await ExecuteAsync(words, Text("a b"))).RawScore.Should().Be(0d);
	}

	[Fact]
	public async Task CancelledToken_Throws()
	{
		Func<Task> length = () => ExecuteWithCancelledTokenAsync(Length(), Text("abc"));
		Func<Task> words = () => ExecuteWithCancelledTokenAsync(Words(), Text("a b"));

		await length.Should().ThrowAsync<OperationCanceledException>();
		await words.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task NullInput_Throws()
	{
		Func<Task> length = () => ExecuteWithNullInputAsync(Length());
		Func<Task> words = () => ExecuteWithNullInputAsync(Words());

		await length.Should().ThrowAsync<ArgumentNullException>();
		await words.Should().ThrowAsync<ArgumentNullException>();
	}
}
