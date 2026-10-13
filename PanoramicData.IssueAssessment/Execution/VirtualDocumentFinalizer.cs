namespace PanoramicData.IssueAssessment.Execution;

/// <summary>
/// Combines virtual documents produced during execution.
/// </summary>
public static class VirtualDocumentFinalizer
{
	/// <summary>
	/// Wraps an assessment's virtual output in an object under the given name: <c>{ "name": output }</c>.
	/// </summary>
	/// <param name="name">The property name, normally the assessment's <see cref="Assessment.VirtualOutputName"/>.</param>
	/// <param name="output">The virtual output document produced by the assessment.</param>
	/// <returns>A new document; the caller disposes it.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is null.</exception>
	public static JsonDocument Wrap(string name, JsonDocument output)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(output);

		using MemoryStream stream = new();
		using (Utf8JsonWriter writer = new(stream))
		{
			writer.WriteStartObject();
			writer.WritePropertyName(name);
			output.RootElement.WriteTo(writer);
			writer.WriteEndObject();
		}

		stream.Position = 0;
		return JsonDocument.Parse(stream);
	}

	/// <summary>
	/// Merges two virtual documents into a single object document.
	/// </summary>
	/// <param name="current">Current virtual document.</param>
	/// <param name="next">Next virtual document to merge.</param>
	/// <returns>Merged virtual document.</returns>
	/// <exception cref="InvalidOperationException">Thrown when merge detects a conflicting value.</exception>
	public static JsonDocument? Merge(JsonDocument? current, JsonDocument? next)
	{
		if (next is null)
		{
			return current;
		}

		if (current is null)
		{
			return JsonDocument.Parse(next.RootElement.GetRawText());
		}

		if (current.RootElement.ValueKind != JsonValueKind.Object || next.RootElement.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidOperationException("Virtual document merge requires JSON object roots.");
		}

		Dictionary<string, JsonElement> merged = new(StringComparer.Ordinal);
		foreach ((string key, JsonElement value) in current.RootElement.EnumerateObject().Select(property => (property.Name, property.Value)))
		{
			merged[key] = value;
		}

		foreach ((string key, JsonElement value) in next.RootElement.EnumerateObject().Select(property => (property.Name, property.Value)))
		{
			if (merged.TryGetValue(key, out JsonElement existing) && existing.GetRawText() != value.GetRawText())
			{
				throw new InvalidOperationException($"Virtual document conflict detected for key '{key}'.");
			}

			merged[key] = value;
		}

		using MemoryStream stream = new();
		using (Utf8JsonWriter writer = new(stream))
		{
			writer.WriteStartObject();
			foreach ((string key, JsonElement value) in merged)
			{
				writer.WritePropertyName(key);
				value.WriteTo(writer);
			}
			writer.WriteEndObject();
		}

		stream.Position = 0;
		return JsonDocument.Parse(stream);
	}
}
