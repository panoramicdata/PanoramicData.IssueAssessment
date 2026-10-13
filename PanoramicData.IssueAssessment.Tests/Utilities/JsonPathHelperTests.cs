namespace PanoramicData.IssueAssessment.Tests.Utilities;

/// <summary>
/// Tests for <see cref="JsonPathHelper"/>.
/// </summary>
public class JsonPathHelperSelectionTests
{
	private const string Issue = """
		{
			"title": "Export fails",
			"fields": { "assignee": { "name": "Alex" }, "labels": ["bug", "export"] },
			"comments": [ { "body": "first" }, { "body": "second" } ],
			"matrix": [[1, 2], [3, 4]],
			"empty": null
		}
		""";

	[Theory]
	[InlineData("$.title", "Export fails")]
	[InlineData("$.fields.assignee.name", "Alex")]
	[InlineData("$.fields.labels[1]", "export")]
	[InlineData("$.comments[0].body", "first")]
	[InlineData("$.comments[1].body", "second")]
	public void TrySelect_ExistingPath_ReturnsValue(string path, string expected)
	{
		using JsonDocument document = JsonDocument.Parse(Issue);

		JsonPathHelper.TrySelect(document, path, out JsonElement value).Should().BeTrue();
		value.GetString().Should().Be(expected);
	}

	[Fact]
	public void TrySelect_Root_ReturnsWholeDocument()
	{
		using JsonDocument document = JsonDocument.Parse(Issue);

		JsonPathHelper.TrySelect(document, "$", out JsonElement value).Should().BeTrue();
		value.ValueKind.Should().Be(JsonValueKind.Object);
	}

	[Fact]
	public void TrySelect_NullValue_IsFoundAsNull()
	{
		using JsonDocument document = JsonDocument.Parse(Issue);

		JsonPathHelper.TrySelect(document, "$.empty", out JsonElement value).Should().BeTrue();
		value.ValueKind.Should().Be(JsonValueKind.Null);
	}

	[Theory]
	[InlineData("$.missing")]
	[InlineData("$.title.length")]
	[InlineData("$.fields.missing.name")]
	[InlineData("$.title[0]")]
	[InlineData("$.fields[0]")]
	[InlineData("$.comments[2].body")]
	[InlineData("$.comments[99]")]
	[InlineData("$.matrix[0].x")]
	public void TrySelect_PathThatDoesNotResolve_ReturnsFalse(string path)
	{
		using JsonDocument document = JsonDocument.Parse(Issue);

		JsonPathHelper.TrySelect(document, path, out JsonElement value).Should().BeFalse();
		value.ValueKind.Should().Be(JsonValueKind.Undefined);
	}

	[Fact]
	public void TrySelect_PropertyOnArrayRoot_ReturnsFalse()
	{
		using JsonDocument document = JsonDocument.Parse("[1,2]");

		JsonPathHelper.TrySelect(document, "$.length", out _).Should().BeFalse();
	}

	[Fact]
	public void TrySelect_NullDocument_Throws()
	{
		Action act = () => JsonPathHelper.TrySelect(null!, "$.title", out _);

		act.Should().Throw<ArgumentNullException>();
	}

	[Theory]
	[InlineData(null, "JSONPath is required.")]
	[InlineData("", "JSONPath is required.")]
	[InlineData("   ", "JSONPath is required.")]
	[InlineData("title", "JSONPath must start with '$.' or be '$'.")]
	[InlineData("$title", "JSONPath must start with '$.' or be '$'.")]
	[InlineData("$.", "JSONPath must include at least one segment after '$.'.")]
	[InlineData("$.a..b", "JSONPath contains an empty segment.")]
	[InlineData("$.a.", "JSONPath contains an empty segment.")]
	[InlineData("$.a[0", "JSONPath segment 'a[0' has a malformed array index.")]
	[InlineData("$.[0]", "JSONPath segment '[0]' is missing a property name.")]
	[InlineData("$.a[x]", "JSONPath segment 'a[x]' contains a non-numeric array index.")]
	[InlineData("$.a[]", "JSONPath segment 'a[]' contains a non-numeric array index.")]
	[InlineData("$.a[ 1]", "JSONPath segment 'a[ 1]' contains a non-numeric array index.")]
	[InlineData("$.a[+1]", "JSONPath segment 'a[+1]' contains a non-numeric array index.")]
	[InlineData("$.a[-1]", "JSONPath segment 'a[-1]' contains a non-numeric array index.")]
	public void Validate_MalformedPath_ThrowsWithReason(string? path, string message)
	{
		Action act = () => JsonPathHelper.Validate(path!);

		act.Should().Throw<InvalidJsonPathException>().WithMessage(message);
	}

	[Theory]
	[InlineData("$")]
	[InlineData("$.a")]
	[InlineData("$.a.b.c")]
	[InlineData("$.a[0]")]
	[InlineData("$.a[10].b")]
	public void Validate_WellFormedPath_DoesNotThrow(string path)
	{
		Action act = () => JsonPathHelper.Validate(path);

		act.Should().NotThrow();
	}

	[Fact]
	public void TrySelect_WithVirtualExtension_PrefersIssueThenFallsBack()
	{
		using JsonDocument issue = JsonDocument.Parse("""{"a":"issue"}""");
		using JsonDocument extension = JsonDocument.Parse("""{"a":"extension","b":"extension"}""");

		JsonPathHelper.TrySelect(issue, extension, "$.a", out JsonElement a).Should().BeTrue();
		JsonPathHelper.TrySelect(issue, extension, "$.b", out JsonElement b).Should().BeTrue();
		JsonPathHelper.TrySelect(issue, extension, "$.c", out _).Should().BeFalse();
		JsonPathHelper.TrySelect(issue, null, "$.b", out _).Should().BeFalse();

		a.GetString().Should().Be("issue");
		b.GetString().Should().Be("extension");
	}
}
