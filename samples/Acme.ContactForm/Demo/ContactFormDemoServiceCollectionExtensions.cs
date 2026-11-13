using Acme.ContactForm.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Acme.ContactForm.Submissions;

namespace Acme.ContactForm.Demo;

public static class ContactFormDemoServiceCollectionExtensions
{
    /// <summary>
    /// When <c>ContactForm:Demo:Enabled</c> is true (read now; there is no runtime toggle), registers the picker's selection,
    /// the demo clock and the demo key source, and replaces the product and licensing so both use the one demo clock. Call it
    /// after <c>AddContactFormLicensing</c> (<see cref="ContactFormDemoComposer"/> runs after the composer that does). Demo mode
    /// wraps the configured key source, not a host's own <c>ILicenseKeySource</c>. Does nothing when the setting is off. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddContactFormDemo(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetValue<bool>($"{ContactFormOptions.SectionName}:Demo:Enabled")
            || services.Any(d => d.ServiceType == typeof(DemoKeySelection)))
        {
            return services;
        }

        services.AddSingleton<DemoKeySelection>();
        services.AddSingleton<DemoClock>();

        services.Replace(ServiceDescriptor.Singleton<ILicenseKeySource>(sp => new DemoKeySource(
            ActivatorUtilities.CreateInstance<ConfiguredLicenseKeySource>(sp),
            sp.GetRequiredService<DemoKeySelection>())));

        // The demo clock reaches the evaluator and the expiring-soon check through the product; the submission store keeps
        // the system clock, so the demo date never changes the monthly count
        services.Replace(ServiceDescriptor.Singleton(sp => new ContactFormProduct(sp.GetRequiredService<DemoClock>())));
        services.Replace(ServiceDescriptor.Singleton(sp => new ContactFormLicensing(
            sp.GetRequiredService<ContactFormProduct>(),
            sp.GetRequiredService<ILicenseKeySource>(),
            sp.GetRequiredService<ISubmissionStore>(),
            sp.GetRequiredService<LicenseStateLog>())));

        return services;
    }
}
