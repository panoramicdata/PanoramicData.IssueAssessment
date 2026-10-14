using PanoramicData.IssueAssessment.Serialization;

namespace PanoramicData.IssueAssessment.Tests.Rules;

/// <summary>
/// Unit tests for <see cref="EvaluationContext"/>, the built-in conditions and measures, and tiered placeholders.
/// </summary>
public class RuleEngineTests : TestBase
{
	private static EvaluationContext Context(string json) => new(JsonDocument.Parse(json));

	private static TextSource Source(string path, string? section = null) => new() { Paths = [path], Section = section };

	[Fact]
	public void Condition_UnknownName_Throws()
	{
		Action act = () => Context("{}").Condition("nope");

		act.Should().Throw<KeyNotFoundException>().WithMessage("*'nope'*");
	}

	[Fact]
	public void Measure_UnknownName_Throws()
	{
		Action act = () => Context("{}").Measure("nope");

		act.Should().Throw<KeyNotFoundException>().WithMessage("*'nope'*");
	}

	[Theory]
	[InlineData("$.n", new[] { "12.5" })]
	[InlineData("$.b", new[] { "true" })]
	[InlineData("$.s", new[] { "x" })]
	[InlineData("$.a", new[] { "x", "3" })]
	[InlineData("$.z", new string[0])]
	[InlineData("$.missing", new string[0])]
	public void Strings_ReadsScalarsAndArraysAsText(string path, string[] expected)
	{
		Context("""{ "n": 12.5, "b": true, "s": "x", "a": [ "x", null, 3 ], "z": null }""").Strings(path).Should().Equal(expected);
	}

	[Fact]
	public void TextMatches_AbsentSection_IsFalse()
	{
		new TextMatchesCondition { Source = Source("$.d", "Steps"), Patterns = ["."] }.Evaluate(Context("""{ "d": "no heading" }"""))
			.Should().BeFalse();
	}

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public void FieldEquals_HonoursCaseSensitivity(bool caseSensitive, bool expected)
	{
		new FieldEqualsCondition { Path = "$.t", Values = ["BUG"], CaseSensitive = caseSensitive }.Evaluate(Context("""{ "t": "bug" }"""))
			.Should().Be(expected);
	}

	[Fact]
	public void AssessmentScore_OutsideCapsAndClassifications_Throws()
	{
		Action act = () => new AssessmentScoreCondition { Assessment = "A" }.Evaluate(Context("{}"));

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void Measures_OnAnAbsentSection_MeasureZero()
	{
		EvaluationContext context = Context("""{ "d": "no heading" }""");
		TextSource absent = Source("$.d", "Steps");

		new PatternMatchCountMeasure { Source = absent, Patterns = ["^$"] }.Evaluate(context).Should().Be(1, "the absent section reads as empty text");
		new OccurrenceCountMeasure { Source = absent, Pattern = "x" }.Evaluate(context).Should().Be(0);
		new LengthMeasure { Source = absent }.Evaluate(context).Should().Be(0);
		new LengthMeasure { Source = Source("$.d") }.Evaluate(context).Should().Be(10);
	}

	[Theory]
	[InlineData(false, 2)]
	[InlineData(true, 1)]
	public void OccurrenceCount_HonoursCaseSensitivity(bool caseSensitive, double expected)
	{
		new OccurrenceCountMeasure { Source = Source("$.d"), Pattern = "ab", CaseSensitive = caseSensitive }
			.Evaluate(Context("""{ "d": "ab AB" }""")).Should().Be(expected);
	}

	[Theory]
	[InlineData("""{ "n": "7" }""", 7)]
	[InlineData("""{ "n": "seven" }""", 0)]
	[InlineData("""{ "n": "1e999" }""", 0)]
	[InlineData("""{ "n": [ 1, 2 ] }""", 0)]
	public void Number_ReadsOneFiniteNumber(string json, double expected)
	{
		new NumberMeasure { Path = "$.n" }.Evaluate(Context(json)).Should().Be(expected);
	}

	[Fact]
	public async Task Tiered_WithoutAContext_EvaluatesAgainstTheIssue()
	{
		TieredAssessment tiered = new()
		{
			Measures = ImmutableDictionary<string, IMeasure>.Empty.Add("words", new WordCountMeasure { Source = Source("$.d") }),
			Tiers =
			[
				new Tier { When = new MeasureCondition { Measure = "words", AtLeast = 3 }, Score = 100, Finding = "{words} words; {$.k~^MS-} {$.k~^OPS-}." },
				new Tier { Score = 0, Finding = "short" },
			],
		};
		using JsonDocument issue = JsonDocument.Parse("""{ "d": "one two three", "k": "MS-1" }""");

		AssessmentExecutionResult result = await tiered.ExecuteAsync(issue, null, CancellationToken);

		result.RawScore.Should().Be(100);
		result.ExplanationText.Should().Be("3 words; MS-1 .");
	}

	[Fact]
	public async Task Tiered_WithoutAnIssue_Throws()
	{
		Func<Task> act = () => new TieredAssessment { Tiers = [new Tier { Score = 0, Finding = "x" }] }.ExecuteAsync((JsonDocument)null!, null, CancellationToken);

		await act.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task NamedDefinition_Executed_Throws()
	{
		using JsonDocument issue = JsonDocument.Parse("{}");

		Func<Task> act = () => new NamedAssessmentDefinition { Name = "custom" }.ExecuteAsync(issue, null, CancellationToken);

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'custom'*");
	}

	[Fact]
	public void RuleRegex_CaseSensitive_DoesNotIgnoreCase()
	{
		new TextMatchesCondition { Source = Source("$.d"), Patterns = ["Bug"], CaseSensitive = true }.Evaluate(Context("""{ "d": "bug" }"""))
			.Should().BeFalse();
	}
}
