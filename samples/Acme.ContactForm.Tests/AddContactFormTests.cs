using Acme.ContactForm.Extensions;
using Acme.ContactForm.Form;
using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

// Exercises the IServiceCollection extension that IUmbracoBuilder.AddContactForm() delegates to; no site is built.
public class AddContactFormTests
{
    private static readonly Type[] LicensingServices =
    [
        typeof(TimeProvider),
        typeof(ContactFormProduct),
        typeof(ILicenseKeySource),
        typeof(ISubmissionStore),
        typeof(LicenseStateLog),
        typeof(ContactFormLicensing),
        typeof(ContactFormViewModelBuilder),
        typeof(ContactFormHandler),
    ];

    [Fact]
    public void Calling_AddContactForm_twice_registers_each_service_once_and_resolves_licensing()
    {
        var key = DemoKeyFile.LoadEmbedded().Single(e => e.Name == "Valid: full plan").Key!;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ContactFormOptions.LicenseKeySettingName] = key })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContactFormLicensing(config);
        services.AddContactFormLicensing(config);

        foreach (var type in LicensingServices)
        {
            Assert.Single(services, d => d.ServiceType == type);
        }

        Assert.Single(services, d => d.ServiceType == typeof(IConfigureOptions<ContactFormOptions>));

        using var provider = services.BuildServiceProvider();
        var licensing = provider.GetRequiredService<ContactFormLicensing>();
        Assert.Same(licensing, provider.GetRequiredService<ContactFormLicensing>());

        var snapshot = licensing.Check("localhost");
        Assert.Equal("Configured key", snapshot.KeySourceName);
        Assert.NotEqual(LicenseState.Missing, snapshot.State);
    }

    [Fact]
    public void A_site_key_in_configuration_reaches_the_snapshot()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContactFormLicensing(config);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(LicenseState.Missing, provider.GetRequiredService<ContactFormLicensing>().Check("localhost").State);
    }
}
