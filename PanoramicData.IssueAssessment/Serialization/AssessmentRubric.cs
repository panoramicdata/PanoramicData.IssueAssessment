using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PanoramicData.IssueAssessment.Serialization;

/// <summary>
/// Reads and writes <see cref="AssessmentOptions"/> as JSON, so a rubric can live in a file shared by every consumer
/// rather than in each one's code.
/// </summary>
/// <remarks>
/// <para>
/// Definitions, conditions, measures and gates carry a <c>"type"</c> discriminator, for example
/// <c>{ "type": "tiered", ... }</c> or <c>{ "type": "fieldEquals", "path": "$.issueType", "values": ["Bug"] }</c>.
/// Property names are camelCase, enums are camelCase strings, and comments and trailing commas are allowed.
/// </para>
/// <para>
/// Definitions that hold delegates, and custom definition types, cannot be written as JSON. A rubric refers to one as
/// <c>{ "type": "named", "name": "..." }</c> and the caller supplies it when loading.
/// </para>
/// <para>
/// Loading does not validate: constructing an <see cref="IssueAssessor"/> does, with messages naming the category and
/// assessment.
/// </para>
/// </remarks>
public static class AssessmentRubric
{
	/// <summary>
	/// Gets the serializer options rubrics are read and written with.
	/// </summary>
	public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

	/// <summary>
	/// Reads a rubric from JSON.
	/// </summary>
	/// <param name="json">The rubric JSON.</param>
	/// <returns>The options the rubric describes.</returns>
	/// <exception cref="JsonException">Thrown when the JSON is not a valid rubric.</exception>
	public static AssessmentOptions Load(string json)
		=> Load(json, ImmutableDictionary<string, IAssessmentDefinition>.Empty);

	/// <summary>
	/// Reads a rubric from JSON, replacing each <see cref="NamedAssessmentDefinition"/> with the definition registered
	/// under its name.
	/// </summary>
	/// <param name="json">The rubric JSON.</param>
	/// <param name="namedDefinitions">Definitions written in code, by name.</param>
	/// <returns>The options the rubric describes.</returns>
	/// <exception cref="JsonException">Thrown when the JSON is not a valid rubric.</exception>
	public static AssessmentOptions Load(string json, IReadOnlyDictionary<string, IAssessmentDefinition> namedDefinitions)
	{
		ArgumentNullException.ThrowIfNull(json);
		ArgumentNullException.ThrowIfNull(namedDefinitions);
		AssessmentOptions options = JsonSerializer.Deserialize<AssessmentOptions>(json, SerializerOptions)
			?? throw new JsonException("The rubric JSON is null.");
		return Resolve(options, namedDefinitions);
	}

	/// <summary>
	/// Reads a rubric from a UTF-8 JSON stream.
	/// </summary>
	/// <param name="utf8Json">The rubric JSON.</param>
	/// <returns>The options the rubric describes.</returns>
	/// <exception cref="JsonException">Thrown when the JSON is not a valid rubric.</exception>
	public static AssessmentOptions Load(Stream utf8Json)
	{
		ArgumentNullException.ThrowIfNull(utf8Json);
		using StreamReader reader = new(utf8Json);
		return Load(reader.ReadToEnd());
	}

	/// <summary>
	/// Writes options as rubric JSON.
	/// </summary>
	/// <param name="options">The options to write.</param>
	/// <returns>Indented rubric JSON.</returns>
	/// <exception cref="NotSupportedException">Thrown when the options hold a definition that cannot be written as JSON;
	/// use a <see cref="NamedAssessmentDefinition"/> in its place.</exception>
	public static string ToJson(AssessmentOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return JsonSerializer.Serialize(options, SerializerOptions);
	}

	private static AssessmentOptions Resolve(AssessmentOptions options, IReadOnlyDictionary<string, IAssessmentDefinition> namedDefinitions)
		=> options with
		{
			Categories = [.. options.Categories.Select(category => category with
			{
				Assessments = [.. category.Assessments.Select(assessment =>
					assessment.Definition is NamedAssessmentDefinition named
						&& namedDefinitions.TryGetValue(named.Name, out IAssessmentDefinition? definition)
						? assessment with { Definition = definition }
						: assessment)],
			})],
		};

	private static JsonSerializerOptions CreateSerializerOptions()
	{
		JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
		};
		options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
		options.MakeReadOnly();
		return options;
	}
}
