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
	public async Task AssessAsync_WhenExecutionNotImplemented_ThrowsNotImplementedException()
	{
		IssueAssessor assessor = new(CreateValidOptions());
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"Example\"}");

		await Assert.ThrowsAsync<NotImplementedException>(
			() => assessor.AssessAsync(issue, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task AssessManyAsync_WhenExecutionNotImplemented_ThrowsNotImplementedException()
	{
		IssueAssessor assessor = new(CreateValidOptions());
		using JsonDocument issue = JsonDocument.Parse("{\"title\":\"Example\"}");

		IAsyncEnumerable<JsonDocument> Issues()
		{
			return CreateIssues();

			async IAsyncEnumerable<JsonDocument> CreateIssues()
			{
				yield return issue;
				await Task.CompletedTask;
			}
		}

		IAsyncEnumerator<IAssessmentResult> enumerator = assessor
			.AssessManyAsync(Issues(), TestContext.Current.CancellationToken)
			.GetAsyncEnumerator(TestContext.Current.CancellationToken);
		await Assert.ThrowsAsync<NotImplementedException>(async () => await enumerator.MoveNextAsync());
		await enumerator.DisposeAsync();
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
