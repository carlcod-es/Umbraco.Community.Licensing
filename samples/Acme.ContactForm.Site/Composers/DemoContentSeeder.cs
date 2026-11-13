using Umbraco.Cms.Core;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Acme.ContactForm.Site.Composers;

/// <summary>
/// Creates, when missing, a "home" document type and template and one published root page that renders the form.
/// Fixed keys make a second start a no-op. The template's view is committed at Views/home.cshtml.
/// </summary>
public sealed class DemoContentSeeder(
    IContentTypeService contentTypes,
    IContentService contentService,
    ITemplateService templates,
    IShortStringHelper shortStringHelper,
    IWebHostEnvironment environment,
    IRuntimeState runtime,
    ILogger<DemoContentSeeder> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string Alias = "home";
    private static readonly Guid TemplateKey = new("a1c3e5f7-0000-4000-8000-00000000a001");
    private static readonly Guid ContentTypeKey = new("a1c3e5f7-0000-4000-8000-00000000a002");
    private static readonly Guid ContentKey = new("a1c3e5f7-0000-4000-8000-00000000a003");

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run)
        {
            return;
        }

        var template = await templates.GetAsync(TemplateKey);
        if (template is null)
        {
            var created = await templates.CreateAsync("Home", Alias, await System.IO.File.ReadAllTextAsync(Path.Combine(environment.ContentRootPath, "Views", "home.cshtml"), cancellationToken), UmbracoConstants.Security.SuperUserKey, TemplateKey);
            if (!created.Success || created.Result is null)
            {
                logger.LogWarning("Could not seed the demo home template: {Status}.", created.Status);
                return;
            }

            template = created.Result;
        }

        var contentType = await contentTypes.GetAsync(ContentTypeKey);
        if (contentType is null)
        {
            contentType = new ContentType(shortStringHelper, -1)
            {
                Key = ContentTypeKey,
                Alias = Alias,
                Name = "Home",
                AllowedAsRoot = true,
            };
            contentType.AllowedTemplates = [template];
            contentType.SetDefaultTemplate(template);
            var createdType = await contentTypes.CreateAsync(contentType, UmbracoConstants.Security.SuperUserKey);
            if (!createdType.Success)
            {
                logger.LogWarning("Could not seed the demo home document type: {Status}.", createdType.Result);
                return;
            }
        }

        if (contentService.GetById(ContentKey) is null)
        {
            var content = contentService.Create("Home", UmbracoConstants.System.Root, Alias);
            content.Key = ContentKey;
            content.TemplateId = template.Id;
            contentService.Save(content);
            contentService.Publish(content, ["*"]);
            logger.LogInformation("Seeded the demo home page.");
        }
    }
}
