using System.Globalization;

namespace PanoramicData.IssueAssessment.Tests.Assessments;

/// <summary>
/// Shared helpers for the assessment definition tests.
/// </summary>
internal static class AssessmentTestHelpers
{
	/// <summary>
	/// Parses <paramref name="json"/> and executes <paramref name="definition"/> against it with no virtual extension.
	/// </summary>
	public static async Task<AssessmentExecutionResult> ExecuteAsync(IAssessmentDefinition definition, string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		return await definition.ExecuteAsync(document, null, TestContext.Current.CancellationToken);
	}

	/// <summary>
	/// Executes <paramref name="definition"/> with a token that is already cancelled.
	/// </summary>
	public static async Task ExecuteWithCancelledTokenAsync(IAssessmentDefinition definition, string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		using CancellationTokenSource cancellationTokenSource = new();
		await cancellationTokenSource.CancelAsync();

		await definition.ExecuteAsync(document, null, cancellationTokenSource.Token);
	}

	/// <summary>
	/// Executes <paramref name="definition"/> with a null input document.
	/// </summary>
	public static Task ExecuteWithNullInputAsync(IAssessmentDefinition definition)
		=> definition.ExecuteAsync(null!, null, TestContext.Current.CancellationToken);

	/// <summary>
	/// Switches the current culture and UI culture until the returned scope is disposed.
	/// </summary>
	public static CultureScope UseCulture(string name) => new(CultureInfo.GetCultureInfo(name));

	/// <summary>
	/// Restores the previous culture on dispose.
	/// </summary>
	internal sealed class CultureScope : IDisposable
	{
		private readonly CultureInfo _previousCulture;
		private readonly CultureInfo _previousUiCulture;

		public CultureScope(CultureInfo culture)
		{
			_previousCulture = CultureInfo.CurrentCulture;
			_previousUiCulture = CultureInfo.CurrentUICulture;
			CultureInfo.CurrentCulture = culture;
			CultureInfo.CurrentUICulture = culture;
		}

		public void Dispose()
		{
			CultureInfo.CurrentCulture = _previousCulture;
			CultureInfo.CurrentUICulture = _previousUiCulture;
		}
	}
}
