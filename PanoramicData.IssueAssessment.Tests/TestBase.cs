namespace PanoramicData.IssueAssessment.Tests;

/// <summary>
/// Base test class
/// </summary>
public abstract class TestBase
{
	/// <summary>
	/// Provides a cancellation token for test methods, which can be used to handle timeouts or cancellations in asynchronous operations during testing.
	/// </summary>
	protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}