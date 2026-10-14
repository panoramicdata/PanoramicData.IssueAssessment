using PanoramicData.IssueAssessment.Serialization;

namespace PanoramicData.IssueAssessment.Rubrics;

/// <summary>
/// Rubrics that ship with the package.
/// </summary>
public static class BuiltInRubrics
{
	private const string TicketQualityResource = "PanoramicData.IssueAssessment.Rubrics.panoramicdata-ticket-quality.json";

	/// <summary>
	/// Gets the Panoramic Data ticket quality rubric: the specification-completeness check Panoramic Data applies to Jira
	/// and GitHub tickets before work or testing starts.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It reads a <see cref="Mapping.NormalizedIssue"/>. Bugs, stories, tasks and change requests are scored on component,
	/// description, testability, steps to reproduce, current versus expected behaviour, fix version and environment,
	/// attachments, documentation, certification questions, UI consistency, business case and (change requests only) a
	/// rollback plan. Epics have their own rubric: design, breakdown, scope and success criteria.
	/// </para>
	/// <para>
	/// A bug without numbered steps to reproduce, a change request without a rollback plan, or acceptance criteria
	/// stated under more than one heading cap the score at 0.79, below EXCELLENT. Grades are EXCELLENT (0.8), GOOD
	/// (0.6), ADEQUATE (0.5), NEEDS IMPROVEMENT (0.3) and POOR. Flags report whether the ticket is a bug, an epic, an
	/// investigation or a UI ticket, and the <c>TestAutomationType</c> classification says whether it suits AI testing
	/// (Ai, Hybrid, Human or None).
	/// </para>
	/// <para>
	/// Each call returns a new copy, so callers can change it with <c>with</c> without affecting others.
	/// </para>
	/// </remarks>
	public static AssessmentOptions PanoramicDataTicketQuality => Load(TicketQualityResource);

	/// <summary>
	/// Gets the JSON of the Panoramic Data ticket quality rubric, for inspection or as a starting point for another.
	/// </summary>
	public static string PanoramicDataTicketQualityJson => Read(TicketQualityResource);

	private static AssessmentOptions Load(string resource) => AssessmentRubric.Load(Read(resource));

	private static string Read(string resource)
	{
		using Stream stream = typeof(BuiltInRubrics).Assembly.GetManifestResourceStream(resource)
			?? throw new InvalidOperationException($"The built-in rubric '{resource}' is missing from the assembly.");
		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	}
}
