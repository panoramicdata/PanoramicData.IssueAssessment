namespace PanoramicData.IssueAssessment.Configuration;

/// <summary>
/// A cap on the overall score: when <see cref="When"/> holds, the score is held at or below <see cref="Cap"/>.
/// </summary>
/// <remarks>
/// Use a cap rather than a weight for an element without which an issue must not reach the top grade however good the
/// rest is, such as steps to reproduce on a bug. A weight only costs points; a cap makes the grade unreachable.
/// </remarks>
public sealed record ScoreCap
{
	/// <summary>Gets the cap's name, reported when it applies.</summary>
	public required string Name { get; init; }

	/// <summary>Gets the highest score allowed, in [0, 1].</summary>
	public required double Cap { get; init; }

	/// <summary>Gets the condition under which the cap applies. It may use <see cref="AssessmentScoreCondition"/>.</summary>
	public required ICondition When { get; init; }

	/// <summary>Gets an optional group name, such as "mandatory", for reporting caps together.</summary>
	public string? Group { get; init; }
}

/// <summary>
/// A grade band: scores of at least <see cref="MinimumScore"/> earn <see cref="Name"/> unless a higher band applies.
/// </summary>
public sealed record GradeBand
{
	/// <summary>Gets the grade's name, such as "EXCELLENT".</summary>
	public required string Name { get; init; }

	/// <summary>Gets the lowest score, in [0, 1], that earns this grade.</summary>
	public required double MinimumScore { get; init; }
}

/// <summary>
/// One rule of a classification: when <see cref="When"/> holds (or is not set), the classification takes
/// <see cref="Value"/>. Rules are evaluated in order and the first match wins.
/// </summary>
public sealed record ClassificationRule
{
	/// <summary>Gets the condition, or null for a rule that always matches. It may use <see cref="AssessmentScoreCondition"/>.</summary>
	public ICondition? When { get; init; }

	/// <summary>Gets the value assigned when the rule matches.</summary>
	public required string Value { get; init; }
}
