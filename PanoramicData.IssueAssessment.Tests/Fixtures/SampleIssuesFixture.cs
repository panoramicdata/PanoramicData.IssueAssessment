using System.Globalization;
using System.Text.Json.Nodes;

namespace PanoramicData.IssueAssessment.Tests.Fixtures;

/// <summary>
/// Realistic issue documents for exercising the built-in categories.
/// </summary>
/// <remarks>
/// The normalized shape is the one the built-in categories read: <c>title</c>, <c>description</c>,
/// <c>acceptanceCriteria</c>, <c>assignee</c>, <c>assigneeState</c>, <c>dueDate</c> and <c>reproductionSteps</c>.
/// The raw Jira, GitHub and ServiceNow shapes are what those systems return before any normalization.
/// Every method returns a new document that the caller must dispose.
/// </remarks>
internal static class SampleIssuesFixture
{
	/// <summary>
	/// A well-written bug report title.
	/// </summary>
	public const string GoodTitle = "CSV export times out for reports over 10,000 rows";

	/// <summary>
	/// A well-written bug report description.
	/// </summary>
	public const string GoodDescription =
		"Exporting the monthly usage report to CSV fails with a gateway timeout when the report has more than "
		+ "10,000 rows. Smaller reports export in a few seconds. The failure started after the 4.2 release, which "
		+ "moved export generation into the web request. Customers on the Enterprise plan are affected and two "
		+ "support tickets have been raised.";

	/// <summary>
	/// Well-written acceptance criteria.
	/// </summary>
	public const string GoodAcceptanceCriteria =
		"- Given a report with 50,000 rows, when the user exports to CSV, then the file downloads within 30 seconds.\n"
		+ "- Given an export is still running, when the user leaves the page, then the export completes and is emailed.\n"
		+ "- Existing exports under 10,000 rows are no slower than today.";

	/// <summary>
	/// Well-structured reproduction steps.
	/// </summary>
	public const string GoodReproductionSteps =
		"1. Sign in as an Enterprise tenant administrator.\n"
		+ "2. Open Reports > Monthly usage and select the last 12 months.\n"
		+ "3. Click Export > CSV.\n"
		+ "4. Observe the 504 Gateway Timeout after about 60 seconds.";

	/// <summary>
	/// A short, vague description that is present but thin.
	/// </summary>
	public const string MediocreDescription = "Export is really slow for big reports.";

	/// <summary>
	/// Reproduction steps written as prose with no step markers.
	/// </summary>
	public const string MediocreReproductionSteps = "Open a big report and export it to CSV, then wait.";

	/// <summary>
	/// Gets today's date in UTC, as the due date category sees it.
	/// </summary>
	public static DateTime UtcToday => DateTime.UtcNow.Date;

	/// <summary>
	/// A complete, well-written bug report in the normalized shape, due in two weeks.
	/// </summary>
	public static JsonDocument GoodNormalized()
	{
		return ToDocument(new JsonObject
		{
			["title"] = GoodTitle,
			["description"] = GoodDescription,
			["acceptanceCriteria"] = GoodAcceptanceCriteria,
			["assignee"] = "Jane Smith",
			["assigneeState"] = "InProgress",
			["dueDate"] = IsoUtc(UtcToday.AddDays(14).AddHours(17)),
			["reproductionSteps"] = GoodReproductionSteps,
		});
	}

	/// <summary>
	/// A ticket in the normalized shape where every field is present but each is thin, vague or out of range.
	/// </summary>
	public static JsonDocument MediocreNormalized()
	{
		return ToDocument(new JsonObject
		{
			["title"] = "CSV",
			["description"] = MediocreDescription,
			["acceptanceCriteria"] = "It should be faster.",
			["assignee"] = "Bob",
			["assigneeState"] = "Unassigned",
			["dueDate"] = IsoUtc(UtcToday.AddDays(-30)),
			["reproductionSteps"] = MediocreReproductionSteps,
		});
	}

	/// <summary>
	/// An almost empty ticket in the normalized shape: blank strings and nulls.
	/// </summary>
	public static JsonDocument PoorNormalized()
	{
		return ToDocument(new JsonObject
		{
			["title"] = "   ",
			["description"] = string.Empty,
			["acceptanceCriteria"] = null,
			["assignee"] = null,
			["dueDate"] = null,
		});
	}

	/// <summary>
	/// A ticket with no properties at all.
	/// </summary>
	public static JsonDocument Empty()
	{
		return JsonDocument.Parse("{}");
	}

	/// <summary>
	/// The same good bug report as returned by the Jira REST API (<c>/rest/api/2/issue/{key}</c>).
	/// </summary>
	public static JsonDocument RawJira()
	{
		return ToDocument(new JsonObject
		{
			["key"] = "EXP-1423",
			["fields"] = new JsonObject
			{
				["summary"] = GoodTitle,
				["description"] = GoodDescription + "\n\nSteps to reproduce:\n" + GoodReproductionSteps,
				["assignee"] = new JsonObject
				{
					["displayName"] = "Jane Smith",
					["emailAddress"] = "jane.smith@example.com",
				},
				["status"] = new JsonObject { ["name"] = "In Progress" },
				["duedate"] = UtcToday.AddDays(14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				["customfield_10035"] = GoodAcceptanceCriteria,
			},
		});
	}

	/// <summary>
	/// The same good bug report as returned by the GitHub REST API (<c>/repos/{owner}/{repo}/issues/{number}</c>).
	/// </summary>
	public static JsonDocument RawGitHub()
	{
		return ToDocument(new JsonObject
		{
			["number"] = 1423,
			["title"] = GoodTitle,
			["body"] = GoodDescription + "\n\n## Steps to reproduce\n" + GoodReproductionSteps,
			["state"] = "open",
			["assignee"] = new JsonObject { ["login"] = "jsmith" },
			["assignees"] = new JsonArray(new JsonObject { ["login"] = "jsmith" }),
			["milestone"] = new JsonObject
			{
				["title"] = "4.3",
				["due_on"] = IsoUtc(UtcToday.AddDays(14).AddHours(7)),
			},
		});
	}

	/// <summary>
	/// The same good bug report as returned by the ServiceNow Table API (<c>/api/now/table/incident</c>).
	/// </summary>
	public static JsonDocument RawServiceNow()
	{
		return ToDocument(new JsonObject
		{
			["number"] = "INC0011423",
			["short_description"] = GoodTitle,
			["description"] = GoodDescription,
			["assigned_to"] = new JsonObject
			{
				["display_value"] = "Jane Smith",
				["value"] = "6816f79cc0a8016401c5a33be04be441",
			},
			["state"] = "2",
			["due_date"] = UtcToday.AddDays(14).AddHours(17).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
		});
	}

	/// <summary>
	/// Builds a normalized document containing only the given properties.
	/// </summary>
	public static JsonDocument With(params (string Name, JsonNode? Value)[] properties)
	{
		JsonObject root = [];
		foreach ((string name, JsonNode? value) in properties)
		{
			root[name] = value;
		}

		return ToDocument(root);
	}

	/// <summary>
	/// Formats a UTC date the way most issue APIs do: ISO 8601 to the second with a Z suffix.
	/// </summary>
	public static string IsoUtc(DateTime value)
	{
		return value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
	}

	private static JsonDocument ToDocument(JsonObject root)
	{
		return JsonDocument.Parse(root.ToJsonString());
	}
}
