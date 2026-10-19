namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// What a rule may refer to where it is used.
/// </summary>
/// <param name="ConditionNames">The rubric's named conditions.</param>
/// <param name="MeasureNames">The measures of the tiered assessment being validated; empty elsewhere.</param>
/// <param name="AssessmentNames">Every assessment's name, where assessment scores are visible (score caps and
/// classifications); null elsewhere.</param>
internal sealed record RuleScope(
	ImmutableHashSet<string> ConditionNames,
	ImmutableHashSet<string> MeasureNames,
	ImmutableHashSet<string>? AssessmentNames);

/// <summary>
/// Validates conditions, measures and text sources. Custom <see cref="ICondition"/> and <see cref="IMeasure"/>
/// implementations are not inspected.
/// </summary>
internal static partial class RuleValidator
{
	public static string? ConditionError(ICondition? condition, RuleScope scope) => condition switch
	{
		null => null,
		AllCondition all => ConditionsError(all.Conditions, scope),
		AnyCondition any => ConditionsError(any.Conditions, scope),
		NotCondition not => not.Condition is null ? "has a 'not' condition without a condition." : ConditionError(not.Condition, scope),
		ConditionReference reference => scope.ConditionNames.Contains(reference.Name)
			? null
			: $"refers to undefined condition '{reference.Name}'.",
		MeasureCondition measure => MeasureConditionError(measure, scope),
		AssessmentScoreCondition score => AssessmentScoreError(score, scope),
		_ => FieldConditionError(condition),
	};

	private static string? FieldConditionError(ICondition condition) => condition switch
	{
		TextMatchesCondition value => SourceError(value.Source) ?? PatternsError(value.Patterns),
		FieldMatchesCondition value => PathError(value.Path) ?? PatternsError(value.Patterns),
		FieldEqualsCondition value => PathError(value.Path) ?? (value.Values is not { Count: > 0 } ? "has a 'fieldEquals' condition without values." : null),
		FieldPresentCondition value => PathError(value.Path),
		FieldsDifferCondition value => PathError(value.Left) ?? PathError(value.Right),
		SectionPresentCondition value => SourceError(value.Source) ?? (value.Source.Section is null ? "has a 'sectionPresent' condition whose source has no section." : null),
		PlaceholderCondition value => SourceError(value.Source),
		_ => null,
	};

	private static string? ConditionsError(ImmutableList<ICondition>? conditions, RuleScope scope)
		=> conditions is null
			? "has a combined condition without conditions."
			: conditions.Select(condition => ConditionError(condition, scope)).FirstOrDefault(error => error is not null);

	private static string? MeasureConditionError(MeasureCondition condition, RuleScope scope)
	{
		if ((condition.Measure is null) == (condition.Of is null))
		{
			return "has a 'measure' condition that must set exactly one of Measure and Of.";
		}

		return condition.Of is not null
			? MeasureError(condition.Of)
			: scope.MeasureNames.Contains(condition.Measure!) ? null : $"refers to undefined measure '{condition.Measure}'.";
	}

	private static string? AssessmentScoreError(AssessmentScoreCondition condition, RuleScope scope)
	{
		if (scope.AssessmentNames is null)
		{
			return "uses an 'assessmentScore' condition, which only score caps and classifications may use.";
		}

		return scope.AssessmentNames.Contains(condition.Assessment) ? null : $"refers to unknown assessment '{condition.Assessment}'.";
	}
}
