using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Rules;

/// <summary>
/// Compiled, time-limited regular expressions for rules, case-insensitive unless asked otherwise.
/// </summary>
internal static class RuleRegex
{
	/// <summary>
	/// The regex for <paramref name="pattern"/>, from <see cref="RegexCache"/>, so it has the same match timeout.
	/// </summary>
	public static Regex Get(string pattern, bool caseSensitive)
		=> RegexCache.GetOrAdd(caseSensitive ? pattern : "(?i)" + pattern);
}
