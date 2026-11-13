using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;

namespace Acme.ContactForm.Controllers;

/// <summary>One submission as the Submissions dashboard shows it. An upload is its file name and size only.</summary>
public sealed record SubmissionItem(
    string Name,
    string Email,
    string Message,
    string? UploadFileName,
    long? UploadSizeBytes,
    DateTimeOffset ReceivedUtc);

/// <summary>This month's submissions and the editor's banner data (null when no banner is due).</summary>
public sealed record SubmissionsResponse(IReadOnlyList<SubmissionItem> Submissions, EditorLicenseBanner? Banner);

/// <summary>This UTC month's submissions, for users with access to the Content section.</summary>
[ApiVersion("1.0")]
public class SubmissionsController(ContactFormLicensing licensing, ISubmissionStore submissions) : ContactFormApiControllerBase
{
    [HttpGet("submissions")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
    [ProducesResponseType<SubmissionsResponse>(StatusCodes.Status200OK)]
    public SubmissionsResponse List()
    {
        // Check before listing so the banner and the list describe the same month
        var snapshot = licensing.Check(Request.Host.Value ?? "");
        var items = submissions.ListThisMonth()
            .Select(s => new SubmissionItem(s.Name, s.Email, s.Message, s.UploadFileName, s.UploadSizeBytes, s.ReceivedUtc))
            .ToList();
        return new SubmissionsResponse(items, EditorLicenseBanner.From(snapshot));
    }
}
