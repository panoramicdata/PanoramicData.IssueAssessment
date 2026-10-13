using PanoramicData.IssueAssessment.Caching;
using static PanoramicData.IssueAssessment.Tests.Caching.CachingTestHelpers;

namespace PanoramicData.IssueAssessment.Tests.Caching;

/// <summary>
/// Tests for <see cref="InMemoryAssessmentCache"/>.
/// </summary>
public class InMemoryAssessmentCacheTests : TestBase
{
	private static JsonDocument Issue(int number) => JsonDocument.Parse($$"""{"number":{{number}}}""");

	[Fact]
	public async Task GetAsync_Empty_ReturnsNull()
	{
		InMemoryAssessmentCache cache = new();
		using JsonDocument issue = Issue(1);

		(await cache.GetAsync(issue, Options(), CancellationToken)).Should().BeNull();
	}

	[Fact]
	public async Task SetAsync_ThenGetAsync_ReturnsStoredResult()
	{
		InMemoryAssessmentCache cache = new();
		AssessmentOptions options = Options();
		IAssessmentResult stored = Result("stored");
		using JsonDocument issue = Issue(1);
		using JsonDocument sameContent = Issue(1);

		(await cache.SetAsync(issue, options, stored, CancellationToken)).Should().BeTrue();

		(await cache.GetAsync(sameContent, options, CancellationToken)).Should().BeSameAs(stored);
	}

	[Fact]
	public async Task GetAsync_DifferentIssueOrOptions_Misses()
	{
		InMemoryAssessmentCache cache = new();
		AssessmentOptions options = Options();
		using JsonDocument issue = Issue(1);
		using JsonDocument other = Issue(2);
		await cache.SetAsync(issue, options, Result("stored"), CancellationToken);

		(await cache.GetAsync(other, options, CancellationToken)).Should().BeNull();
		(await cache.GetAsync(issue, Options(new PresenceAssessment { JsonPath = "$.other" }), CancellationToken)).Should().BeNull();
	}

	[Fact]
	public async Task SetAsync_SameKey_ReplacesEntry()
	{
		InMemoryAssessmentCache cache = new();
		AssessmentOptions options = Options();
		IAssessmentResult replacement = Result("second");
		using JsonDocument issue = Issue(1);

		await cache.SetAsync(issue, options, Result("first"), CancellationToken);
		await cache.SetAsync(issue, options, replacement, CancellationToken);

		(await cache.GetAsync(issue, options, CancellationToken)).Should().BeSameAs(replacement);
	}

	[Fact]
	public async Task GetAsync_AfterTtl_ExpiresAndRemovesEntry()
	{
		ManualTimeProvider clock = new();
		InMemoryAssessmentCache cache = new(ttl: TimeSpan.FromMinutes(5), timeProvider: clock);
		AssessmentOptions options = Options();
		using JsonDocument issue = Issue(1);
		await cache.SetAsync(issue, options, Result("stored"), CancellationToken);

		clock.Advance(TimeSpan.FromMinutes(5));
		(await cache.GetAsync(issue, options, CancellationToken)).Should().NotBeNull("an entry exactly at its TTL is still fresh");

		clock.Advance(TimeSpan.FromTicks(1));
		(await cache.GetAsync(issue, options, CancellationToken)).Should().BeNull();
	}

	[Fact]
	public async Task GetAsync_NoTtl_NeverExpires()
	{
		ManualTimeProvider clock = new();
		InMemoryAssessmentCache cache = new(timeProvider: clock);
		AssessmentOptions options = Options();
		using JsonDocument issue = Issue(1);
		await cache.SetAsync(issue, options, Result("stored"), CancellationToken);

		clock.Advance(TimeSpan.FromDays(3650));

		(await cache.GetAsync(issue, options, CancellationToken)).Should().NotBeNull();
	}

	[Fact]
	public async Task SetAsync_WithTtl_EvictsOtherExpiredEntries()
	{
		ManualTimeProvider clock = new();
		InMemoryAssessmentCache cache = new(maxEntries: 2, ttl: TimeSpan.FromMinutes(1), timeProvider: clock);
		AssessmentOptions options = Options();
		using JsonDocument first = Issue(1);
		using JsonDocument second = Issue(2);
		using JsonDocument third = Issue(3);
		IAssessmentResult secondResult = Result("second");

		await cache.SetAsync(first, options, Result("first"), CancellationToken);
		clock.Advance(TimeSpan.FromMinutes(2));
		await cache.SetAsync(second, options, secondResult, CancellationToken);
		await cache.SetAsync(third, options, Result("third"), CancellationToken);

		// The expired first entry made room, so nothing fresh was evicted to stay within two entries.
		(await cache.GetAsync(second, options, CancellationToken)).Should().BeSameAs(secondResult);
		(await cache.GetAsync(third, options, CancellationToken)).Should().NotBeNull();
		(await cache.GetAsync(first, options, CancellationToken)).Should().BeNull();
	}

	[Fact]
	public async Task SetAsync_OverCapacity_EvictsLeastRecentlyUsed()
	{
		ManualTimeProvider clock = new();
		InMemoryAssessmentCache cache = new(maxEntries: 2, timeProvider: clock);
		AssessmentOptions options = Options();
		using JsonDocument first = Issue(1);
		using JsonDocument second = Issue(2);
		using JsonDocument third = Issue(3);

		await cache.SetAsync(first, options, Result("first"), CancellationToken);
		clock.Advance(TimeSpan.FromSeconds(1));
		await cache.SetAsync(second, options, Result("second"), CancellationToken);
		clock.Advance(TimeSpan.FromSeconds(1));
		await cache.GetAsync(first, options, CancellationToken);
		clock.Advance(TimeSpan.FromSeconds(1));
		await cache.SetAsync(third, options, Result("third"), CancellationToken);

		(await cache.GetAsync(second, options, CancellationToken)).Should().BeNull("it was used least recently");
		(await cache.GetAsync(first, options, CancellationToken)).Should().NotBeNull();
		(await cache.GetAsync(third, options, CancellationToken)).Should().NotBeNull();
	}

	[Fact]
	public async Task ClearAsync_RemovesEverything()
	{
		InMemoryAssessmentCache cache = new();
		AssessmentOptions options = Options();
		using JsonDocument issue = Issue(1);
		await cache.SetAsync(issue, options, Result("stored"), CancellationToken);

		await cache.ClearAsync(CancellationToken);

		(await cache.GetAsync(issue, options, CancellationToken)).Should().BeNull();
	}

	[Fact]
	public async Task Operations_CancelledToken_Throw()
	{
		InMemoryAssessmentCache cache = new();
		using JsonDocument issue = Issue(1);
		using CancellationTokenSource cancelled = new();
		await cancelled.CancelAsync();

		Func<Task> get = () => cache.GetAsync(issue, Options(), cancelled.Token);
		Func<Task> set = () => cache.SetAsync(issue, Options(), Result("x"), cancelled.Token);
		Func<Task> clear = () => cache.ClearAsync(cancelled.Token);

		await get.Should().ThrowAsync<OperationCanceledException>();
		await set.Should().ThrowAsync<OperationCanceledException>();
		await clear.Should().ThrowAsync<OperationCanceledException>();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_NonPositiveMaxEntries_Throws(int maxEntries)
	{
		Action act = () => _ = new InMemoryAssessmentCache(maxEntries);

		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(maxEntries));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Constructor_NonPositiveTtl_Throws(int seconds)
	{
		Action act = () => _ = new InMemoryAssessmentCache(ttl: TimeSpan.FromSeconds(seconds));

		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("ttl");
	}

	[Fact]
	public async Task ConcurrentUse_KeepsWithinCapacityAndDoesNotThrow()
	{
		InMemoryAssessmentCache cache = new(maxEntries: 10);
		AssessmentOptions options = Options();

		await Parallel.ForAsync(0, 200, CancellationToken, async (index, token) =>
		{
			using JsonDocument issue = Issue(index % 25);
			await cache.SetAsync(issue, options, Result("r"), token);
			await cache.GetAsync(issue, options, token);
		});

		int hits = 0;
		for (int index = 0; index < 25; index++)
		{
			using JsonDocument issue = Issue(index);
			if (await cache.GetAsync(issue, options, CancellationToken) is not null)
			{
				hits++;
			}
		}

		hits.Should().BeInRange(1, 10);
	}

	[Fact]
	public async Task IssueAssessor_WithCache_ReturnsCachedResultOnSecondCall()
	{
		InMemoryAssessmentCache cache = new();
		IssueAssessor assessor = new(Options(), cache);
		using JsonDocument issue = JsonDocument.Parse("""{"title":"x"}""");

		IAssessmentResult first = await assessor.AssessAsync(issue, CancellationToken);
		IAssessmentResult second = await assessor.AssessAsync(issue, CancellationToken);

		second.Should().BeSameAs(first);
	}
}
