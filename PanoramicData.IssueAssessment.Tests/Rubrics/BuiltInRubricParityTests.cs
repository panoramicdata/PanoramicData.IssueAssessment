using System.Text.Json.Nodes;
using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// The built-in rubric scores every fixture from PanoramicData.Skills' issue-quality tests exactly as Yoda's
/// <c>IssueQualityCore.ps1</c> did: same score, grade, findings in order, recommendations, flags, automation type and
/// mandatory caps.
/// </summary>
/// <remarks>
/// <c>yoda-fixtures.jsonl</c> was recorded by running Yoda's six <c>Test-IssueQuality*.ps1</c> scripts against an
/// instrumented copy of its engine on 2026-10-14; each line holds the input ticket in Yoda's shape and Yoda's result.
/// One recorded case was left out because its test temporarily changes two of Yoda's weights. The same comparison
/// passed on 320 real Jira tickets, which are not committed because they are customer data.
/// </remarks>
public class BuiltInRubricParityTests : TestBase
{
	// Yoda has no Presentation, Release Notes or Ticket Contract factor, so parity is proved over the rubric without them; their own test classes cover those.
	private static readonly IssueAssessor Assessor = new(Serialization.AssessmentRubric.Load(WithoutPresentation(BuiltInRubrics.PanoramicDataTicketQualityJson)));

	public static TheoryData<int> Cases => [.. Enumerable.Range(0, File.ReadAllLines(FixturePath).Length)];

	private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Rubrics", "yoda-fixtures.jsonl");

	[Theory]
	[MemberData(nameof(Cases))]
	public async Task Assess_YodaFixture_MatchesYoda(int index)
	{
		using JsonDocument record = JsonDocument.Parse(File.ReadAllLines(FixturePath)[index]);
		JsonElement issue = record.RootElement.GetProperty("Issue");
		JsonElement yoda = record.RootElement.GetProperty("Result");
		using JsonDocument document = ToNormalized(issue, record.RootElement.GetProperty("Platform").GetString()!).ToJsonDocument();

		AssessmentReport report = AssessmentReport.From(await Assessor.AssessAsync(document, CancellationToken));

		List<string> mandatory = [.. report.Caps.Where(cap => cap.Group == "mandatory").Select(cap => cap.Name)];
		List<string> findings = [.. report.Findings];
		if (mandatory.Count > 0)
		{
			findings.Add($"✗ Mandatory: {string.Join(", ", mandatory)} not met, so EXCELLENT is unreachable (score capped at 79)");
		}

		bool isEpic = report.Flags["isEpic"];
		report.Score.Should().Be((int)yoda.GetProperty("TotalScore").GetDouble());
		report.Grade.Should().Be(yoda.GetProperty("Grade").GetString());
		findings.Should().Equal(Strings(yoda, "Findings"));
		report.Recommendations.Should().Equal(Strings(yoda, "Recommendations"));
		mandatory.Should().Equal(Strings(yoda, "MandatoryMissing"));
		(!isEpic && report.Flags["isBug"]).Should().Be(yoda.GetProperty("IsBug").GetBoolean());
		report.Flags["isInvestigation"].Should().Be(yoda.GetProperty("IsInvestigation").GetBoolean());
		report.Flags["isUiTicket"].Should().Be(yoda.GetProperty("IsUiTicket").GetBoolean());
		report.Classifications["TestAutomationType"].Should().Be(yoda.GetProperty("TestAutomationType").GetString());
	}

	[Fact]
	public void BuiltInRubric_Json_RoundTripsThroughTheLoader()
	{
		AssessmentOptions loaded = Serialization.AssessmentRubric.Load(BuiltInRubrics.PanoramicDataTicketQualityJson);

		loaded.Categories.Select(category => category.Name).Should().Equal("Ticket Quality", "Epic Quality");
		loaded.GradeBands.Select(band => band.Name).Should().Equal("EXCELLENT", "GOOD", "ADEQUATE", "NEEDS IMPROVEMENT", "POOR");
	}

	private static string WithoutPresentation(string rubricJson)
	{
		JsonNode rubric = JsonNode.Parse(rubricJson, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
		foreach (JsonNode? category in rubric["categories"]!.AsArray())
		{
			JsonArray assessments = category!["assessments"]!.AsArray();
			foreach (JsonNode? assessment in assessments.Where(item => item!["name"]!.GetValue<string>().EndsWith("Presentation", StringComparison.Ordinal) || item["name"]!.GetValue<string>().EndsWith("ReleaseNotes", StringComparison.Ordinal) || item["name"]!.GetValue<string>() == "TicketContract").ToList())
			{
				assessments.Remove(assessment);
			}
		}

		JsonArray caps = rubric["scoreCaps"]!.AsArray();
		foreach (JsonNode? cap in caps.Where(item => item!["name"]!.GetValue<string>() is "Release notes" or "Ticket contract").ToList())
		{
			caps.Remove(cap);
		}

		return rubric.ToJsonString();
	}

	private static NormalizedIssue ToNormalized(JsonElement issue, string platform) => new()
	{
		Key = Text(issue, "Key"),
		Title = Text(issue, "Summary"),
		Description = Text(issue, "Description"),
		IssueType = Text(issue, "IssueType"),
		Priority = Text(issue, "Priority"),
		Status = Text(issue, "Status"),
		Components = [.. Strings(issue, "Components")],
		Labels = [.. Strings(issue, "Labels")],
		FixVersions = [.. Strings(issue, "FixVersions")],
		AttachmentsCount = issue.TryGetProperty("AttachmentsCount", out JsonElement count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : 0,
		SupportsFixVersion = Flag(issue, "SupportsFixVersion"),
		SupportsComponents = Flag(issue, "SupportsComponents"),
		Platform = platform,
	};

	private static string? Text(JsonElement element, string name)
		=> element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static bool? Flag(JsonElement element, string name)
		=> element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

	private static List<string> Strings(JsonElement element, string name)
		=> !element.TryGetProperty(name, out JsonElement value) ? []
			: value.ValueKind == JsonValueKind.Array ? [.. value.EnumerateArray().Select(item => item.GetString()!)]
			: value.ValueKind == JsonValueKind.String ? [value.GetString()!]
			: [];
}
