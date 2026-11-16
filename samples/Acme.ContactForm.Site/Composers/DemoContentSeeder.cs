using Umbraco.Cms.Core;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Acme.ContactForm.Site.Composers;

/// <summary>
/// Creates, when missing, the "home" and "contact" document types and templates and the published Home
/// (root) and Contact (child) pages. Fixed keys make a second start a no-op. The templates' views are
/// committed at Views/home.cshtml and Views/contact.cshtml.
/// </summary>
public sealed class DemoContentSeeder(
    IContentTypeService contentTypes,
    IContentService contentService,
    ITemplateService templates,
    IDataTypeService dataTypes,
    IShortStringHelper shortStringHelper,
    IWebHostEnvironment environment,
    IRuntimeState runtime,
    ILogger<DemoContentSeeder> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string HomeAlias = "home";
    private const string ContactAlias = "contact";
    private static readonly Guid HomeTemplateKey = new("a1c3e5f7-0000-4000-8000-00000000a001");
    private static readonly Guid HomeTypeKey = new("a1c3e5f7-0000-4000-8000-00000000a002");
    private static readonly Guid HomeContentKey = new("a1c3e5f7-0000-4000-8000-00000000a003");
    private static readonly Guid ContactTemplateKey = new("a1c3e5f7-0000-4000-8000-00000000a004");
    private static readonly Guid ContactTypeKey = new("a1c3e5f7-0000-4000-8000-00000000a005");
    private static readonly Guid ContactContentKey = new("a1c3e5f7-0000-4000-8000-00000000a006");

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run)
        {
            return;
        }

        var existingHome = await contentTypes.GetAsync(HomeTypeKey);
        if (existingHome is not null && !existingHome.PropertyTypes.Any(p => p.Alias == "heading"))
        {
            logger.LogWarning("The demo data comes from an earlier version of the sample: the 'home' document type has no 'heading'. Delete umbraco/Data and start again; nothing was seeded.");
            return;
        }

        var homeTemplate = await EnsureTemplateAsync(HomeTemplateKey, "Home", HomeAlias, cancellationToken);
        var contactTemplate = await EnsureTemplateAsync(ContactTemplateKey, "Contact", ContactAlias, cancellationToken);
        if (homeTemplate is null || contactTemplate is null)
        {
            return;
        }

        var textstring = await dataTypes.GetAsync(UmbracoConstants.DataTypes.Guids.TextstringGuid);
        var textarea = await dataTypes.GetAsync(UmbracoConstants.DataTypes.Guids.TextareaGuid);
        if (textstring is null || textarea is null)
        {
            logger.LogWarning("Could not seed the demo pages: the built-in Textstring or Textarea data type is missing.");
            return;
        }

        var contactType = await EnsureTypeAsync(
            ContactTypeKey, ContactAlias, "Contact", contactTemplate, allowedAtRoot: false, childType: null,
            [("heading", "Heading", textstring), ("intro", "Intro", textarea)]);
        if (contactType is null)
        {
            return;
        }

        var homeType = await EnsureTypeAsync(
            HomeTypeKey, HomeAlias, "Home", homeTemplate, allowedAtRoot: true, childType: contactType,
            [("siteName", "Site name", textstring), ("heading", "Heading", textstring), ("intro", "Intro", textarea), ("footerText", "Footer text", textarea)]);
        if (homeType is null)
        {
            return;
        }

        if (contentService.GetById(HomeContentKey) is null)
        {
            var home = contentService.Create("Home", UmbracoConstants.System.Root, HomeAlias);
            home.Key = HomeContentKey;
            home.TemplateId = homeTemplate.Id;
            home.SetValue("siteName", "Acme");
            home.SetValue("heading", "Acme Contact Form");
            home.SetValue("intro", "A demo site for the Acme Contact Form package. Use the contact page to try the form.");
            home.SetValue("footerText", "Demo site for Acme Contact Form");
            contentService.Save(home);
            contentService.Publish(home, ["*"]);
            logger.LogInformation("Seeded the demo Home page.");
        }

        var homeContent = contentService.GetById(HomeContentKey);
        if (homeContent is not null && contentService.GetById(ContactContentKey) is null)
        {
            var contact = contentService.Create("Contact", homeContent, ContactAlias);
            contact.Key = ContactContentKey;
            contact.TemplateId = contactTemplate.Id;
            contact.SetValue("heading", "Contact us");
            contact.SetValue("intro", "Send us a message using the form below.");
            contentService.Save(contact);
            contentService.Publish(contact, ["*"]);
            logger.LogInformation("Seeded the demo Contact page.");
        }
    }

    private async Task<ITemplate?> EnsureTemplateAsync(Guid key, string name, string alias, CancellationToken cancellationToken)
    {
        var template = await templates.GetAsync(key);
        if (template is not null)
        {
            return template;
        }

        var view = await System.IO.File.ReadAllTextAsync(Path.Combine(environment.ContentRootPath, "Views", alias + ".cshtml"), cancellationToken);
        var created = await templates.CreateAsync(name, alias, view, UmbracoConstants.Security.SuperUserKey, key);
        if (!created.Success || created.Result is null)
        {
            logger.LogWarning("Could not seed the demo {Alias} template: {Status}.", alias, created.Status);
            return null;
        }

        return created.Result;
    }

    private async Task<IContentType?> EnsureTypeAsync(
        Guid key, string alias, string name, ITemplate template, bool allowedAtRoot, IContentType? childType,
        (string Alias, string Name, IDataType DataType)[] properties)
    {
        var existing = await contentTypes.GetAsync(key);
        if (existing is not null)
        {
            return existing;
        }

        var contentType = new ContentType(shortStringHelper, -1)
        {
            Key = key,
            Alias = alias,
            Name = name,
            AllowedAsRoot = allowedAtRoot,
        };
        foreach (var (propertyAlias, propertyName, dataType) in properties)
        {
            contentType.AddPropertyType(new PropertyType(shortStringHelper, dataType, propertyAlias) { Name = propertyName });
        }

        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
        if (childType is not null)
        {
            contentType.AllowedContentTypes = [new ContentTypeSort(childType.Key, 0, childType.Alias)];
        }

        var created = await contentTypes.CreateAsync(contentType, UmbracoConstants.Security.SuperUserKey);
        if (!created.Success)
        {
            logger.LogWarning("Could not seed the demo {Alias} document type: {Status}.", alias, created.Result);
            return null;
        }

        return contentType;
    }
}
