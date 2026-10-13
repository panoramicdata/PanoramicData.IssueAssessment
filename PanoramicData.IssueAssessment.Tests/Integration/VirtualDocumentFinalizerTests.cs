using PanoramicData.IssueAssessment.Execution;

namespace PanoramicData.IssueAssessment.Tests.Integration;

/// <summary>
/// Tests for <see cref="VirtualDocumentFinalizer"/>.
/// </summary>
public class VirtualDocumentFinalizerTests
{
	private static string? Raw(JsonDocument? document) => document?.RootElement.GetRawText();

	[Fact]
	public void Merge_NextNull_ReturnsCurrent()
	{
		using JsonDocument current = JsonDocument.Parse("""{"a":1}""");

		VirtualDocumentFinalizer.Merge(current, null).Should().BeSameAs(current);
		VirtualDocumentFinalizer.Merge(null, null).Should().BeNull();
	}

	[Fact]
	public void Merge_CurrentNull_ReturnsCopyOfNext()
	{
		using JsonDocument next = JsonDocument.Parse("""{"a":1}""");

		using JsonDocument? merged = VirtualDocumentFinalizer.Merge(null, next);

		merged.Should().NotBeSameAs(next, "the caller may dispose the assessment's output");
		Raw(merged).Should().Be("""{"a":1}""");
	}

	[Fact]
	public void Merge_TwoObjects_CombinesPropertiesInOrder()
	{
		using JsonDocument current = JsonDocument.Parse("""{"a":1,"b":{"c":true}}""");
		using JsonDocument next = JsonDocument.Parse("""{"d":"x"}""");

		using JsonDocument? merged = VirtualDocumentFinalizer.Merge(current, next);

		Raw(merged).Should().Be("""{"a":1,"b":{"c":true},"d":"x"}""");
	}

	[Fact]
	public void Merge_SameKeyWithEqualValue_IsAllowed()
	{
		using JsonDocument current = JsonDocument.Parse("""{"a":[1,2]}""");
		using JsonDocument next = JsonDocument.Parse("""{"a":[1,2]}""");

		using JsonDocument? merged = VirtualDocumentFinalizer.Merge(current, next);

		Raw(merged).Should().Be("""{"a":[1,2]}""");
	}

	[Fact]
	public void Merge_SameKeyWithDifferentValue_Throws()
	{
		using JsonDocument current = JsonDocument.Parse("""{"a":1}""");
		using JsonDocument next = JsonDocument.Parse("""{"a":2}""");

		Action act = () => VirtualDocumentFinalizer.Merge(current, next);

		act.Should().Throw<InvalidOperationException>().WithMessage("Virtual document conflict detected for key 'a'.");
	}

	[Theory]
	[InlineData("[1]", """{"a":1}""")]
	[InlineData("""{"a":1}""", "\"text\"")]
	public void Merge_NonObjectRoot_Throws(string current, string next)
	{
		using JsonDocument currentDocument = JsonDocument.Parse(current);
		using JsonDocument nextDocument = JsonDocument.Parse(next);

		Action act = () => VirtualDocumentFinalizer.Merge(currentDocument, nextDocument);

		act.Should().Throw<InvalidOperationException>().WithMessage("Virtual document merge requires JSON object roots.");
	}

	[Theory]
	[InlineData("\"text\"")]
	[InlineData("42")]
	[InlineData("""{"x":[1]}""")]
	[InlineData("null")]
	public void Wrap_AnyRoot_NestsItUnderTheName(string output)
	{
		using JsonDocument document = JsonDocument.Parse(output);

		using JsonDocument wrapped = VirtualDocumentFinalizer.Wrap("summary", document);

		Raw(wrapped).Should().Be($$"""{"summary":{{output}}}""");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Wrap_BlankName_Throws(string? name)
	{
		using JsonDocument document = JsonDocument.Parse("1");

		Action act = () => VirtualDocumentFinalizer.Wrap(name!, document);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Wrap_NullOutput_Throws()
	{
		Action act = () => VirtualDocumentFinalizer.Wrap("name", null!);

		act.Should().Throw<ArgumentNullException>();
	}
}
