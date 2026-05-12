namespace PanoramicData.IssueAssessment.Tests;

using PanoramicData.IssueAssessment.Scoring;

/// <summary>
/// Tests for score range and normalization calculations.
/// </summary>
public class ScoreCalculatorTests
{
	[Fact]
	public void CalculateAssessmentRawRange_WithPresenceAssessment_ReturnsExpectedBounds()
	{
		Assessment assessment = new()
		{
			Name = "Presence",
			Description = "Presence",
			Definition = new PresenceAssessment
			{
				JsonPath = "$.title",
				PresentScore = 0.9d,
				AbsentScore = 0.2d,
			},
		};

		(double min, double max) = ScoreCalculator.CalculateAssessmentRawRange(assessment);

		min.Should().Be(0.2d);
		max.Should().Be(0.9d);
	}

	[Fact]
	public void CalculateCategoryRawRange_WithMixedWeights_ReturnsExpectedBounds()
	{
		AssessmentCategory category = new()
		{
			Name = "Category",
			Description = "Category",
			AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
			Assessments = ImmutableList.Create(
				new Assessment
				{
					Name = "A1",
					Description = "A1",
					Weight = 2d,
					Definition = new RegexAssessment
					{
						JsonPath = "$.title",
						Pattern = ".+",
						SuccessScore = 1d,
						FailureScore = 0d,
					},
				},
				new Assessment
				{
					Name = "A2",
					Description = "A2",
					Weight = -1d,
					Definition = new EnumAssessment
					{
						JsonPath = "$.status",
						AllowedValues = ImmutableHashSet.Create("Open"),
						SuccessScore = 1d,
						FailureScore = 0d,
					},
				}),
		};

		(double min, double max) = ScoreCalculator.CalculateCategoryRawRange(category);

		min.Should().Be(-1d);
		max.Should().Be(2d);
	}

	[Fact]
	public void Normalize_WithCollapsedRange_ReturnsOne()
	{
		double normalized = ScoreCalculator.Normalize(rawScore: 0.4d, minPossibleRaw: 0.4d, maxPossibleRaw: 0.4d);

		normalized.Should().Be(1d);
	}

	[Fact]
	public void Normalize_WithValidRange_ReturnsClampedValue()
	{
		double normalized = ScoreCalculator.Normalize(rawScore: 7d, minPossibleRaw: 2d, maxPossibleRaw: 12d);

		normalized.Should().Be(0.5d);
	}

	[Fact]
	public void CalculateWeightedAverages_WithInputValues_ReturnsSimpleAndExponential()
	{
		(double? simple, double? exponential, double weightSum) = ScoreCalculator.CalculateWeightedAverages(
			new[]
			{
				(Score: (double?)0.2d, Weight: 1d),
				(Score: (double?)0.8d, Weight: 2d),
				(Score: (double?)1.0d, Weight: 1d),
			},
			exponentialDecay: 0.5d);

		simple.Should().BeApproximately(0.7d, 0.0001d);
		exponential.Should().NotBeNull();
		exponential!.Value.Should().BeGreaterThan(0.7d);
		weightSum.Should().Be(4d);
	}

	[Fact]
	public void CalculateWeightedAverages_WithNoScoredValues_ReturnsNullAverages()
	{
		(double? simple, double? exponential, double weightSum) = ScoreCalculator.CalculateWeightedAverages(
			new[]
			{
				(Score: (double?)null, Weight: 1d),
				(Score: (double?)null, Weight: 2d),
			});

		simple.Should().BeNull();
		exponential.Should().BeNull();
		weightSum.Should().Be(0d);
	}
}