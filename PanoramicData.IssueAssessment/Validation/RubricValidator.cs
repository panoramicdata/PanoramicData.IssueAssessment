namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// Validates the rubric-level parts of the options: named conditions, category rules, score caps, grade bands and
/// classifications.
/// </summary>
internal static class RubricValidator
{
	/// <summary>
	/// Validates the named conditions and returns the scope rules elsewhere may use.
	/// </summary>
	public static RuleScope ValidateConditions(IAssessmentOptions options)
	{
		RuleScope scope = new([.. options.Conditions.Keys], [], null);
		foreach ((string name, ICondition condition) in options.Conditions)
		{
			string? error = condition is null ? "is missing." : RuleValidator.ConditionError(condition, scope);
			if (error is not null)
			{
				throw new ArgumentException($"Condition '{name}' {error}", nameof(options));
			}
		}

		string? cycle = RuleValidator.CycleError(options.Conditions);
		return cycle is null ? scope : throw new ArgumentException(cycle, nameof(options));
	}

	public static string? CategoryRuleError(AssessmentCategory category, RuleScope scope)
	{
		if (!double.IsFinite(category.Weight) || category.Weight < 0d)
		{
			return string.Create(CultureInfo.InvariantCulture, $"Category '{category.Name}' has invalid weight {category.Weight}; it must be zero or more.");
		}

		return RuleValidator.ConditionError(category.AppliesWhen, scope) is { } error
			? $"Category '{category.Name}' {error}"
			: null;
	}

	/// <summary>
	/// Validates score caps, grade bands and classifications, which may also use assessment scores.
	/// </summary>
	public static void ValidateRubricRules(IAssessmentOptions options, RuleScope scope)
	{
		RuleScope scoring = scope with
		{
			AssessmentNames = [.. options.Categories.SelectMany(category => category.Assessments).Select(assessment => assessment.Name)],
		};

		string? error = options.ScoreCaps.Select(cap => CapError(cap, scoring)).FirstOrDefault(found => found is not null)
			?? BandsError(options.GradeBands)
			?? options.Classifications.Select(pair => ClassificationError(pair.Key, pair.Value, scoring)).FirstOrDefault(found => found is not null);
		if (error is not null)
		{
			throw new ArgumentException(error, nameof(options));
		}
	}

	private static string? CapError(ScoreCap cap, RuleScope scope)
	{
		if (string.IsNullOrWhiteSpace(cap.Name))
		{
			return "A score cap has no name.";
		}

		if (cap.Cap is < 0d or > 1d || double.IsNaN(cap.Cap))
		{
			return $"Score cap '{cap.Name}' must be between 0 and 1.";
		}

		return cap.When is null
			? $"Score cap '{cap.Name}' has no condition."
			: RuleValidator.ConditionError(cap.When, scope) is { } error ? $"Score cap '{cap.Name}' {error}" : null;
	}

	private static string? BandsError(ImmutableList<GradeBand> bands)
	{
		GradeBand? invalid = bands.Find(band => string.IsNullOrWhiteSpace(band.Name) || band.MinimumScore is < 0d or > 1d || double.IsNaN(band.MinimumScore));
		if (invalid is not null)
		{
			return $"Grade band '{invalid.Name}' must have a name and a minimum score between 0 and 1.";
		}

		string? duplicate = bands.GroupBy(band => band.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
		return duplicate is null ? null : $"Grade band '{duplicate}' is defined more than once.";
	}

	private static string? ClassificationError(string name, ImmutableList<ClassificationRule> rules, RuleScope scope)
	{
		if (rules is not { Count: > 0 })
		{
			return $"Classification '{name}' has no rules.";
		}

		if (rules.Exists(rule => string.IsNullOrWhiteSpace(rule.Value)))
		{
			return $"Classification '{name}' has a rule without a value.";
		}

		return rules
			.Select(rule => RuleValidator.ConditionError(rule.When, scope))
			.FirstOrDefault(error => error is not null) is { } found
			? $"Classification '{name}' {found}"
			: null;
	}
}
