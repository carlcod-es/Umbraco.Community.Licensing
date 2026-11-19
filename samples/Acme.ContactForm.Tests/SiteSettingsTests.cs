using System.Text.Json;
using Acme.ContactForm.Extensions;
using Acme.ContactForm.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>The site's committed settings let a clone start with the full-plan key and the demo on.</summary>
public class SiteSettingsTests
{
    private static JsonElement ContactFormSection()
    {
        var path = Path.Combine(DemoKeyFiles.SamplesRoot, "Acme.ContactForm.Site", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return document.RootElement.GetProperty("ContactForm").Clone();
    }

    [Fact]
    public void Committed_key_is_the_valid_full_plan_demo_key()
    {
        var entry = DemoKeyFile.LoadEmbedded().Single(e => e.Name == "Valid: full plan");
        Assert.Equal("Valid", entry.State);
        Assert.Equal(entry.Key, ContactFormSection().GetProperty("LicenseKey").GetString());
    }

    [Fact]
    public void Demo_is_enabled()
    {
        Assert.True(ContactFormSection().GetProperty("Demo").GetProperty("Enabled").GetBoolean());
    }

    [Fact]
    public void Fresh_clone_reports_valid_full_plan_from_the_configured_key()
    {
        var path = Path.Combine(DemoKeyFiles.SamplesRoot, "Acme.ContactForm.Site", "appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContactFormLicensing(config);
        using var provider = services.BuildServiceProvider();

        var status = AdminLicenseStatus.From(provider.GetRequiredService<ContactFormLicensing>().Check("localhost"));

        Assert.Equal("Valid", status.State);
        Assert.Equal("Configured key", status.KeySourceName);
        Assert.Equal(ContactFormOptions.LicenseKeySettingName, status.SettingName);
        Assert.Equal(ContactFormOptions.LicenseKeyEnvironmentVariable, status.EnvironmentVariable);
        Assert.Contains(status.Features, f => f.Name == ContactFormProduct.FileUploadsFeature && f.InEffect);
    }
}
