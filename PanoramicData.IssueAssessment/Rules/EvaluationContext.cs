using System.Collections.Concurrent;
using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// What rules (<see cref="ICondition"/> and <see cref="IMeasure"/>) can see while one issue is assessed: the issue, the
/// virtual extension, the rubric's named conditions, the measures of the assessment being scored and, once scoring is
/// done, the assessment results.
/// </summary>
/// <remarks>
/// Text read from a <see cref="TextSource"/> and the value of each named condition are computed once per issue and
/// shared, so rules that scan the same text do not each rebuild it. Named conditions are always evaluated against the
/// issue itself, never a virtual extension, so their value does not depend on the order assessments run in.
/// </remarks>
public sealed class EvaluationContext
{
	private readonly Shared _shared;
	private readonly ImmutableDictionary<string, IMeasure> _measures;
	private readonly ConcurrentDictionary<string, double> _measureValues = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, string?> _texts;

	/// <summary>
	/// Initializes a context for <paramref name="issue"/> with no named conditions.
	/// </summary>
	/// <param name="issue">The issue being assessed.</param>
	public EvaluationContext(JsonDocument issue)
		: this(issue, ImmutableDictionary<string, ICondition>.Empty)
	{
	}

	/// <summary>
	/// Initializes a context for <paramref name="issue"/> with the rubric's named conditions.
	/// </summary>
	/// <param name="issue">The issue being assessed.</param>
	/// <param name="namedConditions">Conditions that rules refer to by name.</param>
	/// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
	public EvaluationContext(JsonDocument issue, IReadOnlyDictionary<string, ICondition> namedConditions)
	{
		ArgumentNullException.ThrowIfNull(issue);
		ArgumentNullException.ThrowIfNull(namedConditions);
		_shared = new Shared(issue, namedConditions);
		_measures = ImmutableDictionary<string, IMeasure>.Empty;
		_texts = _shared.IssueTexts;
		_shared.Root = this;
	}

	private EvaluationContext(
		Shared shared,
		JsonDocument? virtualExtension,
		ImmutableDictionary<string, IMeasure> measures,
		IReadOnlyDictionary<string, AssessmentResult>? assessmentResults)
	{
		_shared = shared;
		VirtualExtension = virtualExtension;
		_measures = measures;
		AssessmentResults = assessmentResults;
		_texts = virtualExtension is null ? shared.IssueTexts : new(StringComparer.Ordinal);
	}

	/// <summary>
	/// Gets the issue being assessed.
	/// </summary>
	public JsonDocument Issue => _shared.Issue;

	/// <summary>
	/// Gets the virtual extension produced by earlier assessments in a series, if any.
	/// </summary>
	public JsonDocument? VirtualExtension { get; }

	/// <summary>
	/// Gets the results of every scored assessment, by name, once scoring is complete; null while assessments run.
	/// Only score caps and classifications see them.
	/// </summary>
	public IReadOnlyDictionary<string, AssessmentResult>? AssessmentResults { get; }

	/// <summary>
	/// Evaluates a named condition, once per issue.
	/// </summary>
	/// <param name="name">The condition's name in the rubric.</param>
	/// <returns>The condition's value.</returns>
	/// <exception cref="KeyNotFoundException">Thrown when the rubric defines no condition with that name.</exception>
	public bool Condition(string name)
	{
		if (!_shared.NamedConditions.TryGetValue(name, out ICondition? condition))
		{
			throw new KeyNotFoundException($"No condition named '{name}' is defined.");
		}

		return _shared.ConditionValues.GetOrAdd(name, _ => condition.Evaluate(_shared.Root));
	}

	/// <summary>
	/// Evaluates a measure of the assessment being scored, once per assessment.
	/// </summary>
	/// <param name="name">The measure's name in the assessment.</param>
	/// <returns>The measured value.</returns>
	/// <exception cref="KeyNotFoundException">Thrown when the assessment defines no measure with that name.</exception>
	public double Measure(string name)
	{
		if (!_measures.TryGetValue(name, out IMeasure? measure))
		{
			throw new KeyNotFoundException($"No measure named '{name}' is defined.");
		}

		return _measureValues.GetOrAdd(name, _ => measure.Evaluate(this));
	}

	/// <summary>
	/// Reads the text a <see cref="TextSource"/> describes: its fields joined by line breaks, narrowed to a section when
	/// one is given.
	/// </summary>
	/// <param name="source">The source to read.</param>
	/// <returns>The text; null only when a section was asked for and no heading matches.</returns>
	public string? Text(TextSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		return _texts.GetOrAdd(source.Key, _ =>
		{
			string joined = string.Join('\n', source.Paths.Select(path => string.Join('\n', Strings(path))));
			return source.Section is null ? joined : IssueText.SectionBody(joined, source.Section);
		});
	}

	/// <summary>
	/// The string values at <paramref name="jsonPath"/>: one for a scalar, one per element for an array, none when the
	/// value is missing or null. Numbers and booleans are returned as their JSON text.
	/// </summary>
	/// <param name="jsonPath">The JSONPath to read.</param>
	/// <returns>The values, in order.</returns>
	public IReadOnlyList<string> Strings(string jsonPath)
	{
		if (!TrySelect(jsonPath, out JsonElement value))
		{
			return [];
		}

		return value.ValueKind == JsonValueKind.Array
			? [.. value.EnumerateArray().Select(ToText).OfType<string>()]
			: ToText(value) is { } text ? [text] : [];
	}

	/// <summary>
	/// Selects the value at <paramref name="jsonPath"/> from the issue, or from the virtual extension when the issue
	/// does not contain it.
	/// </summary>
	public bool TrySelect(string jsonPath, out JsonElement value)
		=> JsonPathHelper.TrySelect(Issue, VirtualExtension, jsonPath, out value);

	/// <summary>
	/// A context that reads the given virtual extension and shares this one's issue, named conditions and caches.
	/// </summary>
	internal EvaluationContext WithVirtualExtension(JsonDocument? virtualExtension)
		=> virtualExtension is null && AssessmentResults is null && _measures.IsEmpty
			? _shared.Root
			: new(_shared, virtualExtension, ImmutableDictionary<string, IMeasure>.Empty, null);

	/// <summary>
	/// A context in which <paramref name="measures"/> can be referred to by name.
	/// </summary>
	internal EvaluationContext WithMeasures(ImmutableDictionary<string, IMeasure> measures)
		=> new(_shared, VirtualExtension, measures, AssessmentResults);

	/// <summary>
	/// A context that can see the finished assessment results, for score caps and classifications.
	/// </summary>
	internal EvaluationContext WithAssessmentResults(IReadOnlyDictionary<string, AssessmentResult> results)
		=> new(_shared, null, ImmutableDictionary<string, IMeasure>.Empty, results);

	/// <summary>
	/// The value of every named condition, for reporting.
	/// </summary>
	internal ImmutableDictionary<string, bool> AllConditions()
		=> _shared.NamedConditions.Keys.ToImmutableDictionary(name => name, Condition, StringComparer.Ordinal);

	private static string? ToText(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.String => element.GetString(),
		JsonValueKind.Null or JsonValueKind.Undefined => null,
		_ => element.GetRawText(),
	};

	private sealed class Shared(JsonDocument issue, IReadOnlyDictionary<string, ICondition> namedConditions)
	{
		public JsonDocument Issue { get; } = issue;

		public IReadOnlyDictionary<string, ICondition> NamedConditions { get; } = namedConditions;

		public ConcurrentDictionary<string, bool> ConditionValues { get; } = new(StringComparer.Ordinal);

		public ConcurrentDictionary<string, string?> IssueTexts { get; } = new(StringComparer.Ordinal);

		public EvaluationContext Root { get; set; } = null!;
	}
}
