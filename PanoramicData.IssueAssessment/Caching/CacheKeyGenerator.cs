namespace PanoramicData.IssueAssessment.Caching;

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Generates stable content-based cache keys for issues and assessment options.
/// </summary>
/// <remarks>
/// Built-in definitions are described by their configuration, formatted with the invariant culture so a key does not
/// depend on the current culture. Delegates (callbacks, evaluators), gate conditions and custom definition types cannot
/// be described by value, so each instance is identified by a per-instance token: two different callbacks never share
/// a key, and the same instance always produces the same key for the lifetime of the process.
/// </remarks>
public static class CacheKeyGenerator
{
	private static readonly ConditionalWeakTable<object, string> InstanceTokens = [];

	/// <summary>
	/// Generates a cache key from issue content and options configuration.
	/// </summary>
	/// <param name="issue">Issue document.</param>
	/// <param name="options">Assessment options.</param>
	/// <returns>Combined cache key hash.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="issue"/> or <paramref name="options"/> is null.</exception>
	public static string Generate(JsonDocument issue, IAssessmentOptions options)
	{
		ArgumentNullException.ThrowIfNull(issue);
		ArgumentNullException.ThrowIfNull(options);

		string issueHash = Hash(issue.RootElement.GetRawText());
		string optionsHash = Hash(BuildOptionsFingerprint(options));
		return $"{issueHash}:{optionsHash}";
	}

	private static string BuildOptionsFingerprint(IAssessmentOptions options)
	{
		CultureInfo invariant = CultureInfo.InvariantCulture;
		StringBuilder builder = new();
		builder.Append(invariant, $"exec={options.ExecutionStrategy};");
		builder.Append(invariant, $"failure={options.FailureHandling};");
		builder.Append(invariant, $"globalTimeout={options.GlobalTimeout?.Ticks};");
		builder.Append(invariant, $"captureVirtual={options.CaptureVirtualDocument};");

		foreach (AssessmentCategory category in options.Categories)
		{
			builder.Append(invariant, $"|cat:{category.Name}:{category.AssessmentExecutionStrategy}:{category.CategoryFailureHandling}:{category.CategoryTimeout?.Ticks}");
			builder.Append(invariant, $":gate={(category.SeriesGateCondition is null ? string.Empty : InstanceToken(category.SeriesGateCondition))}");
			foreach (Assessment assessment in category.Assessments)
			{
				builder.Append(invariant, $"|asm:{assessment.Name}:{assessment.Weight:R}:{assessment.AssessmentTimeout?.Ticks}:{assessment.AllowVirtualExtension}:{assessment.VirtualOutputName}");
				builder.Append(invariant, $":def={DescribeDefinition(assessment.Definition)}");
			}
		}

		return builder.ToString();
	}

	private static string DescribeDefinition(IAssessmentDefinition definition)
	{
		CultureInfo invariant = CultureInfo.InvariantCulture;
		return definition switch
		{
			PresenceAssessment value => string.Create(invariant, $"Presence:{value.JsonPath}:{value.PresentScore:R}:{value.AbsentScore:R}:{value.ExplanationTemplate}"),
			ValueAssessment value => string.Create(invariant, $"{value.GetType().Name}:{value.JsonPath}:{value.SuccessScore:R}:{value.FailureScore:R}:{value.ExplanationTemplate}:{value.DescribeSettings()}"),
			JsonPathSelectorAssessment value => string.Create(invariant, $"JsonPathSelector:{value.JsonPath}:{value.SuccessScore:R}:{value.FailureScore:R}:{value.ExplanationTemplate}:{InstanceToken(value.Evaluator)}"),
			CustomCallbackAssessment value => string.Create(invariant, $"CustomCallback:{value.ExplanationTemplate}:{InstanceToken(value.Callback)}"),
			_ => $"{definition.GetType().FullName}:{InstanceToken(definition)}",
		};
	}

	private static string InstanceToken(object instance)
		=> InstanceTokens.GetValue(instance, static _ => Guid.NewGuid().ToString("N"));

	private static string Hash(string input)
	{
		byte[] utf8 = Encoding.UTF8.GetBytes(input);
		byte[] hash = SHA256.HashData(utf8);
		return Convert.ToHexString(hash);
	}
}
