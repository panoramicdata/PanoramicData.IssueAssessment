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
internal static class RuleValidator
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

	public static string? MeasureError(IMeasure? measure) => measure switch
	{
		null => "has a missing measure.",
		PatternMatchCountMeasure value => SourceError(value.Source) ?? PatternsError(value.Patterns),
		OccurrenceCountMeasure value => SourceError(value.Source) ?? PatternError(value.Pattern),
		WordCountMeasure value => SourceError(value.Source),
		AlphanumericCountMeasure value => SourceError(value.Source),
		LengthMeasure value => SourceError(value.Source),
		CriteriaHeadingCountMeasure value => SourceError(value.Source),
		ItemCountMeasure value => PathError(value.Path),
		NumberMeasure value => PathError(value.Path),
		_ => null,
	};

	/// <summary>
	/// The first named condition that refers to itself, directly or through others.
	/// </summary>
	public static string? CycleError(IReadOnlyDictionary<string, ICondition> conditions)
	{
		foreach (string name in conditions.Keys)
		{
			if (ReachesItself(name, conditions))
			{
				return $"Condition '{name}' refers to itself.";
			}
		}

		return null;
	}

	public static string? PathError(string? jsonPath)
	{
		try
		{
			JsonPathHelper.Validate(jsonPath!);
			return null;
		}
		catch (InvalidJsonPathException)
		{
			return $"has invalid JSONPath '{jsonPath}'.";
		}
	}

	public static string? PatternError(string? pattern)
	{
		if (string.IsNullOrWhiteSpace(pattern))
		{
			return "has an empty pattern.";
		}

		try
		{
			_ = RegexCache.GetOrAdd(pattern);
			return null;
		}
		catch (ArgumentException)
		{
			return $"has invalid regex pattern '{pattern}'.";
		}
	}

	private static string? FieldConditionError(ICondition condition) => condition switch
	{
		TextMatchesCondition value => SourceError(value.Source) ?? PatternsError(value.Patterns),
		FieldMatchesCondition value => PathError(value.Path) ?? PatternsError(value.Patterns),
		FieldEqualsCondition value => PathError(value.Path) ?? (value.Values is not { Count: > 0 } ? "has a 'fieldEquals' condition without values." : null),
		FieldPresentCondition value => PathError(value.Path),
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

	private static string? SourceError(TextSource? source)
	{
		if (source?.Paths is not { Count: > 0 })
		{
			return "has a text source without paths.";
		}

		return source.Paths.Select(PathError).FirstOrDefault(error => error is not null)
			?? (source.Section is null ? null : PatternError(source.Section));
	}

	private static string? PatternsError(ImmutableList<string>? patterns)
		=> patterns is not { Count: > 0 }
			? "has no patterns."
			: patterns.Select(PatternError).FirstOrDefault(error => error is not null);

	private static bool ReachesItself(string start, IReadOnlyDictionary<string, ICondition> conditions)
	{
		Stack<string> pending = new(References(conditions[start]));
		HashSet<string> seen = [];
		while (pending.TryPop(out string? name))
		{
			if (name == start)
			{
				return true;
			}

			if (seen.Add(name) && conditions.TryGetValue(name, out ICondition? next))
			{
				foreach (string reference in References(next))
				{
					pending.Push(reference);
				}
			}
		}

		return false;
	}

	private static IEnumerable<string> References(ICondition? condition) => condition switch
	{
		ConditionReference reference => [reference.Name],
		AllCondition all => all.Conditions.SelectMany(References),
		AnyCondition any => any.Conditions.SelectMany(References),
		NotCondition not => References(not.Condition),
		_ => [],
	};
}
