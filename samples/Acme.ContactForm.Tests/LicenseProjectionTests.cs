using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

public class LicenseProjectionTests
{
    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> EntryNames => [.. Entries.Select(e => e.Name)];

    public static TheoryData<LicenseState> States => [.. Enum.GetValues<LicenseState>()];

    private sealed class FixedSource(string? key) : ILicenseKeySource
    {
        public LicenseKeyReading Read() => new(key, "Test source");
    }

    private static DemoKeyEntry Entry(string name) => Entries.Single(e => e.Name == name);

    private static LicenseSnapshot Snapshot(string name, string host = "localhost", int submissions = 0, DateTimeOffset? now = null)
    {
        var entry = Entry(name);
        var clock = new TestClock(now ?? (entry.DemoDate is { } d ? DemoKeyFile.ParseInstant(d) : DemoKeyFile.FixedClock));
        var store = new InMemorySubmissionStore(TimeProvider.System);
        for (var i = 0; i < submissions; i++)
        {
            store.Add("n", "e@example.com", "m");
        }

        return new ContactFormLicensing(new ContactFormProduct(clock), new FixedSource(entry.Key), store, clock, new LicenseStateLog(NullLogger<LicenseStateLog>.Instance)).Check(host);
    }

    // ---- FirstActions

    [Theory]
    [MemberData(nameof(States))]
    public void Every_state_has_its_action(LicenseState state)
    {
        var action = FirstActions.For(state);
        if (state == LicenseState.Valid)
        {
            Assert.Null(action);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(action));
    }

    [Theory]
    [InlineData(LicenseState.Missing, "Install", "ask the site owner")]
    [InlineData(LicenseState.Unreadable, "Paste", "again")]
    [InlineData(LicenseState.WrongProduct, "Move this key to the product it names", "")]
    [InlineData(LicenseState.SigningKeyNotRecognised, "Update", "ask the vendor")]
    [InlineData(LicenseState.NotVerified, "Paste", "ask the vendor")]
    [InlineData(LicenseState.NotSupported, "Update", "ask the vendor")]
    [InlineData(LicenseState.Expired, "Renew", "")]
    public void Each_action_carries_the_documented_meaning(LicenseState state, string first, string second)
    {
        var action = FirstActions.For(state)!;
        Assert.Contains(first, action);
        Assert.Contains(second, action);
    }

    [Fact]
    public void Every_state_shows_its_action_on_the_dashboard_data()
    {
        Assert.Equal(FirstActions.For(LicenseState.Missing), AdminLicenseStatus.From(Snapshot("No key")).FirstAction);
        Assert.Equal(FirstActions.For(LicenseState.Expired), AdminLicenseStatus.From(Snapshot("Expired")).FirstAction);
        Assert.Null(AdminLicenseStatus.From(Snapshot("Valid: full plan")).FirstAction);
    }

    // ---- Dashboard data

    [Fact]
    public void Valid_small_plan()
    {
        var status = AdminLicenseStatus.From(Snapshot("Valid: small plan", submissions: 1));

        Assert.Equal("Valid", status.State);
        Assert.Null(status.FirstAction);
        Assert.False(string.IsNullOrEmpty(status.KeyIdentifier));
        Assert.True(status.KeyIdentifierVerified);
        Assert.Null(status.ClaimedProduct);
        Assert.False(status.Perpetual);
        Assert.Equal("2099-12-31", status.ExpiryDate);
        Assert.False(status.ExpiringSoon);

        Assert.Equal(["file-uploads", "max-submissions-per-month", "licensed-domains"], status.Features.Select(f => f.Name));
        var uploads = status.Features[0];
        Assert.Equal("not included", uploads.Grants);
        Assert.False(uploads.Included);
        Assert.Equal("3", status.Features[1].Grants);
        Assert.Equal("localhost", status.Features[2].Grants);
        Assert.All(status.Features.Skip(1), f => Assert.True(f.InEffect));

        Assert.Equal(1, status.Usage.Used);
        Assert.Equal(3, status.Usage.Limit);
        Assert.False(status.Usage.OverLimit);
        Assert.Equal("1 of 3 submissions used this month", status.Usage.Text);

        Assert.Equal("localhost", status.Domain.Host);
        Assert.True(status.Domain.Covered);

        Assert.Equal("ContactForm:LicenseKey", status.SettingName);
        Assert.Equal("ContactForm__LicenseKey", status.EnvironmentVariable);
        Assert.Equal("Test source", status.KeySourceName);
    }

    [Fact]
    public void Wrong_product()
    {
        var status = AdminLicenseStatus.From(Snapshot("Another product's key"));

        Assert.Equal("WrongProduct", status.State);
        Assert.Equal(FirstActions.For(LicenseState.WrongProduct), status.FirstAction);
        Assert.Equal("acme.commerce", status.ClaimedProduct);
        Assert.False(string.IsNullOrEmpty(status.KeyIdentifier));
        Assert.False(status.KeyIdentifierVerified);
        Assert.All(status.Features, f => Assert.False(f.Included));
    }

    [Fact]
    public void Expired_shows_what_lapsed()
    {
        var status = AdminLicenseStatus.From(Snapshot("Expired"));

        Assert.Equal("Expired", status.State);
        Assert.Equal("2026-01-31", status.ExpiryDate);
        Assert.Equal("Expired 2026-01-31", status.Expiry);
        Assert.True(status.KeyIdentifierVerified);
        Assert.Equal(["on", "1000", "localhost"], status.Features.Select(f => f.Grants.Replace(" (no longer in effect)", "")));
        Assert.All(status.Features, f =>
        {
            Assert.True(f.Included);
            Assert.False(f.InEffect);
            Assert.EndsWith("(no longer in effect)", f.Grants);
        });
    }

    [Fact]
    public void Perpetual_license()
    {
        var status = AdminLicenseStatus.From(Snapshot("Valid: full plan"));

        Assert.True(status.Perpetual);
        Assert.Null(status.ExpiryDate);
        Assert.Equal("Perpetual", status.Expiry);
    }

    [Fact]
    public void Expires_in_14_days_on_the_dashboard()
    {
        var status = AdminLicenseStatus.From(Snapshot("Valid: expiring soon"));

        Assert.True(status.ExpiringSoon);
        Assert.Contains("2030-01-31", status.ExpiringSoonText);
    }

    [Fact]
    public void Expires_in_31_days_on_the_dashboard()
    {
        var now = new DateTimeOffset(2030, 1, 31, 23, 59, 59, TimeSpan.Zero) - TimeSpan.FromDays(31);
        var status = AdminLicenseStatus.From(Snapshot("Valid: expiring soon", now: now));

        Assert.False(status.ExpiringSoon);
        Assert.Null(status.ExpiringSoonText);
    }

    [Fact]
    public void Unlisted_host_on_the_dashboard()
    {
        var status = AdminLicenseStatus.From(Snapshot("Valid: another domain"));

        Assert.False(status.Domain.Covered);
        Assert.Contains("does not cover localhost", status.Domain.Text);
    }

    // ---- Banner data

    [Fact]
    public void Not_valid()
    {
        var banner = EditorLicenseBanner.From(Snapshot("No key"));

        Assert.NotNull(banner);
        Assert.Equal([BannerCases.NeedsAttention], banner.Cases);
        Assert.Equal("The contact form's license needs attention. Contact your administrator.", banner.Message);
    }

    [Fact]
    public void Valid_and_within_limits()
    {
        Assert.Null(EditorLicenseBanner.From(Snapshot("Valid: full plan")));
    }

    [Fact]
    public void Over_the_monthly_limit()
    {
        var banner = EditorLicenseBanner.From(Snapshot("Valid: small plan", submissions: 4));

        Assert.NotNull(banner);
        Assert.Equal([BannerCases.OverMonthlyLimit], banner.Cases);
        Assert.Equal(4, banner.Used);
        Assert.Equal(3, banner.Limit);
        Assert.Equal("The contact form is over its monthly submission limit (4 of 3 used). Contact your administrator.", banner.Message);
    }

    [Fact]
    public void Expires_in_14_days_in_the_banner()
    {
        var banner = EditorLicenseBanner.From(Snapshot("Valid: expiring soon"));

        Assert.NotNull(banner);
        Assert.Equal([BannerCases.ExpiringSoon], banner.Cases);
        Assert.Equal("2030-01-31", banner.ExpiryDate);
        Assert.Equal("The contact form's license expires on 2030-01-31. Contact your administrator.", banner.Message);
    }

    [Fact]
    public void Expires_in_31_days_in_the_banner()
    {
        var now = new DateTimeOffset(2030, 1, 31, 23, 59, 59, TimeSpan.Zero) - TimeSpan.FromDays(31);
        Assert.Null(EditorLicenseBanner.From(Snapshot("Valid: expiring soon", now: now)));
    }

    [Fact]
    public void Unlisted_host_in_the_banner()
    {
        var banner = EditorLicenseBanner.From(Snapshot("Valid: another domain"));

        Assert.NotNull(banner);
        Assert.Equal([BannerCases.DomainNotCovered], banner.Cases);
        Assert.Equal("The contact form's license does not cover localhost. Contact your administrator.", banner.Message);
    }

    [Fact]
    public void Several_cases_are_joined()
    {
        var banner = EditorLicenseBanner.From(Snapshot("Valid: expiring soon", host: "www.example.com", submissions: 1001));

        Assert.NotNull(banner);
        Assert.Equal([BannerCases.ExpiringSoon, BannerCases.OverMonthlyLimit, BannerCases.DomainNotCovered], banner.Cases);
        Assert.Equal(3, banner.Message.Split("Contact your administrator.").Length - 1);
        Assert.EndsWith("Contact your administrator.", banner.Message);
    }

    [Fact]
    public void Not_valid_excludes_every_other_case()
    {
        // Expired key, over any limit, host unlisted: only the not-valid case applies.
        var banner = EditorLicenseBanner.From(Snapshot("Expired", host: "www.example.com", submissions: 5));

        Assert.Equal([BannerCases.NeedsAttention], banner!.Cases);
    }

    [Fact]
    public void Banner_case_names_contain_no_state_name()
    {
        var names = typeof(BannerCases).GetFields().Select(f => (string)f.GetRawConstantValue()!);
        foreach (var state in Enum.GetNames<LicenseState>())
        {
            Assert.All(names, n => Assert.DoesNotContain(state, n, StringComparison.Ordinal));
        }
    }

    // ---- Serialisation, every demo key

    private static void AssertDashboardHasNoKey(string name)
    {
        var entry = Entry(name);
        var json = JsonSerializer.Serialize(AdminLicenseStatus.From(Snapshot(name)), Json);
        if (entry.Key is not null)
        {
            Assert.DoesNotContain(entry.Key, json, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Dashboard_data_never_holds_the_key(string name) => AssertDashboardHasNoKey(name);

    [Fact]
    public void Dashboard_data_with_a_valid_key()
    {
        foreach (var entry in Entries.Where(e => e.State == "Valid"))
        {
            AssertDashboardHasNoKey(entry.Name);
        }
    }

    [Fact]
    public void Dashboard_data_with_a_damaged_key()
    {
        Assert.Equal("NotVerified", Entry("Edited key").State);
        AssertDashboardHasNoKey("Edited key");
        AssertDashboardHasNoKey("Damaged key");
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Banner_data_holds_no_license_details(string name)
    {
        var entry = Entry(name);
        foreach (var host in new[] { "localhost", "www.example.com" })
        {
            foreach (var submissions in new[] { 0, 5 })
            {
                var snapshot = Snapshot(name, host, submissions);
                var json = JsonSerializer.Serialize(EditorLicenseBanner.From(snapshot), Json);

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

    [Fact]
    public void Banner_data_for_a_wrong_product_key()
    {
        var snapshot = Snapshot("Another product's key");
        var json = JsonSerializer.Serialize(EditorLicenseBanner.From(snapshot), Json);

        Assert.Equal("acme.commerce", snapshot.Result.ClaimedProduct);
        Assert.DoesNotContain("acme.commerce", json, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.Result.ClaimedKeyIdentifier!, json, StringComparison.Ordinal);
        Assert.DoesNotContain("WrongProduct", json, StringComparison.Ordinal);
        Assert.DoesNotContain(Entry("Another product's key").Key!, json, StringComparison.Ordinal);
    }
}
