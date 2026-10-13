using static PanoramicData.IssueAssessment.Tests.Assessments.AssessmentTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// The contract <see cref="CustomCallbackAssessment"/> and <see cref="JsonPathSelectorAssessment"/> share for the
/// consumer code they run: exceptions become failures, a cancellation the token asked for propagates, and nothing runs
/// once cancellation has been requested.
/// </summary>
public class DelegateAssessmentContractTests
{
	private const string Json = """{"title":"Issue"}""";

	/// <summary>
	/// Builds the named assessment around <paramref name="behaviour"/>, which the consumer delegate runs (and whose task
	/// it awaits) before succeeding.
	/// </summary>
	private static IAssessmentDefinition Build(string kind, Func<CancellationToken, Task> behaviour) => kind == "callback"
		? new CustomCallbackAssessment { Callback = (_, _, token) => ThenSucceed(behaviour(token)) }
		: new JsonPathSelectorAssessment { JsonPath = "$.title", Evaluator = (_, token) => ThenTrue(behaviour(token)), FailureScore = 0.25d };

	private static async Task<(double?, string, Exception?)> ThenSucceed(Task task)
	{
		await task;
		return (1d, "ok", null);
	}

	private static async Task<bool> ThenTrue(Task task)
	{
		await task;
		return true;
	}

	public static TheoryData<string, double?, string> Kinds => new()
	{
		{ "callback", null, "Custom callback threw an exception." },
		{ "selector", 0.25d, "Evaluator threw an exception for '$.title'." },
	};

	[Theory]
	[MemberData(nameof(Kinds))]
	public async Task DelegateThrowsSynchronously_ReturnsThrowingException(string kind, double? score, string explanation)
	{
		InvalidOperationException exception = new("boom");

		AssessmentExecutionResult result = await ExecuteAsync(Build(kind, _ => throw exception), Json);

		result.RawScore.Should().Be(score);
		result.ExplanationText.Should().Be(explanation);
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeSameAs(exception);
	}

	public static TheoryData<string, Func<CancellationToken, Task>, Type> Faults => new()
	{
		{ "callback", static async _ => { await Task.Yield(); throw new FormatException("late"); }, typeof(FormatException) },
		{ "selector", static async _ => { await Task.Yield(); throw new FormatException("late"); }, typeof(FormatException) },
		{ "callback", static _ => null!, typeof(NullReferenceException) },
		{ "selector", static _ => null!, typeof(NullReferenceException) },
		{ "callback", static _ => throw new TaskCanceledException("own timeout"), typeof(TaskCanceledException) },
		{ "selector", static _ => throw new OperationCanceledException("own cancellation"), typeof(OperationCanceledException) },
	};

	[Theory]
	[MemberData(nameof(Faults))]
	public async Task DelegateFaultsReturnsNullOrCancelsOnItsOwn_ReturnsThrowingException(string kind, Func<CancellationToken, Task> behaviour, Type exceptionType)
	{
		AssessmentExecutionResult result = await ExecuteAsync(Build(kind, behaviour), Json);

		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
		result.InnerException.Should().BeOfType(exceptionType);
	}

	[Theory]
	[InlineData("callback")]
	[InlineData("selector")]
	public async Task DelegateCancelledByItsToken_PropagatesOperationCanceledException(string kind)
	{
		using CancellationTokenSource cancellation = new();
		IAssessmentDefinition assessment = Build(kind, async token =>
		{
			await cancellation.CancelAsync();
			token.ThrowIfCancellationRequested();
		});
		using JsonDocument document = JsonDocument.Parse(Json);

		Func<Task> act = () => assessment.ExecuteAsync(document, null, cancellation.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Theory]
	[InlineData("callback")]
	[InlineData("selector")]
	public async Task ExplanationTemplateSetAndDelegateThrows_UsesTemplate(string kind)
	{
		IAssessmentDefinition assessment = Build(kind, _ => throw new InvalidOperationException()) switch
		{
			CustomCallbackAssessment callback => callback with { ExplanationTemplate = "custom" },
			JsonPathSelectorAssessment selector => selector with { ExplanationTemplate = "custom" },
			var other => other,
		};

		AssessmentExecutionResult result = await ExecuteAsync(assessment, Json);

		result.ExplanationText.Should().Be("custom");
		result.FailureReason.Should().Be(AssessmentFailureReason.ThrowingException);
	}

	[Theory]
	[InlineData("callback")]
	[InlineData("selector")]
	public async Task CancellationAlreadyRequested_ThrowsWithoutRunningTheDelegate(string kind)
	{
		bool called = false;
		IAssessmentDefinition assessment = Build(kind, _ =>
		{
			called = true;
			return Task.CompletedTask;
		});

		Func<Task> act = () => ExecuteWithCancelledTokenAsync(assessment, Json);

		await act.Should().ThrowAsync<OperationCanceledException>();
		called.Should().BeFalse();
	}

	[Theory]
	[InlineData("callback")]
	[InlineData("selector")]
	public async Task NullInput_ThrowsArgumentNullException(string kind)
	{
		Func<Task> act = () => ExecuteWithNullInputAsync(Build(kind, _ => Task.CompletedTask));

		(await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("input");
	}
}
