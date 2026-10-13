namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// Validates a <see cref="TieredAssessment"/>: its score range, its tiers and the measures and placeholders they use.
/// </summary>
internal static class TieredValidator
{
	public static string? Error(TieredAssessment tiered, RuleScope scope)
	{
		if (tiered.Tiers is not { Count: > 0 })
		{
			return "must define at least one tier.";
		}

		if (tiered.MinimumScore >= tiered.MaximumScore)
		{
			return "has a MinimumScore that is not below its MaximumScore.";
		}

		RuleScope tierScope = scope with { MeasureNames = [.. tiered.Measures.Keys] };
		return StructureError(tiered)
			?? tiered.Measures.Values.Select(RuleValidator.MeasureError).FirstOrDefault(error => error is not null)
			?? tiered.Tiers.Select(tier => TierError(tiered, tier, tierScope)).FirstOrDefault(error => error is not null);
	}

	private static string? StructureError(TieredAssessment tiered)
	{
		if (tiered.Tiers[^1].When is not null)
		{
			return "must end with a tier that has no condition, so every issue lands in a tier.";
		}

		return tiered.Tiers.Take(tiered.Tiers.Count - 1).Any(tier => tier.When is null)
			? "has a tier without a condition before its last tier, so the tiers after it can never apply."
			: null;
	}

	private static string? TierError(TieredAssessment tiered, Tier tier, RuleScope scope)
	{
		if (string.IsNullOrWhiteSpace(tier.Finding))
		{
			return "has a tier without a finding.";
		}

		if (tier.Score < tiered.MinimumScore || tier.Score > tiered.MaximumScore)
		{
			return string.Create(
				CultureInfo.InvariantCulture,
				$"has a tier score of {tier.Score} outside its range {tiered.MinimumScore}-{tiered.MaximumScore}.");
		}

		return RuleValidator.ConditionError(tier.When, scope)
			?? PlaceholderError(tier.Finding, scope)
			?? PlaceholderError(tier.Recommendation, scope);
	}

	private static string? PlaceholderError(string? text, RuleScope scope)
		=> TieredAssessment.Placeholders(text)
			.Select(key => key.StartsWith('$')
				? RuleValidator.PathError(key)
				: scope.MeasureNames.Contains(key) ? null : $"refers to undefined measure '{key}' in a placeholder.")
			.FirstOrDefault(error => error is not null);
}
