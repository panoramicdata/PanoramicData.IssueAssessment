namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// How the <c>h2.</c> sections of a ticket are laid out in <c>{panel}</c> blocks; see <see cref="IssueText.PanelLayoutOf"/>.
/// </summary>
/// <param name="Panelable">Headings of sections that are panelled, or could be: they have content and no table or code.</param>
/// <param name="Unpanelled">Headings of sections that could be panelled but are not.</param>
/// <param name="Titled">Headings of sections whose panel carries a <c>title=</c> that only repeats the heading.</param>
public sealed record PanelLayout(IReadOnlyList<string> Panelable, IReadOnlyList<string> Unpanelled, IReadOnlyList<string> Titled)
{
	/// <summary>Gets a layout with no sections.</summary>
	public static PanelLayout Empty { get; } = new([], [], []);

	/// <summary>Gets how many of the panelable sections are panelled.</summary>
	public int Panelled => Panelable.Count - Unpanelled.Count;
}
