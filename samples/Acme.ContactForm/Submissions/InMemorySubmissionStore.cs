namespace Acme.ContactForm.Submissions;

/// <summary>Keeps submissions in memory while the site runs. Register as a singleton, with the real clock.
/// An upload is kept as name and size only; a size supplied without a file name is dropped.</summary>
// Unbounded on purpose: a real product needs real storage, and spam handling on a public form is out of scope here.
public sealed class InMemorySubmissionStore(TimeProvider timeProvider) : ISubmissionStore
{
    private readonly Lock _gate = new();
    private readonly List<Submission> _items = [];

    public Submission Add(string name, string email, string message, string? uploadFileName = null, long? uploadSizeBytes = null)
    {
        var hasUpload = uploadFileName is not null;
        var submission = new Submission(
            name, email, message,
            hasUpload ? uploadFileName : null,
            hasUpload ? uploadSizeBytes : null,
            timeProvider.GetUtcNow().ToUniversalTime());
        lock (_gate)
        {
            _items.Add(submission);
        }

        return submission;
    }

    public IReadOnlyList<Submission> ListThisMonth()
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        lock (_gate)
        {
            return _items.Where(s => s.ReceivedUtc.Year == now.Year && s.ReceivedUtc.Month == now.Month).ToList();
        }
    }

    public int CountThisMonth() => ListThisMonth().Count;
}
