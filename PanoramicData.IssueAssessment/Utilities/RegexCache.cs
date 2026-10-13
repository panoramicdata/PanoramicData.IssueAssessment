using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Utilities;
/// <summary>
/// Thread-safe cache for compiled regular expressions.
/// </summary>
public static class RegexCache
{
	/// <summary>
	/// The longest a single match may run. Patterns run against ticket text, which is untrusted input, so a pattern
	/// prone to catastrophic backtracking must not be able to hang an assessment: a match that runs out of time throws
	/// <see cref="RegexMatchTimeoutException"/>, which the assessment reports as a failure.
	/// </summary>
	public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

	private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets a compiled regex from cache or compiles and stores it.
	/// </summary>
	/// <param name="pattern">Regex pattern to compile.</param>
	/// <returns>Compiled regex instance, with <see cref="MatchTimeout"/> as its match timeout.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is blank or not a valid regex.</exception>
	public static Regex GetOrAdd(string pattern)
	{
		if (string.IsNullOrWhiteSpace(pattern))
		{
			throw new ArgumentException("Regex pattern must be provided.", nameof(pattern));
		}

		return Cache.GetOrAdd(
			pattern,
			static key => new Regex(key, RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout));
	}
}
