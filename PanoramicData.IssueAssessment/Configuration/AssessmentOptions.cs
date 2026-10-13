namespace PanoramicData.IssueAssessment.Configuration;
/// <summary>
/// Immutable top-level assessment options.
/// </summary>
public sealed record AssessmentOptions : IAssessmentOptions
{
	/// <summary>
	/// Gets the configured assessment categories.
	/// </summary>
	public required ImmutableList<AssessmentCategory> Categories { get; init; }

	/// <summary>
	/// Gets the category execution strategy.
	/// </summary>
	public required CategoryExecutionStrategy ExecutionStrategy { get; init; }

	/// <summary>
	/// Gets an optional timeout that applies to the full assessment run.
	/// </summary>
	public TimeSpan? GlobalTimeout { get; init; }

	/// <summary>
	/// Gets the failure handling strategy.
	/// </summary>
	public FailureHandlingStrategy FailureHandling { get; init; } = FailureHandlingStrategy.CollectAll;

	/// <summary>
	/// Gets a value indicating whether the final virtual document should be included in results.
	/// </summary>
	public bool CaptureVirtualDocument { get; init; }

	/// <inheritdoc />
	public ImmutableDictionary<string, ICondition> Conditions { get; init; } = ImmutableDictionary<string, ICondition>.Empty;

	/// <inheritdoc />
	public ImmutableList<ScoreCap> ScoreCaps { get; init; } = [];

	/// <inheritdoc />
	public ImmutableList<GradeBand> GradeBands { get; init; } = [];

	/// <inheritdoc />
	public ImmutableDictionary<string, ImmutableList<ClassificationRule>> Classifications { get; init; }
		= ImmutableDictionary<string, ImmutableList<ClassificationRule>>.Empty;
}

/// <summary>
/// Immutable configuration for an assessment category.
/// </summary>
public sealed record AssessmentCategory
{
	/// <summary>
	/// Gets the category name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the category description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets assessments in this category.
	/// </summary>
	public required ImmutableList<Assessment> Assessments { get; init; }

	/// <summary>
	/// Gets the assessment execution strategy for this category.
	/// </summary>
	public required AssessmentExecutionStrategy AssessmentExecutionStrategy { get; init; }

	/// <summary>
	/// Gets an optional category timeout.
	/// </summary>
	public TimeSpan? CategoryTimeout { get; init; }

	/// <summary>
	/// Gets the failure handling strategy for the assessments in this category, or null (the default) to use
	/// <see cref="AssessmentOptions.FailureHandling"/>.
	/// </summary>
	public FailureHandlingStrategy? CategoryFailureHandling { get; init; }

	/// <summary>
	/// Gets an optional gate used when categories execute in series.
	/// </summary>
	public ISeriesGateCondition? SeriesGateCondition { get; init; }

	/// <summary>
	/// Gets the category's weight in the overall score. Default 1; must not be negative.
	/// </summary>
	public double Weight { get; init; } = 1d;

	/// <summary>
	/// Gets an optional condition under which the category applies. When it is false the category is not run, is
	/// reported as not applicable, and does not count in the overall score.
	/// </summary>
	public ICondition? AppliesWhen { get; init; }
}

/// <summary>
/// Immutable configuration for a single assessment.
/// </summary>
public sealed record Assessment
{
	/// <summary>
	/// Gets the assessment name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the assessment description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets the assessment implementation.
	/// </summary>
	public required IAssessmentDefinition Definition { get; init; }

	/// <summary>
	/// Gets an optional assessment timeout.
	/// </summary>
	public TimeSpan? AssessmentTimeout { get; init; }

	/// <summary>
	/// Gets the relative weight used during aggregation.
	/// </summary>
	public double Weight { get; init; } = 1.0d;

	/// <summary>
	/// Gets a value indicating whether this assessment may produce virtual extension output.
	/// </summary>
	public bool AllowVirtualExtension { get; init; }

	/// <summary>
	/// Gets the logical name for the virtual output value.
	/// </summary>
	public string? VirtualOutputName { get; init; }

	/// <summary>
	/// Gets an optional condition under which the assessment applies. When it is false the assessment is not run, is
	/// reported as not applicable, and is left out of its category's score and score range, so the remaining weights
	/// are renormalised.
	/// </summary>
	public ICondition? AppliesWhen { get; init; }

	/// <summary>
	/// Gets what to recommend when the assessment does not pass. A tier's own recommendation takes precedence.
	/// </summary>
	public string? Recommendation { get; init; }
}
