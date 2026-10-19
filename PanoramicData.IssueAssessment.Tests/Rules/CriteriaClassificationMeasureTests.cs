namespace PanoramicData.IssueAssessment.Tests.Rules;

/// <summary>
/// Unit tests for <see cref="CriteriaClassificationMeasure"/>: which criteria count as classified MUST, SHOULD or MAY.
/// </summary>
public class CriteriaClassificationMeasureTests : TestBase
{
	private const string Heading = "h2. Acceptance Criteria\r\n";

	[Theory]
	[InlineData("# MUST: x")]
	[InlineData("# *MUST* x")]
	[InlineData("* (SHOULD) x")]
	[InlineData("| MAY 1 | x |")]
	[InlineData("1. MUST - x")]
	[InlineData("# *MUST* - PASS when a reference is struck through on its page")]
	[InlineData("- MAY x")]
	public void LeadingKeywordForms_AreClassified(string criterion)
		=> Counts(criterion).Should().Be((1, 0, criterion.Contains("MUST", StringComparison.Ordinal) ? 1 : 0));

	[Fact]
	public void InlineKeyword_Ms27557ProseForm_IsClassified()
		=> Counts("# SECTIONPAGES in a header or footer MUST show the page count of its section\r\n# ChartMagic MUST draw X and Y axis titles in regular weight")
			.Should().Be((2, 0, 2));

	[Theory]
	[InlineData("# The schedule list SHOULD show the new offset", 0)]
	[InlineData("# The audit log MAY record the change.", 0)]
	[InlineData("# The run time MUST stay the same, and it MUST not drift", 1)]
	public void OneDistinctInlineKeyword_IsClassified(string criterion, int must)
		=> Counts(criterion).Should().Be((1, 0, must));

	[Theory]
	[InlineData("# The run time MUST stay the same and MAY be logged")]
	[InlineData("# SECTIONPAGES SHOULD refresh; NUMPAGES MUST refresh")]
	public void MixedInlineKeywords_AreUnclassified(string criterion)
		=> Counts(criterion).Should().Be((1, 1, 0));

	[Fact]
	public void MixedKeywords_StartingWithOne_TakeTheLeadingOne()
		=> Counts("# MUST: the run time stays the same and MAY be logged").Should().Be((1, 0, 1));

	[Theory]
	[InlineData("# the run time must stay the same")]
	[InlineData("# the audit log may record the change and should be kept")]
	[InlineData("# the MUSTARD theme keeps its MAYBE flag")]
	public void LowercaseOrEmbeddedKeywords_AreUnclassified(string criterion)
		=> Counts(criterion).Should().Be((1, 1, 0));

	[Fact]
	public void LabelLine_Ms27609Form_ClassifiesTheUnlabelledItemsBelowIt()
		=> Counts("*MUST*\r\n# A value encrypted under the old key is readable\r\n# The rotation is logged")
			.Should().Be((2, 0, 2));

	[Theory]
	[InlineData("*MUST*")]
	[InlineData("*MUST* (the ticket cannot pass without these)")]
	[InlineData("MUST:")]
	[InlineData("(MUST)")]
	[InlineData("[MUST]")]
	public void LabelLineForms_ClassifyTheItemsBelow(string label)
		=> Counts($"{label}\r\n# A value encrypted under the old key is readable").Should().Be((1, 0, 1));

	[Fact]
	public void LabelScope_EndsAtTheNextLabel()
		=> Counts("*MUST*\r\n# The key rotates\r\n# The rotation is logged\r\n\r\n*SHOULD*\r\n# The admin is emailed\r\n\r\n*MAY*\r\n# The dashboard shows a banner")
			.Should().Be((4, 0, 2));

	[Fact]
	public void LabelScope_EndsAtAHeading()
		=> Counts("*MUST*\r\n# The key rotates\r\nh3. Notes\r\n# The rotation is logged")
			.Should().Be((2, 1, 1));

	[Fact]
	public void ItemsBeforeAnyLabel_AreUnclassified()
		=> Counts("# The key rotates\r\n*MUST*\r\n# The rotation is logged").Should().Be((2, 1, 1));

	[Fact]
	public void ItemWithItsOwnKeyword_KeepsItUnderALabel()
		=> Counts("*MUST*\r\n# The rotation SHOULD be logged\r\n# SHOULD: the admin is emailed\r\n# The key rotates")
			.Should().Be((3, 0, 1));

	[Fact]
	public void NestedSubPoint_IsNotALabel()
		=> Counts("*SHOULD*\r\n# The admin is emailed\r\n** MUST include the tenant name\r\n# The dashboard shows a banner")
			.Should().Be((2, 0, 0));

	[Fact]
	public void LowercaseLabelLine_IsNotALabel()
		=> Counts("must:\r\n# The key rotates").Should().Be((1, 1, 0));

	private static (double Items, double Unclassified, double Must) Counts(string criteria)
	{
		using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new { d = Heading + criteria + "\r\n" }));
		EvaluationContext context = new(document);
		double Measure(CriteriaClassificationKind kind)
			=> new CriteriaClassificationMeasure { Path = "$.d", Section = @"acceptance\s+criteria", Count = kind }.Evaluate(context);

		return (Measure(CriteriaClassificationKind.Items), Measure(CriteriaClassificationKind.Unclassified), Measure(CriteriaClassificationKind.Must));
	}
}
