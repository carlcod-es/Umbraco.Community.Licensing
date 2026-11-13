using System.Reflection;
using System.Security.Cryptography;
using Acme.ContactForm.Controllers;
using Acme.ContactForm.Extensions;
using Acme.ContactForm.Form;
using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>
/// The demo picker at service level: real service registration, the real controllers, no site. Demo entries other than
/// the dated one are evaluated against the real clock, which is how the demo site runs.
/// </summary>
public class DemoPickerTests
{
    private const string ExpiringSoon = "Valid: expiring soon";

    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();

    public static TheoryData<string> EntryNames => [.. Entries.Select(e => e.Name)];

    private static DemoKeyEntry Entry(string name) => Entries.Single(e => e.Name == name);

    private static IConfiguration Config(bool demo, string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ContactFormOptions.LicenseKeySettingName] = key,
                ["ContactForm:Demo:Enabled"] = demo ? "true" : "false",
            })
            .Build();

    private static ServiceProvider Build(bool demo, string configured = "Valid: full plan")
    {
        var config = Config(demo, Entry(configured).Key);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContactFormLicensing(config);
        services.AddContactFormDemo(config);
        return services.BuildServiceProvider();
    }

    /// <summary>A container built the way the site builds it, with the controllers over it.</summary>
    private sealed class Rig : IDisposable
    {
        public Rig(bool demo = true, string configured = "Valid: full plan")
        {
            Provider = Build(demo, configured);
            var context = new ControllerContext { HttpContext = new DefaultHttpContext() };
            context.HttpContext.Request.Host = new HostString("localhost");
            var selection = Provider.GetService<DemoKeySelection>();
            Licensing = Provider.GetRequiredService<ContactFormLicensing>();
            Demo = new DemoController(selection) { ControllerContext = context };
            Status = new LicenseStatusController(Licensing, selection) { ControllerContext = context };
            Submissions = new SubmissionsController(Licensing, Provider.GetRequiredService<ISubmissionStore>()) { ControllerContext = context };
        }

        public ServiceProvider Provider { get; }

        public ContactFormLicensing Licensing { get; }

        public DemoController Demo { get; }

        public LicenseStatusController Status { get; }

        public SubmissionsController Submissions { get; }

        public DemoKeySelection Selection => Provider.GetRequiredService<DemoKeySelection>();

        public void Select(string entry) =>
            Assert.IsType<NoContentResult>(Demo.Select(new DemoSelectRequest(entry)));

        public bool ShowUploads => Provider.GetRequiredService<ContactFormViewModelBuilder>().Build("localhost").ShowUploads;

        public void Dispose() => Provider.Dispose();
    }

    // ---- The picker's list

    [Fact]
    public void Entries_list_the_configured_key_first_then_every_demo_entry_and_the_selection()
    {
        using var rig = new Rig();
        var response = rig.Demo.Entries().Value!;

        Assert.Equal(ConfiguredLicenseKeySource.Name, response.Entries[0].Name);
        Assert.False(response.Entries[0].HasDemoDate);
        Assert.Equal(Entries.Select(e => e.Name), response.Entries.Skip(1).Select(e => e.Name));
        Assert.All(response.Entries.Skip(1), (item, i) => Assert.Equal(Entries[i].Description, item.Description));
        Assert.Equal(ConfiguredLicenseKeySource.Name, response.Selected);

        var dated = Assert.Single(response.Entries, e => e.HasDemoDate);
        Assert.Equal(ExpiringSoon, dated.Name);
        Assert.Equal("2030-01-17T12:00:00Z", dated.DemoDate);

        rig.Select("Expired");
        Assert.Equal("Expired", rig.Demo.Entries().Value!.Selected);
    }

    // ---- Choosing

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Choosing_an_entry_changes_the_key_in_force_and_names_the_source(string name)
    {
        using var rig = new Rig();
        var entry = Entry(name);

        rig.Select(name);

        var snapshot = rig.Licensing.Check("localhost");
        Assert.Equal(entry.State, snapshot.State.ToString());
        Assert.Equal($"Demo selection: {name}", snapshot.KeySourceName);

        var response = rig.Status.Status();
        Assert.Equal(entry.State, response.Status.State);
        Assert.Equal($"Demo selection: {name}", response.Status.KeySourceName);
        Assert.Equal(name, response.DemoSelection);
        Assert.Equal(entry.DemoDate, response.DemoDate);
    }

    [Fact]
    public void Choosing_configured_goes_back_to_the_configured_key()
    {
        using var rig = new Rig();
        rig.Select("Expired");
        Assert.Equal(LicenseState.Expired, rig.Licensing.Check("localhost").State);

        rig.Select("configured");

        Assert.Null(rig.Selection.Chosen);
        var snapshot = rig.Licensing.Check("localhost");
        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.Equal(ConfiguredLicenseKeySource.Name, snapshot.KeySourceName);
        var response = rig.Status.Status();
        Assert.Null(response.DemoSelection);
        Assert.Null(response.DemoDate);
    }

    [Fact]
    public void Choosing_the_configured_entry_by_its_listed_name_also_works()
    {
        using var rig = new Rig();
        rig.Select("Expired");
        rig.Select(ConfiguredLicenseKeySource.Name);
        Assert.Null(rig.Selection.Chosen);
    }

    [Fact]
    public void Choosing_an_unknown_entry_is_a_400_and_leaves_the_selection_alone()
    {
        using var rig = new Rig();
        rig.Select("Expired");

        var result = rig.Demo.Select(new DemoSelectRequest("No such entry"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ProblemDetails>(bad.Value);
        Assert.Equal("Expired", rig.Selection.Chosen?.Name);
        Assert.Equal(LicenseState.Expired, rig.Licensing.Check("localhost").State);
    }

    [Fact]
    public void The_picker_with_no_key_entry_reports_missing()
    {
        using var rig = new Rig();
        rig.Select("No key");
        var snapshot = rig.Licensing.Check("localhost");
        Assert.Equal(LicenseState.Missing, snapshot.State);
        Assert.Equal("Demo selection: No key", snapshot.KeySourceName);
    }

    // ---- Scenario: Choose Expired

    [Fact]
    public void Choose_Expired_shows_expired_everywhere_and_hides_uploads()
    {
        using var rig = new Rig();
        Assert.True(rig.ShowUploads);
        Assert.Null(rig.Submissions.List().Banner);

        rig.Select("Expired");

        Assert.Equal(LicenseState.Expired, rig.Licensing.Check("localhost").State);
        Assert.Equal("Expired", rig.Status.Status().Status.State);
        var banner = rig.Submissions.List().Banner;
        Assert.NotNull(banner);
        Assert.Equal([BannerCases.NeedsAttention], banner.Cases);
        Assert.False(rig.ShowUploads);
    }

    // ---- Scenario: Restart

    [Fact]
    public void Restart_starts_on_the_configured_key_with_no_selection()
    {
        var config = Config(demo: true, Entry("Valid: full plan").Key);
        var first = Container(config);
        first.GetRequiredService<DemoKeySelection>().TrySelect("Expired");
        Assert.Equal(LicenseState.Expired, first.GetRequiredService<ContactFormLicensing>().Check("localhost").State);
        first.Dispose();

        using var second = Container(config);

        Assert.Null(second.GetRequiredService<DemoKeySelection>().Chosen);
        var snapshot = second.GetRequiredService<ContactFormLicensing>().Check("localhost");
        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.Equal(ConfiguredLicenseKeySource.Name, snapshot.KeySourceName);

        static ServiceProvider Container(IConfiguration config)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddContactFormLicensing(config);
            services.AddContactFormDemo(config);
            return services.BuildServiceProvider();
        }
    }

    // ---- Scenario: Settings unchanged

    [Fact]
    public void Choosing_writes_no_settings_file()
    {
        string[] Hashes() =>
            [.. Directory.GetFiles(Path.Combine(DemoKeyFiles.SamplesRoot, "Acme.ContactForm.Site"), "appsettings*.json")
                .Order(StringComparer.Ordinal)
                .Select(f => Path.GetFileName(f) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))))];

        var before = Hashes();
        Assert.Contains(before, h => h.StartsWith("appsettings.json:", StringComparison.Ordinal));

        using var rig = new Rig();
        foreach (var name in Entries.Select(e => e.Name).Append("configured"))
        {
            rig.Select(name);
        }

        Assert.Equal(before, Hashes());
    }

    [Fact]
    public void Choosing_does_not_change_the_configuration_the_site_reads()
    {
        var config = Config(demo: true, Entry("Valid: full plan").Key);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContactFormLicensing(config);
        services.AddContactFormDemo(config);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<DemoKeySelection>().TrySelect("Expired");

        Assert.Equal(Entry("Valid: full plan").Key, config[ContactFormOptions.LicenseKeySettingName]);
    }

    // ---- Scenario: Expiring soon at its demo date

    [Fact]
    public void Expiring_soon_at_its_demo_date_through_licensing_admin_data_and_banner()
    {
        using var rig = new Rig();
        rig.Select(ExpiringSoon);

        var snapshot = rig.Licensing.Check("localhost");
        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.True(snapshot.ExpiringSoon);
        Assert.Equal(14, snapshot.DaysLeft);

        var admin = rig.Status.Status().Status;
        Assert.True(admin.ExpiringSoon);
        Assert.Equal("2030-01-31", admin.ExpiryDate);
        Assert.Contains("2030-01-31", admin.ExpiringSoonText);

        var banner = rig.Submissions.List().Banner;
        Assert.NotNull(banner);
        Assert.Contains(BannerCases.ExpiringSoon, banner.Cases);
        Assert.Equal("2030-01-31", banner.ExpiryDate);
    }

    [Fact]
    public void Another_entry_after_the_dated_one_is_back_on_the_real_clock()
    {
        using var rig = new Rig();
        rig.Select(ExpiringSoon);
        rig.Select("Valid: small plan");

        var snapshot = rig.Licensing.Check("localhost");
        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.False(snapshot.ExpiringSoon);
        // 2099-12-31 is decades away on the real clock; at the 2030 demo date it would be about 70 years
        Assert.InRange(snapshot.DaysLeft!.Value, 365 * 70, 365 * 80);
    }

    [Fact]
    public void Demo_clock_is_the_demo_date_only_for_the_dated_entry()
    {
        using var rig = new Rig();
        var clock = rig.Provider.GetRequiredService<DemoClock>();
        var product = rig.Provider.GetRequiredService<ContactFormProduct>();
        Assert.Same(clock, product.Clock);

        foreach (var entry in Entries)
        {
            rig.Select(entry.Name);
            var now = clock.GetUtcNow();
            if (entry.DemoDate is { } date)
            {
                Assert.Equal(DemoKeyFile.ParseInstant(date), now);
            }
            else
            {
                Assert.InRange(now, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
            }
        }

        rig.Select("configured");
        Assert.InRange(clock.GetUtcNow(), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Demo_clock_is_not_the_containers_time_provider_and_the_monthly_count_keeps_the_real_clock()
    {
        using var rig = new Rig();
        Assert.Same(TimeProvider.System, rig.Provider.GetRequiredService<TimeProvider>());

        rig.Select(ExpiringSoon);
        var store = rig.Provider.GetRequiredService<ISubmissionStore>();
        store.Add("Ada", "ada@example.com", "Hello");

        Assert.Equal(1, store.CountThisMonth());
        Assert.True(store.ListThisMonth().Single().ReceivedUtc.Year < 2030);
        Assert.Equal(1, rig.Licensing.Check("localhost").UsedThisMonth);
    }

    // ---- Scenario: Demo setting off

    [Fact]
    public void Demo_setting_off_registers_no_demo_services_and_uses_the_configured_source()
    {
        using var rig = new Rig(demo: false);

        Assert.Null(rig.Provider.GetService<DemoKeySelection>());
        Assert.Null(rig.Provider.GetService<DemoClock>());
        Assert.IsType<ConfiguredLicenseKeySource>(rig.Provider.GetRequiredService<ILicenseKeySource>());
        Assert.Same(TimeProvider.System, rig.Provider.GetRequiredService<ContactFormProduct>().Clock);
        Assert.DoesNotContain(rig.Provider.GetServices<ILicenseKeySource>(), s => s is DemoKeySource);
    }

    [Fact]
    public void Demo_setting_off_answers_404_to_both_demo_endpoints()
    {
        using var rig = new Rig(demo: false);

        Assert.IsType<NotFoundResult>(rig.Demo.Entries().Result);
        Assert.IsType<NotFoundResult>(rig.Demo.Select(new DemoSelectRequest("Expired")));
        Assert.Equal(LicenseState.Valid, rig.Licensing.Check("localhost").State);
    }

    [Fact]
    public void Demo_controllers_are_built_by_the_container_the_way_MVC_builds_them_with_and_without_the_demo()
    {
        foreach (var demo in new[] { false, true })
        {
            using var provider = Build(demo);
            Assert.NotNull(ActivatorUtilities.CreateInstance<DemoController>(provider));
            Assert.NotNull(ActivatorUtilities.CreateInstance<LicenseStatusController>(provider));
        }
    }

    [Fact]
    public void Demo_setting_off_leaves_the_demo_fields_of_license_status_null()
    {
        using var rig = new Rig(demo: false);
        var response = rig.Status.Status();

        Assert.Null(response.DemoSelection);
        Assert.Null(response.DemoDate);
        Assert.Equal("Configured key", response.Status.KeySourceName);
    }

    [Fact]
    public void Demo_setting_missing_is_the_same_as_off()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContactFormLicensing(config);
        services.AddContactFormDemo(config);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(DemoKeySelection));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(DemoClock));
    }

    [Fact]
    public void Demo_setting_off_still_has_the_defaults_of_the_options()
    {
        Assert.False(new ContactFormOptions().Demo.Enabled);
    }

    // ---- Scenario: Editor tries to choose

    private static MethodInfo DemoAction(string name) => typeof(DemoController).GetMethod(name)!;

    [Theory]
    [InlineData(nameof(DemoController.Entries))]
    [InlineData(nameof(DemoController.Select))]
    public void Demo_actions_require_exactly_Settings_access(string actionName)
    {
        var action = DemoAction(actionName);
        var policies = action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(typeof(DemoController).GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Select(a => a.Policy)
            .ToArray();

        Assert.Equal([AuthorizationPolicies.SectionAccessSettings], policies);
        Assert.Empty(typeof(DemoController).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
    }

    [Fact]
    public void A_user_refused_by_the_policy_never_reaches_the_action_so_the_selection_stays_none()
    {
        // The policy refuses before the action runs, so nothing here calls Select; the key in force is still the configured one
        using var rig = new Rig();

        Assert.Null(rig.Selection.Chosen);
        var snapshot = rig.Licensing.Check("localhost");
        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.Equal(ConfiguredLicenseKeySource.Name, snapshot.KeySourceName);
    }

    // ---- Registration

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Registering_twice_registers_each_service_once(bool demo)
    {
        var config = Config(demo, Entry("Valid: full plan").Key);
        var services = new ServiceCollection();
        services.AddLogging();
        for (var i = 0; i < 2; i++)
        {
            services.AddContactFormLicensing(config);
            services.AddContactFormDemo(config);
        }

        foreach (var type in new[]
        {
            typeof(TimeProvider), typeof(ContactFormProduct), typeof(ILicenseKeySource), typeof(ISubmissionStore),
            typeof(LicenseStateLog), typeof(ContactFormLicensing), typeof(ContactFormViewModelBuilder), typeof(ContactFormHandler),
        })
        {
            Assert.Single(services, d => d.ServiceType == type);
        }

        Assert.Equal(demo ? 1 : 0, services.Count(d => d.ServiceType == typeof(DemoKeySelection)));
        Assert.Equal(demo ? 1 : 0, services.Count(d => d.ServiceType == typeof(DemoClock)));

        using var provider = services.BuildServiceProvider();
        Assert.Equal(demo, provider.GetRequiredService<ILicenseKeySource>() is DemoKeySource);
        Assert.Equal(LicenseState.Valid, provider.GetRequiredService<ContactFormLicensing>().Check("localhost").State);
    }

    [Fact]
    public void The_product_and_the_expiring_soon_check_share_one_clock()
    {
        using var provider = Build(demo: true);
        var product = provider.GetRequiredService<ContactFormProduct>();

        Assert.Same(provider.GetRequiredService<DemoClock>(), product.Clock);
        Assert.Same(product, provider.GetRequiredService<ContactFormProduct>());
        Assert.Same(TimeProvider.System, new ContactFormProduct().Clock);
    }

    [Fact]
    public void Selection_is_safe_from_many_threads()
    {
        var selection = new DemoKeySelection();
        var names = Entries.Select(e => e.Name).ToArray();

        Parallel.For(0, 2000, i =>
        {
            Assert.True(selection.TrySelect(names[i % names.Length]));
            _ = selection.Chosen;
        });

        Assert.NotNull(selection.Chosen);
    }
}
