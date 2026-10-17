namespace PanoramicData.IssueAssessment.Validation;

/// <summary>
/// Finds named conditions that refer to themselves.
/// </summary>
internal static partial class RuleValidator
{
	/// <summary>
	/// The first named condition that refers to itself, directly or through others.
	/// </summary>
	public static string? CycleError(IReadOnlyDictionary<string, ICondition> conditions)
	{
		foreach (string name in conditions.Keys)
		{
			if (ReachesItself(name, conditions))
			{
				return $"Condition '{name}' refers to itself.";
			}
		}

		return null;
	}

	private static bool ReachesItself(string start, IReadOnlyDictionary<string, ICondition> conditions)
	{
		Stack<string> pending = new(References(conditions[start]));
		HashSet<string> seen = [];
		while (pending.TryPop(out string? name))
		{
			if (name == start)
			{
				return true;
			}

			if (seen.Add(name) && conditions.TryGetValue(name, out ICondition? next))
			{
				foreach (string reference in References(next))
				{
					pending.Push(reference);
				}
			}
		}

		return false;
	}

	private static IEnumerable<string> References(ICondition? condition) => condition switch
	{
		ConditionReference reference => [reference.Name],
		AllCondition all => all.Conditions.SelectMany(References),
		AnyCondition any => any.Conditions.SelectMany(References),
		NotCondition not => References(not.Condition),
		_ => [],
	};
}
