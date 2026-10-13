namespace PanoramicData.IssueAssessment.Caching;

/// <summary>
/// Settings for <see cref="InMemoryAssessmentCache"/>.
/// </summary>
public sealed record InMemoryAssessmentCacheOptions
{
	/// <summary>
	/// Gets the maximum number of entries; when exceeded, the least recently used entries are evicted. Default 1000.
	/// </summary>
	public int MaxEntries { get; init; } = 1000;

	/// <summary>
	/// Gets how long an entry stays valid after it is stored, or null (the default) for no expiry.
	/// </summary>
	public TimeSpan? TimeToLive { get; init; }

	/// <summary>
	/// Gets the clock used for expiry and recency. Defaults to <see cref="TimeProvider.System"/>.
	/// </summary>
	public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
