namespace PanoramicData.IssueAssessment.Assessments;

/// <summary>
/// Stands in, in a JSON rubric, for an assessment definition written in code. Loading the rubric replaces it with the
/// definition registered under <see cref="Name"/>.
/// </summary>
/// <remarks>
/// Definitions that hold delegates (<see cref="CustomCallbackAssessment"/>, <see cref="JsonPathSelectorAssessment"/>)
/// and custom <see cref="IAssessmentDefinition"/> implementations cannot be written as JSON, so a rubric names them and
/// the caller supplies them to <see cref="Serialization.AssessmentRubric.Load(string, IReadOnlyDictionary{string, IAssessmentDefinition})"/>.
/// An assessor rejects one that was never replaced.
/// </remarks>
public sealed record NamedAssessmentDefinition : IAssessmentDefinition
{
	/// <summary>
	/// Gets the name the definition is registered under.
	/// </summary>
	public required string Name { get; init; }

	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">Always: a named definition must be replaced before use.</exception>
	public Task<AssessmentExecutionResult> ExecuteAsync(JsonDocument input, JsonDocument? virtualExtension, CancellationToken cancellationToken)
		=> throw new InvalidOperationException($"Custom definition '{Name}' was not registered when the rubric was loaded.");
}
