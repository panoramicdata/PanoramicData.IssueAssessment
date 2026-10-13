namespace PanoramicData.IssueAssessment.Assessments;

/// <summary>
/// Assesses whether a selected date value falls within an inclusive range.
/// </summary>
/// <remarks>
/// With a <see cref="DateFormat"/>, values must match it exactly; with a blank format, any date or date-time the
/// invariant culture understands is accepted. A value carrying an offset is compared as UTC. A value that cannot be
/// parsed scores <see cref="ValueAssessment.FailureScore"/> without a failure reason.
/// </remarks>
public sealed record DateRangeAssessment : ValueAssessment
{
	/// <summary>
	/// Gets the inclusive minimum date.
	/// </summary>
	public DateTime? MinDate { get; init; }

	/// <summary>
	/// Gets the inclusive maximum date.
	/// </summary>
	public DateTime? MaxDate { get; init; }

	/// <summary>
	/// Gets the exact format source values must have, or a blank string for flexible parsing.
	/// </summary>
	public string DateFormat { get; init; } = "O";

	private protected override string ValueDescription => "Date value";

	private bool HasFormat => !string.IsNullOrWhiteSpace(DateFormat);

	/// <summary>
	/// The format used to write dates into explanations: the configured format, or the sortable format when parsing is
	/// flexible.
	/// </summary>
	private string DisplayFormat => HasFormat ? DateFormat : "s";

	internal override string DescribeSettings()
		=> string.Create(CultureInfo.InvariantCulture, $"{MinDate?.Ticks}:{MaxDate?.Ticks}:{DateFormat}");

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetString(value, out string text))
		{
			return NotFound();
		}

		if (!TryParseDate(text, out DateTime actualDate))
		{
			return Scored(false, HasFormat
				? $"Date value '{text}' at '{JsonPath}' could not be parsed with format '{DateFormat}'."
				: $"Date value '{text}' at '{JsonPath}' could not be parsed as a date.");
		}

		bool isInRange = IsWithin(actualDate, MinDate, MaxDate);
		return Scored(isInRange, RangeExplanation(
			$"Date {actualDate.ToString(DisplayFormat, CultureInfo.InvariantCulture)}",
			isInRange,
			MinDate?.ToString(DisplayFormat, CultureInfo.InvariantCulture) ?? "-inf",
			MaxDate?.ToString(DisplayFormat, CultureInfo.InvariantCulture) ?? "+inf"));
	}

	private bool TryParseDate(string value, out DateTime date)
	{
		bool parsed = HasFormat
			? DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)
			: DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date);

		// RoundtripKind converts a value carrying an offset (+01:30) to the machine's local time. Compare it as UTC
		// instead, so the result does not depend on the time zone of the machine running the assessment.
		if (parsed && date.Kind == DateTimeKind.Local)
		{
			date = date.ToUniversalTime();
		}

		return parsed;
	}
}
