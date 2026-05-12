namespace PanoramicData.IssueAssessment.Tests;

/// <summary>
/// Tests for built-in assessment definition implementations and related validation.
/// </summary>
public class AssessmentDefinitionsTests
{
	[Fact]
	public async Task PresenceAssessment_WhenValueExists_ReturnsPresentScore()
	{
		PresenceAssessment assessment = new()
		{
			JsonPath = "$.title",
			PresentScore = 0.9d,
			AbsentScore = 0.1d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"title\":\"Issue\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.9d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task PresenceAssessment_WhenValueMissing_ReturnsAbsentScore()
	{
		PresenceAssessment assessment = new()
		{
			JsonPath = "$.title",
			PresentScore = 1d,
			AbsentScore = 0.25d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"summary\":\"Issue\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.25d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task RegexAssessment_WhenPatternMatches_ReturnsSuccessScore()
	{
		RegexAssessment assessment = new()
		{
			JsonPath = "$.title",
			Pattern = "^BUG-\\d+$",
			SuccessScore = 1d,
			FailureScore = 0d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"title\":\"BUG-123\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(1d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task RegexAssessment_WhenPathMissing_ReturnsFailureScoreAndReason()
	{
		RegexAssessment assessment = new()
		{
			JsonPath = "$.title",
			Pattern = "^BUG-\\d+$",
			SuccessScore = 1d,
			FailureScore = 0.2d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"summary\":\"BUG-123\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task StringLengthAssessment_WhenLengthWithinRange_ReturnsSuccessScore()
	{
		StringLengthAssessment assessment = new()
		{
			JsonPath = "$.title",
			MinimumLength = 3,
			MaximumLength = 10,
			SuccessScore = 0.8d,
			FailureScore = 0.1d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"title\":\"Issue\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.8d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task StringLengthAssessment_WhenPathMissing_ReturnsFailureScoreAndReason()
	{
		StringLengthAssessment assessment = new()
		{
			JsonPath = "$.title",
			MinimumLength = 3,
			MaximumLength = 10,
			SuccessScore = 1d,
			FailureScore = 0.2d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"summary\":\"Issue\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Fact]
	public async Task WordCountAssessment_WhenCountWithinRange_ReturnsSuccessScore()
	{
		WordCountAssessment assessment = new()
		{
			JsonPath = "$.description",
			MinimumWordCount = 3,
			MaximumWordCount = 6,
			SuccessScore = 0.7d,
			FailureScore = 0.15d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"description\":\"one two three four\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.7d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task WordCountAssessment_WhenCountOutsideRange_ReturnsFailureScore()
	{
		WordCountAssessment assessment = new()
		{
			JsonPath = "$.description",
			MinimumWordCount = 5,
			MaximumWordCount = 8,
			SuccessScore = 1d,
			FailureScore = 0.3d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"description\":\"one two three\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.3d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public void Constructor_WithInvalidRegexDefinition_ThrowsArgumentException()
	{
		RegexAssessment definition = new()
		{
			JsonPath = "$.title",
			Pattern = "(",
			SuccessScore = 1d,
			FailureScore = 0d,
		};

		Assert.Throws<ArgumentException>(() => new IssueAssessor(CreateOptions(definition)));
	}

	[Fact]
	public void Constructor_WithInvalidJsonPathDefinition_ThrowsArgumentException()
	{
		PresenceAssessment definition = new()
		{
			JsonPath = "title",
		};

		Assert.Throws<ArgumentException>(() => new IssueAssessor(CreateOptions(definition)));
	}

	private static AssessmentOptions CreateOptions(IAssessmentDefinition definition)
	{
		return new AssessmentOptions
		{
			Categories = ImmutableList.Create(
				new AssessmentCategory
				{
					Name = "Category",
					Description = "Category description",
					AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series,
					Assessments = ImmutableList.Create(
						new Assessment
						{
							Name = "Assessment",
							Description = "Assessment description",
							Definition = definition,
						}),
				}),
			ExecutionStrategy = CategoryExecutionStrategy.Series,
		};
	}
}
