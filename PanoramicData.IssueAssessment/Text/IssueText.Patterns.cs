using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Text;

/// <summary>
/// The regular expressions the readers use.
/// </summary>
public static partial class IssueText
{
	private const RegexOptions Options = RegexOptions.CultureInvariant;
	private static readonly TimeSpan Timeout = RegexCache.MatchTimeout;

	private static readonly Regex WikiHeadingAnywhere = new(@"(?im)^[ \t]*h[1-6]\.", Options, Timeout);
	private static readonly Regex WikiHeadingLevel = new(@"(?im)^[ \t]*h([1-6])\.", Options, Timeout);
	private static readonly Regex AtxHeadingLevel = new(@"(?im)^[ \t]*(#{1,6})[ \t]", Options, Timeout);
	private static readonly Regex StartsWithHash = new(@"^[ \t]*#", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithSingleHashItem = new(@"^[ \t]*#[ \t]", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex HashListLine = new(@"^[ \t]*#+[ \t]", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithWiki = new(@"^[ \t]*h[1-6]\.", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithDoubleAsterisk = new(@"^[ \t]*\*\*", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex StartsWithAsterisk = new(@"^[ \t]*\*", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex LineBreak = new(@"\r?\n", Options, Timeout);

	private static readonly Regex WikiNext = new(@"(?im)^[ \t]*(?:h[1-6]\.|#{2,6}[ \t])", Options, Timeout);
	private static readonly Regex AtxNext = new(@"(?im)^[ \t]*(?:h[1-6]\.|#{1,6}[ \t])", Options, Timeout);
	private static readonly Regex Bold2Next = new(@"(?im)^[ \t]*(?:h[1-6]\.|#{1,6}[ \t]|\*{1,2}[^\r\n*]+\*{1,2}[ \t]*\r?$)", Options, Timeout);
	private static readonly Regex Bold1Next = new(@"(?im)^[ \t]*(?:h[1-6]\.|\*{1,2}[^\r\n*]+\*{1,2}[ \t]*\r?$)", Options, Timeout);
	private static readonly Regex LabelNext = new(@"(?im)^[ \t]*(?:h[1-6]\.|\*{1,2}[^\r\n*]+\*{1,2}[ \t]*\r?$|(?=[A-Za-z])[^\r\n:#*]{1,60}:[ \t]*\r?$)", Options, Timeout);

	private static readonly Regex CriteriaHeadingLine = new(@"^[ \t]*(?:h([1-6])\.|(#{1,6})[ \t]|\*{1,2}(?=[^*\r\n]+\*{1,2}[ \t]*:?[ \t]*$))(.*)$", Options, Timeout);
	private static readonly Regex CriteriaHeadingText = new(@"(?i)criteria|definition\s+of\s+done|verification\s+evidence", Options, Timeout);
	private static readonly Regex CriteriaMappingTable = new(@"(?im)^[ \t]*\|.*criteri.*\|.*\btests?\b", Options, Timeout);

	private static readonly Regex PanelSectionHeading = new(@"^[ \t]*h([12])\.(.*)$", Options, Timeout);
	private static readonly Regex PanelOpening = new(@"^[ \t]*\{panel(?::([^}]*))?\}", Options | RegexOptions.IgnoreCase, Timeout);
	private static readonly Regex PanelBlocker = new(@"^[ \t]*\||\{(?:code|noformat)\b", Options | RegexOptions.IgnoreCase, Timeout);

	private static readonly Regex PlaceholderListMarker = new(@"(?m)^[ \t]*(?:[-*#>]+|\d+\.)[ \t]*", Options, Timeout);
	private static readonly Regex PlaceholderPunctuation = new(@"[\*_`{}\[\]\(\)\|:;,\.\-–—/\\]", Options, Timeout);
	private static readonly Regex PlaceholderWords = new(@"^\s*(tbd|to\s*be\s*(done|decided|determined|confirmed|advised)|n\s*/?\s*a|none|nil|todo|to\s*do|pending|unknown|see\s+above)\s*$", Options | RegexOptions.IgnoreCase, Timeout);

	private static readonly Regex Whitespace = new(@"\s+", Options, Timeout);
	private static readonly Regex Alphanumeric = new("[A-Za-z0-9]", Options, Timeout);
}
