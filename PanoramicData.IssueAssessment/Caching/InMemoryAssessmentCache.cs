namespace PanoramicData.IssueAssessment.Caching;

using System.Collections.Concurrent;

/// <summary>
/// Thread-safe in-memory assessment result cache with optional TTL and bounded entry count.
/// </summary>
public class InMemoryAssessmentCache : IAssessmentCacheProvider
{
	private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
	private readonly object _sync = new();
	private readonly int _maxEntries;
	private readonly TimeSpan? _ttl;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new cache instance.
	/// </summary>
	/// <param name="maxEntries">Maximum number of entries before LRU eviction.</param>
	/// <param name="ttl">Optional time-to-live for each entry.</param>
	/// <param name="timeProvider">Clock used for expiry and recency; defaults to <see cref="TimeProvider.System"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxEntries"/> or <paramref name="ttl"/> is not positive.</exception>
	public InMemoryAssessmentCache(int maxEntries = 1000, TimeSpan? ttl = null, TimeProvider? timeProvider = null)
	{
		if (maxEntries <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxEntries), "Max entries must be greater than zero.");
		}

		if (ttl.HasValue && ttl.Value <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero when provided.");
		}

		_maxEntries = maxEntries;
		_ttl = ttl;
		_timeProvider = timeProvider ?? TimeProvider.System;
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

		if (IsExpired(entry))
		{
			_entries.TryRemove(key, out _);
			return Task.FromResult<IAssessmentResult?>(null);
		}

		entry.LastAccessUtc = _timeProvider.GetUtcNow().UtcDateTime;
		return Task.FromResult<IAssessmentResult?>(entry.Result);
	}

	/// <inheritdoc />
	public Task<bool> SetAsync(JsonDocument issue, IAssessmentOptions options, IAssessmentResult result, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		string key = CacheKeyGenerator.Generate(issue, options);

		lock (_sync)
		{
			DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
			_entries[key] = new CacheEntry(result, now, now);
			EvictExpiredEntries();

			while (_entries.Count > _maxEntries)
			{
				(string keyToEvict, CacheEntry _) = _entries
					.OrderBy(entry => entry.Value.LastAccessUtc)
					.First();

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

	private void EvictExpiredEntries()
	{
		if (!_ttl.HasValue)
		{
			return;
		}

		DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
		foreach ((string key, CacheEntry entry) in _entries)
		{
			if (now - entry.CreatedUtc > _ttl.Value)
			{
				_entries.TryRemove(key, out _);
			}
		}
	}

	private bool IsExpired(CacheEntry entry)
	{
		if (!_ttl.HasValue)
		{
			return false;
		}

		return _timeProvider.GetUtcNow().UtcDateTime - entry.CreatedUtc > _ttl.Value;
	}

	private sealed record CacheEntry(IAssessmentResult Result, DateTime CreatedUtc, DateTime LastAccessUtc)
	{
		public DateTime LastAccessUtc { get; set; } = LastAccessUtc;
	}
}
