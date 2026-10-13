namespace PanoramicData.IssueAssessment.Tests.Fixtures;

/// <summary>
/// Small factories for test assessments and assessment definitions.
/// </summary>
internal static class AssessmentBuilderFixture
{
	/// <summary>
	/// The assessment name used when a test does not supply one.
	/// </summary>
	public const string DefaultName = "Assessment";

	/// <summary>
	/// The assessment description used when a test does not supply one.
	/// </summary>
	public const string DefaultDescription = "Assessment description";

	/// <summary>
	/// Creates a valid assessment, defaulting to a definition that always scores 1.
	/// </summary>
	public static Assessment Create(
		IAssessmentDefinition? definition = null,
		string name = DefaultName,
		double weight = 1d)
	{
		return new Assessment
		{
			Name = name,
			Description = DefaultDescription,
			Definition = definition ?? new FixedScoreDefinition(1d),
			Weight = weight,
		};
	}

	/// <summary>
	/// Creates an assessment with a presence definition on the given JSONPath.
	/// </summary>
	public static Assessment Presence(string jsonPath, string name = DefaultName)
	{
		return Create(new PresenceAssessment { JsonPath = jsonPath }, name);
	}

	/// <summary>
	/// An assessment definition that always returns a fixed raw score.
	/// </summary>
	/// <param name="score">The raw score to return.</param>
	internal sealed class FixedScoreDefinition(double score) : IAssessmentDefinition
	{
		/// <summary>
		/// Gets the explanation returned with the score.
		/// </summary>
		public const string Explanation = "fixed";

		/// <inheritdoc />
		public Task<AssessmentExecutionResult> ExecuteAsync(
			JsonDocument input,
			JsonDocument? virtualExtension,
			CancellationToken cancellationToken)
		{
			return Task.FromResult(new AssessmentExecutionResult(
				RawScore: score,
				ExplanationText: Explanation,
				FailureReason: null,
				InnerException: null,
				VirtualExtensionOutput: null));
		}
	}

	/// <summary>
	/// A custom definition exposing an arbitrary JSONPath, for validator tests on third-party definitions.
	/// </summary>
	/// <param name="jsonPath">The JSONPath to expose.</param>
	internal sealed class CustomJsonPathDefinition(string jsonPath) : IAssessmentDefinition, IJsonPathAssessmentDefinition
	{
		/// <inheritdoc />
		public string JsonPath => jsonPath;

		/// <inheritdoc />
		public Task<AssessmentExecutionResult> ExecuteAsync(
			JsonDocument input,
			JsonDocument? virtualExtension,
			CancellationToken cancellationToken)
		{
			return new FixedScoreDefinition(1d).ExecuteAsync(input, virtualExtension, cancellationToken);
		}
	}

	/// <summary>
	/// A custom definition exposing an arbitrary regex pattern, for validator tests on third-party definitions.
	/// </summary>
	/// <param name="pattern">The regex pattern to expose.</param>
	internal sealed class CustomRegexDefinition(string pattern) : IAssessmentDefinition, IRegexPatternAssessmentDefinition
	{
		/// <inheritdoc />
		public string Pattern => pattern;

		/// <inheritdoc />
		public Task<AssessmentExecutionResult> ExecuteAsync(
			JsonDocument input,
			JsonDocument? virtualExtension,
			CancellationToken cancellationToken)
		{
			return new FixedScoreDefinition(1d).ExecuteAsync(input, virtualExtension, cancellationToken);
		}
	}
}
