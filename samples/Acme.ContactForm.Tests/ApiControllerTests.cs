using System.Reflection;
using System.Text.Json;
using Acme.ContactForm.Controllers;
using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Serialization;
using Umbraco.Cms.Api.Management.Serialization;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

public class ApiControllerTests
{
    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();

    public static TheoryData<string> EntryNames => [.. Entries.Select(e => e.Name)];

    private sealed class FixedSource(string? key) : ILicenseKeySource
    {
        public LicenseKeyReading Read() => new(key, "Test source");
    }

    /// <summary>Finds no types, which is all the back-office serializer settings need from it here.</summary>
    private sealed class NoTypes : ITypeFinder
    {
        public IEnumerable<Assembly> AssembliesToScan => [];

        public Type? GetTypeByName(string name) => null;

        public IEnumerable<Type> FindClassesOfTypeWithAttribute(Type assignTypeFrom, Type attributeType, IEnumerable<Assembly>? assemblies, bool onlyConcreteClasses) => [];

        public IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, IEnumerable<Assembly>? assemblies, bool onlyConcreteClasses) => [];

        public IEnumerable<Type> FindClassesWithAttribute(Type attributeType, IEnumerable<Assembly>? assemblies, bool onlyConcreteClasses) => [];
    }

    /// <summary>The options Umbraco's Management API serialises with (Umbraco's own configuration class), built without a host.</summary>
    private static readonly JsonSerializerOptions BackOfficeJson = BuildBackOfficeJson();

    private static JsonSerializerOptions BuildBackOfficeJson()
    {
        var options = new JsonOptions();
        new ConfigureUmbracoBackofficeJsonOptions(new UmbracoJsonTypeInfoResolver(new NoTypes()))
            .Configure(Umbraco.Cms.Core.Constants.JsonOptionsNames.BackOffice, options);
        return options.JsonSerializerOptions;
    }

    private sealed class Rig
    {
        public Rig(string? key, DateTimeOffset now, string host = "localhost")
        {
            Clock = new TestClock(now);
            Store = new InMemorySubmissionStore(Clock);
            var licensing = new ContactFormLicensing(new ContactFormProduct(Clock), new FixedSource(key), Store, new LicenseStateLog(NullLogger<LicenseStateLog>.Instance));
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(host);
            var controllerContext = new ControllerContext { HttpContext = context };
            Status = new LicenseStatusController(licensing) { ControllerContext = controllerContext };
            Submissions = new SubmissionsController(licensing, Store) { ControllerContext = controllerContext };
        }

        public TestClock Clock { get; }

        public InMemorySubmissionStore Store { get; }

        public LicenseStatusController Status { get; }

        public SubmissionsController Submissions { get; }

        public void Add(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Store.Add("n", "e@example.com", "m");
            }
        }
    }

    private static Rig RigFor(string name, string host = "localhost", DateTimeOffset? now = null)
    {
        var entry = Entries.Single(e => e.Name == name);
        return new Rig(entry.Key, now ?? (entry.DemoDate is { } d ? DemoKeyFile.ParseInstant(d) : DemoKeyFile.FixedClock), host);
    }

    // ---- Policies, at attribute level

    private static IEnumerable<MethodInfo> Actions() =>
        typeof(ContactFormApiControllerBase).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ContactFormApiControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any());

    private static string[] PoliciesOf(MethodInfo action) =>
        [.. action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Select(a => a.Policy ?? "(no policy)")];

    [Theory]
    [InlineData(nameof(LicenseStatusController.Status), AuthorizationPolicies.SectionAccessSettings)]
    [InlineData(nameof(SubmissionsController.List), AuthorizationPolicies.SectionAccessContent)]
    [InlineData(nameof(DemoController.Entries), AuthorizationPolicies.SectionAccessSettings)]
    [InlineData(nameof(DemoController.Select), AuthorizationPolicies.SectionAccessSettings)]
    public void Each_action_carries_exactly_its_policy(string actionName, string policy)
    {
        var action = Actions().Single(m => m.Name == actionName);
        Assert.Equal([policy], PoliciesOf(action));
    }

    [Fact]
    public void Editor_asks_for_the_dashboard_data()
    {
        // Content but not Settings access: the dashboard action requires Settings and nothing that admits Content alone,
        // so the Content-only user is refused
        var action = typeof(LicenseStatusController).GetMethod(nameof(LicenseStatusController.Status))!;
        var policies = PoliciesOf(action);

        Assert.Equal([AuthorizationPolicies.SectionAccessSettings], policies);
        Assert.DoesNotContain(AuthorizationPolicies.SectionAccessContent, policies);
        Assert.Empty(typeof(LicenseStatusController).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
    }

    [Fact]
    public void Only_the_four_endpoints_exist_and_every_action_has_a_policy()
    {
        var actions = Actions().ToList();
        Assert.Equal(
            new[]
            {
                nameof(DemoController.Entries), nameof(DemoController.Select),
                nameof(SubmissionsController.List), nameof(LicenseStatusController.Status),
            }.Order(),
            actions.Select(a => a.Name).Order());
        Assert.All(actions, a => Assert.Single(PoliciesOf(a)));
    }

    [Fact]
    public void Controllers_belong_to_the_acme_contact_form_document()
    {
        Assert.Equal("acme-contact-form", ContactFormApi.ApiName);
        foreach (var type in new[] { typeof(LicenseStatusController), typeof(SubmissionsController), typeof(DemoController) })
        {
            Assert.Equal(ContactFormApi.ApiName, type.GetCustomAttribute<MapToApiAttribute>(inherit: true)!.ApiName);
            Assert.NotNull(type.GetCustomAttribute<BackOfficeRouteAttribute>(inherit: true));
            Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>(inherit: true));
            Assert.Equal(Umbraco.Cms.Core.Constants.JsonOptionsNames.BackOffice, type.GetCustomAttribute<Umbraco.Cms.Api.Common.Filters.JsonOptionsNameAttribute>(inherit: true)!.JsonOptionsName);
        }
    }

    // ---- license/status

    [Fact]
    public void License_status_returns_the_admin_data_for_the_request_host()
    {
        var rig = RigFor("Valid: full plan", "localhost:44321");
        var response = rig.Status.Status();
        var status = response.Status;
        Assert.Null(response.DemoSelection);
        Assert.Null(response.DemoDate);

        Assert.Equal("Valid", status.State);
        Assert.Equal("localhost", status.Domain.Host);
        Assert.Null(status.FirstAction);
        Assert.Equal("Test source", status.KeySourceName);
    }

    [Fact]
    public void License_status_with_no_key()
    {
        var status = RigFor("No key").Status.Status().Status;
        Assert.Equal("Missing", status.State);
        Assert.Equal(FirstActions.For(LicenseState.Missing), status.FirstAction);
    }

    [Fact]
    public void Fourth_submission_on_a_three_submission_plan_reaches_the_dashboard_and_the_banner()
    {
        var rig = RigFor("Valid: small plan");
        rig.Add(3);
        var three = rig.Status.Status().Status;
        Assert.False(three.Usage.OverLimit);
        Assert.Null(rig.Submissions.List().Banner);

        rig.Add(1);
        var four = rig.Status.Status().Status;
        Assert.Equal(4, four.Usage.Used);
        Assert.Equal(3, four.Usage.Limit);
        Assert.True(four.Usage.OverLimit);

        var banner = rig.Submissions.List().Banner;
        Assert.NotNull(banner);
        Assert.Contains(BannerCases.OverMonthlyLimit, banner.Cases);
        Assert.Equal(4, banner.Used);
        Assert.Equal(3, banner.Limit);
    }

    [Fact]
    public void Over_limit_report_clears_in_a_new_utc_month()
    {
        var rig = RigFor("Valid: small plan", now: new DateTimeOffset(2026, 6, 30, 23, 59, 0, TimeSpan.Zero));
        rig.Add(4);
        Assert.True(rig.Status.Status().Status.Usage.OverLimit);
        Assert.NotNull(rig.Submissions.List().Banner);

        rig.Clock.Set(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        var status = rig.Status.Status().Status;
        Assert.Equal(0, status.Usage.Used);
        Assert.False(status.Usage.OverLimit);
        Assert.Null(rig.Submissions.List().Banner);
        Assert.Empty(rig.Submissions.List().Submissions);
    }

    // ---- submissions

    [Fact]
    public void Submissions_returns_this_months_submissions_with_no_banner_when_nothing_is_due()
    {
        var rig = RigFor("Valid: full plan", now: new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        rig.Store.Add("Ada", "ada@example.com", "Hello", "cv.pdf", 2048);
        rig.Clock.Advance(TimeSpan.FromHours(1));
        rig.Store.Add("Bo", "bo@example.com", "Hi");

        var response = rig.Submissions.List();

        Assert.Null(response.Banner);
        Assert.Equal(2, response.Submissions.Count);
        var first = response.Submissions[0];
        Assert.Equal(("Ada", "ada@example.com", "Hello", "cv.pdf", 2048L), (first.Name, first.Email, first.Message, first.UploadFileName, first.UploadSizeBytes));
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero), first.ReceivedUtc);
        Assert.Equal("Bo", response.Submissions[1].Name);
        Assert.Null(response.Submissions[1].UploadFileName);
        Assert.Null(response.Submissions[1].UploadSizeBytes);
    }

    [Fact]
    public void Submissions_leaves_out_last_months_submissions()
    {
        var rig = RigFor("Valid: full plan", now: new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero));
        rig.Add(2);
        rig.Clock.Set(new DateTimeOffset(2026, 6, 1, 1, 0, 0, TimeSpan.Zero));
        rig.Add(1);

        Assert.Single(rig.Submissions.List().Submissions);
    }

    [Fact]
    public void Submissions_with_an_unusable_key_carry_the_banner()
    {
        var banner = RigFor("Expired").Submissions.List().Banner;
        Assert.NotNull(banner);
        Assert.Equal([BannerCases.NeedsAttention], banner.Cases);
    }

    [Fact]
    public void Submissions_banner_for_a_host_the_license_does_not_cover()
    {
        var banner = RigFor("Valid: small plan", "www.example.com").Submissions.List().Banner;
        Assert.NotNull(banner);
        Assert.Contains(BannerCases.DomainNotCovered, banner.Cases);
    }

    // ---- Serialised with the Management API's options, every demo key

    [Fact]
    public void Back_office_options_are_camel_case_with_enums_as_strings()
    {
        var json = JsonSerializer.Serialize(new { FirstName = "x", Mode = DayOfWeek.Monday }, BackOfficeJson);
        Assert.Contains("\"firstName\"", json);
        Assert.Contains("\"monday\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Dashboard_data_from_the_controller_never_holds_the_key(string name)
    {
        var entry = Entries.Single(e => e.Name == name);
        var json = JsonSerializer.Serialize(RigFor(name).Status.Status(), BackOfficeJson);
        if (entry.Key is not null)
        {
            Assert.DoesNotContain(entry.Key, json, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Submissions_banner_from_the_controller_holds_no_license_details(string name)
    {
        var entry = Entries.Single(e => e.Name == name);
        foreach (var host in new[] { "localhost", "www.example.com" })
        {
            foreach (var count in new[] { 0, 5 })
            {
                var rig = RigFor(name, host);
                rig.Add(count);
                var snapshot = new ContactFormLicensing(
                    new ContactFormProduct(rig.Clock), new FixedSource(entry.Key), rig.Store,
                    new LicenseStateLog(NullLogger<LicenseStateLog>.Instance)).Check(host);

                var json = JsonSerializer.Serialize(rig.Submissions.List().Banner, BackOfficeJson);

                var forbidden = new List<string>(Enum.GetNames<LicenseState>());
                if (entry.Key is not null)
                {
                    forbidden.Add(entry.Key);
                }

                forbidden.AddRange(
                    new[]
                    {
                        snapshot.Result.License?.KeyIdentifier,
                        snapshot.Result.ClaimedKeyIdentifier,
                        snapshot.Result.License?.Product,
                        snapshot.Result.ClaimedProduct,
                        entry.Product,
                    }.OfType<string>());

                Assert.All(forbidden, f => Assert.DoesNotContain(f, json, StringComparison.Ordinal));
            }
        }
    }
}
