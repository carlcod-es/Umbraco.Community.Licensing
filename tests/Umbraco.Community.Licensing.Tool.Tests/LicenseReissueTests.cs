using System.Text;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class LicenseReissueTests
{
    private const string Pass = ScriptedTool.DefaultPassphrase;
    private static readonly string NewLine = System.Environment.NewLine;

    private static ToolDbContext Open(ScriptedTool tool) => Installation.Open(Path.GetFullPath(tool.HomeInstallation));

    private static DateTimeOffset At(string iso) =>
        DateTimeOffset.Parse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();

    private static ScriptedTool WithProducts(bool shared = false)
    {
        var tool = LicenseIssueTests.WithProducts(shared);
        using var run = tool.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = Pass });
        Assert.Equal(0, run.Run("product", "feature", "add", "acme.commerce", "shipping", "--type", "switch"));
        return tool;
    }

    /// <summary>Runs a command that must succeed and returns its key string.</summary>
    private static string Ok(ScriptedTool tool, params string[] args)
    {
        var (exit, run) = LicenseIssueTests.Run(tool, args);
        using (run)
        {
            Assert.True(exit == 0, run.Stderr);
            return LicenseIssueTests.KeyOf(run);
        }
    }

    /// <summary>Issues <c>acme.commerce</c> with the clock at <paramref name="issuedAt"/> and returns the displayed reference.</summary>
    private static string IssueAt(ScriptedTool tool, string issuedAt, string expires, params string[] extra)
    {
        tool.Clock.Set(At(issuedAt));
        Ok(tool, ["license", "issue", "acme.commerce", "--expires", expires, .. extra]);
        return Reference(tool);
    }

    private static string Reference(ScriptedTool tool)
    {
        using var db = Open(tool);
        var reference = db.Licenses.Select(l => l.Reference).AsEnumerable().Single();
        return $"LIC-{reference[..5]}-{reference[5..]}";
    }

    private static List<IssueEntry> Entries(ScriptedTool tool)
    {
        using var db = Open(tool);
        return [.. db.IssueEntries.OrderBy(e => e.Id)];
    }

    private static string Stored(ScriptedTool tool)
    {
        using var db = Open(tool);
        var email = db.Licenses.Select(l => l.CustomerEmail).AsEnumerable().FirstOrDefault();
        return $"{db.Licenses.Count()}/{db.IssueEntries.Count()}/{db.SigningKeys.Count()}/{email}";
    }

    private static VerifiedLicense Verify(ScriptedTool tool, string key, bool shared = false)
    {
        var result = LicenseIssueTests.Evaluate(tool, "acme.commerce", key, shared);
        Assert.Equal(LicenseState.Valid, result.State);
        return result.License!;
    }

    // ---- Reissuing from the record

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_on_bought(bool shared)
    {
        using var tool = WithProducts(shared);
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z",
            "--feature", "ecommerce=on", "--feature", "max-orders=500", "--display-name", "Acme Ltd", "--vendor-tag", "gold");
        tool.Clock.Set(At("2026-09-01T00:00:00Z"));

        var key = Ok(tool, "license", "reissue", reference, "--feature", "shipping=on", "--expires", "suggested");

        var verified = Verify(tool, key, shared);
        Assert.Equal(reference, verified.Reference);
        Assert.Equal(At("2027-01-15T10:00:00Z"), verified.Expires);
        Assert.Equal(FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m).With("shipping", true), verified.Features);
        Assert.Equal("Acme Ltd", verified.DisplayName);
        Assert.Equal("gold", verified.VendorTag);
        Assert.Equal(At("2026-09-01T00:00:00Z"), verified.Issued);

        var entries = Entries(tool);
        Assert.Equal(2, entries.Count);
        var added = entries[1];
        Assert.Equal(IssueKind.Reissue, added.Kind);
        Assert.Equal(entries[0].Reference, added.Reference);
        Assert.Equal(At("2027-01-15T10:00:00Z"), added.ExpiresUtc);
        Assert.Equal(verified.KeyIdentifier, added.KeyIdentifier);
        Assert.Equal(verified.SigningKeyId, added.SigningKeyId);
        Assert.NotEqual(entries[0].KeyIdentifier, added.KeyIdentifier);
        Assert.Null(added.OrderRef);
        Assert.Equal(FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m).With("shipping", true), FeaturesJson.Read(added.FeaturesJson));
        using var db = Open(tool);
        Assert.Single(db.Licenses);
    }

    [Fact]
    public void A_reissue_changes_features_display_name_vendor_tag_expiry_and_email()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z",
            "--feature", "ecommerce=on", "--feature", "max-orders=500", "--display-name", "Acme Ltd", "--vendor-tag", "gold", "--email", "old@example.com");
        tool.Clock.Advance(TimeSpan.FromDays(10));

        var key = Ok(tool, "license", "reissue", reference,
            "--expires", "2028-02-02",
            "--feature", "max-orders=900",
            "--remove-feature", "ecommerce",
            "--display-name", "Acme Group",
            "--vendor-tag", "platinum",
            "--email", "new@example.com",
            "--order-ref", "ORD-2000");

        var verified = Verify(tool, key);
        Assert.Equal(At("2028-02-02T23:59:59Z"), verified.Expires);
        Assert.Equal(FeatureList.Empty.With("max-orders", 900m), verified.Features);
        Assert.Equal("Acme Group", verified.DisplayName);
        Assert.Equal("platinum", verified.VendorTag);
        using var db = Open(tool);
        Assert.Equal("new@example.com", db.Licenses.Single().CustomerEmail);
        Assert.Equal("ORD-2000", db.IssueEntries.OrderBy(e => e.Id).Last().OrderRef);
    }

    [Fact]
    public void A_reissue_keeps_what_it_is_not_told_to_change()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z",
            "--feature", "ecommerce=on", "--display-name", "Acme Ltd", "--vendor-tag", "gold", "--email", "old@example.com");
        tool.Clock.Advance(TimeSpan.FromDays(10));

        Ok(tool, "license", "reissue", reference, "--feature", "max-orders=5", "--expires", "suggested");

        var added = Entries(tool)[1];
        Assert.Equal("Acme Ltd", added.DisplayName);
        Assert.Equal("gold", added.VendorTag);
        using var db = Open(tool);
        Assert.Equal("old@example.com", db.Licenses.Single().CustomerEmail);
    }

    [Theory]
    [InlineData("lic-{0}")]
    [InlineData("{0}")]
    [InlineData("{1}")]
    public void A_reference_is_read_ignoring_case_hyphens_and_the_prefix(string form)
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");
        var bare = reference[4..].Replace("-", "");
        var typed = string.Format(form, reference[4..].ToLowerInvariant(), bare.ToLowerInvariant());

        var key = Ok(tool, "license", "reissue", typed, "--expires", "suggested");

        Assert.Equal(reference, Verify(tool, key).Reference);
    }

    [Fact]
    public void A_reissue_signs_with_the_active_key_of_the_product()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");

        Ok(tool, "license", "resend", reference);

        using var db = Open(tool);
        var active = db.SigningKeys.AsEnumerable().Single(k => k.Role == SigningKeyRole.Active && k.ProductId == "acme.commerce");
        Assert.All(db.IssueEntries, e => Assert.Equal(active.SigningKeyId, e.SigningKeyId));
    }

    // ---- Resending

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Site_owner_lost_the_key(bool shared)
    {
        using var tool = WithProducts(shared);
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z",
            "--feature", "ecommerce=on", "--feature", "region=eu", "--display-name", "Acme Ltd", "--vendor-tag", "gold",
            "--order-ref", "ORD-1042", "--email", "owner@example.com");
        tool.Clock.Advance(TimeSpan.FromDays(40));

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "resend", reference);
        using var _ = run;

        Assert.Equal(0, exit);
        var key = LicenseIssueTests.KeyOf(run);
        var verified = Verify(tool, key, shared);
        Assert.Equal(reference, verified.Reference);
        Assert.Equal(At("2027-01-15T10:00:00Z"), verified.Expires);
        Assert.Equal(FeatureList.Empty.With("ecommerce", true).With("region", "eu"), verified.Features);
        Assert.Equal("Acme Ltd", verified.DisplayName);
        Assert.Equal("gold", verified.VendorTag);

        var entries = Entries(tool);
        Assert.Equal(2, entries.Count);
        Assert.Equal(IssueKind.Resend, entries[1].Kind);
        Assert.Null(entries[1].OrderRef);
        Assert.Equal("ORD-1042", entries[0].OrderRef);
        Assert.Equal(entries[0].ExpiresUtc, entries[1].ExpiresUtc);
        Assert.Equal(entries[0].FeaturesJson, entries[1].FeaturesJson);
        Assert.Equal(entries[0].DisplayName, entries[1].DisplayName);
        Assert.Equal(entries[0].VendorTag, entries[1].VendorTag);
        Assert.NotEqual(entries[0].KeyIdentifier, entries[1].KeyIdentifier);
        Assert.Equal(tool.Clock.GetUtcNow(), entries[1].IssuedUtc);
        using var db = Open(tool);
        Assert.Equal("owner@example.com", db.Licenses.Single().CustomerEmail);
    }

    [Fact]
    public void Resend_has_no_expiry_option()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");
        var before = Stored(tool);

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "resend", reference, "--expires", "perpetual");
        using var _ = run;

        Assert.Equal(2, exit);
        Assert.Equal("", run.Stdout);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void Resending_a_perpetual_license_stays_perpetual()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "perpetual");

        var key = Ok(tool, "license", "resend", reference);

        Assert.True(Verify(tool, key).IsPerpetual);
        Assert.Null(Entries(tool)[1].ExpiresUtc);
    }

    // ---- Renewing and order references

    [Fact]
    public void Renewal_keeps_the_earlier_order_reference()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z", "--order-ref", "ORD-1001");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested", "--order-ref", "ORD-1874");

        var entries = Entries(tool);
        Assert.Equal("ORD-1001", entries[0].OrderRef);
        Assert.Equal("ORD-1874", entries[1].OrderRef);
        Assert.Equal(IssueKind.Renewal, entries[1].Kind);
        Assert.Equal(reference, Verify(tool, key).Reference);
    }

    [Fact]
    public void A_renewal_without_an_order_reference_has_none()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z", "--order-ref", "ORD-1001");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        Ok(tool, "license", "renew", reference, "--expires", "suggested");

        var entries = Entries(tool);
        Assert.Equal("ORD-1001", entries[0].OrderRef);
        Assert.Null(entries[1].OrderRef);
    }

    [Fact]
    public void A_renewal_can_change_the_email_and_keeps_features()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z", "--feature", "ecommerce=on", "--email", "old@example.com");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested", "--email", "new@example.com");

        Assert.Equal(FeatureList.Empty.With("ecommerce", true), Verify(tool, key).Features);
        using var db = Open(tool);
        Assert.Equal("new@example.com", db.Licenses.Single().CustomerEmail);
    }

    [Fact]
    public void A_renewal_takes_a_typed_expiry()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "2027-06-30");

        Assert.Equal(At("2027-06-30T23:59:59Z"), Verify(tool, key).Expires);
        Assert.Equal(IssueKind.Renewal, Entries(tool)[1].Kind);
    }

    // ---- Suggested expiry, through the commands

    [Fact]
    public void Renewal_of_a_one_year_license()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2027-11-30T23:59:59Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Second_renewal_after_an_early_renewal()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));
        Ok(tool, "license", "renew", reference, "--expires", "suggested");
        tool.Clock.Set(At("2027-11-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2028-11-30T23:59:59Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Add_on_before_renewal()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");
        tool.Clock.Set(At("2026-09-01T00:00:00Z"));
        Ok(tool, "license", "reissue", reference, "--feature", "shipping=on", "--expires", "suggested");
        tool.Clock.Set(At("2026-12-01T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2028-01-15T10:00:00Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Renewal_after_a_lapse()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2027-02-01T09:00:00Z"));
        Ok(tool, "license", "renew", reference, "--expires", "2028-02-01T09:00:00Z");
        tool.Clock.Set(At("2028-01-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2029-02-01T09:00:00Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Feature_change()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-06-01T00:00:00Z"));

        var key = Ok(tool, "license", "reissue", reference, "--feature", "shipping=on", "--expires", "suggested");

        Assert.Equal(At("2026-11-30T23:59:59Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Perpetual_license()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "perpetual");
        tool.Clock.Advance(TimeSpan.FromDays(30));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.True(Verify(tool, key).IsPerpetual);
        Assert.Null(Entries(tool)[1].ExpiresUtc);
    }

    [Fact]
    public void Never_reissued()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2026-07-15T10:00:00Z");
        tool.Clock.Advance(TimeSpan.FromDays(150));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2027-01-15T10:00:00Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Term_not_a_whole_number_of_months()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-03-10T09:00:00Z", "2026-04-09T09:00:00Z");
        tool.Clock.Advance(TimeSpan.FromDays(20));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2026-05-09T09:00:00Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Date_expiry_issued_during_the_day()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T14:23:11Z", "2026-11-30");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2027-11-30T23:59:59Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Term_in_whole_days()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-03-10T14:00:00Z", "2026-04-09");
        tool.Clock.Advance(TimeSpan.FromDays(20));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2026-05-09T23:59:59Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Term_within_one_day()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-03-10T09:00:00Z", "2026-03-10T11:00:00Z");
        tool.Clock.Set(At("2026-03-10T10:00:00Z"));

        var key = Ok(tool, "license", "renew", reference, "--expires", "suggested");

        Assert.Equal(At("2026-03-10T11:00:00Z"), Verify(tool, key).Expires);
    }

    [Fact]
    public void Reissue_and_renew_with_nobody_present_name_every_missing_input()
    {
        using var tool = WithProducts();
        var before = Stored(tool);

        foreach (var verb in new[] { "reissue", "renew" })
        {
            var (exit, run) = LicenseIssueTests.Run(tool, "license", verb);
            using (run)
            {
                Assert.Equal(1, exit);
                Assert.Equal("", run.Stdout);
                Assert.Contains("Missing <reference>, --expires", run.Stderr);
            }
        }

        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void A_suggestion_in_the_past_is_refused_by_the_issue_rules_and_stores_nothing()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2028-01-01T00:00:00Z"));
        var before = Stored(tool);

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "renew", reference, "--expires", "suggested");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("expires", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    // ---- Asking for the expiry

    [Fact]
    public void Without_an_expiry_and_nobody_present_the_suggestion_is_not_taken()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));
        var before = Stored(tool);

        foreach (var command in new[] { "renew", "reissue" })
        {
            var (exit, run) = LicenseIssueTests.Run(tool, "license", command, reference);
            using (run)
            {
                Assert.Equal(1, exit);
                Assert.Equal("", run.Stdout);
                Assert.Contains("--expires", run.Stderr);
            }
        }

        using var noInput = tool.Another(stdinRedirected: false, variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = Pass });
        Assert.Equal(1, noInput.Run("license", "renew", reference, "--no-input"));
        Assert.Contains("--expires", noInput.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void At_a_terminal_the_suggestion_is_the_default()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        using var run = tool.Another(stdin: "\n", stdinRedirected: false, hiddenKeys: Pass + "\n");
        var exit = run.Run("license", "renew", reference);

        Assert.Equal(0, exit);
        Assert.Contains("Expiry [2027-11-30T23:59:59Z]: ", run.Stderr);
        Assert.Equal(At("2027-11-30T23:59:59Z"), Verify(tool, LicenseIssueTests.KeyOf(run)).Expires);
    }

    [Fact]
    public void At_a_terminal_the_suggestion_can_be_replaced()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));

        using var run = tool.Another(stdin: "2027-06-30\n", stdinRedirected: false, hiddenKeys: Pass + "\n");
        var exit = run.Run("license", "renew", reference);

        Assert.Equal(0, exit);
        Assert.Equal(At("2027-06-30T23:59:59Z"), Verify(tool, LicenseIssueTests.KeyOf(run)).Expires);
        Assert.Equal(IssueKind.Renewal, Entries(tool)[1].Kind);
    }

    [Fact]
    public void A_perpetual_suggestion_is_shown_as_perpetual()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "perpetual");

        using var run = tool.Another(stdin: "\n", stdinRedirected: false, hiddenKeys: Pass + "\n");
        var exit = run.Run("license", "renew", reference);

        Assert.Equal(0, exit);
        Assert.Contains("Expiry [perpetual]: ", run.Stderr);
        Assert.True(Verify(tool, LicenseIssueTests.KeyOf(run)).IsPerpetual);
    }

    [Fact]
    public void A_missing_reference_is_prompted_at_a_terminal_and_named_otherwise()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "resend");
        using (run)
        {
            Assert.Equal(1, exit);
            Assert.Contains("<reference>", run.Stderr);
        }

        using var prompted = tool.Another(stdin: reference + "\n", stdinRedirected: false, hiddenKeys: Pass + "\n");
        Assert.Equal(0, prompted.Run("license", "resend"));
        Assert.Equal(reference, Verify(tool, LicenseIssueTests.KeyOf(prompted)).Reference);
    }

    // ---- Refusals

    [Theory]
    [InlineData("reissue")]
    [InlineData("renew")]
    [InlineData("resend")]
    public void Unknown_reference(string command)
    {
        using var tool = WithProducts();
        IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z");
        var before = Stored(tool);

        // resend takes no --expires; the others are given one so the reference is what is refused.
        string[] args = command == "resend"
            ? ["license", "resend", "LIC-22222-22222"]
            : ["license", command, "LIC-22222-22222", "--expires", "perpetual"];
        var (exit, run) = LicenseIssueTests.Run(tool, args);
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Unknown license LIC-22222-22222", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Theory]
    [InlineData("reissue")]
    [InlineData("renew")]
    [InlineData("resend")]
    public void Wrong_passphrase(string command)
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2025-11-30T23:59:59Z", "2026-11-30T23:59:59Z");
        tool.Clock.Set(At("2026-11-20T00:00:00Z"));
        var before = Stored(tool);

        // At a terminal: a wrong hidden passphrase, with the expiry answer waiting on stdin.
        using var run = tool.Another(stdin: "2027-06-30\n", stdinRedirected: false, hiddenKeys: "not the passphrase\n");
        var exit = run.Run("license", command, reference);

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Wrong passphrase", run.Stderr);
        Assert.DoesNotContain("Expiry", run.Stderr);
        Assert.Equal("2027-06-30", run.Environment.Console.ReadLine());
        Assert.Equal(before, Stored(tool));
        Assert.Single(Entries(tool));
    }

    [Fact]
    public void A_wrong_passphrase_from_the_environment_changes_nothing_even_with_an_email()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z", "--email", "old@example.com");
        var before = Stored(tool);

        using var run = tool.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = "nope" });
        var exit = run.Run("license", "reissue", reference, "--expires", "suggested", "--email", "new@example.com");

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void A_refused_reissue_names_every_problem_once_and_stores_nothing()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z", "--feature", "ecommerce=on", "--email", "old@example.com");
        var before = Stored(tool);

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "reissue", reference,
            "--expires", "next year",
            "--feature", "max-order=500",
            "--feature", "max-orders=lots",
            "--remove-feature", "region",
            "--order-ref", " ",
            "--email", "");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        foreach (var named in new[] { "expires", "features.max-order", "features.max-orders", "--remove-feature", "orderRef", "email" })
        {
            Assert.Single(run.Stderr.Split(NewLine), line => line.StartsWith($"  {named}:", StringComparison.Ordinal));
        }

        Assert.Contains("Nothing stored.", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void A_feature_both_changed_and_removed_is_refused()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z", "--feature", "ecommerce=on");
        var before = Stored(tool);

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "reissue", reference, "--expires", "suggested",
            "--feature", "ecommerce=on", "--remove-feature", "ecommerce");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Contains("both changed and removed", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void Switching_a_feature_off_is_refused_by_the_issue_rules()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z", "--feature", "ecommerce=on");
        var before = Stored(tool);

        var (exit, run) = LicenseIssueTests.Run(tool, "license", "reissue", reference, "--expires", "suggested", "--feature", "ecommerce=off");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Contains("features.ecommerce", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    // ---- Key strings are not stored

    [Fact]
    public void Reissued_keys_are_not_stored()
    {
        using var tool = WithProducts();
        var reference = IssueAt(tool, "2026-01-15T10:00:00Z", "2027-01-15T10:00:00Z", "--email", "owner@example.com");
        var keys = new List<string>
        {
            Ok(tool, "license", "reissue", reference, "--feature", "shipping=on", "--expires", "suggested"),
            Ok(tool, "license", "renew", reference, "--expires", "suggested"),
            Ok(tool, "license", "resend", reference),
        };

        foreach (var file in Directory.GetFiles(Path.GetFullPath(tool.HomeInstallation), "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            foreach (var key in keys)
            {
                Assert.True(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(key)) < 0, $"{Path.GetFileName(file)} holds a key string");
                Assert.True(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(key[(key.IndexOf('.') + 1)..])) < 0, $"{Path.GetFileName(file)} holds a key's signed part");
            }
        }

        Assert.Equal([IssueKind.Issue, IssueKind.Reissue, IssueKind.Renewal, IssueKind.Resend], Entries(tool).Select(e => e.Kind));
        Assert.All(keys, key => Assert.Equal(reference, Verify(tool, key).Reference));
    }
}
