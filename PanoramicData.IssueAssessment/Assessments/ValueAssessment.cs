namespace PanoramicData.IssueAssessment.Assessments;

/// <summary>
/// Base for the built-in assessments that select one value with JSONPath and score it as passing or failing.
/// </summary>
/// <remarks>
/// The value is read from the issue and, when the issue does not contain it, from the virtual extension. A value that
/// is missing or of the wrong JSON type scores <see cref="FailureScore"/> with
/// <see cref="AssessmentFailureReason.JsonPathNotFound"/>. Derived types are defined in this library only; implement
/// <see cref="IAssessmentDefinition"/> for a custom check.
/// </remarks>
public abstract record ValueAssessment : IAssessmentDefinition, IJsonPathAssessmentDefinition
{
	/// <summary>
	/// Gets the JSONPath selector.
	/// </summary>
	public required string JsonPath { get; init; }

	/// <summary>
	/// Gets the score returned when the value passes.
	/// </summary>
	public double SuccessScore { get; init; } = 1d;

	/// <summary>
	/// Gets the score returned when the value fails, or is missing.
	/// </summary>
	public double FailureScore { get; init; }

	/// <summary>
	/// Gets an optional explanation that replaces every explanation the assessment would produce.
	/// </summary>
	public string? ExplanationTemplate { get; init; }

	/// <summary>
	/// Gets how a missing value is described in explanations, for example "String value".
	/// </summary>
	private protected virtual string ValueDescription => "String value";

	/// <inheritdoc />
	public Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);
		cancellationToken.ThrowIfCancellationRequested();

		ValueEvaluation evaluation = JsonPathHelper.TrySelect(input, virtualExtension, JsonPath, out JsonElement value)
			? Evaluate(value)
			: NotFound();

		return Task.FromResult(new AssessmentExecutionResult(
			RawScore: evaluation.Passed == true ? SuccessScore : FailureScore,
			ExplanationText: ExplanationTemplate ?? evaluation.Explanation,
			FailureReason: evaluation.Passed is null ? AssessmentFailureReason.JsonPathNotFound : null,
			InnerException: null,
			VirtualExtensionOutput: null));
	}

	/// <summary>
	/// Describes the type-specific settings, invariantly, for cache keys.
	/// </summary>
	internal abstract string DescribeSettings();

	/// <summary>
	/// Scores the selected value.
	/// </summary>
	private protected abstract ValueEvaluation Evaluate(JsonElement value);

	/// <summary>
	/// The result for a value that is missing or of the wrong type.
	/// </summary>
	private protected ValueEvaluation NotFound() => new(null, $"{ValueDescription} was not found at '{JsonPath}'.");

	/// <summary>
	/// The result for a value that was scored.
	/// </summary>
	private protected static ValueEvaluation Scored(bool passed, string explanation) => new(passed, explanation);

	/// <summary>
	/// Explains an inclusive range check: "{subject} at '{path}' is within range {minimum}-{maximum}."
	/// </summary>
	private protected string RangeExplanation(string subject, bool isInRange, string minimum, string maximum)
		=> $"{subject} at '{JsonPath}' is {(isInRange ? "within" : "outside")} range {minimum}-{maximum}.";

	/// <summary>
	/// True when <paramref name="value"/> lies in the inclusive range; a null bound is open.
	/// </summary>
	private protected static bool IsWithin<T>(T value, T? minimum, T? maximum)
		where T : struct, IComparable<T>
		=> (minimum is not { } low || value.CompareTo(low) >= 0) && (maximum is not { } high || value.CompareTo(high) <= 0);

	/// <summary>
	/// Returns the value as a string when it is a JSON string.
	/// </summary>
	private protected static bool TryGetString(JsonElement value, out string text)
	{
		text = value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
		return value.ValueKind == JsonValueKind.String;
	}

	/// <summary>
	/// The outcome of evaluating a value: passed, failed, or (when <see cref="Passed"/> is null) not found.
	/// </summary>
	private protected readonly record struct ValueEvaluation(bool? Passed, string Explanation);
}
