namespace PanoramicData.IssueAssessment.Utilities;

/// <summary>
/// Provides minimal JSONPath parsing and selection helpers for assessment definitions.
/// </summary>
public static class JsonPathHelper
{
	/// <summary>
	/// Validates a JSONPath expression and throws when malformed.
	/// </summary>
	/// <param name="jsonPath">JSONPath to validate.</param>
	public static void Validate(string jsonPath)
	{
		_ = Parse(jsonPath);
	}

	/// <summary>
	/// Attempts to select a value from a JSON document using a minimal JSONPath subset.
	/// Supported format: $.property, $.property[index], chained segments.
	/// </summary>
	/// <param name="document">Source document.</param>
	/// <param name="jsonPath">JSONPath selector.</param>
	/// <param name="result">Selected element when found.</param>
	/// <returns><see langword="true"/> when value is found, otherwise <see langword="false"/>.</returns>
	public static bool TrySelect(JsonDocument document, string jsonPath, out JsonElement result)
	{
		ArgumentNullException.ThrowIfNull(document);

		IReadOnlyList<PathToken> tokens = Parse(jsonPath);
		JsonElement current = document.RootElement;

		foreach (PathToken token in tokens)
		{
			if (!TryApplyToken(current, token, out JsonElement next))
			{
				result = default;
				return false;
			}

			current = next;
		}

		result = current;
		return true;
	}

	private static bool TryApplyToken(JsonElement current, PathToken token, out JsonElement next)
	{
		if (token.PropertyName is { Length: > 0 })
		{
			if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(token.PropertyName, out JsonElement property))
			{
				next = default;
				return false;
			}

			current = property;
		}

		if (token.ArrayIndex is null)
		{
			next = current;
			return true;
		}

		if (current.ValueKind != JsonValueKind.Array)
		{
			next = default;
			return false;
		}

		int index = token.ArrayIndex.Value;
		if (index < 0 || index >= current.GetArrayLength())
		{
			next = default;
			return false;
		}

		next = current[index];
		return true;
	}

	private static IReadOnlyList<PathToken> Parse(string jsonPath)
	{
		if (string.IsNullOrWhiteSpace(jsonPath))
		{
			throw new InvalidJsonPathException("JSONPath is required.");
		}

		if (jsonPath == "$")
		{
			return Array.Empty<PathToken>();
		}

		if (!jsonPath.StartsWith("$.", StringComparison.Ordinal))
		{
			throw new InvalidJsonPathException("JSONPath must start with '$.' or be '$'.");
		}

		string path = jsonPath[2..];
		if (path.Length == 0)
		{
			throw new InvalidJsonPathException("JSONPath must include at least one segment after '$.'.");
		}

		string[] segments = path.Split('.', StringSplitOptions.None);
		List<PathToken> tokens = new(segments.Length);

		foreach (string segment in segments)
		{
			if (segment.Length == 0)
			{
				throw new InvalidJsonPathException("JSONPath contains an empty segment.");
			}

			tokens.Add(ParseSegment(segment));
		}

		return tokens;
	}

	private static PathToken ParseSegment(string segment)
	{
		int openBracketIndex = segment.IndexOf('[');
		if (openBracketIndex < 0)
		{
			return new PathToken(segment, null);
		}

		if (!segment.EndsWith("]", StringComparison.Ordinal))
		{
			throw new InvalidJsonPathException($"JSONPath segment '{segment}' has a malformed array index.");
		}

		string propertyName = segment[..openBracketIndex];
		string indexText = segment[(openBracketIndex + 1)..^1];

		if (propertyName.Length == 0)
		{
			throw new InvalidJsonPathException($"JSONPath segment '{segment}' is missing a property name.");
		}

		if (!int.TryParse(indexText, out int index))
		{
			throw new InvalidJsonPathException($"JSONPath segment '{segment}' contains a non-numeric array index.");
		}

		if (index < 0)
		{
			throw new InvalidJsonPathException($"JSONPath segment '{segment}' contains a negative array index.");
		}

		return new PathToken(propertyName, index);
	}

	private sealed record PathToken(string PropertyName, int? ArrayIndex);
}
