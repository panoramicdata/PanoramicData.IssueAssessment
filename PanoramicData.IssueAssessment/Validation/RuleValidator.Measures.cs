namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// Validates measures and the text sources and pattern lists they read.
/// </summary>
internal static partial class RuleValidator
{
	public static string? MeasureError(IMeasure? measure) => measure switch
	{
		null => "has a missing measure.",
		PatternMatchCountMeasure value => SourceError(value.Source) ?? PatternsError(value.Patterns),
		OccurrenceCountMeasure value => SourceError(value.Source) ?? PatternError(value.Pattern),
		WordCountMeasure value => SourceError(value.Source),
		AlphanumericCountMeasure value => SourceError(value.Source),
		LengthMeasure value => SourceError(value.Source),
		CriteriaHeadingCountMeasure value => SourceError(value.Source),
		SectionPanelCountMeasure value => SourceError(value.Source),
		ItemCountMeasure value => PathError(value.Path),
		NumberMeasure value => PathError(value.Path),
		_ => null,
	};

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
}
