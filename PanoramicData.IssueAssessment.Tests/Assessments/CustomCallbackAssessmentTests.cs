using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Tests for <see cref="CustomCallbackAssessment"/>.
/// </summary>
public class CustomCallbackAssessmentTests : TestBase
{
	private const string Json = """{"title":"Issue"}""";

	private static CustomCallbackAssessment Returning(double? score, string explanation, Exception? exception = null) => new()
	{
		Callback = (_, _, _) => Task.FromResult<(double?, string, Exception?)>((score, explanation, exception)),
	};

	private static CustomCallbackAssessment Throwing(Exception exception) => new()
	{
		Callback = (_, _, _) => throw exception,
	};

	[Fact]
	public async Task ExecuteAsync_CallbackReturnsScore_UsesCallbackResult()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Returning(0.77d, "custom explanation"), Json);

		result.RawScore.Should().Be(0.77d);
		result.ExplanationText.Should().Be("custom explanation");
		result.FailureReason.Should().BeNull();
		result.InnerException.Should().BeNull();
		result.VirtualExtensionOutput.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteAsync_CallbackReturnsNullScore_PassesNullThrough()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Returning(null, "no opinion"), Json);

		result.RawScore.Should().BeNull();
		result.FailureReason.Should().BeNull();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public async Task ExecuteAsync_CallbackExplanationBlank_UsesDefaultExplanation(string? explanation)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Returning(1d, explanation!), Json);

		result.ExplanationText.Should().Be("Custom callback completed.");
	}

	[Fact]
	public async Task ExecuteAsync_CallbackReturnsException_ReportsThrowingExceptionAndKeepsScore()
	{
		InvalidOperationException exception = new("reported");

		AssessmentExecutionResult result = await ExecuteAsync(Returning(0.4d, "partial", exception), Json);

		result.RawScore.Should().Be(0.4d);
		result.ExplanationText.Should().Be("partial");
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeSameAs(exception);
	}

	[Fact]
	public async Task ExecuteAsync_CallbackThrowsSynchronously_ReturnsThrowingException()
	{
		InvalidOperationException exception = new("boom");

		AssessmentExecutionResult result = await ExecuteAsync(Throwing(exception), Json);

		result.RawScore.Should().BeNull();
		result.ExplanationText.Should().Be("Custom callback threw an exception.");
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeSameAs(exception);
	}

	[Fact]
	public async Task ExecuteAsync_CallbackFaultsAsynchronously_ReturnsThrowingException()
	{
		CustomCallbackAssessment assessment = new()
		{
			Callback = static async (_, _, _) =>
			{
				await Task.Yield();
				throw new FormatException("late");
			},
		};

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<FormatException>();
	}

	[Fact]
	public async Task ExecuteAsync_CallbackReturnsNullTask_ReturnsThrowingException()
	{
		CustomCallbackAssessment assessment = new() { Callback = static (_, _, _) => null! };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<NullReferenceException>();
	}

	[Fact]
	public async Task ExecuteAsync_CallbackCancelledByItsToken_PropagatesOperationCanceledException()
	{
		using CancellationTokenSource cancellationTokenSource = new();
		CustomCallbackAssessment assessment = new()
		{
			Callback = async (_, _, token) =>
			{
				await cancellationTokenSource.CancelAsync();
				token.ThrowIfCancellationRequested();
				return (1d, "unreachable", null);
			},
		};
		using JsonDocument document = JsonDocument.Parse(Json);

		Func<Task> act = () => assessment.ExecuteAsync(document, null, cancellationTokenSource.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_CallbackThrowsCancellationWhileTokenNotCancelled_ReturnsThrowingException()
	{
		AssessmentExecutionResult result = await ExecuteAsync(Throwing(new TaskCanceledException("own timeout")), Json);

		result.RawScore.Should().BeNull();
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType<TaskCanceledException>();
	}

	[Fact]
	public async Task ExecuteAsync_CallbackReceivesInputVirtualExtensionAndToken()
	{
		using CancellationTokenSource cancellationTokenSource = new();
		using JsonDocument input = JsonDocument.Parse(Json);
		using JsonDocument extension = JsonDocument.Parse("""{"derived":true}""");
		(JsonDocument? Input, JsonDocument? Extension, CancellationToken Token) seen = default;
		CustomCallbackAssessment assessment = new()
		{
			Callback = (i, e, t) =>
			{
				seen = (i, e, t);
				return Task.FromResult<(double?, string, Exception?)>((1d, "ok", null));
			},
		};

		await assessment.ExecuteAsync(input, extension, cancellationTokenSource.Token);

		seen.Input.Should().BeSameAs(input);
		seen.Extension.Should().BeSameAs(extension);
		seen.Token.Should().Be(cancellationTokenSource.Token);
	}

	[Fact]
	public async Task ExecuteAsync_ExplanationTemplateSet_OverridesCallbackExplanation()
	{
		CustomCallbackAssessment assessment = Returning(1d, "from callback") with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.ExplanationText.Should().Be("custom");
	}

	[Fact]
	public async Task ExecuteAsync_ExplanationTemplateSetAndCallbackThrows_UsesTemplate()
	{
		CustomCallbackAssessment assessment = Throwing(new InvalidOperationException()) with { ExplanationTemplate = "custom" };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.ExplanationText.Should().Be("custom");
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
	}

	[Fact]
	public async Task ExecuteAsync_WhitespaceExplanationTemplate_IsIgnoredInFavourOfCallbackExplanation()
	{
		CustomCallbackAssessment assessment = Returning(1d, "from callback") with { ExplanationTemplate = "   " };

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.ExplanationText.Should().Be("from callback");
	}

	[Fact]
	public async Task ExecuteAsync_CancellationAlreadyRequested_ThrowsWithoutCallingCallback()
	{
		bool called = false;
		CustomCallbackAssessment assessment = new()
		{
			Callback = (_, _, _) =>
			{
				called = true;
				return Task.FromResult<(double?, string, Exception?)>((1d, "ok", null));
			},
		};

		Func<Task> act = () => ExecuteWithCancelledTokenAsync(assessment, Json);

		await act.Should().ThrowAsync<OperationCanceledException>();
		called.Should().BeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_NullInput_ThrowsArgumentNullException()
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Returning(1d, "ok"));

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}
}
