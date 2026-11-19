using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Filters;
using Umbraco.Cms.Web.Common.Routing;

namespace Acme.ContactForm.Controllers;

/// <summary>
/// Base of the package's back-office API controllers. It carries no policy: each action names its own, because
/// policies stack as AND and a class-level one would refuse a user the action's own policy admits.
/// </summary>
[ApiController]
[BackOfficeRoute(ContactFormApi.Route)]
[MapToApi(ContactFormApi.ApiName)]
[JsonOptionsName(Umbraco.Cms.Core.Constants.JsonOptionsNames.BackOffice)]
public abstract class ContactFormApiControllerBase : ControllerBase
{
}
