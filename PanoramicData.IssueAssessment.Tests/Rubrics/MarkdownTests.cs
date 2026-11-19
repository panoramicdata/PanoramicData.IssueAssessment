using PanoramicData.IssueAssessment.Mapping;
using PanoramicData.IssueAssessment.Rubrics;
using PanoramicData.IssueAssessment.Rules;
using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Tests.Rubrics;

/// <summary>
/// The Markdown factor and <see cref="MarkdownSyntax"/>: a description written as Markdown renders wrongly in Jira's wiki
/// markup. Each construct below was checked against Jira's renderer by PanoramicData.Skills' <c>Find-JiraMarkdownSyntax</c>.
/// </summary>
public class MarkdownTests : TestBase
{
	[Theory]
	[InlineData("## Objective\r\nWords.", "Markdown heading")]
	[InlineData("### Objective", "Markdown heading")]
	[InlineData("Run `microk8s dbctl` first.", "backtick code span")]
	[InlineData("```bash\r\nls\r\n```", "code fence")]
	[InlineData("This is **important** text.", "double-asterisk bold")]
	[InlineData("See [the docs](https://example.com/a) now.", "Markdown link")]
	[InlineData("[local](./page) and [anchor](#top)", "Markdown link")]
	[InlineData("| a | b |\r\n|---|---|\r\n| 1 | 2 |", "Markdown table separator")]
	[InlineData("|:---:|---:|", "Markdown table separator")]
	public void Constructs_EachMarkdownForm_IsFound(string text, string kind)
		=> MarkdownSyntax.Constructs(text).Should().Contain(kind);

	[Theory]
	[InlineData("h2. Objective\r\nWords.")]
	[InlineData("# one\r\n# two")]
	[InlineData("* one\r\n* two")]
	[InlineData("- one\r\n- two")]
	[InlineData("A single numbered line:\r\n1. one")]
	[InlineData("1. one\r\n2. two")]
	[InlineData("Use {{microk8s dbctl}} here.")]
	[InlineData("*bold* and _italic_ and [text|https://example.com]")]
	[InlineData("||a||b||\r\n|1|2|")]
	[InlineData("IEnumerable`1 source, Func`2 selector")]
	[InlineData("text.`n`nMore")]
	[InlineData("")]
	[InlineData("   ")]
	public void Constructs_ValidWikiMarkupOrProse_IsNotFound(string text)
		=> MarkdownSyntax.Constructs(text).Should().BeEmpty();

	[Fact]
	public void Constructs_Null_IsEmpty() => MarkdownSyntax.Constructs(null).Should().BeEmpty();

	[Theory]
	[InlineData("{code:bash}\r\n## not a heading\r\nrun `x`\r\n{code}")]
	[InlineData("{noformat}\r\n## not a heading\r\n**x**\r\n{noformat}")]
	[InlineData("Inline {{`x` and **y**}} only.")]
	public void Constructs_LiteralRegions_AreSkipped(string text)
		=> MarkdownSyntax.Constructs(text).Should().BeEmpty();

	[Fact]
	public void Constructs_MonospaceThatSaysCode_DoesNotHideTheRealBlock()
		=> MarkdownSyntax.Constructs("{{code}}\r\n## Heading\r\n{{code}}").Should().Contain("Markdown heading");

	[Theory]
	[InlineData("# Decide\r\n## repoint")]
	[InlineData("* Decide\r\n## repoint")]
	[InlineData("## one\r\n### two")]
	[InlineData("# Decide\r\n## one\r\n### two")]
	public void Constructs_DoubleHashUnderAListLine_IsANestedItem_NotAHeading(string text)
	{
		// "## one" with no list line above it is a Markdown heading, but "### two" under it is its nested item.
		MarkdownSyntax.Constructs(text).Count(kind => kind == "Markdown heading").Should().Be(text.StartsWith("## one", StringComparison.Ordinal) ? 1 : 0);
	}

	[Fact]
	public void Constructs_HeadingTooDeepToNestUnderTheListLineAbove_IsStillAHeading()
		=> MarkdownSyntax.Constructs("# Decide\r\n#### deep").Should().Equal("Markdown heading");

	[Fact]
	public void Constructs_SeveralOnOneLine_AreEachCounted()
		=> MarkdownSyntax.Constructs("**bold** and `code` and [a](https://x.com/y)").Should().Equal("backtick code span", "double-asterisk bold", "Markdown link");

	[Fact]
	public void Constructs_AFenceLine_IsOnlyAFence()
		=> MarkdownSyntax.Constructs("```bash `x` **y**").Should().Equal("code fence");

	[Fact]
	public void Measure_CountsConstructs_AndDescribesTheDistinctKinds()
	{
		using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new { d = "## A\r\nRun `x` and `y`.\r\n- one\r\nSee `z`." }));
		EvaluationContext context = new(document);
		MarkdownSyntaxMeasure measure = new() { Source = new TextSource { Paths = ["$.d"] } };

		measure.Evaluate(context).Should().Be(3);
		measure.Describe(context).Should().Be("Markdown heading; backtick code span");
	}

	[Fact]
	public void Measure_NullContext_Throws()
	{
		MarkdownSyntaxMeasure measure = new() { Source = new TextSource { Paths = ["$.d"] } };

		FluentActions.Invoking(() => measure.Evaluate(null!)).Should().Throw<ArgumentNullException>();
		FluentActions.Invoking(() => measure.Describe(null!)).Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public async Task Assess_MarkdownDescription_IsFlaggedWithWhatWasFound()
	{
		AssessmentReport report = await AssessDescriptionAsync("## Summary\r\nUse `Foo.Bar` here.");

		report.Findings.Should().Contain("✗ Markdown: 2 Markdown construct(s) that Jira renders wrongly (Markdown heading; backtick code span)");
		report.Recommendations.Should().Contain(recommendation => recommendation.StartsWith("Rewrite the description as Jira wiki markup", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Assess_WikiMarkupDescription_LeavesMarkdownOut()
	{
		AssessmentReport report = await AssessDescriptionAsync("h2. Summary\r\nUse {{Foo.Bar}} here.");

		report.Findings.Should().NotContain(finding => finding.Contains("Markdown", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Assess_RemovingTheMarkdown_RaisesTheScore()
	{
		AssessmentReport markdown = await AssessDescriptionAsync("## Summary\r\nUse `Foo.Bar` here, which fails when the export runs with line breaks in the data.");
		AssessmentReport wiki = await AssessDescriptionAsync("h2. Summary\r\nUse {{Foo.Bar}} here, which fails when the export runs with line breaks in the data.");

		wiki.Score!.Value.Should().BeGreaterThan(markdown.Score!.Value);
	}

	[Fact]
	public async Task Assess_Epic_IsFlaggedToo()
	{
		AssessmentReport report = await AssessDescriptionAsync("## Scope\r\nRun `x`.", issueType: "Epic");

		report.Findings.Should().Contain(finding => finding.StartsWith("✗ Markdown: ", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Assess_NotJira_StillReadsTheDescription()
	{
		AssessmentReport report = await AssessDescriptionAsync("## Summary\r\nRun `x`.", platform: "GitHub");

		report.Findings.Should().NotContain(finding => finding.Contains("Markdown", StringComparison.Ordinal));
	}
}
