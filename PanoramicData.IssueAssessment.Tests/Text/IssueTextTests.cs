using PanoramicData.IssueAssessment.Text;

namespace PanoramicData.IssueAssessment.Tests.Text;

/// <summary>
/// Tests for <see cref="IssueText"/>.
/// </summary>
public class IssueTextTests
{
	private const string Steps = @"(?:reproduc|\brepro\b)";

	public static TheoryData<string, string?> Sections => new()
	{
		// Wiki heading: closes at the next wiki heading of the same or stronger level; sub-headings stay inside.
		{ "h2. Steps to reproduce\r\n# one\r\n# two\r\nh3. Detail\r\nmore\r\nh2. Expected\r\nx", "\r\n# one\r\n# two\r\nh3. Detail\r\nmore\r\n" },
		// In wiki text "# item" and a "## item" continuing it are list items, not headings.
		{ "h2. Steps to reproduce\n# one\n## nested\n# two\nh2. Next", "\n# one\n## nested\n# two\n" },
		// A Markdown "##" heading after wiki text closes the section when it is not a list item.
		{ "h2. Steps to reproduce\ntext\n\n## Next\nafter", "\ntext\n\n" },
		// ATX heading.
		{ "## Steps to reproduce\n1. open\n### Sub\nx\n## Next", "\n1. open\n### Sub\nx\n" },
		// Double-asterisk bold heading closes at the next bold line.
		{ "**Steps to reproduce**\n1. open\n**Expected**\nx", "\n1. open\n" },
		// Single-asterisk (Jira wiki) bold heading.
		{ "*Steps to reproduce*\n# open\n*Expected*\nx", "\n# open\n" },
		// Label line.
		{ "Steps to reproduce:\n1. open\nExpected result:\nx", "\n1. open\n" },
		// Heading without a closer runs to the end.
		{ "Intro\n## Repro\nall the rest", "\nall the rest" },
		// Bold heading without a closer runs to the end.
		{ "**Steps to reproduce**\n1. open\n2. export", "\n1. open\n2. export" },
		// A Markdown heading on the first line of wiki text has no previous line to continue.
		{ "## Repro\nbody\nh2. Next\nx", "\nbody\n" },
		{ "No heading here", null },
		{ "   ", null },
	};

	[Theory]
	[MemberData(nameof(Sections))]
	public void SectionBody_ReadsTheSectionForEachDialect(string text, string? expected)
	{
		IssueText.SectionBody(text, Steps).Should().Be(expected);
	}

	[Fact]
	public void SectionBody_SeveralMatchingHeadings_ReturnsTheLongestBody()
	{
		const string text = "## Repro\nshort\n## Other\nx\n## Steps to reproduce\nmuch longer body here\n## End";

		IssueText.SectionBody(text, Steps).Should().Be("\nmuch longer body here\n");
	}

	[Fact]
	public void SectionBody_WikiTextHashLines_CannotOpenASection()
	{
		// "# Repro" is a list item; "## Repro nested" continues it; and "## Repro heading", whose previous non-blank line
		// is that "##" item, continues the list too. Yoda's Get-IssueSectionBody returns null for this text.
		const string text = "h2. Summary\n# Repro the issue\n## Repro nested\n\n## Repro heading\nbody";

		IssueText.SectionBody(text, Steps).Should().BeNull();
	}

	[Fact]
	public void SectionBody_WikiTextMarkdownHeadingAfterProse_OpensASection()
	{
		const string text = "h2. Summary\nprose\n\n## Repro heading\nbody";

		IssueText.SectionBody(text, Steps).Should().Be("\nbody");
	}

	[Theory]
	[InlineData(null)]
	[InlineData(" ")]
	public void SectionBody_BlankPattern_Throws(string? pattern)
	{
		Action act = () => IssueText.SectionBody("x", pattern!);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void CriteriaHeadings_SkipsListItemsAndSectionsHoldingAMappingTable()
	{
		// "Definition of done" runs to the end (bold and h3. lines are weaker), so its section holds the
		// criterion-to-test table and is evidence, as is "Verification evidence". Matches Yoda's Get-IssueCriteriaHeadings.
		const string text = """
			h2. Acceptance criteria
			# a wiki list item about criteria
			h2. Definition of done:
			done
			*Verification evidence*
			|| Criterion || Test ||
			| one | test A |
			h3. Other
			""";

		IssueText.CriteriaHeadings(text).Should().Equal("Acceptance criteria");
	}

	[Fact]
	public void CriteriaHeadings_LabelledHeadingsWithoutATable_AreAllCounted()
	{
		const string text = "h2. Acceptance criteria\na\nh2. Definition of done:\nb\nh2. Verification evidence\nc";

		IssueText.CriteriaHeadings(text).Should().Equal("Acceptance criteria", "Definition of done", "Verification evidence");
	}

	[Fact]
	public void CriteriaHeadings_MarkdownText_CountsAtxAndBoldHeadings()
	{
		const string text = "## Acceptance Criteria\n- a\n**Success criteria:**\n- b\n# Criteria item";

		IssueText.CriteriaHeadings(text).Should().Equal("Acceptance Criteria", "Success criteria", "Criteria item");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void CriteriaHeadings_BlankText_IsEmpty(string? text)
	{
		IssueText.CriteriaHeadings(text).Should().BeEmpty();
	}

	[Theory]
	[InlineData(null, true)]
	[InlineData("  ", true)]
	[InlineData("TBD", true)]
	[InlineData("* N/A", true)]
	[InlineData("1. To be confirmed.", true)]
	[InlineData("- see above", true)]
	[InlineData("Restore the backup", false)]
	public void IsPlaceholder_RecognisesPlaceholderBodies(string? body, bool expected)
	{
		IssueText.IsPlaceholder(body).Should().Be(expected);
	}

	[Theory]
	[InlineData(null, 0)]
	[InlineData("", 0)]
	[InlineData("  one  two\tthree\n", 3)]
	public void WordCount_CountsWords(string? text, int expected)
	{
		IssueText.WordCount(text).Should().Be(expected);
	}

	[Theory]
	[InlineData(null, 0)]
	[InlineData("a1-*é", 2)]
	public void AlphanumericCount_CountsAsciiLettersAndDigits(string? text, int expected)
	{
		IssueText.AlphanumericCount(text).Should().Be(expected);
	}

	[Theory]
	[InlineData("h3. Title", 3)]
	[InlineData("  ## Title", 2)]
	[InlineData("**Bold**", 7)]
	[InlineData("", 7)]
	[InlineData(null, 7)]
	public void HeadingLevel_MapsBothDialectsToOneScale(string? line, int expected)
	{
		IssueText.HeadingLevel(line).Should().Be(expected);
	}
}
