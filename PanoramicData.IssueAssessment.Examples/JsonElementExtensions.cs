using System.Text.Json;

namespace PanoramicData.IssueAssessment.Examples;

internal static class JsonElementExtensions
{
	/// <summary>
	/// Returns a string property's value, or null when the property is absent or not a string.
	/// </summary>
	public static string? GetString(this JsonElement element, string propertyName)
		=> element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
}
