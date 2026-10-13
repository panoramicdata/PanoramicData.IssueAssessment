namespace PanoramicData.IssueAssessment.Tests.Integration;

using PanoramicData.IssueAssessment.Execution;
using static PanoramicData.IssueAssessment.Tests.Integration.IntegrationTestHelpers;

/// <summary>
/// Tests for assessment, category and global timeouts and for caller cancellation.
/// </summary>
public class TimeoutTests : TestBase
{
	private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(50);

	[Fact]
	public async Task AssessAsync_AssessmentTimeoutExceeded_RecordsTimeoutFailureAndKeepsOtherResults()
	{
		DelayingDefinition slow = new();
		StubDefinition fast = new() { RawScore = 1d };
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory(
				"Category",
				CreateAssessment("Slow", slow, timeout: ShortTimeout),
				CreateAssessment("Fast", fast)));
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		slow.CancellationObserved.Should().BeTrue();
		result.Failures.Should().ContainSingle();
		AssessmentFailure failure = result.Failures[0];
		failure.Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		failure.CategoryName.Should().Be("Category");
		failure.AssessmentName.Should().Be("Slow");
		failure.Message.Should().Be($"Assessment exceeded timeout of {ShortTimeout}.");
		AssessmentResult slowResult = result.Categories[0].Assessments[0];
		slowResult.FailureReason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		slowResult.Score.RawScore.Should().BeNull();
		slowResult.Score.NormalizedScore.Should().BeNull();
		result.Categories[0].Assessments[1].Score.RawScore.Should().Be(1d);
		fast.ExecutionCount.Should().Be(1);
	}

	[Fact]
	public async Task AssessAsync_AssessmentCompletesWithinTimeout_ReturnsScore()
	{
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("Category", CreateAssessment("Quick", new StubDefinition { RawScore = 0.25d }, timeout: SafetyNet)));
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Failures.Should().BeEmpty();
		result.Categories[0].Assessments[0].Score.RawScore.Should().Be(0.25d);
	}

	[Theory]
	[InlineData(AssessmentExecutionStrategy.Series)]
	[InlineData(AssessmentExecutionStrategy.Parallel)]
	public async Task AssessAsync_CategoryTimeoutExceeded_RecordsTimeoutFailurePerUnfinishedAssessment(AssessmentExecutionStrategy strategy)
	{
		AssessmentCategory category = CreateCategory(
			"Timed",
			strategy,
			CreateAssessment("Slow", new DelayingDefinition()),
			CreateAssessment("Fast", new StubDefinition())) with
		{
			CategoryTimeout = ShortTimeout,
		};
		AssessmentOptions options = CreateOptions(CategoryExecutionStrategy.Series, category);
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories.Should().ContainSingle();
		result.Categories[0].Assessments.Should().HaveCount(2);
		result.Failures.Should().ContainSingle();
		result.Failures[0].AssessmentName.Should().Be("Slow");
		result.Failures[0].Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		result.Failures[0].Message.Should().Be("Assessment execution was canceled due to timeout.");
		result.Categories[0].Score.RawScore.Should().BeNull("an assessment without a raw score leaves the category unscored");
	}

	[Fact]
	public async Task AssessAsync_CategoryTimeoutNotExceeded_ReturnsFullResult()
	{
		AssessmentCategory category = CreateSeriesCategory("Timed", CreateAssessment("Fast", new StubDefinition())) with
		{
			CategoryTimeout = SafetyNet,
		};
		AssessmentOptions options = CreateOptions(CategoryExecutionStrategy.Series, category);
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Failures.Should().BeEmpty();
		result.Categories[0].Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task AssessAsync_CategoryTimeoutExceeded_LaterCategoriesStillRun()
	{
		StubDefinition later = new();
		AssessmentCategory timed = CreateSeriesCategory("Timed", CreateAssessment("Slow", new DelayingDefinition())) with
		{
			CategoryTimeout = ShortTimeout,
		};
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			timed,
			CreateSeriesCategory("Later", CreateAssessment("Later", later)));
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories.Select(category => category.CategoryName).Should().Equal("Timed", "Later");
		result.Failures.Should().ContainSingle().Which.CategoryName.Should().Be("Timed");
		later.ExecutionCount.Should().Be(1);
	}

	[Theory]
	[InlineData(CategoryExecutionStrategy.Series)]
	[InlineData(CategoryExecutionStrategy.Parallel)]
	public async Task AssessAsync_GlobalTimeoutExceeded_ReturnsPartialResult(CategoryExecutionStrategy strategy)
	{
		AssessmentOptions options = CreateOptions(
			strategy,
			CreateSeriesCategory("Slow", CreateAssessment("Slow", new DelayingDefinition())),
			CreateSeriesCategory("Fast", CreateAssessment("Fast", new StubDefinition { RawScore = 1d }))) with
		{
			GlobalTimeout = ShortTimeout,
		};
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories.Should().HaveCount(2);
		result.Failures.Should().ContainSingle();
		result.Failures[0].AssessmentName.Should().Be("Slow");
		result.Failures[0].Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		result.Categories[1].Score.NormalizedScore.Should().Be(1d);
	}

	[Fact]
	public async Task AssessAsync_GlobalTimeoutExpiresWhileGateWaits_ReturnsPartialResultWithGateTimeoutFailure()
	{
		StubDefinition next = new();
		AssessmentCategory first = CreateSeriesCategory("First", CreateAssessment("Quick", new StubDefinition())) with
		{
			SeriesGateCondition = new CancellationObservingGate(),
		};
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			first,
			CreateSeriesCategory("Next", CreateAssessment("Next", next))) with
		{
			GlobalTimeout = ShortTimeout,
		};
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories.Should().ContainSingle().Which.CategoryName.Should().Be("First");
		AssessmentFailure failure = result.Failures.Should().ContainSingle().Subject;
		failure.Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		failure.CategoryName.Should().Be("First");
		failure.AssessmentName.Should().Be(nameof(AssessmentCategory.SeriesGateCondition));
		failure.Message.Should().Be("Series gate condition of category 'First' was canceled due to timeout.");
		next.ExecutionCount.Should().Be(0);
	}

	[Fact]
	public async Task AssessAsync_GlobalTimeoutExpiredBeforeGate_RecordsGateTimeoutFailure()
	{
		AssessmentCategory first = CreateSeriesCategory("First", CreateAssessment("Slow", new DelayingDefinition())) with
		{
			SeriesGateCondition = new CancellationObservingGate(),
		};
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			first,
			CreateSeriesCategory("Next", CreateAssessment("Next", new StubDefinition()))) with
		{
			GlobalTimeout = ShortTimeout,
		};
		using JsonDocument issue = ParseIssue();

		IAssessmentResult result = await new IssueAssessor(options).AssessAsync(issue, CancellationToken);

		result.Categories.Should().ContainSingle();
		result.Failures.Select(failure => failure.AssessmentName).Should().Equal("Slow", nameof(AssessmentCategory.SeriesGateCondition));
		result.Failures.Should().OnlyContain(failure => failure.Reason == AssessmentFailureReason.TimeoutExceeded);
	}

	[Fact]
	public async Task AssessAsync_CallerCancelsDuringGate_ThrowsOperationCanceledException()
	{
		using CancellationTokenSource callerCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		AssessmentCategory first = CreateSeriesCategory("First", CreateAssessment("Quick", new StubDefinition())) with
		{
			SeriesGateCondition = new CancellationObservingGate(),
		};
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			first,
			CreateSeriesCategory("Next", CreateAssessment("Next", new StubDefinition())));
		using JsonDocument issue = ParseIssue();
		callerCts.CancelAfter(ShortTimeout);

		Func<Task> act = () => new IssueAssessor(options).AssessAsync(issue, callerCts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task AssessAsync_CallerCancelsDuringAssessment_ThrowsOperationCanceledException()
	{
		using CancellationTokenSource callerCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		DelayingDefinition slow = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("Category", CreateAssessment("Slow", slow, timeout: SafetyNet)));
		using JsonDocument issue = ParseIssue();
		callerCts.CancelAfter(ShortTimeout);

		Func<Task> act = () => new IssueAssessor(options).AssessAsync(issue, callerCts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
		slow.CancellationObserved.Should().BeTrue();
	}

	[Fact]
	public async Task AssessAsync_CallerCancelsWithGlobalTimeoutConfigured_ThrowsOperationCanceledException()
	{
		using CancellationTokenSource callerCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			CreateSeriesCategory("Category", CreateAssessment("Slow", new DelayingDefinition()))) with
		{
			GlobalTimeout = SafetyNet,
		};
		using JsonDocument issue = ParseIssue();
		callerCts.CancelAfter(ShortTimeout);

		Func<Task> act = () => new IssueAssessor(options).AssessAsync(issue, callerCts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task AssessAsync_TokenAlreadyCancelled_ThrowsBeforeExecuting()
	{
		StubDefinition definition = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("Category", CreateAssessment("Assessment", definition)));
		using JsonDocument issue = ParseIssue();

		Func<Task> act = () => new IssueAssessor(options).AssessAsync(issue, new CancellationToken(canceled: true));

		await act.Should().ThrowAsync<OperationCanceledException>();
		definition.ExecutionCount.Should().Be(0);
	}

	[Fact]
	public async Task ExecuteAsync_OuterTokenCancelledBeforeAssessmentTimeout_RecordsCancellationAsTimeout()
	{
		using CancellationTokenSource outerCts = new();
		Assessment assessment = CreateAssessment(
			"Cancelled",
			new DelegateDefinition(async (_, _, token) =>
			{
				await outerCts.CancelAsync();
				token.ThrowIfCancellationRequested();
				return new AssessmentExecutionResult(1d, "Not reached.", null, null, null);
			}),
			timeout: SafetyNet);
		using JsonDocument issue = ParseIssue();

		AssessmentExecutionItemResult item = await AssessmentExecutor.ExecuteAsync("Category", assessment, issue, null, outerCts.Token);

		item.Failure.Should().NotBeNull();
		item.Failure!.Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		item.Failure.Message.Should().Be("Assessment execution was canceled due to timeout.");
	}

	[Fact]
	public async Task ExecuteAsync_OuterTokenAndAssessmentTimeoutBothCancelled_RecordsCancellationAsTimeout()
	{
		using CancellationTokenSource outerCts = new();
		Assessment assessment = CreateAssessment(
			"BothCancelled",
			new DelegateDefinition(async (_, _, _) =>
			{
				await outerCts.CancelAsync();

				// Outlive the 1 ms assessment timeout so both tokens are cancelled when the cancellation is thrown.
				await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken);
				throw new OperationCanceledException();
			}),
			timeout: TimeSpan.FromMilliseconds(1));
		using JsonDocument issue = ParseIssue();

		AssessmentExecutionItemResult item = await AssessmentExecutor.ExecuteAsync("Category", assessment, issue, null, outerCts.Token);

		item.Failure!.Reason.Should().Be(AssessmentFailureReason.TimeoutExceeded);
		item.Failure.Message.Should().Be("Assessment execution was canceled due to timeout.");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ExecuteAsync_DefinitionThrowsUnrequestedCancellation_RecordsThrowingException(bool withAssessmentTimeout)
	{
		OperationCanceledException thrown = new("Spontaneous cancellation.");
		Assessment assessment = CreateAssessment(
			"Spontaneous",
			new StubDefinition { ExceptionToThrow = thrown },
			timeout: withAssessmentTimeout ? SafetyNet : null);
		using JsonDocument issue = ParseIssue();

		AssessmentExecutionItemResult item = await AssessmentExecutor.ExecuteAsync("Category", assessment, issue, null, CancellationToken);

		item.Failure!.Reason.Should().Be(AssessmentFailureReason.ThrowingException);
		item.Failure.InnerException.Should().BeSameAs(thrown);
		item.AssessmentResult.InnerException.Should().BeSameAs(thrown);
	}
}
