namespace PanoramicData.IssueAssessment.Caching;

using System.Collections.Concurrent;

/// <summary>
/// Thread-safe in-memory assessment result cache with optional TTL and bounded entry count.
/// </summary>
/// <remarks>Cached results are shared instances: treat them as read-only, and do not dispose their documents.</remarks>
public class InMemoryAssessmentCache : IAssessmentCacheProvider
{
	private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
	private readonly object _sync = new();
	private readonly int _maxEntries;
	private readonly TimeSpan? _ttl;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new cache with the default settings: up to 1000 entries and no expiry.
	/// </summary>
	public InMemoryAssessmentCache()
		: this(new InMemoryAssessmentCacheOptions())
	{
	}

	/// <summary>
	/// Initializes a new cache with the given settings.
	/// </summary>
	/// <param name="options">Cache settings.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or its time provider is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the maximum entry count or time-to-live is not positive.</exception>
	public InMemoryAssessmentCache(InMemoryAssessmentCacheOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(options.TimeProvider, nameof(options));
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxEntries, nameof(options));

		if (options.TimeToLive <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(options), "TimeToLive must be greater than zero when provided.");
		}

		_maxEntries = options.MaxEntries;
		_ttl = options.TimeToLive;
		_timeProvider = options.TimeProvider;
	}

	/// <inheritdoc />
	public Task<IAssessmentResult?> GetAsync(JsonDocument issue, IAssessmentOptions options, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		string key = CacheKeyGenerator.Generate(issue, options);

		if (!_entries.TryGetValue(key, out CacheEntry? entry))
		{
			return Task.FromResult<IAssessmentResult?>(null);
		}

		if (IsExpired(entry, Now()))
		{
			_entries.TryRemove(key, out _);
			return Task.FromResult<IAssessmentResult?>(null);
		}

		entry.LastAccessUtc = Now();
		return Task.FromResult<IAssessmentResult?>(entry.Result);
	}

	/// <inheritdoc />
	public Task<bool> SetAsync(JsonDocument issue, IAssessmentOptions options, IAssessmentResult result, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		string key = CacheKeyGenerator.Generate(issue, options);

		lock (_sync)
		{
			DateTime now = Now();
			_entries[key] = new CacheEntry(result, now, now);
			EvictExpiredEntries(now);

			while (_entries.Count > _maxEntries)
			{
				string keyToEvict = _entries.MinBy(pair => pair.Value.LastAccessUtc).Key;
				_entries.TryRemove(keyToEvict, out _);
			}
		}

		return Task.FromResult(true);
	}

	/// <inheritdoc />
	public Task ClearAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_entries.Clear();
		return Task.CompletedTask;
	}

	private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

	private void EvictExpiredEntries(DateTime now)
	{
		foreach ((string key, CacheEntry entry) in _entries)
		{
			if (IsExpired(entry, now))
			{
				_entries.TryRemove(key, out _);
			}
		}
	}

	private bool IsExpired(CacheEntry entry, DateTime now) => now - entry.CreatedUtc > _ttl;

	private sealed record CacheEntry(IAssessmentResult Result, DateTime CreatedUtc, DateTime LastAccessUtc)
	{
		public DateTime LastAccessUtc { get; set; } = LastAccessUtc;
	}
}
