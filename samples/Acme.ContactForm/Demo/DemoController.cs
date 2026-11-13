using Acme.ContactForm.Demo;
using Acme.ContactForm.Licensing;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;

// Kept in the Controllers namespace so the operation-ID handler in the API composer covers it
namespace Acme.ContactForm.Controllers;

/// <param name="Name">The entry name; the first entry is the configured key.</param>
/// <param name="DemoDate">The demo date (yyyy-MM-dd'T'HH:mm:ss'Z', UTC) while the entry is chosen; null when it has none.</param>
public sealed record DemoEntryItem(string Name, string Description, bool HasDemoDate, string? DemoDate);

/// <param name="Selected">The name of the chosen entry, or the configured key's name when none is chosen.</param>
public sealed record DemoEntriesResponse(IReadOnlyList<DemoEntryItem> Entries, string Selected);

/// <param name="Entry">An entry name, or <c>configured</c> to go back to the configured key.</param>
public sealed record DemoSelectRequest(string Entry);

/// <summary>
/// The demo picker's endpoints, for users with access to the Settings section. Answers 404 when the demo setting is off,
/// when the demo services are not registered. Delete with the <c>Demo/</c> folder.
/// </summary>
[ApiVersion("1.0")]
public class DemoController(DemoKeySelection? demo = null) : ContactFormApiControllerBase
{
    [HttpGet("demo/entries")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType<DemoEntriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DemoEntriesResponse> Entries()
    {
        if (demo is null)
        {
            return NotFound();
        }

        var items = new List<DemoEntryItem>
        {
            new(ConfiguredLicenseKeySource.Name, "The key set in the site's settings.", false, null),
        };
        items.AddRange(demo.Entries.Select(e => new DemoEntryItem(e.Name, e.Description, e.DemoDate is not null, e.DemoDate)));
        return new DemoEntriesResponse(items, demo.Chosen?.Name ?? ConfiguredLicenseKeySource.Name);
    }

    /// <summary>204 on success; 400 when the entry is unknown (the selection is left as it was).</summary>
    [HttpPost("demo/select")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Select([FromBody] DemoSelectRequest request)
    {
        if (demo is null)
        {
            return NotFound();
        }

        return demo.TrySelect(request.Entry)
            ? NoContent()
            : BadRequest(new ProblemDetails
            {
                Title = "Unknown demo entry",
                Detail = $"There is no demo entry named '{request.Entry}'.",
                Status = StatusCodes.Status400BadRequest,
            });
    }
}
