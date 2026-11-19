using Acme.ContactForm.Licensing;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;

namespace Acme.ContactForm.Controllers;

/// <summary>The licensing dashboard's data, for users with access to the Settings section.</summary>
[ApiVersion("1.0")]
public class LicenseStatusController(ContactFormLicensing licensing) : ContactFormApiControllerBase
{
    [HttpGet("license/status")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType<AdminLicenseStatus>(StatusCodes.Status200OK)]
    public AdminLicenseStatus Status() =>
        AdminLicenseStatus.From(licensing.Check(Request.Host.Value ?? ""));
}
