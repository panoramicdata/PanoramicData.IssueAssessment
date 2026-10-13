namespace PanoramicData.IssueAssessment.Assessments;
/// <summary>
/// Assesses whether a selected string value is one of an allowed set.
/// </summary>
/// <remarks>Comparison uses the set's comparer, so case sensitivity is chosen when the set is created.</remarks>
public sealed record EnumAssessment : ValueAssessment
{
	/// <summary>
	/// Gets the allowed string values.
	/// </summary>
	public required ImmutableHashSet<string> AllowedValues { get; init; }

	internal override string DescribeSettings()
		=> string.Join(',', AllowedValues.Order(StringComparer.Ordinal));

	private protected override ValueEvaluation Evaluate(JsonElement value)
	{
		if (!TryGetString(value, out string candidate))
		{
			return NotFound();
		}

		bool isAllowed = AllowedValues.Contains(candidate);
		return Scored(isAllowed, isAllowed
			? $"Value '{candidate}' at '{JsonPath}' is allowed."
			: $"Value '{candidate}' at '{JsonPath}' is not allowed.");
	}
}
