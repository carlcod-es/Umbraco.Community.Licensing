using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Website.Controllers;

namespace Acme.ContactForm.Form;

/// <summary>Posts the contact form. Thin: the work, including the upload size cap, is in <see cref="ContactFormHandler"/>. A request-size attribute has no effect here, as Umbraco reads the form first.</summary>
public sealed class ContactFormSurfaceController(
    IUmbracoContextAccessor umbracoContextAccessor,
    IUmbracoDatabaseFactory databaseFactory,
    ServiceContext services,
    AppCaches appCaches,
    IProfilingLogger profilingLogger,
    IPublishedUrlProvider publishedUrlProvider,
    ContactFormHandler handler)
    : SurfaceController(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
{
    public const string ThankYouKey = "ContactFormThankYou";

    [HttpPost]
    [ValidateUmbracoFormRouteString]
    public IActionResult Submit(ContactFormPost post, IFormFile? upload)
    {
        var result = handler.Handle(post, upload, Request.Host.Value ?? "");
        if (!result.Accepted)
        {
            foreach (var (field, message) in result.FieldErrors)
            {
                ModelState.AddModelError(field, message);
            }

            return CurrentUmbracoPage();
        }

        TempData[ThankYouKey] = ContactFormHandler.ThankYou;
        return RedirectToCurrentUmbracoPage();
    }
}
