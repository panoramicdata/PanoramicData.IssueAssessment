using System.Text.RegularExpressions;

namespace PanoramicData.IssueAssessment.Tests.Utilities;

/// <summary>
/// Tests for <see cref="RegexCache"/>.
/// </summary>
public class RegexCacheTests
{
	[Fact]
	public void GetOrAdd_SamePattern_ReturnsSameInstance()
	{
		Regex first = RegexCache.GetOrAdd("^cache-test-[0-9]+$");
		Regex second = RegexCache.GetOrAdd("^cache-test-[0-9]+$");

		second.Should().BeSameAs(first);
		first.IsMatch("cache-test-42").Should().BeTrue();
	}

	[Fact]
	public void GetOrAdd_DifferentPatterns_ReturnsDifferentInstances()
	{
		RegexCache.GetOrAdd("^a$").Should().NotBeSameAs(RegexCache.GetOrAdd("^b$"));
	}

	[Fact]
	public void GetOrAdd_Always_SetsMatchTimeoutAndCultureInvariance()
	{
		Regex regex = RegexCache.GetOrAdd("timeout-test");

		regex.MatchTimeout.Should().Be(RegexCache.MatchTimeout);
		regex.Options.Should().HaveFlag(RegexOptions.CultureInvariant);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void GetOrAdd_BlankPattern_Throws(string? pattern)
	{
		Action act = () => RegexCache.GetOrAdd(pattern!);

		act.Should().Throw<ArgumentException>().WithParameterName(nameof(pattern));
	}

	[Fact]
	public void GetOrAdd_InvalidPattern_ThrowsArgumentException()
	{
		Action act = () => RegexCache.GetOrAdd("([unclosed");

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public async Task RegexAssessment_CatastrophicBacktracking_TimesOutInsteadOfHanging()
	{
		// (a+)+$ backtracks exponentially on a run of a's followed by a non-matching character.
		RegexAssessment assessment = new() { JsonPath = "$.text", Pattern = "^(a+)+$", SuccessScore = 1d, FailureScore = 0d };
		Assessment configured = new() { Name = "ReDoS", Description = "ReDoS", Definition = assessment };
		IssueAssessor assessor = new(new AssessmentOptions
		{
			Categories = [new AssessmentCategory { Name = "C", Description = "C", Assessments = [configured], AssessmentExecutionStrategy = AssessmentExecutionStrategy.Series }],
			ExecutionStrategy = CategoryExecutionStrategy.Series,
		});
		using JsonDocument issue = JsonDocument.Parse(JsonSerializer.Serialize(new { text = new string('a', 40) + "!" }));

		IAssessmentResult result = await assessor.AssessAsync(issue, TestContext.Current.CancellationToken);

		AssessmentFailure failure = result.Failures.Should().ContainSingle().Subject;
		failure.Reason.Should().Be(AssessmentFailureReason.ThrowingException);
		failure.InnerException.Should().BeOfType<RegexMatchTimeoutException>();
	}
}
