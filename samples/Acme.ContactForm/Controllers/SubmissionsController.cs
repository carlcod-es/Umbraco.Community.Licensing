using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;

namespace Acme.ContactForm.Controllers;

/// <summary>
/// One submission as the Submissions dashboard shows it. An upload is its file name and size only. When <paramref name="Hidden"/>
/// the license does not cover it: the email is masked, <paramref name="Note"/> says why and every other detail is null.
/// </summary>
public sealed record SubmissionItem(
    bool Hidden,
    string? Name,
    string Email,
    string? Message,
    string? UploadFileName,
    long? UploadSizeBytes,
    DateTimeOffset? ReceivedUtc,
    string? Note)
{
    public const string HiddenNote = "Details hidden until the license covers this submission.";

    public static SubmissionItem Shown(Submission s) =>
        new(false, s.Name, s.Email, s.Message, s.UploadFileName, s.UploadSizeBytes, s.ReceivedUtc, null);

    public static SubmissionItem Covered(Submission s) =>
        new(true, null, EmailMask.Mask(s.Email), null, null, null, null, HiddenNote);
}

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
        // One snapshot for the banner and the visibility, so both describe the same month
        var snapshot = licensing.Check(Request.Host.Value ?? "");
        var all = submissions.ListThisMonth();
        var hidden = SubmissionVisibility.HiddenFlags(snapshot, all.Count);
        var items = all
            .Select((s, i) => hidden[i] ? SubmissionItem.Covered(s) : SubmissionItem.Shown(s))
            .ToList();
        return new SubmissionsResponse(items, EditorLicenseBanner.From(snapshot));
    }
}
