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
	public async Task NumericRangeAssessment_WhenNumberWithinRange_ReturnsSuccessScore()
	{
		NumericRangeAssessment assessment = new()
		{
			JsonPath = "$.storyPoints",
			MinValue = 3d,
			MaxValue = 8d,
			SuccessScore = 0.85d,
			FailureScore = 0.2d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"storyPoints\":5}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.85d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task NumericRangeAssessment_WhenValueOutsideRange_ReturnsFailureScore()
	{
		NumericRangeAssessment assessment = new()
		{
			JsonPath = "$.storyPoints",
			MinValue = 3d,
			MaxValue = 8d,
			SuccessScore = 1d,
			FailureScore = 0.25d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"storyPoints\":10}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.25d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task EnumAssessment_WhenValueAllowed_ReturnsSuccessScore()
	{
		EnumAssessment assessment = new()
		{
			JsonPath = "$.status",
			AllowedValues = ImmutableHashSet.Create("Open", "In Progress", "Done"),
			SuccessScore = 0.95d,
			FailureScore = 0.1d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"status\":\"In Progress\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.95d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task EnumAssessment_WhenValueNotAllowed_ReturnsFailureScore()
	{
		EnumAssessment assessment = new()
		{
			JsonPath = "$.status",
			AllowedValues = ImmutableHashSet.Create("Open", "In Progress", "Done"),
			SuccessScore = 1d,
			FailureScore = 0.2d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"status\":\"Blocked\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.2d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task DateRangeAssessment_WhenDateWithinRange_ReturnsSuccessScore()
	{
		DateRangeAssessment assessment = new()
		{
			JsonPath = "$.dueDate",
			MinDate = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
			MaxDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
			DateFormat = "O",
			SuccessScore = 0.9d,
			FailureScore = 0.2d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"dueDate\":\"2026-05-12T00:00:00.0000000Z\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.9d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task PatternCountAssessment_WhenCountWithinRange_ReturnsSuccessScore()
	{
		PatternCountAssessment assessment = new()
		{
			JsonPath = "$.description",
			Pattern = "BUG-\\d+",
			MinMatches = 1,
			MaxMatches = 2,
			SuccessScore = 0.8d,
			FailureScore = 0.1d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"description\":\"Related BUG-123 was fixed\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.8d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task JsonPathSelectorAssessment_WhenEvaluatorReturnsTrue_ReturnsSuccessScore()
	{
		JsonPathSelectorAssessment assessment = new()
		{
			JsonPath = "$.priority",
			Evaluator = static (value, _) => Task.FromResult(value.ValueKind == JsonValueKind.String && value.GetString() == "High"),
			SuccessScore = 0.88d,
			FailureScore = 0.12d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"priority\":\"High\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.88d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task JsonPathSelectorAssessment_WhenEvaluatorThrows_ReturnsFailureReason()
	{
		JsonPathSelectorAssessment assessment = new()
		{
			JsonPath = "$.priority",
			Evaluator = static (_, _) => throw new InvalidOperationException("bad evaluator"),
			SuccessScore = 1d,
			FailureScore = 0.25d,
		};
		using JsonDocument document = JsonDocument.Parse("{\"priority\":\"High\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.25d);
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task CustomCallbackAssessment_WhenCallbackReturnsScore_UsesCallbackResult()
	{
		CustomCallbackAssessment assessment = new()
		{
			Callback = static (_, _, _) => Task.FromResult<(double?, string, Exception?)>((0.77d, "custom explanation", null)),
		};
		using JsonDocument document = JsonDocument.Parse("{\"title\":\"Issue\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().Be(0.77d);
		result.ExplanationText.Should().Be("custom explanation");
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task CustomCallbackAssessment_WhenCallbackThrows_ReturnsFailureReason()
	{
		CustomCallbackAssessment assessment = new()
		{
			Callback = static (_, _, _) => throw new InvalidOperationException("boom"),
		};
		using JsonDocument document = JsonDocument.Parse("{\"title\":\"Issue\"}");

		var result = await assessment.ExecuteAsync(document, null, TestContext.Current.CancellationToken);

		result.RawScore.Should().BeNull();
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<InvalidOperationException>();
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

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
	}

	[Fact]
	public void Constructor_WithInvalidJsonPathDefinition_ThrowsArgumentException()
	{
		PresenceAssessment definition = new()
		{
			JsonPath = "title",
		};

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
	}

	[Fact]
	public void Constructor_WithInvalidStringLengthRange_ThrowsArgumentException()
	{
		StringLengthAssessment definition = new()
		{
			JsonPath = "$.title",
			MinimumLength = 10,
			MaximumLength = 3,
		};

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
	}

	[Fact]
	public void Constructor_WithInvalidNumericRange_ThrowsArgumentException()
	{
		NumericRangeAssessment definition = new()
		{
			JsonPath = "$.storyPoints",
			MinValue = 9d,
			MaxValue = 2d,
		};

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
	}

	[Fact]
	public void Constructor_WithEmptyEnumAllowedValues_ThrowsArgumentException()
	{
		EnumAssessment definition = new()
		{
			JsonPath = "$.status",
			AllowedValues = ImmutableHashSet<string>.Empty,
		};

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
	}

	[Fact]
	public void Constructor_WithInvalidDateRange_ThrowsArgumentException()
	{
		DateRangeAssessment definition = new()
		{
			JsonPath = "$.dueDate",
			MinDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
			MaxDate = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
		};

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
	}

	[Fact]
	public void Constructor_WithInvalidPatternCountRange_ThrowsArgumentException()
	{
		PatternCountAssessment definition = new()
		{
			JsonPath = "$.description",
			Pattern = "BUG-\\d+",
			MinMatches = 3,
			MaxMatches = 1,
		};

		Assert.Throws<ArgumentException>(() =>
		{
			_ = new IssueAssessor(CreateOptions(definition));
		});
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
