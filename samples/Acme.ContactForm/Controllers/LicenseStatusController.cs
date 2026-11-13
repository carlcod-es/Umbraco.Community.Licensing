using Acme.ContactForm.Demo;
using Acme.ContactForm.Licensing;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;

namespace Acme.ContactForm.Controllers;

/// <summary>
/// The dashboard's data and, in the demo site, the demo key in force.
/// </summary>
/// <param name="Status">The licensing dashboard's data.</param>
/// <param name="DemoSelection">The chosen demo entry's name; null when the demo is off or none is chosen. Demo only.</param>
/// <param name="DemoDate">The chosen entry's demo date (yyyy-MM-dd'T'HH:mm:ss'Z', UTC); null when none. Demo only.</param>
public sealed record LicenseStatusResponse(AdminLicenseStatus Status, string? DemoSelection, string? DemoDate);

/// <summary>The licensing dashboard's data, for users with access to the Settings section.</summary>
/// <param name="demo">Present only when the demo is on (<c>Demo/</c>: delete it, its use below and the two demo fields when copying).</param>
[ApiVersion("1.0")]
public class LicenseStatusController(ContactFormLicensing licensing, DemoKeySelection? demo = null) : ContactFormApiControllerBase
{
    [HttpGet("license/status")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType<LicenseStatusResponse>(StatusCodes.Status200OK)]
    public LicenseStatusResponse Status() =>
        new(
            AdminLicenseStatus.From(licensing.Check(Request.Host.Value ?? "")),
            demo?.Chosen?.Name,
            demo?.Chosen?.DemoDate);
}
