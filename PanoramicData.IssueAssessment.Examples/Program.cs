using PanoramicData.IssueAssessment.Examples;

// Each example loads a ticket in its platform's own JSON shape, maps it to the normalized shape the built-in
// categories read, assesses it and prints the scores with their explanations.
using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(30));

await JiraIssueAssessmentExample.RunAsync(cancellation.Token).ConfigureAwait(false);
await GitHubIssueAssessmentExample.RunAsync(cancellation.Token).ConfigureAwait(false);
await ServiceNowTicketAssessmentExample.RunAsync(cancellation.Token).ConfigureAwait(false);
