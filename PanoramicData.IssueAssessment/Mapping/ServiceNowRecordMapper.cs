namespace PanoramicData.IssueAssessment.Mapping;

/// <summary>
/// Maps a record from the ServiceNow Table API (<c>GET /api/now/table/{table}/{sys_id}</c>) to a
/// <see cref="NormalizedIssue"/>.
/// </summary>
/// <remarks>
/// Accepts the response with or without its <c>result</c> wrapper. Reference fields may be plain strings or, with
/// <c>sysparm_display_value=true</c>, objects whose <c>display_value</c> is used. ServiceNow has no fix versions or
/// components, so both are marked unsupported.
/// </remarks>
public static class ServiceNowRecordMapper
{
	/// <summary>
	/// Maps a ServiceNow Table API record.
	/// </summary>
	/// <param name="record">The record JSON.</param>
	/// <returns>The normalized issue.</returns>
	/// <exception cref="ArgumentException">Thrown when the JSON is not an object.</exception>
	public static NormalizedIssue FromTableJson(JsonElement record)
	{
		if (record.ValueKind == JsonValueKind.Object && record.TryGetProperty("result", out JsonElement result))
		{
			record = result;
		}

		if (record.ValueKind != JsonValueKind.Object)
		{
			throw new ArgumentException("A ServiceNow record must be an object.", nameof(record));
		}

		return new NormalizedIssue
		{
			Key = Value(record, "number"),
			Title = Value(record, "short_description"),
			Description = Value(record, "description"),
			IssueType = Value(record, "sys_class_name"),
			Priority = Value(record, "priority"),
			Status = Value(record, "state"),
			Assignee = Value(record, "assigned_to"),
			SupportsFixVersion = false,
			SupportsComponents = false,
			DueDate = Value(record, "due_date"),
			Platform = "ServiceNow",
		};
	}

	/// <summary>
	/// A field's value: a non-blank string, or a reference object's display value.
	/// </summary>
	private static string? Value(JsonElement record, string property)
	{
		if (!record.TryGetProperty(property, out JsonElement value))
		{
			return null;
		}

		string? text = value.ValueKind switch
		{
			JsonValueKind.String => value.GetString(),
			JsonValueKind.Object when value.TryGetProperty("display_value", out JsonElement display) && display.ValueKind == JsonValueKind.String => display.GetString(),
			_ => null,
		};
		return string.IsNullOrWhiteSpace(text) ? null : text;
	}
}
