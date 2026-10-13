using System.Globalization;
using PanoramicData.IssueAssessment.Core;
using PanoramicData.IssueAssessment.Results;

namespace PanoramicData.IssueAssessment.Examples;

internal static class ResultPrinter
{
	public static void Print(string heading, IAssessmentResult result)
	{
		Console.WriteLine();
		Console.WriteLine($"=== {heading}: overall {Percent(result.Aggregations.Weighted.WeightedAverage)} ===");

		foreach (CategoryResult category in result.Categories)
		{
			Console.WriteLine($"  {category.CategoryName,-22} {Percent(category.Score.NormalizedScore),5}");
			foreach (AssessmentResult assessment in category.Assessments)
			{
				string mark = assessment.Score.NormalizedScore >= 1d ? "+" : "-";
				Console.WriteLine($"      {mark} {assessment.AssessmentName}: {assessment.ExplanationText}");
			}
		}
	}

	private static string Percent(double? value)
		=> value is null ? "n/a" : value.Value.ToString("P0", CultureInfo.InvariantCulture);
}
