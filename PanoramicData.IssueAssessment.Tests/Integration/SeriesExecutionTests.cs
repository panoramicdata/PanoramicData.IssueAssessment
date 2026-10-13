using static PanoramicData.IssueAssessment.Tests.Integration.IntegrationTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Integration;

/// <summary>
/// Series execution: category order, gate conditions, failure handling and virtual extension chaining.
/// </summary>
public class SeriesExecutionTests : TestBase
{
	private static readonly StubDefinition Failing = new() { RawScore = 0d, FailureReason = AssessmentFailureReason.JsonPathNotFound };


	[Fact]
	public async Task Gate_ReturningTrue_RunsNextCategoryAndSeesPriorResult()
	{
		RecordingGate gate = new(shouldContinue: true);
		StubDefinition second = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("First", CreateAssessment("A", Failing)) with { SeriesGateCondition = gate },
			CreateSeriesCategory("Second", CreateAssessment("B", second)));

		IAssessmentResult result = await AssessAsync(options);

		result.Categories.Select(category => category.CategoryName).Should().Equal("First", "Second");
		second.ExecutionCount.Should().Be(1);
		(CategoryResult prior, ImmutableList<AssessmentFailure> priorFailures) = gate.Calls.Should().ContainSingle().Subject;
		prior.CategoryName.Should().Be("First");
		priorFailures.Should().ContainSingle().Which.AssessmentName.Should().Be("A");
	}

	[Fact]
	public async Task Gate_ReturningFalse_SkipsAllLaterCategories()
	{
		StubDefinition second = new();
		StubDefinition third = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("First", CreateAssessment("A", new StubDefinition())) with { SeriesGateCondition = new RecordingGate(false) },
			CreateSeriesCategory("Second", CreateAssessment("B", second)),
			CreateSeriesCategory("Third", CreateAssessment("C", third)));

		IAssessmentResult result = await AssessAsync(options);

		result.Categories.Should().ContainSingle().Which.CategoryName.Should().Be("First");
		second.ExecutionCount.Should().Be(0);
		third.ExecutionCount.Should().Be(0);
	}

	[Fact]
	public async Task Gate_OnLastCategory_IsNeverAsked()
	{
		RecordingGate gate = new(shouldContinue: false);
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("Only", CreateAssessment("A", new StubDefinition())) with { SeriesGateCondition = gate });

		await AssessAsync(options);

		gate.Calls.Should().BeEmpty();
	}

	[Fact]
	public async Task Gate_Throwing_RecordsFailureAndStopsTheSeries()
	{
		InvalidOperationException boom = new("gate failed");
		StubDefinition second = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("First", CreateAssessment("A", new StubDefinition())) with { SeriesGateCondition = new ThrowingGate(boom) },
			CreateSeriesCategory("Second", CreateAssessment("B", second)));

		IAssessmentResult result = await AssessAsync(options);

		second.ExecutionCount.Should().Be(0);
		AssessmentFailure failure = result.Failures.Should().ContainSingle().Subject;
		failure.CategoryName.Should().Be("First");
		failure.AssessmentName.Should().Be("SeriesGateCondition");
		failure.Reason.Should().Be(AssessmentFailureReason.ThrowingException);
		failure.InnerException.Should().BeSameAs(boom);
	}

	[Fact]
	public async Task OptionsFailFast_AppliesToCategoriesWithoutTheirOwnSetting()
	{
		StubDefinition afterFailure = new();
		StubDefinition laterCategory = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("First", CreateAssessment("A", Failing), CreateAssessment("B", afterFailure)),
			CreateSeriesCategory("Second", CreateAssessment("C", laterCategory))) with
		{ FailureHandling = FailureHandlingStrategy.FailFast };

		IAssessmentResult result = await AssessAsync(options);

		afterFailure.ExecutionCount.Should().Be(0, "the category inherits FailFast from the options");
		laterCategory.ExecutionCount.Should().Be(0, "FailFast also stops later categories");
		result.Failures.Should().ContainSingle();
		result.Categories.Should().ContainSingle().Which.Assessments.Should().ContainSingle();
	}

	[Fact]
	public async Task CategoryCollectAll_OverridesOptionsFailFastWithinTheCategory()
	{
		StubDefinition afterFailure = new();
		StubDefinition laterCategory = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("First", CreateAssessment("A", Failing), CreateAssessment("B", afterFailure)) with
			{
				CategoryFailureHandling = FailureHandlingStrategy.CollectAll,
			},
			CreateSeriesCategory("Second", CreateAssessment("C", laterCategory))) with
		{ FailureHandling = FailureHandlingStrategy.FailFast };

		IAssessmentResult result = await AssessAsync(options);

		afterFailure.ExecutionCount.Should().Be(1, "the category collects all its failures");
		laterCategory.ExecutionCount.Should().Be(0, "the options still stop the series after a failing category");
		result.Failures.Should().ContainSingle();
	}

	[Fact]
	public async Task CategoryFailFast_OverridesOptionsCollectAll()
	{
		StubDefinition afterFailure = new();
		StubDefinition laterCategory = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("First", CreateAssessment("A", Failing), CreateAssessment("B", afterFailure)) with
			{
				CategoryFailureHandling = FailureHandlingStrategy.FailFast,
			},
			CreateSeriesCategory("Second", CreateAssessment("C", laterCategory)));

		IAssessmentResult result = await AssessAsync(options);

		afterFailure.ExecutionCount.Should().Be(0);
		laterCategory.ExecutionCount.Should().Be(1, "the options collect all, so later categories still run");
		result.Categories.Should().HaveCount(2);
	}

	[Fact]
	public async Task VirtualOutput_IsStoredUnderItsNameAndReadByLaterBuiltInAssessments()
	{
		// The first assessment derives a value; a built-in assessment in a later category scores it by name.
		DelegateDefinition extractor = new((input, _, _) =>
		{
			string description = input.RootElement.GetProperty("description").GetString()!;
			string firstSentence = description.Split('.')[0];
			return Task.FromResult(new AssessmentExecutionResult(1d, "Extracted.", null, null, JsonDocument.Parse(JsonSerializer.Serialize(firstSentence))));
		});
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("Extract", CreateAssessment("Summary", extractor, virtualOutputName: "summary")),
			CreateSeriesCategory("Score", CreateAssessment("Summary words", new WordCountAssessment
			{
				JsonPath = "$.summary",
				MinimumWordCount = 3,
				MaximumWordCount = 20,
			}))) with
		{ CaptureVirtualDocument = true };

		IAssessmentResult result = await AssessAsync(options, """{"description":"The export drops rows. It started on Monday."}""");

		result.Failures.Should().BeEmpty();
		result.Categories[1].Assessments[0].ExplanationText.Should().Be("Word count 4 at '$.summary' is within range 3-20.");
		result.VirtualDocument!.RootElement.GetRawText().Should().Be("""{"summary":"The export drops rows"}""");
	}

	[Fact]
	public async Task VirtualOutputs_FromSeveralAssessments_AccumulateAndReachLaterAssessments()
	{
		StubDefinition first = new() { VirtualOutputJson = """{"a":1}""" };
		StubDefinition second = new() { VirtualOutputJson = "[1,2]" };
		StubDefinition observer = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("One", CreateAssessment("First", first, virtualOutputName: "first"), CreateAssessment("Second", second, virtualOutputName: "second")),
			CreateSeriesCategory("Two", CreateAssessment("Observer", observer))) with
		{ CaptureVirtualDocument = true };

		IAssessmentResult result = await AssessAsync(options);

		second.ReceivedVirtualExtensions.Should().Equal("""{"first":{"a":1}}""");
		observer.ReceivedVirtualExtensions.Should().Equal("""{"first":{"a":1},"second":[1,2]}""");
		result.VirtualDocument!.RootElement.GetRawText().Should().Be("""{"first":{"a":1},"second":[1,2]}""");
	}

	[Fact]
	public async Task VirtualOutput_FromAssessmentNotAllowedToExtend_IsIgnored()
	{
		StubDefinition producer = new() { VirtualOutputJson = """{"a":1}""" };
		StubDefinition observer = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("One", CreateAssessment("Producer", producer), CreateAssessment("Observer", observer))) with
		{ CaptureVirtualDocument = true };

		IAssessmentResult result = await AssessAsync(options);

		observer.ReceivedVirtualExtensions.Should().Equal([null]);
		result.VirtualDocument.Should().BeNull();
	}

	[Fact]
	public async Task CaptureVirtualDocumentOff_OmitsTheDocumentFromTheResult()
	{
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Series,
			CreateSeriesCategory("One", CreateAssessment("Producer", new StubDefinition { VirtualOutputJson = "1" }, virtualOutputName: "v")));

		IAssessmentResult result = await AssessAsync(options);

		result.VirtualDocument.Should().BeNull();
	}

	[Fact]
	public async Task ParallelCategories_DoNotShareVirtualExtensions()
	{
		StubDefinition observer = new();
		AssessmentOptions options = CreateOptions(
			CategoryExecutionStrategy.Parallel,
			CreateSeriesCategory("One", CreateAssessment("Producer", new StubDefinition { VirtualOutputJson = "1" }, virtualOutputName: "v")),
			CreateSeriesCategory("Two", CreateAssessment("Observer", observer))) with
		{ CaptureVirtualDocument = true };

		IAssessmentResult result = await AssessAsync(options);

		observer.ReceivedVirtualExtensions.Should().Equal([null]);
		result.VirtualDocument.Should().BeNull();
	}
}
