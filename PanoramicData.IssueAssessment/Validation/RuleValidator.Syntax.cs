namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// Validates JSONPath and regex syntax.
/// </summary>
internal static partial class RuleValidator
{
	public static string? PathError(string? jsonPath)
	{
		try
		{
			JsonPathHelper.Validate(jsonPath!);
			return null;
		}
		catch (InvalidJsonPathException)
		{
			return $"has invalid JSONPath '{jsonPath}'.";
		}
	}

	public static string? PatternError(string? pattern)
	{
		if (string.IsNullOrWhiteSpace(pattern))
		{
			return "has an empty pattern.";
		}

		try
		{
			_ = RegexCache.GetOrAdd(pattern);
			return null;
		}
		catch (ArgumentException)
		{
			return $"has invalid regex pattern '{pattern}'.";
		}
	}
}
