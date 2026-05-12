namespace PanoramicData.IssueAssessment.Utilities;

/// <summary>
/// Exception thrown when a JSONPath expression is malformed.
/// </summary>
public sealed class InvalidJsonPathException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidJsonPathException"/> class.
	/// </summary>
	/// <param name="message">Exception message.</param>
	public InvalidJsonPathException(string message)
		: base(message)
	{
	}
}
