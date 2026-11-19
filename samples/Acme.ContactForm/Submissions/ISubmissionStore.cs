namespace Acme.ContactForm.Submissions;

/// <summary>A stored submission. An upload is kept as its file name and size only, never its content.</summary>
public sealed record Submission(
    string Name,
    string Email,
    string Message,
    string? UploadFileName,
    long? UploadSizeBytes,
    DateTimeOffset ReceivedUtc);

public interface ISubmissionStore
{
    Submission Add(string name, string email, string message, string? uploadFileName = null, long? uploadSizeBytes = null);

    /// <summary>This UTC month's submissions, oldest first.</summary>
    IReadOnlyList<Submission> ListThisMonth();

    int CountThisMonth();
}
