
namespace PanoramicData.IssueAssessment.Assessments;

/// <summary>
/// Scores an issue by the first of its <see cref="Tiers"/> whose condition holds: partial credit, with a finding and a
/// recommendation for each level.
/// </summary>
/// <remarks>
/// <para>
/// A tier's condition can use the assessment's named <see cref="Measures"/> (through <see cref="MeasureCondition"/>), the
/// rubric's named conditions and any other <see cref="ICondition"/>. The last tier must have no condition, so every
/// issue lands in some tier.
/// </para>
/// <para>
/// Findings and recommendations may contain placeholders: <c>{name}</c> is replaced by the value of a measure,
/// <c>{name:text}</c> by a described measure's text (for example the duplicated criteria headings),
/// <c>{$.path}</c> by the values at a JSONPath, comma-separated, and <c>{$.path~pattern}</c> by the first of them that
/// matches the pattern. A finding may be empty, for a tier that only recommends.
/// </para>
/// </remarks>
/// <example>
/// Three or more business-case phrases score 100, one or two score 50, none scores 0:
/// <code>
/// new TieredAssessment
/// {
///     Measures = ImmutableDictionary&lt;string, IMeasure&gt;.Empty.Add("matches", new PatternMatchCountMeasure { Source = fullText, Patterns = businessCase }),
///     Tiers =
///     [
///         new Tier { When = new MeasureCondition { Measure = "matches", AtLeast = 3 }, Score = 100, Finding = "✓ Business case: clear" },
///         new Tier { When = new MeasureCondition { Measure = "matches", AtLeast = 1 }, Score = 50, Finding = "⚠ Business case: brief", Recommendation = "Expand the business case" },
///         new Tier { Score = 0, Finding = "✗ Business case: none", Recommendation = "Add a business case" },
///     ],
/// }
/// </code>
/// </example>
public sealed record TieredAssessment : IAssessmentDefinition, IContextualAssessmentDefinition
{
	/// <summary>
	/// Gets measures the tiers can refer to by name.
	/// </summary>
	public ImmutableDictionary<string, IMeasure> Measures { get; init; } = ImmutableDictionary<string, IMeasure>.Empty;

	/// <summary>
	/// Gets the tiers, evaluated in order; the first whose condition holds gives the score.
	/// </summary>
	public required ImmutableList<Tier> Tiers { get; init; }

	/// <summary>
	/// Gets the lowest possible score, for normalization. Default 0.
	/// </summary>
	public double MinimumScore { get; init; }

	/// <summary>
	/// Gets the highest possible score, for normalization. Default 100.
	/// </summary>
	public double MaximumScore { get; init; } = 100d;

	/// <inheritdoc />
	public Task<AssessmentExecutionResult> ExecuteAsync(
		JsonDocument input,
		JsonDocument? virtualExtension,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);
		return ExecuteAsync(new EvaluationContext(input).WithVirtualExtension(virtualExtension), cancellationToken);
	}

	/// <inheritdoc />
	public Task<AssessmentExecutionResult> ExecuteAsync(EvaluationContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		cancellationToken.ThrowIfCancellationRequested();

		EvaluationContext scoped = context.WithMeasures(Measures);
		Tier tier = Tiers.Find(candidate => candidate.When is null || candidate.When.Evaluate(scoped))
			?? throw new InvalidOperationException("No tier matched; the last tier must have no condition.");

		return Task.FromResult(new AssessmentExecutionResult(
			RawScore: tier.Score,
			ExplanationText: Format(tier.Finding, scoped),
			FailureReason: null,
			InnerException: null,
			VirtualExtensionOutput: null)
		{
			Recommendation = tier.Recommendation is null ? null : Format(tier.Recommendation, scoped),
		});
	}

	private string Format(string text, EvaluationContext context) => PlaceholderText.Format(text, context, Measures);
}

/// <summary>
/// One level of a <see cref="TieredAssessment"/>.
/// </summary>
public sealed record Tier
{
	/// <summary>Gets the condition for this tier, or null for the final, catch-all tier.</summary>
	public ICondition? When { get; init; }

	/// <summary>Gets the score this tier gives.</summary>
	public required double Score { get; init; }

	/// <summary>Gets the finding reported for this tier; it may contain placeholders.</summary>
	public required string Finding { get; init; }

	/// <summary>Gets what to recommend at this tier, if anything; it may contain placeholders.</summary>
	public string? Recommendation { get; init; }
}
