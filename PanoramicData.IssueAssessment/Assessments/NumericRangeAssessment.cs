namespace PanoramicData.IssueAssessment.Assessments;

/// <summary>
/// Assesses whether a selected numeric value falls within an inclusive range.
/// </summary>
/// <remarks>
/// Accepts JSON numbers and numeric strings in the invariant culture. NaN, infinities and numbers too large for a
/// <see cref="double"/> are treated as not found.
/// </remarks>
public sealed record NumericRangeAssessment : ValueAssessment
{
	/// <summary>
	/// Gets the inclusive minimum allowed value.
	/// </summary>
	public double? MinValue { get; init; }

	/// <summary>
	/// Gets the inclusive maximum allowed value.
	/// </summary>
	public double? MaxValue { get; init; }

	private protected override string ValueDescription => "Numeric value";

	internal override string DescribeSettings()
		=> string.Create(CultureInfo.InvariantCulture, $"{MinValue:R}:{MaxValue:R}");

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetNumericValue(value, out double actualValue))
		{
			return NotFound();
		}

		bool isInRange = IsWithin(actualValue, MinValue, MaxValue);
		return Scored(isInRange, RangeExplanation(
			$"Value {actualValue.ToString(CultureInfo.InvariantCulture)}",
			isInRange,
			MinValue?.ToString(CultureInfo.InvariantCulture) ?? "-inf",
			MaxValue?.ToString(CultureInfo.InvariantCulture) ?? "+inf"));
	}

	private static bool TryGetNumericValue(JsonElement value, out double actualValue)
	{
		if (value.ValueKind == JsonValueKind.Number)
		{
			// A JSON number always converts; one too large for a double becomes infinity, which is rejected.
			actualValue = value.GetDouble();
			return double.IsFinite(actualValue);
		}

		if (value.ValueKind == JsonValueKind.String)
		{
			// "NaN", "Infinity" and out-of-range values parse, but are not numbers a range can meaningfully contain.
			return double.TryParse(
				value.GetString(),
				NumberStyles.Float | NumberStyles.AllowThousands,
				CultureInfo.InvariantCulture,
				out actualValue)
				&& double.IsFinite(actualValue);
		}

		actualValue = default;
		return false;
	}
}
