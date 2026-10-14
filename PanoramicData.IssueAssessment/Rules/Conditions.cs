using System.Text.Json.Serialization;
using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// A true-or-false question about an issue, such as "is it a bug?" or "does its description have a steps section?".
/// Conditions decide which tier an assessment scores, whether an assessment or category applies at all, which score
/// caps apply and how an issue is classified.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AllCondition), "all")]
[JsonDerivedType(typeof(AnyCondition), "any")]
[JsonDerivedType(typeof(NotCondition), "not")]
[JsonDerivedType(typeof(ConditionReference), "ref")]
[JsonDerivedType(typeof(MeasureCondition), "measure")]
[JsonDerivedType(typeof(TextMatchesCondition), "textMatches")]
[JsonDerivedType(typeof(FieldMatchesCondition), "fieldMatches")]
[JsonDerivedType(typeof(FieldEqualsCondition), "fieldEquals")]
[JsonDerivedType(typeof(FieldPresentCondition), "fieldPresent")]
[JsonDerivedType(typeof(SectionPresentCondition), "sectionPresent")]
[JsonDerivedType(typeof(PlaceholderCondition), "placeholder")]
[JsonDerivedType(typeof(AssessmentScoreCondition), "assessmentScore")]
public interface ICondition
{
	/// <summary>
	/// Evaluates the condition for the issue in <paramref name="context"/>.
	/// </summary>
	bool Evaluate(EvaluationContext context);
}

/// <summary>True when every one of <see cref="Conditions"/> is true (and when there are none).</summary>
public sealed record AllCondition : ICondition
{
	/// <summary>Gets the conditions that must all hold.</summary>
	public required ImmutableList<ICondition> Conditions { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context) => Conditions.TrueForAll(condition => condition.Evaluate(context));
}

/// <summary>True when at least one of <see cref="Conditions"/> is true.</summary>
public sealed record AnyCondition : ICondition
{
	/// <summary>Gets the conditions of which one must hold.</summary>
	public required ImmutableList<ICondition> Conditions { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context) => Conditions.Exists(condition => condition.Evaluate(context));
}

/// <summary>True when <see cref="Condition"/> is false.</summary>
public sealed record NotCondition : ICondition
{
	/// <summary>Gets the condition to negate.</summary>
	public required ICondition Condition { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context) => !Condition.Evaluate(context);
}

/// <summary>The value of a named condition defined in <see cref="AssessmentOptions.Conditions"/>.</summary>
public sealed record ConditionReference : ICondition
{
	/// <summary>Gets the name of the condition.</summary>
	public required string Name { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Condition(Name);
	}
}

/// <summary>
/// Compares a measure with bounds: true when it is at least <see cref="AtLeast"/> and less than
/// <see cref="LessThan"/>, ignoring a bound that is not set.
/// </summary>
/// <remarks>Set exactly one of <see cref="Measure"/> (a named measure of the assessment) or <see cref="Of"/>.</remarks>
public sealed record MeasureCondition : ICondition
{
	/// <summary>Gets the name of one of the assessment's measures.</summary>
	public string? Measure { get; init; }

	/// <summary>Gets a measure defined in place, for use outside a tiered assessment.</summary>
	public IMeasure? Of { get; init; }

	/// <summary>Gets the inclusive lower bound.</summary>
	public double? AtLeast { get; init; }

	/// <summary>Gets the exclusive upper bound.</summary>
	public double? LessThan { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		double value = Of?.Evaluate(context) ?? context.Measure(Measure!);
		return Bounds.Contain(value, AtLeast, LessThan);
	}
}

/// <summary>
/// The optional bounds conditions compare a value against.
/// </summary>
internal static class Bounds
{
	/// <summary>
	/// True when <paramref name="value"/> is at least <paramref name="atLeast"/> and less than
	/// <paramref name="lessThan"/>; an unset bound always holds.
	/// </summary>
	public static bool Contain(double value, double? atLeast, double? lessThan)
		=> (atLeast is not { } low || value >= low) && (lessThan is not { } high || value < high);
}

/// <summary>
/// True when the text matches any of <see cref="Patterns"/>, case-insensitively unless <see cref="CaseSensitive"/> is
/// set. An absent section matches nothing.
/// </summary>
public sealed record TextMatchesCondition : ICondition
{
	/// <summary>Gets the text to search.</summary>
	public required TextSource Source { get; init; }

	/// <summary>Gets the regular expressions, any of which may match.</summary>
	public required ImmutableList<string> Patterns { get; init; }

	/// <summary>Gets a value indicating whether matching is case-sensitive. Default false.</summary>
	public bool CaseSensitive { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Text(Source) is { } text && Patterns.Exists(pattern => RuleRegex.Get(pattern, CaseSensitive).IsMatch(text));
	}
}

/// <summary>
/// Tests the values at a JSONPath (a scalar, or each element of an array) against <see cref="Patterns"/>,
/// case-insensitively unless <see cref="CaseSensitive"/> is set.
/// </summary>
/// <remarks>
/// With <see cref="RequireAll"/> false (the default), true when any value matches any pattern. With it true, true when
/// there is at least one value and every value matches some pattern: for example, "every component is a back-end one".
/// </remarks>
public sealed record FieldMatchesCondition : ICondition
{
	/// <summary>Gets the JSONPath to read.</summary>
	public required string Path { get; init; }

	/// <summary>Gets the regular expressions to test values against.</summary>
	public required ImmutableList<string> Patterns { get; init; }

	/// <summary>Gets a value indicating whether every value, rather than any, must match. Default false.</summary>
	public bool RequireAll { get; init; }

	/// <summary>Gets a value indicating whether matching is case-sensitive. Default false.</summary>
	public bool CaseSensitive { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		IReadOnlyList<string> values = context.Strings(Path);
		bool Matches(string value) => Patterns.Exists(pattern => RuleRegex.Get(pattern, CaseSensitive).IsMatch(value));
		return RequireAll ? values.Count > 0 && values.All(Matches) : values.Any(Matches);
	}
}

/// <summary>
/// True when any value at a JSONPath (a scalar, or any element of an array) equals one of <see cref="Values"/>,
/// ignoring case unless <see cref="CaseSensitive"/> is set. Booleans and numbers compare by their JSON text
/// (<c>true</c>, <c>42</c>).
/// </summary>
public sealed record FieldEqualsCondition : ICondition
{
	/// <summary>Gets the JSONPath to read.</summary>
	public required string Path { get; init; }

	/// <summary>Gets the accepted values.</summary>
	public required ImmutableList<string> Values { get; init; }

	/// <summary>Gets a value indicating whether comparison is case-sensitive. Default false.</summary>
	public bool CaseSensitive { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		StringComparer comparer = CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
		return context.Strings(Path).Any(value => Values.Contains(value, comparer));
	}
}

/// <summary>
/// True when the JSONPath has a value: not missing, not null, not a blank string and not an empty array.
/// </summary>
public sealed record FieldPresentCondition : ICondition
{
	/// <summary>Gets the JSONPath to read.</summary>
	public required string Path { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Strings(Path).Any(value => !string.IsNullOrWhiteSpace(value));
	}
}

/// <summary>
/// True when the source's section heading is present, whatever is under it. The source must have a
/// <see cref="TextSource.Section"/>.
/// </summary>
public sealed record SectionPresentCondition : ICondition
{
	/// <summary>Gets the section to look for.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Text(Source) is not null;
	}
}

/// <summary>
/// True when the text is blank or only a placeholder such as TBD, N/A, none or to do (see
/// <see cref="IssueText.IsPlaceholder"/>). An absent section is a placeholder.
/// </summary>
public sealed record PlaceholderCondition : ICondition
{
	/// <summary>Gets the text to test.</summary>
	public required TextSource Source { get; init; }

	/// <inheritdoc />
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return IssueText.IsPlaceholder(context.Text(Source));
	}
}

/// <summary>
/// Compares another assessment's raw score with bounds, once scoring is complete. Only score caps and classifications
/// can use it. False when that assessment did not apply or was not scored.
/// </summary>
public sealed record AssessmentScoreCondition : ICondition
{
	/// <summary>Gets the name of the assessment.</summary>
	public required string Assessment { get; init; }

	/// <summary>Gets the inclusive lower bound on its raw score.</summary>
	public double? AtLeast { get; init; }

	/// <summary>Gets the exclusive upper bound on its raw score.</summary>
	public double? LessThan { get; init; }

	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">Thrown when used before scoring is complete.</exception>
	public bool Evaluate(EvaluationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (context.AssessmentResults is null)
		{
			throw new InvalidOperationException("An assessment score condition can only be used by score caps and classifications.");
		}

		return context.AssessmentResults.TryGetValue(Assessment, out AssessmentResult? result)
			&& result.Outcome != AssessmentOutcome.NotApplicable
			&& result.Score.RawScore is { } score
			&& Bounds.Contain(score, AtLeast, LessThan);
	}
}
