namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// Where a rule reads text from: one or more fields joined by line breaks, optionally narrowed to the body under a
/// heading.
/// </summary>
/// <example>
/// The summary and description together: <c>new TextSource { Paths = ["$.title", "$.description"] }</c>.
/// The steps-to-reproduce section of the description:
/// <c>new TextSource { Paths = ["$.description"], Section = @"(?:reproduc|\brepro\b)" }</c>.
/// </example>
public sealed record TextSource
{
	/// <summary>
	/// Gets the JSONPaths whose values are joined, in order, with a line break. A missing value contributes an empty
	/// line; an array contributes its elements, one per line.
	/// </summary>
	public required ImmutableList<string> Paths { get; init; }

	/// <summary>
	/// Gets an optional regular expression fragment matched against heading text. When set, the text is the body under
	/// the best matching heading (see <see cref="Text.IssueText.SectionBody"/>), or absent when no heading matches.
	/// </summary>
	public string? Section { get; init; }

	/// <summary>
	/// A key identifying this source's text within one evaluation, so equal sources share one read.
	/// </summary>
	internal string Key => string.Join('\u001f', Paths) + '\u001e' + Section;
}
