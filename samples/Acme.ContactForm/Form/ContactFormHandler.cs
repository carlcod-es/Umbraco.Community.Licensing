using Acme.ContactForm.Submissions;
using Microsoft.AspNetCore.Http;

namespace Acme.ContactForm.Form;

/// <summary>The posted text fields.</summary>
public sealed record ContactFormPost(string? Name, string? Email, string? Message);

/// <param name="Accepted">False only when a required field is missing; licensing never makes it false.</param>
/// <param name="FieldErrors">Field name to message, for the fields that are missing.</param>
public sealed record ContactFormResult(bool Accepted, IReadOnlyDictionary<string, string> FieldErrors);

/// <summary>
/// Handles a posted form: checks the required fields and records the submission whatever the license state.
/// Licensing only decides whether a posted file is read. Register as a singleton.
/// </summary>
public sealed class ContactFormHandler(ContactFormViewModelBuilder builder, ISubmissionStore submissions)
{
    /// <summary>The one thank-you, the same in every license state.</summary>
    public const string ThankYou = "Thank you. We have received your message.";

    public const long MaxRequestBytes = 1024 * 1024;

    /// <param name="file">The posted file, if any. Ignored unless uploads are shown for this request.</param>
    /// <param name="host">The request host, possibly with a port.</param>
    public ContactFormResult Handle(ContactFormPost post, IFormFile? file, string host)
    {
        ArgumentNullException.ThrowIfNull(post);

        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(post.Name)) errors[nameof(post.Name)] = "Enter your name.";
        if (string.IsNullOrWhiteSpace(post.Email)) errors[nameof(post.Email)] = "Enter your email address.";
        if (string.IsNullOrWhiteSpace(post.Message)) errors[nameof(post.Message)] = "Enter a message.";
        if (errors.Count > 0)
        {
            return new ContactFormResult(false, errors);
        }

        // Checked again for this request: a file is read (name and length only, never its content) only when the form showed it.
        // A file over the request cap is treated as not read; the rest of the submission is kept.
        string? fileName = null;
        long? size = null;
        if (builder.Build(host).ShowUploads && file is { Length: > 0 and <= MaxRequestBytes })
        {
            fileName = Path.GetFileName(file.FileName);
            size = file.Length;
        }

        submissions.Add(post.Name!.Trim(), post.Email!.Trim(), post.Message!.Trim(), fileName, size);
        return new ContactFormResult(true, new Dictionary<string, string>());
    }
}
