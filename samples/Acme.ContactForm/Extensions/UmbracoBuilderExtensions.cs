using Acme.ContactForm.Demo;
using Umbraco.Cms.Core.DependencyInjection;

namespace Acme.ContactForm.Extensions;

public static class UmbracoBuilderExtensions
{
    /// <summary>Adds Acme Contact Form to the site (and its demo picker when <c>ContactForm:Demo:Enabled</c> is true). Safe to call from both the composer and <c>Program.cs</c>.</summary>
    public static IUmbracoBuilder AddContactForm(this IUmbracoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddContactFormLicensing(builder.Config);
        builder.Services.AddContactFormDemo(builder.Config);
        return builder;
    }
}
