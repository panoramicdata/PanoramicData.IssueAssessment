
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Utilities;
/// <summary>
/// Thread-safe cache for compiled regular expressions.
/// </summary>
public static class RegexCache
{
	private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets a compiled regex from cache or compiles and stores it.
	/// </summary>
	/// <param name="pattern">Regex pattern to compile.</param>
	/// <returns>Compiled regex instance.</returns>
	public static Regex GetOrAdd(string pattern)
	{
		if (string.IsNullOrWhiteSpace(pattern))
		{
			throw new ArgumentException("Regex pattern must be provided.", nameof(pattern));
		}

		return Cache.GetOrAdd(pattern, static key => new Regex(key, RegexOptions.Compiled | RegexOptions.CultureInvariant));
	}
}
