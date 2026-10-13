namespace PanoramicData.IssueAssessment.Caching;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Generates stable content-based cache keys for issues and assessment options.
/// </summary>
public static class CacheKeyGenerator
{
	/// <summary>
	/// Generates a cache key from issue content and options configuration.
	/// </summary>
	/// <param name="issue">Issue document.</param>
	/// <param name="options">Assessment options.</param>
	/// <returns>Combined cache key hash.</returns>
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
		StringBuilder builder = new();
		builder.Append($"exec={options.ExecutionStrategy};");
		builder.Append($"failure={options.FailureHandling};");
		builder.Append($"globalTimeout={options.GlobalTimeout?.Ticks};");
		builder.Append($"captureVirtual={options.CaptureVirtualDocument};");

		foreach (AssessmentCategory category in options.Categories)
		{
			builder.Append($"|cat:{category.Name}:{category.AssessmentExecutionStrategy}:{category.CategoryFailureHandling}:{category.CategoryTimeout?.Ticks}");
			foreach (Assessment assessment in category.Assessments)
			{
				builder.Append($"|asm:{assessment.Name}:{assessment.Weight}:{assessment.AssessmentTimeout?.Ticks}:{assessment.AllowVirtualExtension}:{assessment.VirtualOutputName}");
				builder.Append($":def={DescribeDefinition(assessment.Definition)}");
			}
		}

		return builder.ToString();
	}

	private static string DescribeDefinition(IAssessmentDefinition definition)
	{
		return definition switch
		{
			PresenceAssessment value => $"Presence:{value.JsonPath}:{value.PresentScore}:{value.AbsentScore}",
			RegexAssessment value => $"Regex:{value.JsonPath}:{value.Pattern}:{value.SuccessScore}:{value.FailureScore}",
			StringLengthAssessment value => $"StringLength:{value.JsonPath}:{value.MinimumLength}:{value.MaximumLength}:{value.SuccessScore}:{value.FailureScore}",
			WordCountAssessment value => $"WordCount:{value.JsonPath}:{value.MinimumWordCount}:{value.MaximumWordCount}:{value.SuccessScore}:{value.FailureScore}",
			NumericRangeAssessment value => $"NumericRange:{value.JsonPath}:{value.MinValue}:{value.MaxValue}:{value.SuccessScore}:{value.FailureScore}",
			DateRangeAssessment value => $"DateRange:{value.JsonPath}:{value.MinDate?.Ticks}:{value.MaxDate?.Ticks}:{value.DateFormat}:{value.SuccessScore}:{value.FailureScore}",
			EnumAssessment value => $"Enum:{value.JsonPath}:{string.Join(',', value.AllowedValues.OrderBy(entry => entry, StringComparer.Ordinal))}:{value.SuccessScore}:{value.FailureScore}",
			PatternCountAssessment value => $"PatternCount:{value.JsonPath}:{value.Pattern}:{value.MinMatches}:{value.MaxMatches}:{value.SuccessScore}:{value.FailureScore}",
			JsonPathSelectorAssessment value => $"JsonPathSelector:{value.JsonPath}:{value.SuccessScore}:{value.FailureScore}",
			CustomCallbackAssessment => "CustomCallback",
			_ => definition.GetType().FullName ?? definition.GetType().Name,
		};
	}

	private static string Hash(string input)
	{
		byte[] utf8 = Encoding.UTF8.GetBytes(input);
		byte[] hash = SHA256.HashData(utf8);
		return Convert.ToHexString(hash);
	}
}