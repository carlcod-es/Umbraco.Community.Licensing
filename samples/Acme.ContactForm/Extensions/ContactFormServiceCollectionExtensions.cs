using Acme.ContactForm.Form;
using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acme.ContactForm.Extensions;

public static class ContactFormServiceCollectionExtensions
{
    /// <summary>
    /// Registers the licensing core, the key source, the state log and the submission store. Safe to call more than once:
    /// each service and the options binding are registered once.
    /// </summary>
    public static IServiceCollection AddContactFormLicensing(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // TryAdd does not dedupe the options binding, so an earlier call is recognised by its product registration.
        if (services.Any(d => d.ServiceType == typeof(ContactFormProduct)))
        {
            return services;
        }

        services.AddOptions<ContactFormOptions>().Bind(configuration.GetSection(ContactFormOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISubmissionStore>(sp => new InMemorySubmissionStore(sp.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<ILicenseKeySource, ConfiguredLicenseKeySource>();
        services.TryAddSingleton<LicenseStateLog>();
        services.TryAddSingleton(sp => new ContactFormProduct(sp.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton(sp => new ContactFormLicensing(
            sp.GetRequiredService<ContactFormProduct>(),
            sp.GetRequiredService<ILicenseKeySource>(),
            sp.GetRequiredService<ISubmissionStore>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<LicenseStateLog>()));
        services.TryAddSingleton<ContactFormViewModelBuilder>();
        services.TryAddSingleton<ContactFormHandler>();

        return services;
    }
}
