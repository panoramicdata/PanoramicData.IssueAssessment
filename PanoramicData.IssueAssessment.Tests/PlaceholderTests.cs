namespace PanoramicData.IssueAssessment.Tests;

/// <summary>
/// Tests for IssueAssessor Phase 1 construction and validation behavior.
/// </summary>
public class IssueAssessorTests
{
	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new IssueAssessor(options: null!));
	}

	[Fact]
	public void Constructor_WithNoCategories_ThrowsArgumentException()
	{
		AssessmentOptions options = new()
		{
			Categories = ImmutableList<AssessmentCategory>.Empty,
			ExecutionStrategy = CategoryExecutionStrategy.Series,
		};

		Assert.Throws<ArgumentException>(() => new IssueAssessor(options));
	}

	[Fact]
	public void Constructor_WithParallelCategoryAndVirtualExtension_ThrowsInvalidOperationException()
	{
		AssessmentOptions options = CreateValidOptions() with
		{
			Categories = ImmutableList.Create(
				new AssessmentCategory
				{
					Name = "Category",
					Description = "Category description",
					AssessmentExecutionStrategy = AssessmentExecutionStrategy.Parallel,
					Assessments = ImmutableList.Create(
						new Assessment
						{
							Name = "Assessment",
							Description = "Assessment description",
							Definition = new TestAssessmentDefinition(),
							AllowVirtualExtension = true,
							VirtualOutputName = "value",
						}),
				}),
		};

		Assert.Throws<InvalidOperationException>(() => new IssueAssessor(options));
	}

	[Fact]
	public void Constructor_WithInvalidWeight_ThrowsArgumentException()
	{
		AssessmentOptions options = CreateValidOptions(weight: 150d);

		Assert.Throws<ArgumentException>(() => new IssueAssessor(options));
	}

	[Fact]
	public async Task AssessAsync_WithValidOptions_ReturnsAssessmentResult()
	{
		IssueAssessor assessor = new(CreateValidOptions());
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"Example\"}");

		IAssessmentResult result = await assessor.AssessAsync(issue, TestContext.Current.CancellationToken);

		result.Categories.Should().HaveCount(1);
		result.Failures.Should().BeEmpty();
	}

	[Fact]
	public async Task AssessManyAsync_WithValidOptions_ReturnsResults()
	{
		IssueAssessor assessor = new(CreateValidOptions());
		using JsonDocument issue1 = JsonDocument.Parse("{\"title\":\"Example 1\"}");
		using JsonDocument issue2 = JsonDocument.Parse("{\"title\":\"Example 2\"}");

		IAsyncEnumerable<JsonDocument> Issues()
		{
			return CreateIssues();

			async IAsyncEnumerable<JsonDocument> CreateIssues()
			{
				yield return issue1;
				yield return issue2;
				await Task.CompletedTask;
			}
		}

		List<IAssessmentResult> results = new();
		await foreach (IAssessmentResult result in assessor.AssessManyAsync(Issues(), TestContext.Current.CancellationToken))
		{
			results.Add(result);
		}

		results.Should().HaveCount(2);
		results.All(result => result.Categories.Count == 1).Should().BeTrue();
	}

	[Fact]
	public void Constructor_WithValidOptions_DoesNotThrow()
	{
		IssueAssessor assessor = new(CreateValidOptions());

		assessor.Should().NotBeNull();
	}

	private static AssessmentOptions CreateValidOptions(double weight = 1d)
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
							Definition = new TestAssessmentDefinition(),
							Weight = weight,
						}),
				}),
			ExecutionStrategy = CategoryExecutionStrategy.Series,
		};
	}

	private sealed class TestAssessmentDefinition : IAssessmentDefinition
	{
		public Task<AssessmentExecutionResult> ExecuteAsync(
			JsonDocument input,
			JsonDocument? virtualExtension,
			CancellationToken cancellationToken)
		{
			AssessmentExecutionResult result = new(
				RawScore: 1d,
				ExplanationText: "ok",
				FailureReason: null,
				InnerException: null,
				VirtualExtensionOutput: null);

			return Task.FromResult(result);
		}
	}
}
