using PanoramicData.IssueAssessment.Serialization;

namespace PanoramicData.IssueAssessment.Tests.Validation;

/// <summary>
/// Every rubric error (conditions, measures, tiers, placeholders, caps, bands and classifications) is rejected when
/// <see cref="IssueAssessor"/> is constructed, with a message saying what is wrong.
/// </summary>
public class RubricValidationTests
{
	private const string Tiered = """{ "type": "tiered", "tiers": [ { "score": 100, "finding": "ok" } ] }""";

	/// <summary>
	/// A one-category, one-assessment rubric with the given parts substituted.
	/// </summary>
	private static string Rubric(
		string conditions = "{}",
		string category = "",
		string assessment = "",
		string definition = Tiered,
		string caps = "[]",
		string bands = "[]",
		string classifications = "{}")
		=> $$"""
		{
			"executionStrategy": "series",
			"conditions": {{conditions}},
			"categories": [ {
				"name": "C", "description": "C", "assessmentExecutionStrategy": "series" {{category}},
				"assessments": [ { "name": "A", "description": "A", "definition": {{definition}} {{assessment}} } ]
			} ],
			"scoreCaps": {{caps}},
			"gradeBands": {{bands}},
			"classifications": {{classifications}}
		}
		""";

	private static Action Construct(string json) => () => _ = new IssueAssessor(AssessmentRubric.Load(json));

	private static string Tier(string when) => $$"""{ "type": "tiered", "tiers": [ { "when": {{when}}, "score": 0, "finding": "x" }, { "score": 100, "finding": "ok" } ] }""";

	[Fact]
	public void ValidRubric_IsAccepted()
	{
		Construct(Rubric(
			conditions: """{ "bug": { "type": "fieldEquals", "path": "$.type", "values": [ "Bug" ] } }""",
			category: """, "weight": 2, "appliesWhen": { "type": "ref", "name": "bug" }""",
			assessment: """, "appliesWhen": { "type": "not", "condition": { "type": "ref", "name": "bug" } }, "notApplicableExplanation": "n/a for {$.type}" """,
			caps: """[ { "name": "cap", "cap": 0.5, "when": { "type": "assessmentScore", "assessment": "A", "lessThan": 1 } } ]""",
			bands: """[ { "name": "GOOD", "minimumScore": 0.5 }, { "name": "BAD", "minimumScore": 0 } ]""",
			classifications: """{ "kind": [ { "when": { "type": "ref", "name": "bug" }, "value": "bug" }, { "value": "other" } ] }"""))
			.Should().NotThrow();
	}

	public static TheoryData<string, string> InvalidConditions => new()
	{
		{ """{ "type": "all", "conditions": null }""", "combined condition without conditions" },
		{ """{ "type": "any", "conditions": [ { "type": "ref", "name": "missing" } ] }""", "undefined condition 'missing'" },
		{ """{ "type": "not", "condition": null }""", "'not' condition without a condition" },
		{ """{ "type": "measure", "atLeast": 1 }""", "exactly one of Measure and Of" },
		{ """{ "type": "measure", "measure": "m", "of": { "type": "number", "path": "$.a" } }""", "exactly one of Measure and Of" },
		{ """{ "type": "measure", "measure": "m" }""", "undefined measure 'm'" },
		{ """{ "type": "measure", "of": { "type": "itemCount", "path": "bad" } }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "assessmentScore", "assessment": "A" }""", "only score caps and classifications may use" },
		{ """{ "type": "textMatches", "source": { "paths": [] }, "patterns": [ "x" ] }""", "text source without paths" },
		{ """{ "type": "textMatches", "source": null, "patterns": [ "x" ] }""", "text source without paths" },
		{ """{ "type": "textMatches", "source": { "paths": [ "$.a", "bad" ] }, "patterns": [ "x" ] }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "textMatches", "source": { "paths": [ "$.a" ], "section": "(" }, "patterns": [ "x" ] }""", "invalid regex pattern '('" },
		{ """{ "type": "textMatches", "source": { "paths": [ "$.a" ] }, "patterns": [] }""", "has no patterns" },
		{ """{ "type": "textMatches", "source": { "paths": [ "$.a" ] }, "patterns": [ " " ] }""", "has an empty pattern" },
		{ """{ "type": "fieldMatches", "path": "bad", "patterns": [ "x" ] }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "fieldMatches", "path": "$.a", "patterns": [ "[" ] }""", "invalid regex pattern '['" },
		{ """{ "type": "fieldEquals", "path": "$.a", "values": [] }""", "'fieldEquals' condition without values" },
		{ """{ "type": "fieldEquals", "path": "bad", "values": [ "x" ] }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "fieldPresent", "path": "bad" }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "sectionPresent", "source": { "paths": [ "$.a" ] } }""", "whose source has no section" },
		{ """{ "type": "sectionPresent", "source": { "paths": [ "bad" ], "section": "x" } }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "placeholder", "source": { "paths": [ "bad" ] } }""", "invalid JSONPath 'bad'" },
	};

	[Theory]
	[MemberData(nameof(InvalidConditions))]
	public void InvalidCondition_InAppliesWhen_IsRejected(string condition, string message)
	{
		Construct(Rubric(assessment: $$""", "appliesWhen": {{condition}} """)).Should().Throw<ArgumentException>()
			.WithMessage($"Assessment 'A' in category 'C' *{message}*");
	}

	[Theory]
	[MemberData(nameof(InvalidConditions))]
	public void InvalidCondition_AsANamedCondition_IsRejected(string condition, string message)
	{
		Construct(Rubric(conditions: $$"""{ "named": {{condition}} }""")).Should().Throw<ArgumentException>()
			.WithMessage($"Condition 'named' *{message}*");
	}

	public static TheoryData<string, string> InvalidMeasures => new()
	{
		{ "null", "has a missing measure" },
		{ """{ "type": "patternMatchCount", "source": { "paths": [ "$.a" ] }, "patterns": [ "(" ] }""", "invalid regex pattern '('" },
		{ """{ "type": "occurrenceCount", "source": { "paths": [ "$.a" ] }, "pattern": "" }""", "has an empty pattern" },
		{ """{ "type": "wordCount", "source": { "paths": [ "bad" ] } }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "alphanumericCount", "source": { "paths": [ "bad" ] } }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "length", "source": { "paths": [ "bad" ] } }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "criteriaHeadingCount", "source": { "paths": [ "bad" ] } }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "itemCount", "path": "bad" }""", "invalid JSONPath 'bad'" },
		{ """{ "type": "number", "path": "bad" }""", "invalid JSONPath 'bad'" },
	};

	[Theory]
	[MemberData(nameof(InvalidMeasures))]
	public void InvalidMeasure_InATieredAssessment_IsRejected(string measure, string message)
	{
		string definition = $$"""{ "type": "tiered", "measures": { "m": {{measure}} }, "tiers": [ { "score": 100, "finding": "ok" } ] }""";

		Construct(Rubric(definition: definition)).Should().Throw<ArgumentException>().WithMessage($"*{message}*");
	}

	public static TheoryData<string, string> InvalidTiers => new()
	{
		{ """{ "type": "tiered", "tiers": [] }""", "at least one tier" },
		{ """{ "type": "tiered", "tiers": null }""", "at least one tier" },
		{ """{ "type": "tiered", "minimumScore": 5, "maximumScore": 5, "tiers": [ { "score": 5, "finding": "x" } ] }""", "MinimumScore that is not below its MaximumScore" },
		{ """{ "type": "tiered", "tiers": [ { "when": { "type": "fieldPresent", "path": "$.a" }, "score": 1, "finding": "x" } ] }""", "must end with a tier that has no condition" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": "x" }, { "score": 2, "finding": "y" } ] }""", "tier without a condition before its last tier" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": null } ] }""", "tier without a finding" },
		{ """{ "type": "tiered", "tiers": [ { "score": 101, "finding": "x" } ] }""", "tier score of 101 outside its range 0-100" },
		{ """{ "type": "tiered", "tiers": [ { "score": -1, "finding": "x" } ] }""", "tier score of -1 outside its range 0-100" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": "{unknown}" } ] }""", "undefined measure 'unknown' in a placeholder" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": "x", "recommendation": "{m:text}" } ] }""", "undefined measure 'm' in a placeholder" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": "{$bad}" } ] }""", "invalid JSONPath '$bad'" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": "{$.a~(}" } ] }""", "invalid regex pattern '('" },
		{ """{ "type": "tiered", "tiers": [ { "score": 1, "finding": "{$bad~x}" } ] }""", "invalid JSONPath '$bad'" },
		{ """{ "type": "named", "name": "custom" }""", "custom definition 'custom', which was not registered" },
	};

	[Theory]
	[MemberData(nameof(InvalidTiers))]
	public void InvalidTieredDefinition_IsRejected(string definition, string message)
	{
		Construct(Rubric(definition: definition)).Should().Throw<ArgumentException>().WithMessage($"Assessment 'A' in category 'C' *{message}*");
	}

	[Fact]
	public void TierCondition_UsingAnUndefinedMeasure_IsRejected()
	{
		Construct(Rubric(definition: Tier("""{ "type": "measure", "measure": "m", "atLeast": 1 }"""))).Should().Throw<ArgumentException>()
			.WithMessage("*undefined measure 'm'*");
	}

	[Fact]
	public void TierCondition_UsingAnAssessmentScore_IsRejected()
	{
		Construct(Rubric(definition: Tier("""{ "type": "assessmentScore", "assessment": "A" }"""))).Should().Throw<ArgumentException>()
			.WithMessage("*only score caps and classifications may use*");
	}

	[Theory]
	[InlineData("{done}")]
	[InlineData("{$bad}")]
	public void NotApplicableExplanation_WithAnInvalidPlaceholder_IsRejected(string explanation)
	{
		Construct(Rubric(assessment: $$""", "notApplicableExplanation": "{{explanation}}" """)).Should().Throw<ArgumentException>()
			.WithMessage("Assessment 'A' in category 'C' *");
	}

	[Theory]
	[InlineData("""{ "a": { "type": "ref", "name": "a" } }""", "Condition 'a' refers to itself.")]
	[InlineData("""{ "a": { "type": "not", "condition": { "type": "ref", "name": "b" } }, "b": { "type": "any", "conditions": [ { "type": "all", "conditions": [ { "type": "ref", "name": "a" } ] } ] } }""", "Condition '?' refers to itself.")]
	public void CyclicNamedConditions_AreRejected(string conditions, string message)
	{
		Construct(Rubric(conditions: conditions)).Should().Throw<ArgumentException>().WithMessage($"{message}*");
	}

	[Fact]
	public void SharedButAcyclicNamedConditions_AreAccepted()
	{
		Construct(Rubric(conditions: """
			{
				"a": { "type": "all", "conditions": [ { "type": "ref", "name": "c" }, { "type": "ref", "name": "c" } ] },
				"b": { "type": "ref", "name": "c" },
				"c": { "type": "fieldPresent", "path": "$.x" }
			}
			""")).Should().NotThrow();
	}

	[Fact]
	public void NullNamedCondition_IsRejected()
	{
		Construct(Rubric(conditions: """{ "a": null }""")).Should().Throw<ArgumentException>().WithMessage("Condition 'a' is missing.*");
	}

	[Theory]
	[InlineData(""", "weight": -1""", "Category 'C' has invalid weight -1")]
	[InlineData(""", "appliesWhen": { "type": "ref", "name": "missing" }""", "Category 'C' refers to undefined condition 'missing'")]
	public void InvalidCategoryRule_IsRejected(string category, string message)
	{
		Construct(Rubric(category: category)).Should().Throw<ArgumentException>().WithMessage($"{message}*");
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void NonFiniteCategoryWeight_IsRejected(double weight)
	{
		AssessmentOptions options = AssessmentRubric.Load(Rubric());
		options = options with { Categories = [options.Categories[0] with { Weight = weight }] };

		Action act = () => _ = new IssueAssessor(options);

		act.Should().Throw<ArgumentException>().WithMessage("Category 'C' has invalid weight*");
	}

	public static TheoryData<string, string> InvalidCaps => new()
	{
		{ """[ { "name": " ", "cap": 0.5, "when": { "type": "fieldPresent", "path": "$.a" } } ]""", "A score cap has no name." },
		{ """[ { "name": "x", "cap": 1.5, "when": { "type": "fieldPresent", "path": "$.a" } } ]""", "Score cap 'x' must be between 0 and 1." },
		{ """[ { "name": "x", "cap": -0.1, "when": { "type": "fieldPresent", "path": "$.a" } } ]""", "Score cap 'x' must be between 0 and 1." },
		{ """[ { "name": "x", "cap": 0.5, "when": null } ]""", "Score cap 'x' has no condition." },
		{ """[ { "name": "x", "cap": 0.5, "when": { "type": "assessmentScore", "assessment": "Nope" } } ]""", "Score cap 'x' refers to unknown assessment 'Nope'." },
	};

	[Theory]
	[MemberData(nameof(InvalidCaps))]
	public void InvalidScoreCap_IsRejected(string caps, string message)
	{
		Construct(Rubric(caps: caps)).Should().Throw<ArgumentException>().WithMessage($"{message}*");
	}

	[Fact]
	public void NaNScoreCap_IsRejected()
	{
		AssessmentOptions options = AssessmentRubric.Load(Rubric()) with
		{
			ScoreCaps = [new ScoreCap { Name = "x", Cap = double.NaN, When = new FieldPresentCondition { Path = "$.a" } }],
		};

		Action act = () => _ = new IssueAssessor(options);

		act.Should().Throw<ArgumentException>().WithMessage("Score cap 'x' must be between 0 and 1.*");
	}

	public static TheoryData<string, string> InvalidBands => new()
	{
		{ """[ { "name": "", "minimumScore": 0.5 } ]""", "Grade band '' must have a name" },
		{ """[ { "name": "A", "minimumScore": 2 } ]""", "Grade band 'A' must have a name and a minimum score between 0 and 1." },
		{ """[ { "name": "A", "minimumScore": -1 } ]""", "Grade band 'A' must have a name and a minimum score between 0 and 1." },
		{ """[ { "name": "A", "minimumScore": 0.5 }, { "name": "A", "minimumScore": 0.1 } ]""", "Grade band 'A' is defined more than once." },
	};

	[Theory]
	[MemberData(nameof(InvalidBands))]
	public void InvalidGradeBand_IsRejected(string bands, string message)
	{
		Construct(Rubric(bands: bands)).Should().Throw<ArgumentException>().WithMessage($"{message}*");
	}

	[Fact]
	public void NaNGradeBand_IsRejected()
	{
		AssessmentOptions options = AssessmentRubric.Load(Rubric()) with { GradeBands = [new GradeBand { Name = "A", MinimumScore = double.NaN }] };

		Action act = () => _ = new IssueAssessor(options);

		act.Should().Throw<ArgumentException>().WithMessage("Grade band 'A'*");
	}

	public static TheoryData<string, string> InvalidClassifications => new()
	{
		{ """{ "k": [] }""", "Classification 'k' has no rules." },
		{ """{ "k": null }""", "Classification 'k' has no rules." },
		{ """{ "k": [ { "value": " " } ] }""", "Classification 'k' has a rule without a value." },
		{ """{ "k": [ { "when": { "type": "ref", "name": "missing" }, "value": "v" } ] }""", "Classification 'k' refers to undefined condition 'missing'." },
	};

	[Theory]
	[MemberData(nameof(InvalidClassifications))]
	public void InvalidClassification_IsRejected(string classifications, string message)
	{
		Construct(Rubric(classifications: classifications)).Should().Throw<ArgumentException>().WithMessage($"{message}*");
	}

	[Fact]
	public void CustomConditionAndMeasureTypes_AreNotInspected()
	{
		AssessmentOptions options = AssessmentRubric.Load(Rubric()) with
		{
			Conditions = ImmutableDictionary<string, ICondition>.Empty.Add("custom", new AlwaysTrue()),
		};
		options = options with
		{
			Categories =
			[
				options.Categories[0] with
				{
					Assessments =
					[
						options.Categories[0].Assessments[0] with
						{
							Definition = new TieredAssessment
							{
								Measures = ImmutableDictionary<string, IMeasure>.Empty.Add("m", new Seven()),
								Tiers = [new Tier { Score = 100, Finding = "{m}" }],
							},
						},
					],
				},
			],
		};

		Action act = () => _ = new IssueAssessor(options);

		act.Should().NotThrow();
	}

	private sealed class AlwaysTrue : ICondition
	{
		public bool Evaluate(EvaluationContext context) => context is not null;
	}

	private sealed class Seven : IMeasure
	{
		public double Evaluate(EvaluationContext context) => context is null ? 0d : 7d;
	}
}
