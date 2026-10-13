using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="NumericRangeAssessment"/>.
/// </summary>
public class NumericRangeAssessmentTests : TestBase
{
	private static NumericRangeAssessment Create(double? min = 3d, double? max = 8d) => new()
	{
		JsonPath = "$.storyPoints",
		MinValue = min,
		MaxValue = max,
		SuccessScore = 0.85d,
		FailureScore = 0.2d,
	};

	[Fact]
	public async Task ExecuteAsync_NumberWithinRange_ReturnsSuccessScoreAndWithinExplanation()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"storyPoints":5}""");

		result.RawScore.Should().Be(0.85d);
		result.ExplanationText.Should().Be("Value 5 at '$.storyPoints' is within range 3-8.");
		result.FailureReason.Should().BeNull();
		result.InnerException.Should().BeNull();
		result.VirtualExtensionOutput.Should().BeNull();
	}

	[Theory]
	[InlineData("2.999")]
	[InlineData("8.001")]
	[InlineData("-5")]
	public async Task ExecuteAsync_NumberOutsideRange_ReturnsFailureScoreAndOutsideExplanation(string number)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), $$"""{"storyPoints":{{number}}}""");

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be($"Value {number} at '$.storyPoints' is outside range 3-8.");
		result.FailureReason.Should().BeNull();
	}

	[Theory]
	[InlineData("3")]
	[InlineData("8")]
	[InlineData("3.0")]
	public async Task ExecuteAsync_NumberOnBoundary_IsInclusive(string number)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), $$"""{"storyPoints":{{number}}}""");

		result.RawScore.Should().Be(0.85d);
	}

	[Fact]
	public async Task ExecuteAsync_NoBounds_AnyNumberIsInRangeWithInfinityLabels()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(null, null), """{"storyPoints":-1e300}""");

		result.RawScore.Should().Be(0.85d);
		result.ExplanationText.Should().Be("Value -1E+300 at '$.storyPoints' is within range -inf-+inf.");
	}

	[Fact]
	public async Task ExecuteAsync_OnlyMinimum_RejectsLowerAndAcceptsHigher()
	{
		NumericRangeAssessment assessment = Create(max: null);

		AssessmentExecutionResult low = await ExecuteAsync(assessment, """{"storyPoints":2}""");
		AssessmentExecutionResult high = await ExecuteAsync(assessment, """{"storyPoints":2000}""");

		low.RawScore.Should().Be(0.2d);
		high.RawScore.Should().Be(0.85d);
		high.ExplanationText.Should().Be("Value 2000 at '$.storyPoints' is within range 3-+inf.");
	}

	[Fact]
	public async Task ExecuteAsync_OnlyMaximum_RejectsHigherAndAcceptsLower()
	{
		NumericRangeAssessment assessment = Create(min: null);

		AssessmentExecutionResult low = await ExecuteAsync(assessment, """{"storyPoints":-2000}""");
		AssessmentExecutionResult high = await ExecuteAsync(assessment, """{"storyPoints":9}""");

		low.RawScore.Should().Be(0.85d);
		low.ExplanationText.Should().Be("Value -2000 at '$.storyPoints' is within range -inf-8.");
		high.RawScore.Should().Be(0.2d);
	}

	[Theory]
	[InlineData("5", 5d)]
	[InlineData(" 5.5 ", 5.5d)]
	[InlineData("6e0", 6d)]
	[InlineData("-2.5", -2.5d)]
	public async Task ExecuteAsync_NumericString_IsParsedInvariantly(string text, double expected)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(null, null), $$"""{"storyPoints":"{{text}}"}""");

		result.RawScore.Should().Be(0.85d);
		result.ExplanationText.Should().Be($"Value {expected.ToString(System.Globalization.CultureInfo.InvariantCulture)} at '$.storyPoints' is within range -inf-+inf.");
	}

	[Fact]
	public async Task ExecuteAsync_NumericStringWithThousandsSeparator_IsAccepted()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(1000d, 2000d), """{"storyPoints":"1,500.25"}""");

		result.RawScore.Should().Be(0.85d);
		result.ExplanationText.Should().Be("Value 1500.25 at '$.storyPoints' is within range 1000-2000.");
	}

	[Fact]
	public async Task ExecuteAsync_DecimalCommaUnderGermanCulture_IsNotTreatedAsDecimalSeparator()
	{
		using CultureScope culture = UseCulture("de-DE");

		// Invariant parsing treats the comma as a thousands separator, so "5,5" is 55, not 5.5.
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"storyPoints":"5,5"}""");

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be("Value 55 at '$.storyPoints' is outside range 3-8.");
	}

	[Fact]
	public async Task ExecuteAsync_FractionalBoundsUnderGermanCulture_AreExplainedInvariantly()
	{
		using CultureScope culture = UseCulture("de-DE");

		AssessmentExecutionResult result = await ExecuteAsync(Create(1.5d, 2.5d), """{"storyPoints":2.25}""");

		result.ExplanationText.Should().Be("Value 2.25 at '$.storyPoints' is within range 1.5-2.5.");
	}

	[Theory]
	[InlineData("""{"storyPoints":"five"}""")]
	[InlineData("""{"storyPoints":""}""")]
	[InlineData("""{"storyPoints":"   "}""")]
	[InlineData("""{"storyPoints":true}""")]
	[InlineData("""{"storyPoints":null}""")]
	[InlineData("""{"storyPoints":[5]}""")]
	[InlineData("""{"storyPoints":{"value":5}}""")]
	[InlineData("""{"points":5}""")]
	public async Task ExecuteAsync_ValueMissingOrNotNumeric_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be("Numeric value was not found at '$.storyPoints'.");
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task ExecuteAsync_NumberOverflowingDouble_ReturnsJsonPathNotFound()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(null, null), """{"storyPoints":1e400}""");

		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("-Infinity")]
	[InlineData("1e400")]
	public async Task ExecuteAsync_NonFiniteNumericString_ReturnsJsonPathNotFound(string text)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(null, null), $$"""{"storyPoints":"{{text}}"}""");

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task ExecuteAsync_ArrayElementPath_SelectsIndexedNumber()
	{
		NumericRangeAssessment assessment = Create() with { JsonPath = "$.estimates[2]" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, """{"estimates":[1,100,4]}""");

		result.RawScore.Should().Be(0.85d);
	}

	[Theory]
	[InlineData("""{"storyPoints":5}""")]
	[InlineData("""{"storyPoints":50}""")]
	[InlineData("""{"storyPoints":"x"}""")]
	public async Task ExecuteAsync_ExplanationTemplateSet_OverridesEveryExplanation(string json)
	{
		NumericRangeAssessment assessment = Create() with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_InvalidJsonPath_ThrowsInvalidJsonPathException()
	{
		Func<Task> act = () => ExecuteAsync(Create() with { JsonPath = "$.storyPoints[x]" }, """{"storyPoints":5}""");

		await act.Should().ThrowAsync<InvalidJsonPathException>();
	}

	[Fact]
	public async Task ExecuteAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException()
	{
		Func<Task> act = () => ExecuteWithCancelledTokenAsync(Create(), """{"storyPoints":5}""");

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_ThrowsArgumentNullException()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create());

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}
}
