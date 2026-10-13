using PanoramicData.IssueAssessment.Validation;

namespace PanoramicData.IssueAssessment.Tests.Validation;

/// <summary>
/// Every configuration error is rejected when <see cref="IssueAssessor"/> is constructed, with a message that names the
/// offending category and assessment.
/// </summary>
public class OptionsValidationTests
{
	private static readonly IAssessmentDefinition Valid = new PresenceAssessment { JsonPath = "$.title" };

	private static Assessment Assessment(IAssessmentDefinition? definition = null)
		=> new() { Name = "Check", Description = "Check", Definition = definition ?? Valid };

	private static AssessmentCategory Category(params Assessment[] assessments) => new()
	{
		Name = "Quality",
		Description = "Quality",
		AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
		Assessments = assessments.Length == 0 ? [Assessment()] : [.. assessments],
	};

	private static AssessmentOptions Options(params AssessmentCategory[] categories) => new()
	{
		Categories = categories.Length == 0 ? [Category()] : [.. categories],
		ExecutionStrategy = CategoryExecutionStrategy.Series,
	};

	private static Action Construct(AssessmentOptions options) => () => _ = new IssueAssessor(options);

	private static Action Construct(Assessment assessment) => Construct(Options(Category(assessment)));

	[Fact]
	public void Valid_Options_AreAccepted()
	{
		Construct(Options()).Should().NotThrow();
	}

	[Fact]
	public void NullOptions_Throw()
	{
		Action act = () => _ = new IssueAssessor(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void NoCategories_Throw()
	{
		Construct(Options() with { Categories = [] }).Should().Throw<ArgumentException>()
			.WithMessage("At least one assessment category is required.*");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void NonPositiveGlobalTimeout_Throws(int milliseconds)
	{
		Construct(Options() with { GlobalTimeout = TimeSpan.FromMilliseconds(milliseconds) }).Should().Throw<ArgumentException>()
			.WithMessage("GlobalTimeout must be greater than zero.*");
	}

	[Fact]
	public void PositiveTimeouts_AreAccepted()
	{
		AssessmentOptions options = Options(Category(Assessment() with { AssessmentTimeout = TimeSpan.FromSeconds(1) }) with
		{
			CategoryTimeout = TimeSpan.FromSeconds(1),
		}) with
		{ GlobalTimeout = TimeSpan.FromSeconds(1) };

		Construct(options).Should().NotThrow();
	}

	[Fact]
	public void NullCategory_Throws()
	{
		Construct(Options() with { Categories = [null!] }).Should().Throw<ArgumentNullException>();
	}

	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	public void BlankCategoryName_Throws(string name)
	{
		Construct(Options(Category() with { Name = name })).Should().Throw<ArgumentException>()
			.WithMessage("Category name is required.*");
	}

	[Fact]
	public void BlankCategoryDescription_Throws()
	{
		Construct(Options(Category() with { Description = " " })).Should().Throw<ArgumentException>()
			.WithMessage("Category 'Quality' description is required.*");
	}

	[Fact]
	public void CategoryWithoutAssessments_Throws()
	{
		Construct(Options(Category() with { Assessments = [] })).Should().Throw<ArgumentException>()
			.WithMessage("Category 'Quality' must include at least one assessment.*");
	}

	[Fact]
	public void NonPositiveCategoryTimeout_Throws()
	{
		Construct(Options(Category() with { CategoryTimeout = TimeSpan.Zero })).Should().Throw<ArgumentException>()
			.WithMessage("Category 'Quality' timeout must be greater than zero.*");
	}

	[Fact]
	public void VirtualExtensionInParallelCategory_Throws()
	{
		AssessmentCategory category = Category(Assessment() with { AllowVirtualExtension = true, VirtualOutputName = "v" }) with
		{
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Parallel,
		};

		Construct(Options(category)).Should().Throw<InvalidOperationException>()
			.WithMessage("Category 'Quality' cannot allow virtual extension when assessments execute in parallel.");
	}

	[Fact]
	public void DuplicateCategoryNames_Throw()
	{
		Construct(Options(Category(), Category())).Should().Throw<ArgumentException>()
			.WithMessage("Category name 'Quality' is used more than once.*");
	}

	[Fact]
	public void DuplicateVirtualOutputNames_Throw()
	{
		Assessment producer = Assessment() with { AllowVirtualExtension = true, VirtualOutputName = "summary" };

		Construct(Options(Category(producer), Category(producer) with { Name = "Other" })).Should().Throw<ArgumentException>()
			.WithMessage("VirtualOutputName 'summary' is used by more than one assessment.*");
	}

	[Fact]
	public void SameVirtualOutputNameOnAssessmentsThatDoNotExtend_IsAccepted()
	{
		Assessment named = Assessment() with { VirtualOutputName = "unused" };

		Construct(Options(Category(named, named with { Name = "Second" }))).Should().NotThrow();
	}

	[Fact]
	public void NullAssessment_Throws()
	{
		Construct(Options(Category() with { Assessments = [null!] })).Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void BlankAssessmentName_Throws()
	{
		Construct(Assessment() with { Name = "" }).Should().Throw<ArgumentException>()
			.WithMessage("Category 'Quality' contains an assessment with no name.*");
	}

	[Fact]
	public void BlankAssessmentDescription_Throws()
	{
		Construct(Assessment() with { Description = "\t" }).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' must include a description.*");
	}

	[Fact]
	public void NullDefinition_Throws()
	{
		Construct(Assessment() with { Definition = null! }).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' must include a definition.*");
	}

	[Fact]
	public void InvalidJsonPath_ThrowsWithInnerException()
	{
		Construct(Assessment(new PresenceAssessment { JsonPath = "title" })).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' has invalid JSONPath 'title'.*")
			.WithInnerException<InvalidJsonPathException>();
	}

	[Fact]
	public void InvalidRegex_ThrowsWithInnerException()
	{
		Construct(Assessment(new RegexAssessment { JsonPath = "$.a", Pattern = "(", SuccessScore = 1d, FailureScore = 0d }))
			.Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' has invalid regex pattern '('.*")
			.WithInnerException<ArgumentException>();
	}

	[Theory]
	[InlineData(-100d)]
	[InlineData(0d)]
	[InlineData(100d)]
	public void WeightWithinBounds_IsAccepted(double weight)
	{
		Construct(Assessment() with { Weight = weight }).Should().NotThrow();
	}

	[Theory]
	[InlineData(-100.01d)]
	[InlineData(100.01d)]
	public void WeightOutsideBounds_Throws(double weight)
	{
		Construct(Assessment() with { Weight = weight }).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' has invalid weight *. Weight must be between -100 and 100.*");
	}

	[Fact]
	public void NonPositiveAssessmentTimeout_Throws()
	{
		Construct(Assessment() with { AssessmentTimeout = TimeSpan.FromTicks(-1) }).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' timeout must be greater than zero.*");
	}

	[Theory]
	[InlineData(null)]
	[InlineData(" ")]
	public void VirtualExtensionWithoutOutputName_Throws(string? name)
	{
		Construct(Assessment() with { AllowVirtualExtension = true, VirtualOutputName = name }).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'Check' in category 'Quality' enables virtual extension but does not define VirtualOutputName.*");
	}

	public static TheoryData<IAssessmentDefinition, string> InvalidDefinitions => new()
	{
		{ new StringLengthAssessment { JsonPath = "$.a", MinimumLength = -1, MaximumLength = 5 }, "has invalid MinimumLength -1." },
		{ new StringLengthAssessment { JsonPath = "$.a", MinimumLength = 5, MaximumLength = 4 }, "has MaximumLength less than MinimumLength." },
		{ new WordCountAssessment { JsonPath = "$.a", MinimumWordCount = -1, MaximumWordCount = 5 }, "has invalid MinimumWordCount -1." },
		{ new WordCountAssessment { JsonPath = "$.a", MinimumWordCount = 5, MaximumWordCount = 4 }, "has MaximumWordCount less than MinimumWordCount." },
		{ new NumericRangeAssessment { JsonPath = "$.a", MinValue = 2d, MaxValue = 1d }, "has MinValue greater than MaxValue." },
		{ new DateRangeAssessment { JsonPath = "$.a", MinDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), MaxDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, "has MinDate greater than MaxDate." },
		{ new PatternCountAssessment { JsonPath = "$.a", Pattern = "x", MinMatches = -1 }, "has invalid MinMatches -1." },
		{ new PatternCountAssessment { JsonPath = "$.a", Pattern = "x", MinMatches = 3, MaxMatches = 2 }, "has MaxMatches less than MinMatches." },
		{ new EnumAssessment { JsonPath = "$.a", AllowedValues = [] }, "must define at least one allowed value." },
		{ new JsonPathSelectorAssessment { JsonPath = "$.a", Evaluator = null! }, "must define an evaluator." },
		{ new CustomCallbackAssessment { Callback = null! }, "must define a callback." },
	};

	[Theory]
	[MemberData(nameof(InvalidDefinitions))]
	public void InvalidDefinitionConfiguration_Throws(IAssessmentDefinition definition, string reason)
	{
		Construct(Assessment(definition)).Should().Throw<ArgumentException>()
			.WithMessage($"Assessment 'Check' in category 'Quality' {reason}*");
	}

	public static TheoryData<IAssessmentDefinition> ValidDefinitions => new()
	{
		new StringLengthAssessment { JsonPath = "$.a", MinimumLength = 0, MaximumLength = 0 },
		new WordCountAssessment { JsonPath = "$.a", MinimumWordCount = 2, MaximumWordCount = 2 },
		new NumericRangeAssessment { JsonPath = "$.a", MinValue = 1d, MaxValue = 1d },
		new NumericRangeAssessment { JsonPath = "$.a", MinValue = 1d },
		new NumericRangeAssessment { JsonPath = "$.a", MaxValue = 1d },
		new DateRangeAssessment { JsonPath = "$.a", MinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
		new DateRangeAssessment { JsonPath = "$.a", MaxDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
		new DateRangeAssessment { JsonPath = "$.a", MinDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), MaxDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
		new PatternCountAssessment { JsonPath = "$.a", Pattern = "x" },
		new PatternCountAssessment { JsonPath = "$.a", Pattern = "x", MinMatches = 2, MaxMatches = 2 },
		new PatternCountAssessment { JsonPath = "$.a", Pattern = "x", MaxMatches = 0 },
		new EnumAssessment { JsonPath = "$.a", AllowedValues = ["x"] },
		new JsonPathSelectorAssessment { JsonPath = "$.a", Evaluator = (_, _) => Task.FromResult(true) },
		new CustomCallbackAssessment { Callback = (_, _, _) => Task.FromResult<(double?, string, Exception?)>((1d, "", null)) },
	};

	[Theory]
	[MemberData(nameof(ValidDefinitions))]
	public void ValidDefinitionConfiguration_IsAccepted(IAssessmentDefinition definition)
	{
		Construct(Assessment(definition)).Should().NotThrow();
	}

	[Fact]
	public void Validate_CalledDirectly_BehavesLikeTheConstructor()
	{
		Action valid = () => OptionsValidator.Validate(Options());
		Action invalid = () => OptionsValidator.Validate(Options() with { Categories = [] });

		valid.Should().NotThrow();
		invalid.Should().Throw<ArgumentException>();
	}
}
