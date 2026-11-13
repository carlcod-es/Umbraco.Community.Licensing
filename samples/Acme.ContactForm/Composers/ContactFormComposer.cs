using Acme.ContactForm.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Acme.ContactForm.Composers;

/// <summary>Registers the package's services when the site starts.</summary>
public sealed class ContactFormComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AddContactForm();
}
