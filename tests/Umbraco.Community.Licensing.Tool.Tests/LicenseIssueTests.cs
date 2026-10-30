using System.Text;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class LicenseIssueTests
{
    private const string Pass = ScriptedTool.DefaultPassphrase;
    private static readonly string NewLine = System.Environment.NewLine;

    private static readonly IReadOnlyDictionary<string, string> WithPassphrase =
        new Dictionary<string, string> { [ToolContext.PassphraseVariable] = Pass };

    private static ToolDbContext Open(ScriptedTool tool) => Installation.Open(Path.GetFullPath(tool.HomeInstallation));

    /// <summary>A run with the passphrase in <c>UCL_PASSPHRASE</c> and nobody at a terminal.</summary>
    internal static (int Exit, ScriptedTool Run) Run(ScriptedTool tool, params string[] args)
    {
        var run = tool.Another(variables: WithPassphrase);
        return (run.Run(args), run);
    }

    private static void Succeeds(ScriptedTool tool, params string[] args)
    {
        var (exit, run) = Run(tool, args);
        using (run)
        {
            Assert.True(exit == 0, run.Stderr);
        }
    }

    /// <summary>An installation with <c>acme.commerce</c> (switch ecommerce, number max-orders, text region) and a second product, so the active key differs per product.</summary>
    internal static ScriptedTool WithProducts(bool shared)
    {
        var tool = ScriptedTool.Installed(new SetupAnswers(Shared: shared));
        Succeeds(tool, "product", "add", "acme.commerce", "--name", "Acme Commerce");
        Succeeds(tool, "product", "add", "acme.shipping", "--name", "Acme Shipping");
        Succeeds(tool, "product", "feature", "add", "acme.commerce", "ecommerce", "--type", "switch");
        Succeeds(tool, "product", "feature", "add", "acme.commerce", "max-orders", "--type", "number");
        Succeeds(tool, "product", "feature", "add", "acme.commerce", "region", "--type", "text");
        return tool;
    }

    /// <summary>The key string a run wrote: stdout is that one line and nothing else.</summary>
    internal static string KeyOf(ScriptedTool run)
    {
        Assert.EndsWith(NewLine, run.Stdout);
        var key = run.Stdout[..^NewLine.Length];
        Assert.NotEmpty(key);
        Assert.All(key, c => Assert.True(c is > ' ' and < '\u007f', "the key string is one line of printable ASCII"));
        return key;
    }

    /// <summary>Evaluates <paramref name="key"/> trusting the product's public keys, as a release would.</summary>
    internal static LicenseResult Evaluate(ScriptedTool tool, string productId, string key, bool shared)
    {
        using var db = Open(tool);
        var trusted = TrustedSigningKeys.Create(db.SigningKeys.AsEnumerable()
            .Where(k => k.ProductId == (shared ? null : productId))
            .Select(k => SigningPublicKey.Parse(k.PublicKey)));
        return new LicenseEvaluator(productId, trusted, tool.Clock).Evaluate(key);
    }

    private static string Stored(ScriptedTool tool)
    {
        using var db = Open(tool);
        return $"{db.Licenses.Count()}/{db.IssueEntries.Count()}/{db.SigningKeys.Count()}/{db.Settings.Count()}";
    }

    private static string[] IssueArgs(params string[] extra) =>
        ["license", "issue", "acme.commerce", "--expires", "2027-03-01T23:59:59Z", .. extra];

    // ---- License issued

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void License_issued(bool shared)
    {
        using var tool = WithProducts(shared);

        var (exit, run) = Run(tool, IssueArgs(
            "--feature", "ecommerce=on",
            "--feature", "max-orders=500",
            "--order-ref", "ORD-1042",
            "--email", "owner@example.com",
            "--display-name", "Acme Ltd",
            "--vendor-tag", "gold"));
        using var _ = run;

        Assert.Equal(0, exit);
        var key = KeyOf(run);
        Assert.Contains($"Using {Path.GetFullPath(tool.HomeInstallation)}{NewLine}", run.Stderr);
        Assert.DoesNotContain(key, run.Stderr);

        using var db = Open(tool);
        var license = Assert.Single(db.Licenses);
        Assert.Matches("^[2-9A-HJKMNP-Z]{10}$", license.Reference);
        Assert.Equal("acme.commerce", license.ProductId);
        Assert.Equal("owner@example.com", license.CustomerEmail);
        Assert.Equal(tool.Clock.GetUtcNow(), license.CreatedUtc);
        var entry = Assert.Single(db.IssueEntries);
        Assert.Equal(license.Reference, entry.Reference);
        Assert.Equal(IssueKind.Issue, entry.Kind);
        Assert.Equal(tool.Clock.GetUtcNow(), entry.IssuedUtc);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), entry.ExpiresUtc);
        Assert.Equal("Acme Ltd", entry.DisplayName);
        Assert.Equal("gold", entry.VendorTag);
        Assert.Equal("ORD-1042", entry.OrderRef);
        Assert.StartsWith($"LIC-{license.Reference[..5]}-{license.Reference[5..]}-", entry.KeyIdentifier);
        Assert.StartsWith(entry.KeyIdentifier + ".", key);

        // The active key signed it: the product's own in per-product mode, the shared key in shared mode.
        var active = db.SigningKeys.AsEnumerable().Single(k =>
            k.Role == SigningKeyRole.Active && k.ProductId == (shared ? null : "acme.commerce"));
        Assert.Equal(active.SigningKeyId, entry.SigningKeyId);
        if (!shared)
        {
            Assert.NotEqual(db.SigningKeys.AsEnumerable().Single(k => k.Role == SigningKeyRole.Active && k.ProductId == "acme.shipping").SigningKeyId, entry.SigningKeyId);
        }

        var features = FeaturesJson.Read(entry.FeaturesJson);
        Assert.Equal(FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m), features);

        var result = Evaluate(tool, "acme.commerce", key, shared);
        Assert.Equal(LicenseState.Valid, result.State);
        var verified = result.License!;
        Assert.Equal(active.SigningKeyId, verified.SigningKeyId);
        Assert.Equal(entry.KeyIdentifier, verified.KeyIdentifier);
        Assert.Equal(entry.ExpiresUtc, verified.Expires);
        Assert.Equal(entry.IssuedUtc, verified.Issued);
        Assert.Equal("Acme Ltd", verified.DisplayName);
        Assert.Equal("gold", verified.VendorTag);
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.Equal(500m, result.GetNumber("max-orders"));
    }

    [Fact]
    public void A_date_expires_at_the_end_of_that_day_and_perpetual_has_no_expiry()
    {
        using var tool = WithProducts(shared: false);

        var (exitDate, date) = Run(tool, "license", "issue", "acme.commerce", "--expires", "2027-03-01");
        var (exitPerpetual, perpetual) = Run(tool, "license", "issue", "acme.commerce", "--expires", "perpetual");
        using var _ = date;
        using var __ = perpetual;

        Assert.Equal(0, exitDate);
        Assert.Equal(0, exitPerpetual);
        using var db = Open(tool);
        var entries = db.IssueEntries.ToList();
        Assert.Contains(entries, e => e.ExpiresUtc == new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero));
        Assert.Contains(entries, e => e.ExpiresUtc is null);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), Evaluate(tool, "acme.commerce", KeyOf(date), false).License!.Expires);
        Assert.True(Evaluate(tool, "acme.commerce", KeyOf(perpetual), false).License!.IsPerpetual);
    }

    [Fact]
    public void A_license_issued_without_options_stores_no_customer_details()
    {
        using var tool = WithProducts(shared: false);

        var (exit, run) = Run(tool, IssueArgs());
        using var _ = run;

        Assert.Equal(0, exit);
        using var db = Open(tool);
        var license = Assert.Single(db.Licenses);
        Assert.Null(license.CustomerEmail);
        var entry = Assert.Single(db.IssueEntries);
        Assert.Null(entry.OrderRef);
        Assert.Null(entry.DisplayName);
        Assert.Null(entry.VendorTag);
        Assert.Equal("[]", entry.FeaturesJson);
    }

    [Theory]
    [InlineData("ACME", "ACME-")]
    [InlineData("none", "")]
    public void The_installations_prefix_is_used(string typed, string expected)
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Shared: true, Prefix: typed));
        Succeeds(tool, "product", "add", "acme.commerce", "--name", "Acme Commerce");

        var (exit, run) = Run(tool, IssueArgs());
        using var _ = run;

        Assert.Equal(0, exit);
        using var db = Open(tool);
        var entry = Assert.Single(db.IssueEntries);
        Assert.StartsWith(expected, entry.KeyIdentifier);
        Assert.Equal(expected.Length + 5 + 1 + 5 + 1 + 4, entry.KeyIdentifier.Length);
        var license = Assert.Single(db.Licenses);
        Assert.Equal(license.Reference, entry.Reference);
        Assert.Equal(10, license.Reference.Length);
        Assert.Equal(LicenseState.Valid, Evaluate(tool, "acme.commerce", KeyOf(run), shared: true).State);
    }

    // ---- Refusals

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Undefined_feature(bool shared)
    {
        using var tool = WithProducts(shared);
        var before = Stored(tool);

        var (exit, run) = Run(tool, IssueArgs("--feature", "max-order=500"));
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("features.max-order", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void A_refused_issue_names_every_problem_and_stores_nothing()
    {
        using var tool = WithProducts(shared: false);
        var before = Stored(tool);

        var (exit, run) = Run(tool, "license", "issue", "acme.commerce", "--expires", "2020-01-01",
            "--feature", "max-order=500",
            "--feature", "max-orders=lots",
            "--feature", "ecommerce=maybe",
            "--feature", "region",
            "--feature", "ecommerce=off",
            "--order-ref", " ",
            "--email", "");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        foreach (var named in new[] { "expires", "features.max-order", "features.max-orders", "features.ecommerce", "--feature", "orderRef", "email" })
        {
            Assert.Contains(named, run.Stderr);
        }

        Assert.Contains("Nothing stored.", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void An_expiry_in_no_known_form_is_refused_naming_expires()
    {
        using var tool = WithProducts(shared: false);

        var (exit, run) = Run(tool, "license", "issue", "acme.commerce", "--expires", "next year");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Contains("expires", run.Stderr);
        Assert.Contains("perpetual", run.Stderr);
        Assert.Single(run.Stderr.Split(NewLine), line => line.StartsWith("  expires:", StringComparison.Ordinal));
        Assert.Equal("", run.Stdout);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_product(bool shared)
    {
        using var tool = WithProducts(shared);
        var before = Stored(tool);

        var (exit, run) = Run(tool, "license", "issue", "acme.nothing", "--expires", "perpetual");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Unknown product acme.nothing", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void Wrong_passphrase()
    {
        using var tool = WithProducts(shared: false);
        var before = Stored(tool);

        using var run = tool.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = "not the passphrase" });
        var exit = run.Run(IssueArgs());

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Wrong passphrase", run.Stderr);
        Assert.Equal(before, Stored(tool));
        using var db = Open(tool);
        Assert.Empty(db.Licenses);
        Assert.Empty(db.IssueEntries);
    }

    [Fact]
    public void A_wrong_passphrase_is_found_before_anything_else_is_asked()
    {
        using var tool = WithProducts(shared: false);
        var before = Stored(tool);

        // At a terminal: a wrong hidden passphrase, with the expiry answer waiting on stdin.
        using var run = tool.Another(stdin: "2027-03-01\n", stdinRedirected: false, hiddenKeys: "not the passphrase\n");
        var exit = run.Run("license", "issue", "acme.commerce");

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Wrong passphrase", run.Stderr);
        Assert.DoesNotContain("Expiry", run.Stderr);
        Assert.Equal("2027-03-01", run.Environment.Console.ReadLine());
        Assert.Equal(before, Stored(tool));
    }

    // ---- Running by hand or from scripts

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scripted_issue(bool shared)
    {
        using var tool = WithProducts(shared);

        // No terminal (stdin redirected, empty), passphrase in the environment setting, everything given.
        using var run = tool.Another(variables: WithPassphrase);
        var exit = run.Run(IssueArgs("--feature", "ecommerce=on", "--feature", "max-orders=500", "--feature", "region=eu"));

        Assert.Equal(0, exit);
        var key = KeyOf(run);
        var result = Evaluate(tool, "acme.commerce", key, shared);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal("eu", result.GetText("region"));
        Assert.Contains("Using", run.Stderr);
    }

    [Fact]
    public void Scripted_issue_with_the_passphrase_on_stdin()
    {
        using var tool = WithProducts(shared: false);

        using var run = tool.Another(stdin: Pass + "\n");
        var exit = run.Run(IssueArgs("--passphrase-stdin"));

        Assert.Equal(0, exit);
        Assert.Equal(LicenseState.Valid, Evaluate(tool, "acme.commerce", KeyOf(run), false).State);
    }

    [Fact]
    public void Missing_input_with_nobody_present()
    {
        using var tool = WithProducts(shared: false);
        var before = Stored(tool);

        var (exit, run) = Run(tool, "license", "issue", "acme.commerce");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("--expires", run.Stderr);
        Assert.Equal(before, Stored(tool));
    }

    [Fact]
    public void Every_missing_input_is_named()
    {
        using var tool = WithProducts(shared: false);

        var (exit, run) = Run(tool, "license", "issue");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Contains("<product-id>", run.Stderr);
        Assert.Contains("--expires", run.Stderr);
        Assert.Equal("", run.Stdout);
    }

    [Fact]
    public void Prompted_by_hand()
    {
        using var tool = WithProducts(shared: false);

        using var run = tool.Another(stdin: "2027-03-01\n", stdinRedirected: false, hiddenKeys: Pass + "\n");
        var exit = run.Run("license", "issue", "acme.commerce");

        Assert.Equal(0, exit);
        Assert.Contains("Expiry", run.Stderr);
        var key = KeyOf(run);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), Evaluate(tool, "acme.commerce", key, false).License!.Expires);
    }

    // ---- Key strings are not stored

    [Fact]
    public void Copied_folder()
    {
        using var tool = WithProducts(shared: false);
        var keys = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var (exit, run) = Run(tool, IssueArgs("--feature", $"max-orders={i + 1}", "--email", "owner@example.com"));
            using (run)
            {
                Assert.Equal(0, exit);
                keys.Add(KeyOf(run));
            }
        }

        var folder = Path.GetFullPath(tool.HomeInstallation);
        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            foreach (var key in keys)
            {
                Assert.True(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(key)) < 0, $"{Path.GetFileName(file)} holds a key string");
                // The signed part alone (after the identifier) must not be there either.
                Assert.True(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(key[(key.IndexOf('.') + 1)..])) < 0, $"{Path.GetFileName(file)} holds a key's signed part");
            }
        }
    }

    // ---- Parts

    [Fact]
    public void Features_are_stored_as_issued_and_read_back()
    {
        var features = FeatureList.Empty.With("ecommerce", true).With("max-orders", 1.50m).With("region", "eu \"west\"");

        Assert.Equal(features, FeaturesJson.Read(FeaturesJson.Write(features)));
        Assert.Equal("[]", FeaturesJson.Write(FeatureList.Empty));
        Assert.Equal(FeatureList.Empty, FeaturesJson.Read("[]"));
    }

    [Theory]
    [InlineData("LIC-8F3AK-M7RXB", "8F3AKM7RXB")]
    [InlineData("8F3AK-M7RXB", "8F3AKM7RXB")]
    [InlineData("ACME2-8F3AK-M7RXB", "8F3AKM7RXB")]
    public void A_displayed_reference_gives_its_ten_characters(string display, string reference) =>
        Assert.Equal(reference, Issuing.ReferenceOf(display));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_active_key_is_chosen_by_mode(bool shared)
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Shared: shared, Standby: true));
        Succeeds(tool, "product", "add", "acme.commerce", "--name", "Acme Commerce");
        Succeeds(tool, "product", "add", "acme.shipping", "--name", "Acme Shipping");
        using var db = Open(tool);
        var settings = InstallationSettings.Load(db);

        var commerce = SigningKeyStore.Active(db, settings, "acme.commerce");
        var shipping = SigningKeyStore.Active(db, settings, "acme.shipping");

        Assert.Equal(SigningKeyRole.Active, commerce.Role);
        if (shared)
        {
            Assert.Null(commerce.ProductId);
            Assert.Equal(commerce.SigningKeyId, shipping.SigningKeyId);
        }
        else
        {
            Assert.Equal("acme.commerce", commerce.ProductId);
            Assert.Equal("acme.shipping", shipping.ProductId);
            Assert.NotEqual(commerce.SigningKeyId, shipping.SigningKeyId);
        }
    }

    [Fact]
    public void No_active_key_is_refused()
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Shared: false));
        using var db = Open(tool);

        var refusal = Assert.Throws<ToolRefusal>(() => SigningKeyStore.Active(db, InstallationSettings.Load(db), "acme.commerce"));

        Assert.Contains("acme.commerce has no active signing key", refusal.Message);
    }
}
