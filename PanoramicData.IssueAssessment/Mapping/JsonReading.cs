namespace PanoramicData.IssueAssessment.Mapping;

/// <summary>
/// Reads values out of the JSON a platform returns, tolerating anything of the wrong shape as absent.
/// </summary>
internal static class JsonReading
{
	/// <summary>
	/// The string at <paramref name="property"/> of an object; null when it is not an object, the property is missing or
	/// the value is not a string.
	/// </summary>
	public static string? StringAt(JsonElement element, string property)
		=> element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	/// <summary>
	/// The string at <paramref name="nested"/> of the object at <paramref name="property"/>.
	/// </summary>
	public static string? StringAt(JsonElement element, string property, string nested)
		=> element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out JsonElement value) ? StringAt(value, nested) : null;
}
