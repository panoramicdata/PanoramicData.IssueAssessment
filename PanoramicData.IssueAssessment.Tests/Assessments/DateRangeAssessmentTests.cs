using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="DateRangeAssessment"/>.
/// </summary>
public class DateRangeAssessmentTests : TestBase
{
	private static readonly DateTime Min = new(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);
	private static readonly DateTime Max = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

	private static DateRangeAssessment Create(string dateFormat = "O") => new()
	{
		JsonPath = "$.dueDate",
		MinDate = Min,
		MaxDate = Max,
		DateFormat = dateFormat,
		SuccessScore = 0.9d,
		FailureScore = 0.2d,
	};

	private static string Doc(string dueDate) => $$"""{"dueDate":"{{dueDate}}"}""";

	[Fact]
	public async Task ExecuteAsync_DateWithinRange_ReturnsSuccessScoreAndWithinExplanation()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), Doc("2026-05-12T00:00:00.0000000Z"));

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be(
			"Date 2026-05-12T00:00:00.0000000Z at '$.dueDate' is within range "
			+ "2026-01-01T00:00:00.0000000Z-2026-12-31T00:00:00.0000000Z.");
		result.FailureReason.Should().BeNull();
		result.InnerException.Should().BeNull();
		result.VirtualExtensionOutput.Should().BeNull();
	}

	[Theory]
	[InlineData("2025-12-31T23:59:59.9999999Z")]
	[InlineData("2026-12-31T00:00:00.0000001Z")]
	public async Task ExecuteAsync_DateOutsideRange_ReturnsFailureScoreAndOutsideExplanation(string dueDate)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), Doc(dueDate));

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be(
			$"Date {dueDate} at '$.dueDate' is outside range "
			+ "2026-01-01T00:00:00.0000000Z-2026-12-31T00:00:00.0000000Z.");
		result.FailureReason.Should().BeNull();
	}

	[Theory]
	[InlineData("2026-01-01T00:00:00.0000000Z")]
	[InlineData("2026-12-31T00:00:00.0000000Z")]
	public async Task ExecuteAsync_DateOnBoundary_IsInclusive(string dueDate)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), Doc(dueDate));

		result.RawScore.Should().Be(0.9d);
	}

	[Fact]
	public async Task ExecuteAsync_NoBounds_AnyParsableDateIsInRangeWithInfinityLabels()
	{
		DateRangeAssessment assessment = Create() with { MinDate = null, MaxDate = null };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Doc("1900-01-01T00:00:00.0000000Z"));

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be("Date 1900-01-01T00:00:00.0000000Z at '$.dueDate' is within range -inf-+inf.");
	}

	[Fact]
	public async Task ExecuteAsync_OnlyMinimum_RejectsEarlierAndAcceptsLater()
	{
		DateRangeAssessment assessment = Create() with { MaxDate = null };

		AssessmentExecutionResult early = await ExecuteAsync(assessment, Doc("2025-06-01T00:00:00.0000000Z"));
		AssessmentExecutionResult late = await ExecuteAsync(assessment, Doc("2099-06-01T00:00:00.0000000Z"));

		early.RawScore.Should().Be(0.2d);
		late.RawScore.Should().Be(0.9d);
		late.ExplanationText.Should().EndWith("within range 2026-01-01T00:00:00.0000000Z-+inf.");
	}

	[Fact]
	public async Task ExecuteAsync_OnlyMaximum_RejectsLaterAndAcceptsEarlier()
	{
		DateRangeAssessment assessment = Create() with { MinDate = null };

		AssessmentExecutionResult early = await ExecuteAsync(assessment, Doc("1999-06-01T00:00:00.0000000Z"));
		AssessmentExecutionResult late = await ExecuteAsync(assessment, Doc("2027-06-01T00:00:00.0000000Z"));

		early.RawScore.Should().Be(0.9d);
		early.ExplanationText.Should().EndWith("within range -inf-2026-12-31T00:00:00.0000000Z.");
		late.RawScore.Should().Be(0.2d);
	}

	[Fact]
	public async Task ExecuteAsync_PathMissing_ReturnsJsonPathNotFound()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), """{"created":"2026-05-12T00:00:00.0000000Z"}""");

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be("Date value was not found at '$.dueDate'.");
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("""{"dueDate":20260512}""")]
	[InlineData("""{"dueDate":null}""")]
	[InlineData("""{"dueDate":{"date":"2026-05-12"}}""")]
	public async Task ExecuteAsync_ValueIsNotString_ReturnsJsonPathNotFound(string json)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), json);

		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("not a date")]
	[InlineData("")]
	[InlineData("2026-05-12")]
	public async Task ExecuteAsync_ValueDoesNotMatchFormat_ReturnsFailureScoreWithParseExplanation(string dueDate)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(), Doc(dueDate));

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be($"Date value '{dueDate}' at '$.dueDate' could not be parsed with format 'O'.");
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_CustomFormat_ParsesAndFormatsWithIt()
	{
		DateRangeAssessment assessment = Create("dd/MM/yyyy");

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Doc("12/05/2026"));

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be("Date 12/05/2026 at '$.dueDate' is within range 01/01/2026-31/12/2026.");
	}

	[Fact]
	public async Task ExecuteAsync_CustomFormatUnderNonInvariantCulture_ParsesAndFormatsInvariantly()
	{
		using CultureScope culture = UseCulture("de-DE");
		DateRangeAssessment assessment = Create("dd/MM/yyyy");

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Doc("12/05/2026"));

		result.RawScore.Should().Be(0.9d);
		result.ExplanationText.Should().Be("Date 12/05/2026 at '$.dueDate' is within range 01/01/2026-31/12/2026.");
	}

	[Fact]
	public async Task ExecuteAsync_MonthNameFormatUnderFrenchCulture_UsesInvariantMonthNames()
	{
		using CultureScope culture = UseCulture("fr-FR");
		DateRangeAssessment assessment = Create("dd MMMM yyyy");

		AssessmentExecutionResult english = await ExecuteAsync(assessment, Doc("12 May 2026"));
		AssessmentExecutionResult french = await ExecuteAsync(assessment, Doc("12 mai 2026"));

		english.RawScore.Should().Be(0.9d);
		french.ExplanationText.Should().Be("Date value '12 mai 2026' at '$.dueDate' could not be parsed with format 'dd MMMM yyyy'.");
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("   ")]
	public async Task ExecuteAsync_BlankFormat_ParsesFlexiblyAndExplainsWithSortableFormat(string dateFormat)
	{
		DateRangeAssessment assessment = Create(dateFormat);

		AssessmentExecutionResult dateOnly = await ExecuteAsync(assessment, Doc("2026-11-20"));
		AssessmentExecutionResult utc = await ExecuteAsync(assessment, Doc("2027-12-01T08:00:00Z"));

		dateOnly.RawScore.Should().Be(0.9d);
		dateOnly.ExplanationText.Should().Be("Date 2026-11-20T00:00:00 at '$.dueDate' is within range 2026-01-01T00:00:00-2026-12-31T00:00:00.");
		utc.RawScore.Should().Be(0.2d);
		utc.ExplanationText.Should().Be("Date 2027-12-01T08:00:00 at '$.dueDate' is outside range 2026-01-01T00:00:00-2026-12-31T00:00:00.");
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public async Task ExecuteAsync_BlankFormatAndUnparsableValue_ExplainsWithoutFormat(string dateFormat)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(dateFormat), Doc("someday"));

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().Be("Date value 'someday' at '$.dueDate' could not be parsed as a date.");
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_BlankFormatUnderNonInvariantCulture_ParsesInvariantly()
	{
		using CultureScope culture = UseCulture("de-DE");
		DateRangeAssessment assessment = Create(string.Empty);

		// Invariant culture reads 05/12/2026 as 12 May; de-DE would read it as 5 December.
		AssessmentExecutionResult result = await ExecuteAsync(assessment, Doc("05/12/2026"));

		result.ExplanationText.Should().Be("Date 2026-05-12T00:00:00 at '$.dueDate' is within range 2026-01-01T00:00:00-2026-12-31T00:00:00.");
	}

	[Fact]
	public async Task ExecuteAsync_ValueWithOffset_IsComparedAsUtcRegardlessOfMachineTimeZone()
	{
		DateRangeAssessment assessment = Create() with { MinDate = new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc) };

		// 00:30 at UTC+01:00 is 23:30 UTC on the previous day, which is before the minimum.
		AssessmentExecutionResult result = await ExecuteAsync(assessment, Doc("2026-06-01T00:30:00.0000000+01:00"));

		result.RawScore.Should().Be(0.2d);
		result.ExplanationText.Should().StartWith("Date 2026-05-31T23:30:00.0000000Z at '$.dueDate' is outside range");
	}

	[Theory]
	[InlineData("""{"dueDate":"2026-05-12T00:00:00.0000000Z"}""")]
	[InlineData("""{"dueDate":"2030-05-12T00:00:00.0000000Z"}""")]
	[InlineData("""{"dueDate":"garbage"}""")]
	[InlineData("""{"other":1}""")]
	public async Task ExecuteAsync_ExplanationTemplateSet_OverridesEveryExplanation(string json)
	{
		DateRangeAssessment assessment = Create() with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_InvalidJsonPath_ThrowsInvalidJsonPathException()
	{
		Func<Task> act = () => ExecuteAsync(Create() with { JsonPath = "$..dueDate" }, Doc("2026-05-12T00:00:00.0000000Z"));

		await act.Should().ThrowAsync<InvalidJsonPathException>();
	}

	[Fact]
	public async Task ExecuteAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException()
	{
		Func<Task> act = () => ExecuteWithCancelledTokenAsync(Create(), Doc("2026-05-12T00:00:00.0000000Z"));

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_ThrowsArgumentNullException()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create());

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}
}
