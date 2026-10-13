using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="JsonPathSelectorAssessment"/>.
/// </summary>
public class JsonPathSelectorAssessmentTests : TestBase
{
	private const string Json = """{"priority":"High","labels":["a","b"],"meta":{"points":3}}""";

	private static JsonPathSelectorAssessment Create(
		Func<JsonElement, CancellationToken, Task<bool>> evaluator,
		string jsonPath = "$.priority") => new()
		{
			JsonPath = jsonPath,
			Evaluator = evaluator,
			SuccessScore = 0.88d,
			FailureScore = 0.12d,
		};

	[Fact]
	public async Task ExecuteAsync_EvaluatorReturnsTrue_ReturnsSuccessScore()
	{
		AssessmentExecutionResult result = await ExecuteAsync(
			Create(static (value, _) => Task.FromResult(value.GetString() == "High")),
			Json);

		result.RawScore.Should().Be(0.88d);
		result.ExplanationText.Should().Be("Evaluator returned success for '$.priority'.");
		result.FailureReason.Should().BeNull();
		result.InnerException.Should().BeNull();
		result.VirtualExtensionOutput.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorReturnsFalse_ReturnsFailureScoreWithoutFailureReason()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(static (_, _) => Task.FromResult(false)), Json);

		result.RawScore.Should().Be(0.12d);
		result.ExplanationText.Should().Be("Evaluator returned failure for '$.priority'.");
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_PathMissing_ReturnsJsonPathNotFoundWithoutCallingEvaluator()
	{
		bool called = false;
		JsonPathSelectorAssessment assessment = Create(
			(_, _) =>
			{
				called = true;
				return Task.FromResult(true);
			},
			"$.severity");

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		called.Should().BeFalse();
		result.RawScore.Should().Be(0.12d);
		result.ExplanationText.Should().Be("Value was not found at '$.severity'.");
		result.FailureReason.Should().Be(AssessmentFailureReason.JsonPathNotFound);
	}

	[Theory]
	[InlineData("$.labels", JsonValueKind.Array)]
	[InlineData("$.meta", JsonValueKind.Object)]
	[InlineData("$.meta.points", JsonValueKind.Number)]
	[InlineData("$.labels[1]", JsonValueKind.String)]
	[InlineData("$", JsonValueKind.Object)]
	public async Task ExecuteAsync_AnyValueKind_IsPassedToEvaluator(string jsonPath, JsonValueKind expectedKind)
	{
		JsonValueKind? seen = null;
		JsonPathSelectorAssessment assessment = Create(
			(value, _) =>
			{
				seen = value.ValueKind;
				return Task.FromResult(true);
			},
			jsonPath);

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		seen.Should().Be(expectedKind);
		result.RawScore.Should().Be(0.88d);
	}

	[Fact]
	public async Task ExecuteAsync_NullValue_IsFoundAndPassedToEvaluator()
	{
		JsonPathSelectorAssessment assessment = Create(static (value, _) => Task.FromResult(value.ValueKind == JsonValueKind.Null));

		AssessmentExecutionResult result = await ExecuteAsync(assessment, """{"priority":null}""");

		result.RawScore.Should().Be(0.88d);
		result.FailureReason.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorReceivesCallerToken()
	{
		using CancellationTokenSource cancellationTokenSource = new();
		CancellationToken seen = default;
		JsonPathSelectorAssessment assessment = Create((_, token) =>
		{
			seen = token;
			return Task.FromResult(true);
		});
		using JsonDocument document = JsonDocument.Parse(Json);

		await assessment.ExecuteAsync(document, null, cancellationTokenSource.Token);

		seen.Should().Be(cancellationTokenSource.Token);
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorThrowsSynchronously_ReturnsThrowingException()
	{
		InvalidOperationException exception = new("bad evaluator");

		AssessmentExecutionResult result = await ExecuteAsync(Create((_, _) => throw exception), Json);

		result.RawScore.Should().Be(0.12d);
		result.ExplanationText.Should().Be("Evaluator threw an exception for '$.priority'.");
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeSameAs(exception);
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorFaultsAsynchronously_ReturnsThrowingException()
	{
		JsonPathSelectorAssessment assessment = Create(static async (_, _) =>
		{
			await Task.Yield();
			throw new FormatException("late failure");
		});

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<FormatException>();
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorReturnsNullTask_ReturnsThrowingException()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Create(static (_, _) => null!), Json);

		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<NullReferenceException>();
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorCancelledByItsToken_PropagatesOperationCanceledException()
	{
		using CancellationTokenSource cancellationTokenSource = new();
		JsonPathSelectorAssessment assessment = Create(async (_, token) =>
		{
			await cancellationTokenSource.CancelAsync();
			token.ThrowIfCancellationRequested();
			return true;
		});
		using JsonDocument document = JsonDocument.Parse(Json);

		Func<Task> act = () => assessment.ExecuteAsync(document, null, cancellationTokenSource.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_EvaluatorThrowsCancellationWhileTokenNotCancelled_ReturnsThrowingException()
	{
		JsonPathSelectorAssessment assessment = Create(static (_, _) => throw new OperationCanceledException("own cancellation"));

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<OperationCanceledException>();
	}

	[Theory]
	[InlineData(true, "$.priority")]
	[InlineData(false, "$.priority")]
	[InlineData(true, "$.missing")]
	public async Task ExecuteAsync_ExplanationTemplateSet_OverridesEveryExplanation(bool outcome, string jsonPath)
	{
		JsonPathSelectorAssessment assessment = Create((_, _) => Task.FromResult(outcome), jsonPath) with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_ExplanationTemplateSetAndEvaluatorThrows_UsesTemplate()
	{
		JsonPathSelectorAssessment assessment = Create(static (_, _) => throw new InvalidOperationException()) with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.ExplanationText.Should().Be("custom");
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
	}

	[Fact]
	public async Task ExecuteAsync_InvalidJsonPath_ThrowsInvalidJsonPathException()
	{
		Func<Task> act = () => ExecuteAsync(Create(static (_, _) => Task.FromResult(true), "$.labels[-1]"), Json);

		await act.Should().ThrowAsync<InvalidJsonPathException>();
	}

	[Fact]
	public async Task ExecuteAsync_CancellationAlreadyRequested_ThrowsWithoutCallingEvaluator()
	{
		bool called = false;
		JsonPathSelectorAssessment assessment = Create((_, _) =>
		{
			called = true;
			return Task.FromResult(true);
		});

		Func<Task> act = () => ExecuteWithCancelledTokenAsync(assessment, Json);

		await act.Should().ThrowAsync<OperationCanceledException>();
		called.Should().BeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_ThrowsArgumentNullException()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Create(static (_, _) => Task.FromResult(true)));

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}
}
