using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

public class ContactFormLicensingTests
{
    private const string SourceName = "Test source";

    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();
    private static readonly DateTimeOffset ExpiringExpiry = new(2030, 1, 31, 23, 59, 59, TimeSpan.Zero);

    public static TheoryData<string> EntryNames => [.. Entries.Select(e => e.Name)];

    private sealed class SwitchableSource(string? key) : ILicenseKeySource
    {
        public string? Key { get; set; } = key;

        public LicenseKeyReading Read() => new(Key, SourceName);
    }

    private sealed class Rig
    {
        public Rig(string? key, DateTimeOffset now)
        {
            Clock = new TestClock(now);
            Source = new SwitchableSource(key);
            Store = new InMemorySubmissionStore(TimeProvider.System);
            Licensing = new ContactFormLicensing(new ContactFormProduct(Clock), Source, Store, Clock, new LicenseStateLog(NullLogger<LicenseStateLog>.Instance));
        }

        public TestClock Clock { get; }

        public SwitchableSource Source { get; }

        public InMemorySubmissionStore Store { get; }

        public ContactFormLicensing Licensing { get; }
    }

    private static DemoKeyEntry Entry(string name) => Entries.Single(e => e.Name == name);

    private static DateTimeOffset ClockFor(DemoKeyEntry entry) =>
        entry.DemoDate is { } date ? DemoKeyFile.ParseInstant(date) : DemoKeyFile.FixedClock;

    private static Rig RigFor(string name, DateTimeOffset? now = null)
    {
        var entry = Entry(name);
        return new Rig(entry.Key, now ?? ClockFor(entry));
    }

    private static string IssueWithDomains(string domains)
    {
        var issuer = new LicenseIssuer(KeyPrefix.Of("ACME"), new DemoKeyFile.FixedTimeProvider(new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero)));
        var features = FeatureList.Empty.With(ContactFormProduct.LicensedDomainsFeature, domains);
        var definitions = FeatureDefinitions.None.Switch(ContactFormProduct.FileUploadsFeature)
            .Number(ContactFormProduct.MaxSubmissionsPerMonthFeature).Text(ContactFormProduct.LicensedDomainsFeature);
        return issuer.Issue(
            new LicenseRequest
            {
                Product = ContactFormProduct.ProductId,
                DisplayName = "Test domains",
                Expiry = LicenseExpiry.Perpetual,
                Features = features,
            },
            DemoKeyFiles.LoadPrivate("demo.pem"),
            definitions).KeyString;
    }

    private static void AddSubmissions(Rig rig, int count)
    {
        for (var i = 0; i < count; i++)
        {
            rig.Store.Add("n", "e@example.com", "m");
        }
    }

    [Fact]
    public void Fourth_submission_on_a_three_submission_plan()
    {
        var rig = RigFor("Valid: small plan");
        AddSubmissions(rig, 3);
        var three = rig.Licensing.Check("localhost");
        Assert.Equal(3, three.MonthlyLimit);
        Assert.Equal(3, three.UsedThisMonth);
        Assert.False(three.OverLimit);

        AddSubmissions(rig, 1);
        var four = rig.Licensing.Check("localhost");
        Assert.Equal(3, four.MonthlyLimit);
        Assert.Equal(4, four.UsedThisMonth);
        Assert.True(four.OverLimit);
    }

    [Theory]
    [InlineData("LOCALHOST:44321")]
    [InlineData("localhost")]
    public void Listed_host_with_a_port(string host)
    {
        var rig = new Rig(IssueWithDomains("www.example.com, localhost"), DemoKeyFile.FixedClock);
        var snapshot = rig.Licensing.Check(host);

        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.True(snapshot.DomainCovered);
        Assert.Equal(host.Split(':')[0], snapshot.HostChecked);
        Assert.Equal(["www.example.com", "localhost"], snapshot.LicensedDomains);
    }

    [Fact]
    public void Domain_list_with_empty_entries_and_spaces()
    {
        var rig = new Rig(IssueWithDomains("localhost,,"), DemoKeyFile.FixedClock);
        Assert.True(rig.Licensing.Check("localhost:5000").DomainCovered);
        Assert.False(rig.Licensing.Check("").DomainCovered);

        rig.Source.Key = IssueWithDomains("a.example.com ,  , localhost");
        Assert.True(rig.Licensing.Check("localhost").DomainCovered);
        Assert.Equal(["a.example.com", "localhost"], rig.Licensing.Check("localhost").LicensedDomains);
    }

    [Fact]
    public void Ipv6_host_with_a_port()
    {
        var rig = new Rig(IssueWithDomains("[::1]"), DemoKeyFile.FixedClock);
        var snapshot = rig.Licensing.Check("[::1]:5000");

        Assert.Equal("[::1]", snapshot.HostChecked);
        Assert.True(snapshot.DomainCovered);
        Assert.False(rig.Licensing.Check("localhost").DomainCovered);
    }

    [Fact]
    public void Unlisted_host()
    {
        var snapshot = RigFor("Valid: another domain").Licensing.Check("localhost");

        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.False(snapshot.DomainCovered);
        Assert.Equal("localhost", snapshot.HostChecked);
    }

    [Fact]
    public void Expires_in_14_days()
    {
        var snapshot = RigFor("Valid: expiring soon").Licensing.Check("localhost");

        Assert.True(snapshot.ExpiringSoon);
        Assert.Equal(14, snapshot.DaysLeft);
        Assert.Equal(ExpiringExpiry, snapshot.ExpiresUtc);
    }

    [Theory]
    [InlineData(30, 0, 0, 0, true, 30)]
    [InlineData(30, 23, 59, 59, true, 30)]
    [InlineData(31, 0, 0, 0, false, 31)]
    [InlineData(14, 11, 59, 59, true, 14)]
    [InlineData(0, 0, 0, 1, true, 0)]
    public void Expiring_soon_boundaries(int days, int hours, int minutes, int seconds, bool soon, int daysLeft)
    {
        var now = ExpiringExpiry - new TimeSpan(days, hours, minutes, seconds);
        var snapshot = RigFor("Valid: expiring soon", now).Licensing.Check("localhost");

        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.Equal(soon, snapshot.ExpiringSoon);
        Assert.Equal(daysLeft, snapshot.DaysLeft);
    }

    [Fact]
    public void Expires_in_31_days()
    {
        var snapshot = RigFor("Valid: expiring soon", ExpiringExpiry - TimeSpan.FromDays(31)).Licensing.Check("localhost");

        Assert.False(snapshot.ExpiringSoon);
        Assert.Equal(31, snapshot.DaysLeft);
    }

    [Theory]
    [InlineData(2026)]
    [InlineData(2100)]
    [InlineData(2500)]
    public void A_perpetual_license_is_never_expiring_soon(int year)
    {
        var snapshot = RigFor("Valid: full plan", new DateTimeOffset(year, 6, 1, 0, 0, 0, TimeSpan.Zero)).Licensing.Check("localhost");

        Assert.Equal(LicenseState.Valid, snapshot.State);
        Assert.False(snapshot.ExpiringSoon);
        Assert.Null(snapshot.DaysLeft);
        Assert.Null(snapshot.ExpiresUtc);
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Every_demo_key(string name)
    {
        var entry = Entry(name);
        var snapshot = RigFor(name).Licensing.Check("localhost");

        Assert.True(entry.State == snapshot.State.ToString(), $"{name}: expected {entry.State}, got {snapshot.State}.");
        Assert.Equal(SourceName, snapshot.KeySourceName);
        Assert.Equal("localhost", snapshot.HostChecked);
        Assert.Equal(0, snapshot.UsedThisMonth);

        if (snapshot.State != LicenseState.Valid)
        {
            Assert.False(snapshot.UploadsGranted);
            Assert.Null(snapshot.MonthlyLimit);
            Assert.False(snapshot.OverLimit);
            Assert.True(snapshot.DomainCovered);
            Assert.False(snapshot.ExpiringSoon);
            return;
        }

        var uploads = entry.Features.TryGetValue("file-uploads", out var u) && u.ValueKind == System.Text.Json.JsonValueKind.True;
        Assert.Equal(uploads, snapshot.UploadsGranted);

        int? limit = entry.Features.TryGetValue("max-submissions-per-month", out var m) ? m.GetInt32() : null;
        Assert.Equal(limit, snapshot.MonthlyLimit);

        var covered = !entry.Features.TryGetValue("licensed-domains", out var d)
            || d.GetString()!.Split(',', StringSplitOptions.TrimEntries).Contains("localhost");
        Assert.Equal(covered, snapshot.DomainCovered);
    }

    [Fact]
    public void Evaluated_when_used()
    {
        var rig = RigFor("Valid: full plan");
        Assert.Equal(LicenseState.Valid, rig.Licensing.Check("localhost").State);

        rig.Source.Key = Entry("Expired").Key;
        Assert.Equal(LicenseState.Expired, rig.Licensing.Check("localhost").State);

        rig.Source.Key = null;
        Assert.Equal(LicenseState.Missing, rig.Licensing.Check("localhost").State);
    }

    [Fact]
    public void The_snapshot_exposes_no_key_text()
    {
        var key = Entry("Valid: full plan").Key!;
        var snapshot = RigFor("Valid: full plan").Licensing.Check("localhost");

        var strings = typeof(LicenseSnapshot).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (string?)p.GetValue(snapshot));
        Assert.DoesNotContain(key, strings);
    }
}
