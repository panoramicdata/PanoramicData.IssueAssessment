using PanoramicData.IssueAssessment.Mapping;

namespace PanoramicData.IssueAssessment.Tests.Mapping;

/// <summary>
/// Tests for <see cref="JiraIssueMapper"/>, <see cref="GitHubIssueMapper"/>, <see cref="ServiceNowRecordMapper"/> and
/// <see cref="NormalizedIssue"/>.
/// </summary>
public class MapperTests
{
	private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

	[Fact]
	public void Jira_V2Issue_MapsEveryField()
	{
		NormalizedIssue issue = JiraIssueMapper.FromRestJson(
			Json("""
			{
				"key": "MS-42",
				"fields": {
					"summary": "Export fails",
					"description": "h2. Steps\r\n# one",
					"project": { "key": "MS" },
					"issuetype": { "name": "Bug" },
					"priority": { "name": "Major" },
					"status": { "name": "Open" },
					"assignee": { "displayName": "Alex" },
					"components": [ { "name": "ReportMagic" }, { "id": "no-name" } ],
					"labels": [ "ui", 7 ],
					"fixVersions": [ { "name": "4.7" } ],
					"attachment": [ {}, {} ],
					"duedate": "2026-11-20"
				}
			}
			"""),
			new Uri("https://jira.example.com/"));

		issue.Should().BeEquivalentTo(new NormalizedIssue
		{
			Key = "MS-42",
			Project = "MS",
			Title = "Export fails",
			Description = "h2. Steps\r\n# one",
			IssueType = "Bug",
			Priority = "Major",
			Status = "Open",
			Assignee = "Alex",
			Components = ["ReportMagic"],
			Labels = ["ui"],
			FixVersions = ["4.7"],
			AttachmentsCount = 2,
			DueDate = "2026-11-20",
			Url = "https://jira.example.com/browse/MS-42",
			Platform = "Jira",
		});
	}

	[Fact]
	public void Jira_MinimalIssue_LeavesMissingFieldsUnset()
	{
		NormalizedIssue issue = JiraIssueMapper.FromRestJson(Json("""{ "fields": { "description": 5, "assignee": null, "components": "x" } }"""));

		issue.Key.Should().BeNull();
		issue.Description.Should().BeNull();
		issue.Assignee.Should().BeNull();
		issue.Components.Should().BeEmpty();
		issue.AttachmentsCount.Should().Be(0);
		issue.Url.Should().BeNull("there is no key to link to");
	}

	[Theory]
	[InlineData("[]")]
	[InlineData("""{ "key": "MS-1" }""")]
	[InlineData("""{ "fields": [] }""")]
	public void Jira_NotAnIssue_Throws(string json)
	{
		Action act = () => JiraIssueMapper.FromRestJson(Json(json));

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Jira_V3Description_IsFlattenedToWikiText()
	{
		NormalizedIssue issue = JiraIssueMapper.FromRestJson(Json("""
			{
				"fields": {
					"description": {
						"type": "doc",
						"content": [
							{ "type": "heading", "attrs": { "level": 3 }, "content": [ { "type": "text", "text": "Steps to reproduce" } ] },
							{ "type": "orderedList", "content": [
								{ "type": "listItem", "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "Open" } ] } ] },
								{ "type": "listItem", "content": [
									{ "type": "paragraph", "content": [ { "type": "text", "text": "Export" } ] },
									{ "type": "bulletList", "content": [ { "type": "listItem", "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "as PDF" } ] } ] } ] }
								] }
							] },
							{ "type": "heading", "content": [ { "type": "text", "text": "No level" } ] },
							{ "type": "paragraph", "content": [
								{ "type": "text", "text": "Ask " },
								{ "type": "mention", "attrs": { "text": "@Alex" } },
								{ "type": "hardBreak" },
								{ "type": "inlineCard", "attrs": { "url": "https://example.invalid/x" } },
								{ "type": "emoji", "attrs": { "text": ":)" } },
								{ "type": "mention" },
								{ "type": "inlineCard" },
								{ "type": "strong", "content": [ { "type": "text", "text": " now" } ] }
							] },
							{ "type": "codeBlock", "content": [ { "type": "text", "text": "x = 1" } ] },
							{ "type": "table", "content": [ { "type": "tableRow", "content": [ { "type": "tableCell", "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "cell" } ] } ] } ] } ] },
							{ "type": "rule" }
						]
					}
				}
			}
			"""));

		issue.Description.Should().Be(
			"h3. Steps to reproduce\n# Open\n# Export\n#* as PDF\nh2. No level\nAsk @Alex\nhttps://example.invalid/x:)" + " now\n{code}\nx = 1\n{code}\ncell");
	}

	[Fact]
	public void Jira_V3Description_ListItemParagraphsAndAttributelessNodes_Flatten()
	{
		NormalizedIssue issue = JiraIssueMapper.FromRestJson(Json("""
			{
				"fields": {
					"description": {
						"type": "doc",
						"content": [
							{ "type": "bulletList", "content": [ { "type": "listItem", "content": [
								{ "type": "paragraph", "content": [ { "type": "text", "text": "first" } ] },
								{ "type": "paragraph", "content": [ { "type": "text" }, { "type": "text", "text": "second" } ] }
							] } ] },
							{ "type": "paragraph", "content": [
								{ "type": "mention", "attrs": {} },
								{ "type": "emoji", "attrs": { "shortName": ":)" } },
								{ "type": "inlineCard", "attrs": {} },
								{ "type": "text", "text": "end" }
							] }
						]
					}
				}
			}
			"""));

		issue.Description.Should().Be("* first\nsecond\nend");
	}

	[Fact]
	public void FromRestJson_ReadsReleaseNotesFromCustomField10600()
	{
		JiraIssueMapper.FromRestJson(Json("""{ "fields": { "customfield_10600": "Fixed the export." } }"""))
			.ReleaseNotes.Should().Be("Fixed the export.");

		JiraIssueMapper.FromRestJson(Json("""
			{ "fields": { "customfield_10600": { "type": "doc", "version": 1, "content": [
				{ "type": "paragraph", "content": [ { "type": "text", "text": "Fixed the export." } ] } ] } } }
			""")).ReleaseNotes.Should().Be("Fixed the export.");

		JiraIssueMapper.FromRestJson(Json("""{ "fields": {} }""")).ReleaseNotes.Should().BeNull();
	}

	[Fact]
	public void FromRestJson_ReadsResolutionName()
	{
		JiraIssueMapper.FromRestJson(Json("""{ "fields": { "resolution": { "name": "Won't Do" } } }"""))
			.Resolution.Should().Be("Won't Do");
		JiraIssueMapper.FromRestJson(Json("""{ "fields": { "resolution": null } }""")).Resolution.Should().BeNull();
	}

	[Fact]
	public void ToJsonObject_OmitsNullReleaseNotesAndResolution()
	{
		var empty = new NormalizedIssue().ToJsonObject();
		empty.ContainsKey("releaseNotes").Should().BeFalse();
		empty.ContainsKey("resolution").Should().BeFalse();

		var set = new NormalizedIssue { ReleaseNotes = "n", Resolution = "Fixed" }.ToJsonObject();
		set["releaseNotes"]!.GetValue<string>().Should().Be("n");
		set["resolution"]!.GetValue<string>().Should().Be("Fixed");
	}

	[Fact]
	public void Jira_NoDescription_IsNull()
	{
		JiraIssueMapper.FromRestJson(Json("""{ "fields": {} }""")).Description.Should().BeNull();
	}

	[Fact]
	public void FlattenAdf_NonObjectNode_ReturnsEmpty()
	{
		JiraIssueMapper.FlattenAdf(Json("[1]")).Should().BeEmpty();
	}

	[Fact]
	public void GitHub_Issue_ReadsTypePriorityComponentsAndImagesFromTheBodyAndLabels()
	{
		NormalizedIssue issue = GitHubIssueMapper.FromRestJson(Json("""
			{
				"number": 7,
				"title": "Crash",
				"body": "![shot](a.png) and https://example.invalid/b.PNG",
				"state": "open",
				"assignees": [ { "login": "alex" } ],
				"labels": [ { "name": "Bug" }, { "name": "priority:HIGH" }, "area:Export", { "name": "component-Export" }, { "id": 1 } ],
				"milestone": { "due_on": "2026-12-01T00:00:00Z" },
				"html_url": "https://github.com/o/r/issues/7"
			}
			"""));

		issue.Key.Should().Be("#7");
		issue.IssueType.Should().Be("Bug");
		issue.Priority.Should().Be("High");
		issue.Components.Should().Equal("Export");
		issue.Labels.Should().Equal("Bug", "priority:HIGH", "area:Export", "component-Export");
		issue.AttachmentsCount.Should().Be(2);
		issue.Assignee.Should().Be("alex");
		issue.DueDate.Should().Be("2026-12-01T00:00:00Z");
		issue.SupportsFixVersion.Should().BeFalse();
		issue.Platform.Should().Be("GitHub");
	}

	[Theory]
	[InlineData("""[ "enhancement" ]""", "Improvement")]
	[InlineData("""[ "user story" ]""", "Improvement")]
	[InlineData("""[ "spike" ]""", "Task")]
	[InlineData("[]", "Task")]
	public void GitHub_IssueType_FollowsLabels(string labels, string expected)
	{
		GitHubIssueMapper.FromRestJson(Json($$"""{ "labels": {{labels}} }""")).IssueType.Should().Be(expected);
	}

	[Fact]
	public void GitHub_MinimalIssue_HasDefaults()
	{
		NormalizedIssue issue = GitHubIssueMapper.FromRestJson(Json("""{ "number": "x", "assignees": [], "milestone": null }"""));

		issue.Key.Should().BeNull();
		issue.Description.Should().BeEmpty();
		issue.Priority.Should().Be("Unknown");
		issue.Assignee.Should().BeNull();
		issue.AttachmentsCount.Should().Be(0);
		issue.DueDate.Should().BeNull();
	}

	[Fact]
	public void GitHub_NotAnObject_Throws()
	{
		Action act = () => GitHubIssueMapper.FromRestJson(Json("[]"));

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ServiceNow_Record_ReadsPlainAndReferenceFields()
	{
		NormalizedIssue issue = ServiceNowRecordMapper.FromTableJson(Json("""
			{ "result": { "number": "INC1", "short_description": "VPN down", "description": " ", "sys_class_name": "incident",
				"priority": "2", "state": "1", "assigned_to": { "display_value": "Alex", "link": "x" }, "due_date": "" } }
			"""));

		issue.Key.Should().Be("INC1");
		issue.Title.Should().Be("VPN down");
		issue.Description.Should().BeNull("blank values are left unset");
		issue.IssueType.Should().Be("incident");
		issue.Assignee.Should().Be("Alex");
		issue.DueDate.Should().BeNull();
		issue.SupportsComponents.Should().BeFalse();
		issue.Platform.Should().Be("ServiceNow");
	}

	[Fact]
	public void ServiceNow_UnwrappedRecordWithOddReference_Maps()
	{
		NormalizedIssue issue = ServiceNowRecordMapper.FromTableJson(Json("""{ "number": "INC2", "assigned_to": { "value": "abc" }, "state": 3 }"""));

		issue.Key.Should().Be("INC2");
		issue.Assignee.Should().BeNull();
		issue.Status.Should().BeNull();
	}

	[Theory]
	[InlineData("[]")]
	[InlineData("""{ "result": [] }""")]
	public void ServiceNow_NotAnObject_Throws(string json)
	{
		Action act = () => ServiceNowRecordMapper.FromTableJson(Json(json));

		act.Should().Throw<ArgumentException>();
	}

	[Theory]
	[InlineData("MS-1234", "MS")]
	[InlineData("ops2-1", "ops2")]
	[InlineData("#42", null)]
	[InlineData("MS-", null)]
	[InlineData(null, null)]
	public void ProjectFromKey_ReadsTheProjectPrefix(string? key, string? expected)
	{
		NormalizedIssue.ProjectFromKey(key).Should().Be(expected);
	}

	[Fact]
	public void ToJsonDocument_WritesSetValuesAndAlwaysTheLists()
	{
		NormalizedIssue issue = new()
		{
			Key = "MS-5",
			Title = "t",
			Labels = ["a"],
			SupportsComponents = true,
			SupportsFixVersion = false,
			AssigneeState = "Assigned",
			AcceptanceCriteria = "ac",
			ReproductionSteps = "rs",
		};

		using JsonDocument document = issue.ToJsonDocument();

		document.RootElement.GetRawText().Should().Be(
			"""{"key":"MS-5","project":"MS","title":"t","assigneeState":"Assigned","components":[],"labels":["a"],"fixVersions":[],"attachmentsCount":0,"supportsFixVersion":false,"supportsComponents":true,"acceptanceCriteria":"ac","reproductionSteps":"rs"}""");
	}

	[Fact]
	public void ToJsonObject_ExplicitProject_WinsOverTheKey()
	{
		new NormalizedIssue { Key = "MS-5", Project = "OPS" }.ToJsonObject()["project"]!.GetValue<string>().Should().Be("OPS");
		new NormalizedIssue().ToJsonObject().ContainsKey("project").Should().BeFalse();
	}
}
