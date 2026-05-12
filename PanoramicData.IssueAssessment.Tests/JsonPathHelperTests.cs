namespace PanoramicData.IssueAssessment.Tests;

/// <summary>
/// Tests for JsonPath helper parsing and selection behavior.
/// </summary>
public class JsonPathHelperTests
{
	[Fact]
	public void TrySelect_WithNestedProperty_ReturnsExpectedValue()
	{
		using JsonDocument document = JsonDocument.Parse("{\"issue\":{\"title\":\"Hello\"}}");

		bool found = JsonPathHelper.TrySelect(document, "$.issue.title", out JsonElement value);

		found.Should().BeTrue();
		value.GetString().Should().Be("Hello");
	}

	[Fact]
	public void TrySelect_WithArrayIndex_ReturnsExpectedValue()
	{
		using JsonDocument document = JsonDocument.Parse("{\"labels\":[\"one\",\"two\"]}");

		bool found = JsonPathHelper.TrySelect(document, "$.labels[1]", out JsonElement value);

		found.Should().BeTrue();
		value.GetString().Should().Be("two");
	}

	[Fact]
	public void TrySelect_WithMissingPath_ReturnsFalse()
	{
		using JsonDocument document = JsonDocument.Parse("{\"title\":\"Hello\"}");

		bool found = JsonPathHelper.TrySelect(document, "$.description", out _);

		found.Should().BeFalse();
	}

	[Fact]
	public void Validate_WithInvalidPath_ThrowsInvalidJsonPathException()
	{
		Assert.Throws<InvalidJsonPathException>(() => JsonPathHelper.Validate("issue.title"));
	}
}
