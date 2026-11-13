using Acme.ContactForm.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

public class LicensingCoreTests
{
    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();
    private static readonly DateTimeOffset Now = DemoKeyFile.FixedClock;

    private static string KeyOf(string name) => Entries.Single(e => e.Name == name).Key!;

    private static ContactFormProduct Product() => new(new TestClock(Now));

    private static IOptionsMonitor<ContactFormOptions> MonitorOver(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddOptions<ContactFormOptions>().Bind(config.GetSection(ContactFormOptions.SectionName));
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<ContactFormOptions>>();
    }

    private static ConfiguredLicenseKeySource SourceFor(string? key)
    {
        var values = new Dictionary<string, string?> { [ContactFormOptions.SectionName + ":LicenseKey"] = key };
        return new(MonitorOver(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Fact]
    public void The_public_key_constant_equals_the_committed_demo_public_key()
    {
        var file = DemoKeyFiles.LoadPublic("demo.public.txt");
        var constant = SigningPublicKey.Parse(ContactFormProduct.DemoPublicKey);
        Assert.Equal(file.SigningKeyId, constant.SigningKeyId);
        Assert.True(ContactFormProduct.TrustedSigningKeys.Contains(file.SigningKeyId));
        Assert.Equal(
            ContactFormProduct.DemoPublicKey,
            DemoKeyFiles.Read("demo.public.txt").Split('\n').Single(l => !l.StartsWith("//", StringComparison.Ordinal) && l.Trim().Length > 0).Trim());
    }

    [Fact]
    public void Feature_names_and_product_id()
    {
        Assert.Equal("acme.contact-form", ContactFormProduct.ProductId);
        Assert.Equal("acme.contact-form", Product().Evaluator.ProductId);
        Assert.Equal(["file-uploads", "max-submissions-per-month", "licensed-domains"],
            [ContactFormProduct.FileUploadsFeature, ContactFormProduct.MaxSubmissionsPerMonthFeature, ContactFormProduct.LicensedDomainsFeature]);
    }

    [Fact]
    public void Demo_key_trusted() =>
        Assert.Equal(LicenseState.Valid, Product().Evaluator.Evaluate(KeyOf("Valid: full plan")).State);

    [Fact]
    public void Other_signer_refused() =>
        Assert.Equal(LicenseState.SigningKeyNotRecognised, Product().Evaluator.Evaluate(KeyOf("Untrusted signer")).State);

    [Fact]
    public void Key_set_by_environment_variable()
    {
        const string prefix = "ACMECFTEST_";
        var name = prefix + ContactFormOptions.LicenseKeyEnvironmentVariable;
        Environment.SetEnvironmentVariable(name, KeyOf("Valid: full plan"));
        try
        {
            var config = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            var reading = new ConfiguredLicenseKeySource(MonitorOver(config)).Read();

            Assert.Equal(KeyOf("Valid: full plan"), reading.Key);
            Assert.Equal(LicenseState.Valid, Product().Evaluator.Evaluate(reading.Key).State);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void Demo_is_off_by_default_and_binds_from_configuration()
    {
        Assert.False(new ContactFormOptions().Demo.Enabled);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ContactForm:Demo:Enabled"] = "true" }).Build();
        Assert.True(config.GetSection(ContactFormOptions.SectionName).Get<ContactFormOptions>()!.Demo.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_key_configured(string? value)
    {
        var reading = SourceFor(value).Read();

        Assert.Null(reading.Key);
        Assert.Equal("Configured key", reading.SourceName);
        Assert.Equal(LicenseState.Missing, Product().Evaluator.Evaluate(reading.Key).State);
    }

    [Fact]
    public void Renewed_key_installed_while_running()
    {
        var provider = new MemoryConfigurationProvider(new MemoryConfigurationSource());
        provider.Set("ContactForm:LicenseKey", KeyOf("Expired"));
        var config = new ConfigurationRoot([provider]);
        var monitor = MonitorOver(config);
        var source = new ConfiguredLicenseKeySource(monitor);
        var evaluator = Product().Evaluator;

        Assert.Equal(LicenseState.Expired, evaluator.Evaluate(source.Read().Key).State);

        provider.Set("ContactForm:LicenseKey", KeyOf("Valid: full plan"));
        config.Reload();

        Assert.Equal(LicenseState.Valid, evaluator.Evaluate(source.Read().Key).State);
    }
}
