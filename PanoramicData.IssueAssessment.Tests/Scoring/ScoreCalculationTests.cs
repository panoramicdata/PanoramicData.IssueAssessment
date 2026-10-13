using PanoramicData.IssueAssessment.Scoring;
using PanoramicData.IssueAssessment.Tests.Integration;

namespace PanoramicData.IssueAssessment.Tests.Scoring;

/// <summary>
/// Tests for <see cref="ScoreCalculator"/>.
/// </summary>
public class ScoreCalculationTests
{
	private static Assessment With(IAssessmentDefinition definition, double weight = 1d)
		=> new() { Name = "A", Description = "A", Definition = definition, Weight = weight };

	public static TheoryData<IAssessmentDefinition, double, double> DefinitionRanges => new()
	{
		{ new PresenceAssessment { JsonPath = "$.a", PresentScore = 3d, AbsentScore = -1d }, -1d, 3d },
		{ new RegexAssessment { JsonPath = "$.a", Pattern = "x", SuccessScore = 2d, FailureScore = 0.5d }, 0.5d, 2d },
		{ new StringLengthAssessment { JsonPath = "$.a", SuccessScore = 0d, FailureScore = 4d }, 0d, 4d },
		{ new WordCountAssessment { JsonPath = "$.a", SuccessScore = 5d, FailureScore = 1d }, 1d, 5d },
		{ new NumericRangeAssessment { JsonPath = "$.a", SuccessScore = 6d, FailureScore = 2d }, 2d, 6d },
		{ new DateRangeAssessment { JsonPath = "$.a", SuccessScore = 7d, FailureScore = 3d }, 3d, 7d },
		{ new EnumAssessment { JsonPath = "$.a", AllowedValues = ["x"], SuccessScore = 8d, FailureScore = 4d }, 4d, 8d },
		{ new PatternCountAssessment { JsonPath = "$.a", Pattern = "x", SuccessScore = 9d, FailureScore = 5d }, 5d, 9d },
		{ new JsonPathSelectorAssessment { JsonPath = "$.a", Evaluator = (_, _) => Task.FromResult(true), SuccessScore = 10d, FailureScore = 6d }, 6d, 10d },
		{ new CustomCallbackAssessment { Callback = (_, _, _) => Task.FromResult<(double?, string, Exception?)>((1d, "", null)) }, 0d, 1d },
		{ new StubDefinition(), 0d, 1d },
	};

	[Theory]
	[MemberData(nameof(DefinitionRanges))]
	public void CalculateAssessmentRawRange_EveryDefinitionType_ReturnsOrderedScoreBounds(IAssessmentDefinition definition, double minimum, double maximum)
	{
		ScoreCalculator.CalculateAssessmentRawRange(With(definition)).Should().Be((minimum, maximum));
	}

	[Fact]
	public void CalculateAssessmentRawRange_NullAssessment_Throws()
	{
		Action act = () => ScoreCalculator.CalculateAssessmentRawRange(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void CalculateCategoryRawRange_NegativeWeight_SwapsBounds()
	{
		AssessmentCategory category = new()
		{
			Name = "C",
			Description = "C",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments =
			[
				With(new PresenceAssessment { JsonPath = "$.a" }, weight: 2d),
				With(new PresenceAssessment { JsonPath = "$.b" }, weight: -1d),
				With(new PresenceAssessment { JsonPath = "$.c" }, weight: 0d),
			],
		};

		ScoreCalculator.CalculateCategoryRawRange(category).Should().Be((-1d, 2d));
	}

	[Fact]
	public void CalculateCategoryRawRange_NullCategory_Throws()
	{
		Action act = () => ScoreCalculator.CalculateCategoryRawRange((AssessmentCategory)null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Theory]
	[InlineData(0d, -1d, 3d, 0.25d)]
	[InlineData(-5d, -1d, 3d, 0d)]
	[InlineData(10d, -1d, 3d, 1d)]
	[InlineData(3d, 3d, 3d, 1d)]
	public void Normalize_MapsIntoUnitIntervalAndClamps(double raw, double minimum, double maximum, double expected)
	{
		ScoreCalculator.Normalize(raw, minimum, maximum).Should().Be(expected);
	}

	[Fact]
	public void Normalize_MinimumAboveMaximum_Throws()
	{
		Action act = () => ScoreCalculator.Normalize(0d, 2d, 1d);

		act.Should().Throw<ArgumentException>().WithMessage("Minimum raw score cannot be greater than maximum raw score.");
	}

	[Theory]
	[InlineData(-0.01d)]
	[InlineData(1.01d)]
	public void CalculateWeightedAverages_DecayOutsideUnitInterval_Throws(double decay)
	{
		Action act = () => ScoreCalculator.CalculateWeightedAverages([(1d, 1d)], decay);

		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("exponentialDecay");
	}

	[Fact]
	public void CalculateWeightedAverages_NullValues_Throws()
	{
		Action act = () => ScoreCalculator.CalculateWeightedAverages(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void CalculateWeightedAverages_SkipsNullScoresAndZeroWeights()
	{
		(double? simple, double? exponential, double weightSum) = ScoreCalculator.CalculateWeightedAverages(
			[(1d, 1d), (null, 5d), (0d, 0d), (0.5d, 1d)],
			exponentialDecay: 1d);

		simple.Should().Be(0.75d);
		exponential.Should().Be(0.75d, "a decay of 1 weights every entry equally");
		weightSum.Should().Be(2d);
	}

	[Fact]
	public void CalculateWeightedAverages_WeightsThatCancelOut_ReturnNullAverages()
	{
		(double? simple, double? exponential, double weightSum) = ScoreCalculator.CalculateWeightedAverages(
			[(1d, 1d), (0d, -1d)],
			exponentialDecay: 1d);

		simple.Should().BeNull("the weights sum to zero, so there is no meaningful average");
		exponential.Should().BeNull();
		weightSum.Should().Be(2d, "the weight sum reports absolute weights");
	}

	[Fact]
	public void CalculateWeightedAverages_ZeroDecay_UsesOnlyTheLastEntry()
	{
		(_, double? exponential, _) = ScoreCalculator.CalculateWeightedAverages([(0d, 1d), (0.2d, 1d), (0.9d, 1d)], exponentialDecay: 0d);

		exponential.Should().Be(0.9d);
	}
}
