using PanoramicData.IssueAssessment.Validation;
using static PanoramicData.IssueAssessment.Tests.Validation.ValidationTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Validation;

/// <summary>
/// Options- and category-level configuration errors are rejected when <see cref="IssueAssessor"/> is constructed.
/// </summary>
public class OptionsValidationTests
{
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
	public void Validate_CalledDirectly_BehavesLikeTheConstructor()
	{
		Action valid = () => OptionsValidator.Validate(Options());
		Action invalid = () => OptionsValidator.Validate(Options() with { Categories = [] });

		valid.Should().NotThrow();
		invalid.Should().Throw<ArgumentException>();
	}
}
