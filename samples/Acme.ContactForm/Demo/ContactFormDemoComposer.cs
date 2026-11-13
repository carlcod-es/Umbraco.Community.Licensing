using Acme.ContactForm.Composers;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Acme.ContactForm.Demo;

/// <summary>
/// Registers the demo picker's services when <c>ContactForm:Demo:Enabled</c> is true. Runs after
/// <see cref="ContactFormComposer"/>, which registers the licensing it replaces. Deleted with the <c>Demo/</c> folder.
/// </summary>
[ComposeAfter(typeof(ContactFormComposer))]
public sealed class ContactFormDemoComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddContactFormDemo(builder.Config);
}
