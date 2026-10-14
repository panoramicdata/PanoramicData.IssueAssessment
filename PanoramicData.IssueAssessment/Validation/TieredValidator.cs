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
		if (tier.Finding is null)
		{
			return "has a tier without a finding; use an empty finding for a tier that only recommends.";
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
		=> PlaceholderText.Keys(text).Select(key => PlaceholderKeyError(key, scope)).FirstOrDefault(error => error is not null);

	private static string? PlaceholderKeyError(string key, RuleScope scope)
	{
		if (key.StartsWith('$'))
		{
			int tilde = key.IndexOf('~', StringComparison.Ordinal);
			return tilde < 0 ? RuleValidator.PathError(key) : RuleValidator.PathError(key[..tilde]) ?? RuleValidator.PatternError(key[(tilde + 1)..]);
		}

		string name = key.EndsWith(":text", StringComparison.Ordinal) ? key[..^":text".Length] : key;
		return scope.MeasureNames.Contains(name) ? null : $"refers to undefined measure '{name}' in a placeholder.";
	}
}
