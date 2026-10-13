namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Every built-in JSONPath assessment reads a value from the issue first and, when the issue lacks it, from the
/// virtual extension produced by earlier assessments in a series.
/// </summary>
public class VirtualExtensionFallbackTests
{
	public static TheoryData<IAssessmentDefinition, string> DefinitionsAndPassingValues => new()
	{
		{ new PresenceAssessment { JsonPath = "$.summary" }, "\"text\"" },
		{ new RegexAssessment { JsonPath = "$.summary", Pattern = "^ok$", SuccessScore = 1d, FailureScore = 0d }, "\"ok\"" },
		{ new StringLengthAssessment { JsonPath = "$.summary", MinimumLength = 1, MaximumLength = 5 }, "\"ok\"" },
		{ new WordCountAssessment { JsonPath = "$.summary", MinimumWordCount = 1, MaximumWordCount = 2 }, "\"ok\"" },
		{ new NumericRangeAssessment { JsonPath = "$.summary", MinValue = 1d, MaxValue = 3d }, "2" },
		{ new DateRangeAssessment { JsonPath = "$.summary", DateFormat = string.Empty, MinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, "\"2026-06-01\"" },
		{ new EnumAssessment { JsonPath = "$.summary", AllowedValues = ["ok"] }, "\"ok\"" },
		{ new PatternCountAssessment { JsonPath = "$.summary", Pattern = "o", MinMatches = 1 }, "\"ok\"" },
		{ new JsonPathSelectorAssessment { JsonPath = "$.summary", Evaluator = (_, _) => Task.FromResult(true) }, "\"ok\"" },
	};

	[Theory]
	[MemberData(nameof(DefinitionsAndPassingValues))]
	public async Task ExecuteAsync_ValueOnlyInVirtualExtension_IsFound(IAssessmentDefinition definition, string value)
	{
		using JsonDocument issue = JsonDocument.Parse("{}");
		using JsonDocument virtualExtension = JsonDocument.Parse($$"""{"summary":{{value}}}""");

		AssessmentExecutionResult result = await definition.ExecuteAsync(issue, virtualExtension, TestContext.Current.CancellationToken);

		result.FailureReason.Should().BeNull();
		result.RawScore.Should().Be(1d);
	}

	[Theory]
	[MemberData(nameof(DefinitionsAndPassingValues))]
	public async Task ExecuteAsync_ValueInIssue_WinsOverVirtualExtension(IAssessmentDefinition definition, string value)
	{
		using JsonDocument issue = JsonDocument.Parse($$"""{"summary":{{value}}}""");
		using JsonDocument virtualExtension = JsonDocument.Parse("""{"summary":null}""");

		AssessmentExecutionResult result = await definition.ExecuteAsync(issue, virtualExtension, TestContext.Current.CancellationToken);

		result.FailureReason.Should().BeNull();
		result.RawScore.Should().Be(1d);
	}

	[Fact]
	public async Task ExecuteAsync_ValueInNeitherDocument_IsNotFound()
	{
		StringLengthAssessment definition = new() { JsonPath = "$.summary", MinimumLength = 1, MaximumLength = 5 };
		using JsonDocument issue = JsonDocument.Parse("{}");
		using JsonDocument virtualExtension = JsonDocument.Parse("""{"other":"x"}""");

		AssessmentExecutionResult result = await definition.ExecuteAsync(issue, virtualExtension, TestContext.Current.CancellationToken);

		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}
}
