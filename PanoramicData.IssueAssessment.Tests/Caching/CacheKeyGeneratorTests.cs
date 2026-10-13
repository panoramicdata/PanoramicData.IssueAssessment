using PanoramicData.IssueAssessment.Caching;
using PanoramicData.IssueAssessment.Tests.Assessments;
using PanoramicData.IssueAssessment.Tests.Integration;
using static PanoramicData.IssueAssessment.Tests.Caching.CachingTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Caching;

/// <summary>
/// Tests for <see cref="CacheKeyGenerator"/>.
/// </summary>
public class CacheKeyGeneratorTests
{
	private static readonly DateTime Date = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private static string Key(AssessmentOptions options, string json = """{"title":"x"}""")
	{
		using JsonDocument issue = JsonDocument.Parse(json);
		return CacheKeyGenerator.Generate(issue, options);
	}

	private static string Key(params IAssessmentDefinition[] definitions) => Key(Options(definitions));

	/// <summary>
	/// Pairs of definitions that differ in exactly one setting; each pair must produce different keys.
	/// </summary>
	public static TheoryData<IAssessmentDefinition, IAssessmentDefinition> DifferingPairs
	{
		get
		{
			PresenceAssessment presence = new() { JsonPath = "$.a" };
			RegexAssessment regex = new() { JsonPath = "$.a", Pattern = "x", SuccessScore = 1d, FailureScore = 0d };
			StringLengthAssessment length = new() { JsonPath = "$.a", MinimumLength = 1, MaximumLength = 2 };
			WordCountAssessment words = new() { JsonPath = "$.a", MinimumWordCount = 1, MaximumWordCount = 2 };
			NumericRangeAssessment numeric = new() { JsonPath = "$.a", MinValue = 1d, MaxValue = 2d };
			DateRangeAssessment date = new() { JsonPath = "$.a", MinDate = Date, MaxDate = Date.AddDays(1) };
			EnumAssessment enumeration = new() { JsonPath = "$.a", AllowedValues = ["x"] };
			PatternCountAssessment pattern = new() { JsonPath = "$.a", Pattern = "x", MinMatches = 1, MaxMatches = 2 };

			return new()
			{
				{ presence, presence with { JsonPath = "$.b" } },
				{ presence, presence with { PresentScore = 0.5d } },
				{ presence, presence with { AbsentScore = 0.5d } },
				{ presence, presence with { ExplanationTemplate = "t" } },
				{ regex, regex with { Pattern = "y" } },
				{ regex, regex with { SuccessScore = 0.5d } },
				{ regex, regex with { FailureScore = 0.5d } },
				{ length, length with { MinimumLength = 0 } },
				{ length, length with { MaximumLength = 3 } },
				{ words, words with { MinimumWordCount = 0 } },
				{ words, words with { MaximumWordCount = 3 } },
				{ numeric, numeric with { MinValue = null } },
				{ numeric, numeric with { MaxValue = 2.5d } },
				{ date, date with { MinDate = null } },
				{ date, date with { MaxDate = Date.AddDays(2) } },
				{ date, date with { DateFormat = string.Empty } },
				{ enumeration, enumeration with { AllowedValues = ["x", "y"] } },
				{ pattern, pattern with { MinMatches = null } },
				{ pattern, pattern with { MaxMatches = 3 } },
				{ pattern, pattern with { Pattern = "y" } },
				{ presence, regex },
			};
		}
	}

	[Theory]
	[MemberData(nameof(DifferingPairs))]
	public void Generate_DefinitionsDifferingInOneSetting_ProduceDifferentKeys(IAssessmentDefinition first, IAssessmentDefinition second)
	{
		Key(first).Should().NotBe(Key(second));
	}

	[Fact]
	public void Generate_EqualConfiguration_ProducesEqualKeys()
	{
		Key(new RegexAssessment { JsonPath = "$.a", Pattern = "x", SuccessScore = 1d, FailureScore = 0d })
			.Should().Be(Key(new RegexAssessment { JsonPath = "$.a", Pattern = "x", SuccessScore = 1d, FailureScore = 0d }));
	}

	[Fact]
	public void Generate_EnumValuesInDifferentOrder_ProduceEqualKeys()
	{
		Key(new EnumAssessment { JsonPath = "$.a", AllowedValues = ["x", "y", "z"] })
			.Should().Be(Key(new EnumAssessment { JsonPath = "$.a", AllowedValues = ["z", "x", "y"] }));
	}

	[Fact]
	public void Generate_DifferentIssueContent_ProducesDifferentKeys()
	{
		AssessmentOptions options = Options();

		Key(options).Should().NotBe(Key(options, """{"title":"y"}"""));
	}

	[Fact]
	public void Generate_KeyIsTwoUppercaseSha256HexHashes()
	{
		Key(Options()).Should().MatchRegex("^[0-9A-F]{64}:[0-9A-F]{64}$");
	}

	[Fact]
	public void Generate_DifferentCallbacks_ProduceDifferentKeys()
	{
		CustomCallbackAssessment first = new() { Callback = (_, _, _) => Task.FromResult<(double?, string, Exception?)>((1d, "a", null)) };
		CustomCallbackAssessment second = new() { Callback = (_, _, _) => Task.FromResult<(double?, string, Exception?)>((0d, "b", null)) };

		Key(first).Should().NotBe(Key(second));
		Key(first).Should().Be(Key(first), "the same callback instance always produces the same key");
	}

	[Fact]
	public void Generate_DifferentEvaluators_ProduceDifferentKeys()
	{
		JsonPathSelectorAssessment first = new() { JsonPath = "$.a", Evaluator = (_, _) => Task.FromResult(true) };
		JsonPathSelectorAssessment second = first with { Evaluator = (_, _) => Task.FromResult(false) };

		Key(first).Should().NotBe(Key(second));
		Key(first).Should().Be(Key(first));
	}

	[Fact]
	public void Generate_CustomDefinitionTypes_AreKeyedPerInstance()
	{
		StubDefinition first = new();
		StubDefinition second = new();

		Key(first).Should().NotBe(Key(second));
		Key(first).Should().Be(Key(first));
	}

	[Fact]
	public void Generate_GateConditionPresenceAndIdentity_ChangeTheKey()
	{
		AssessmentOptions plain = Options();
		AssessmentOptions gated = plain with { Categories = [plain.Categories[0] with { SeriesGateCondition = new RecordingGate(shouldContinue: true) }] };
		AssessmentOptions otherGate = plain with { Categories = [plain.Categories[0] with { SeriesGateCondition = new RecordingGate(shouldContinue: true) }] };

		Key(plain).Should().NotBe(Key(gated));
		Key(gated).Should().NotBe(Key(otherGate));
		Key(gated).Should().Be(Key(gated));
	}

	[Fact]
	public void Generate_OptionAndCategorySettings_ChangeTheKey()
	{
		AssessmentOptions baseline = Options();
		AssessmentCategory category = baseline.Categories[0];
		Assessment assessment = category.Assessments[0];

		List<AssessmentOptions> variants =
		[
			baseline with { ExecutionStrategy = CategoryExecutionStrategy.Parallel },
			baseline with { FailureHandling = FailureHandlingStrategy.FailFast },
			baseline with { GlobalTimeout = TimeSpan.FromSeconds(1) },
			baseline with { CaptureVirtualDocument = true },
			baseline with { Categories = [category with { Name = "Other" }] },
			baseline with { Categories = [category with { AssessmentExecutionStrategy = AssessmentExecutionStrategy.Parallel }] },
			baseline with { Categories = [category with { CategoryFailureHandling = FailureHandlingStrategy.FailFast }] },
			baseline with { Categories = [category with { CategoryTimeout = TimeSpan.FromSeconds(1) }] },
			baseline with { Categories = [category with { Assessments = [assessment with { Name = "Other" }] }] },
			baseline with { Categories = [category with { Assessments = [assessment with { Weight = 2d }] }] },
			baseline with { Categories = [category with { Assessments = [assessment with { AssessmentTimeout = TimeSpan.FromSeconds(1) }] }] },
			baseline with { Categories = [category with { Assessments = [assessment with { AllowVirtualExtension = true, VirtualOutputName = "v" }] }] },
		];

		string baselineKey = Key(baseline);
		variants.Select(variant => Key(variant)).Should().OnlyHaveUniqueItems().And.NotContain(baselineKey);
	}

	[Theory]
	[InlineData("de-DE")]
	[InlineData("fr-FR")]
	[InlineData("ar-SA")]
	public void Generate_UnderAnyCulture_ProducesTheInvariantKey(string culture)
	{
		AssessmentOptions options = Options(
			new NumericRangeAssessment { JsonPath = "$.a", MinValue = 1.5d, MaxValue = 2.25d, SuccessScore = 0.75d },
			new DateRangeAssessment { JsonPath = "$.b", MinDate = Date });
		options = options with { Categories = [options.Categories[0] with { Assessments = [.. options.Categories[0].Assessments.Select(a => a with { Weight = 0.5d })] }] };
		string invariantKey;
		using (AssessmentTestHelpers.UseCulture("en-US"))
		{
			invariantKey = Key(options);
		}

		using (AssessmentTestHelpers.UseCulture(culture))
		{
			Key(options).Should().Be(invariantKey);
		}
	}

	[Fact]
	public void Generate_NullArguments_Throw()
	{
		using JsonDocument issue = JsonDocument.Parse("{}");

		Action nullIssue = () => CacheKeyGenerator.Generate(null!, Options());
		Action nullOptions = () => CacheKeyGenerator.Generate(issue, null!);

		nullIssue.Should().Throw<ArgumentNullException>();
		nullOptions.Should().Throw<ArgumentNullException>();
	}
}
