using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Acme.ContactForm.Site.Composers;

/// <summary>Seeds the demo Home and Contact pages when the site has started.</summary>
public sealed class DemoContentComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoContentSeeder>();
}
