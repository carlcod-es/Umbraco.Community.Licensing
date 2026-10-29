using Umbraco.Community.Licensing.Tool.Data;
using StoredType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class ProductTests
{
    private const string Pass = ScriptedTool.DefaultPassphrase;
    private static readonly string NewLine = System.Environment.NewLine;

    private static readonly IReadOnlyDictionary<string, string> WithPassphrase =
        new Dictionary<string, string> { [ToolContext.PassphraseVariable] = Pass };

    private static ScriptedTool Installation(bool shared = false, bool standby = true) =>
        ScriptedTool.Installed(new SetupAnswers(Shared: shared, Standby: standby));

    private static ToolDbContext Open(ScriptedTool tool) => Tool.Installation.Open(Path.GetFullPath(tool.HomeInstallation));

    /// <summary>A run with the passphrase in <c>UCL_PASSPHRASE</c> and nobody at a terminal.</summary>
    private static (int Exit, ScriptedTool Run) Run(ScriptedTool tool, params string[] args)
    {
        var run = tool.Another(variables: WithPassphrase);
        return (run.Run(args), run);
    }

    /// <summary>Everything a refused command must leave as it was.</summary>
    private static string Snapshot(ScriptedTool tool)
    {
        using var db = Open(tool);
        return string.Join(
            "|",
            db.Products.AsEnumerable().OrderBy(p => p.ProductId, StringComparer.Ordinal).Select(p => $"{p.ProductId}={p.DisplayName}"))
            + "#" + string.Join(",", db.FeatureDefinitions.AsEnumerable().OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => $"{f.ProductId}/{f.Name}/{f.Type}"))
            + "#" + string.Join(",", db.SigningKeys.AsEnumerable().Select(k => k.SigningKeyId).Order(StringComparer.Ordinal))
            + "#" + db.Settings.Count();
    }

    private static void AddAcme(ScriptedTool tool)
    {
        var (exit, run) = Run(tool, "product", "add", "acme.commerce", "--name", "Acme Commerce");
        using (run)
        {
            Assert.Equal(0, exit);
        }
    }

    // ---- Adding a product

    [Fact]
    public void Product_added_in_per_product_mode()
    {
        using var tool = Installation();

        var (exit, run) = Run(tool, "product", "add", "acme.commerce", "--name", "  Acme Commerce  ");
        using var _ = run;

        Assert.Equal(0, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains($"Using {Path.GetFullPath(tool.HomeInstallation)}{NewLine}", run.Stderr);
        using var db = Open(tool);
        var product = Assert.Single(db.Products);
        Assert.Equal("acme.commerce", product.ProductId);
        Assert.Equal("Acme Commerce", product.DisplayName);
        Assert.Equal(tool.Clock.GetUtcNow(), product.CreatedUtc);

        var keys = db.SigningKeys.ToList();
        Assert.Equal(2, keys.Count);
        Assert.All(keys, k => Assert.Equal("acme.commerce", k.ProductId));
        Assert.Equal(1, keys.Count(k => k.Role == SigningKeyRole.Active));
        Assert.Equal(1, keys.Count(k => k.Role == SigningKeyRole.Standby));
        Assert.Equal(2, keys.Select(k => k.SigningKeyId).Distinct().Count());
        Assert.All(keys, k =>
        {
            Assert.Equal(tool.Clock.GetUtcNow(), k.CreatedUtc);
            Assert.Contains("ENCRYPTED PRIVATE KEY", k.EncryptedPrivateKeyPem);
            using var opened = KeyProtection.Decrypt(k.EncryptedPrivateKeyPem, Pass);
            Assert.Equal(k.SigningKeyId, opened.SigningKeyId);
            Assert.Equal(k.PublicKey, opened.PublicKey.Export());
            Assert.ThrowsAny<Exception>(() => KeyProtection.Decrypt(k.EncryptedPrivateKeyPem, "another passphrase here"));
        });
    }

    [Fact]
    public void Product_added_in_per_product_mode_with_standby_off_has_an_active_key_only()
    {
        using var tool = Installation(standby: false);

        AddAcme(tool);

        using var db = Open(tool);
        var key = Assert.Single(db.SigningKeys);
        Assert.Equal(SigningKeyRole.Active, key.Role);
        Assert.Equal("acme.commerce", key.ProductId);
    }

    [Fact]
    public void Products_get_keys_of_their_own()
    {
        using var tool = Installation();
        AddAcme(tool);
        var (exit, run) = Run(tool, "product", "add", "acme.shipping", "--name", "Acme Shipping");
        using var _ = run;

        Assert.Equal(0, exit);
        using var db = Open(tool);
        Assert.Equal(4, db.SigningKeys.Select(k => k.SigningKeyId).Distinct().Count());
        Assert.Equal(2, db.SigningKeys.Count(k => k.ProductId == "acme.shipping"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Product_added_in_shared_mode_needs_no_passphrase_and_creates_no_key(bool standby)
    {
        using var tool = Installation(shared: true, standby: standby);
        string[] before;
        using (var db = Open(tool))
        {
            before = [.. db.SigningKeys.Select(k => k.SigningKeyId).OrderBy(i => i)];
        }

        // Nobody at a terminal, no passphrase source anywhere, and stdin left unread.
        using var run = tool.Another(stdin: "unread\n");
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce");

        Assert.Equal(0, exit);
        Assert.Contains("shared signing key", run.Stderr);
        Assert.Equal("unread", run.Environment.Console.ReadLine());
        using var after = Open(tool);
        Assert.Equal("acme.commerce", Assert.Single(after.Products).ProductId);
        Assert.Equal(before, after.SigningKeys.Select(k => k.SigningKeyId).OrderBy(i => i).ToArray());
        Assert.Equal(standby ? 2 : 1, after.SigningKeys.Count());
        Assert.All(after.SigningKeys, k => Assert.Null(k.ProductId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Duplicate_product_id(bool shared)
    {
        using var tool = Installation(shared);
        AddAcme(tool);
        var before = Snapshot(tool);

        var (exit, run) = Run(tool, "product", "add", "acme.commerce", "--name", "Another Name");
        using var _ = run;

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("acme.commerce", run.Stderr);
        Assert.Contains("already exists", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_product_id(bool shared)
    {
        using var tool = Installation(shared);
        var before = Snapshot(tool);

        // No passphrase source: the rule is what is refused, so inputs are checked before the passphrase is asked for.
        using var run = tool.Another();
        var exit = run.Run("product", "add", "Acme Commerce", "--name", "Acme Commerce");

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Invalid product ID", run.Stderr);
        Assert.Contains("two dot-separated parts", run.Stderr);
        Assert.DoesNotContain("Passphrase", run.Stderr);
        Assert.DoesNotContain("(Parameter", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void A_wrong_passphrase_changes_nothing()
    {
        using var tool = Installation();
        var before = Snapshot(tool);

        using var run = tool.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = "not the passphrase" });
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce");

        Assert.Equal(1, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("Wrong passphrase", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
        using var db = Open(tool);
        Assert.Empty(db.Products);
        Assert.Empty(db.SigningKeys);
    }

    [Fact]
    public void No_passphrase_source_in_per_product_mode_changes_nothing()
    {
        using var tool = Installation();
        var before = Snapshot(tool);

        using var run = tool.Another();
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce");

        Assert.Equal(1, exit);
        Assert.Contains("Passphrase needed", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Passphrase_on_stdin_adds_a_product_with_no_terminal()
    {
        using var tool = Installation();

        using var run = tool.Another(stdin: Pass + "\n");
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce", "--passphrase-stdin");

        Assert.Equal(0, exit);
        using var db = Open(tool);
        Assert.Equal(2, db.SigningKeys.Count(k => k.ProductId == "acme.commerce"));
    }

    [Fact]
    public void Passphrase_variable_adds_a_product_with_no_terminal()
    {
        using var tool = Installation();

        AddAcme(tool);

        using var db = Open(tool);
        Assert.Equal(2, db.SigningKeys.Count(k => k.ProductId == "acme.commerce"));
    }

    [Fact]
    public void Passphrase_is_prompted_hidden_at_a_terminal()
    {
        using var tool = Installation();

        using var run = tool.Another(stdinRedirected: false, hiddenKeys: Pass + "\n");
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce");

        Assert.Equal(0, exit);
        using var db = Open(tool);
        Assert.Equal(2, db.SigningKeys.Count());
    }

    [Fact]
    public void Add_missing_inputs_with_nobody_present_are_refused_naming_each()
    {
        using var tool = Installation();
        var before = Snapshot(tool);

        using var both = tool.Another(variables: WithPassphrase);
        Assert.Equal(1, both.Run("product", "add"));
        Assert.Contains("<product-id>", both.Stderr);
        Assert.Contains("--name", both.Stderr);

        using var noName = tool.Another(variables: WithPassphrase);
        Assert.Equal(1, noName.Run("product", "add", "acme.commerce"));
        Assert.Contains("Missing --name", noName.Stderr);
        Assert.DoesNotContain("<product-id>", noName.Stderr);

        using var noId = tool.Another(variables: WithPassphrase);
        Assert.Equal(1, noId.Run("product", "add", "--name", "Acme Commerce"));
        Assert.Contains("Missing <product-id>", noId.Stderr);

        using var blankName = tool.Another(variables: WithPassphrase);
        Assert.Equal(1, blankName.Run("product", "add", "acme.commerce", "--name", "   "));
        Assert.Contains("--name", blankName.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Add_missing_inputs_at_a_terminal_are_prompted()
    {
        using var tool = Installation();

        using var run = tool.Another(stdin: "acme.commerce\n Acme Commerce \n", stdinRedirected: false, variables: WithPassphrase);
        var exit = run.Run("product", "add");

        Assert.Equal(0, exit);
        Assert.Contains("Product ID: ", run.Stderr);
        Assert.Contains("Display name: ", run.Stderr);
        Assert.Equal("", run.Stdout);
        using var db = Open(tool);
        var product = Assert.Single(db.Products);
        Assert.Equal("acme.commerce", product.ProductId);
        Assert.Equal("Acme Commerce", product.DisplayName);
    }

    [Fact]
    public void Add_with_no_installation_is_refused()
    {
        using var tool = new ScriptedTool();

        Assert.Equal(1, tool.Run("product", "add", "acme.commerce", "--name", "Acme Commerce"));
        Assert.Contains("No installation found", tool.Stderr);
    }

    // ---- Feature definitions

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Feature_added(bool shared)
    {
        using var tool = Installation(shared);
        AddAcme(tool);
        tool.Clock.Advance(TimeSpan.FromHours(2));

        using var run = tool.Another();
        var exit = run.Run("product", "feature", "add", "acme.commerce", "max-orders", "--type", "number");

        Assert.Equal(0, exit);
        Assert.Equal("", run.Stdout);
        using var db = Open(tool);
        var row = Assert.Single(db.FeatureDefinitions);
        Assert.Equal(("acme.commerce", "max-orders", StoredType.Number), (row.ProductId, row.Name, row.Type));
        Assert.Equal(tool.Clock.GetUtcNow(), row.AddedUtc);

        // The product's keys may carry max-orders as a number, and not as text.
        db.FeatureDefinitions.Add(new Data.FeatureDefinition { ProductId = "acme.commerce", Name = "ecommerce", Type = StoredType.Switch, AddedUtc = tool.Clock.GetUtcNow() });
        db.FeatureDefinitions.Add(new Data.FeatureDefinition { ProductId = "acme.commerce", Name = "region", Type = StoredType.Text, AddedUtc = tool.Clock.GetUtcNow() });
        db.SaveChanges();
        var definitions = ProductRules.DefinitionsFor(db, "acme.commerce");
        Assert.Equal(
            new[] { "ecommerce:Switch", "max-orders:Number", "region:Text" },
            definitions.Select(d => $"{d.Key}:{d.Value}").ToArray());

        using var pair = SigningKeyPair.Create();
        var issuer = new LicenseIssuer();
        var asNumber = new LicenseRequest
        {
            Product = "acme.commerce",
            Expiry = LicenseExpiry.Perpetual,
            Features = FeatureList.Empty.With("max-orders", 50m),
        };
        Assert.NotNull(issuer.Issue(asNumber, pair.PrivateKey, definitions).KeyString);

        var asText = asNumber with { Features = FeatureList.Empty.With("max-orders", "fifty") };
        var refused = Assert.Throws<LicenseIssueException>(() => issuer.Issue(asText, pair.PrivateKey, definitions));
        Assert.Contains("max-orders", refused.Message);

        Assert.Same(FeatureDefinitions.None, ProductRules.DefinitionsFor(db, "acme.shipping"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Feature_already_defined(bool shared)
    {
        using var tool = Installation(shared);
        AddAcme(tool);
        using (var first = tool.Another())
        {
            Assert.Equal(0, first.Run("product", "feature", "add", "acme.commerce", "max-orders", "--type", "number"));
        }

        var before = Snapshot(tool);

        using var asText = tool.Another();
        Assert.Equal(1, asText.Run("product", "feature", "add", "acme.commerce", "max-orders", "--type", "text"));
        Assert.Equal("", asText.Stdout);
        Assert.Contains("max-orders", asText.Stderr);
        Assert.Contains("number", asText.Stderr);

        using var again = tool.Another();
        Assert.Equal(1, again.Run("product", "feature", "add", "acme.commerce", "max-orders", "--type", "number"));
        Assert.Contains("already defined", again.Stderr);

        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Feature_add_unknown_product_is_refused()
    {
        using var tool = Installation();
        var before = Snapshot(tool);

        using var run = tool.Another();
        var exit = run.Run("product", "feature", "add", "acme.nothing", "max-orders", "--type", "number");

        Assert.Equal(1, exit);
        Assert.Contains("Unknown product acme.nothing", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Feature_add_invalid_name_is_refused_naming_the_rule()
    {
        using var tool = Installation();
        AddAcme(tool);
        var before = Snapshot(tool);

        using var run = tool.Another();
        var exit = run.Run("product", "feature", "add", "acme.commerce", "Max Orders", "--type", "number");

        Assert.Equal(1, exit);
        Assert.Contains("A feature name must be", run.Stderr);
        Assert.DoesNotContain("(Parameter", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Feature_add_invalid_type_is_refused_listing_the_three()
    {
        using var tool = Installation();
        AddAcme(tool);
        var before = Snapshot(tool);

        using var run = tool.Another();
        var exit = run.Run("product", "feature", "add", "acme.commerce", "max-orders", "--type", "decimal");

        Assert.Equal(1, exit);
        Assert.Contains("decimal", run.Stderr);
        Assert.Contains("switch, number or text", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Theory]
    [InlineData("SWITCH", "Switch")]
    [InlineData("Text", "Text")]
    public void Feature_type_is_accepted_in_any_case(string typed, string expected)
    {
        using var tool = Installation();
        AddAcme(tool);

        using var run = tool.Another();
        Assert.Equal(0, run.Run("product", "feature", "add", "acme.commerce", "ecommerce", "--type", typed));

        using var db = Open(tool);
        Assert.Equal(expected, Assert.Single(db.FeatureDefinitions).Type.ToString());
    }

    [Fact]
    public void Feature_add_missing_inputs_with_nobody_present_are_refused_naming_each()
    {
        using var tool = Installation();
        AddAcme(tool);
        var before = Snapshot(tool);

        using var all = tool.Another();
        Assert.Equal(1, all.Run("product", "feature", "add"));
        Assert.Contains("<product-id>", all.Stderr);
        Assert.Contains("<feature>", all.Stderr);
        Assert.Contains("--type", all.Stderr);

        using var noType = tool.Another();
        Assert.Equal(1, noType.Run("product", "feature", "add", "acme.commerce", "max-orders"));
        Assert.Contains("Missing --type", noType.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Feature_add_missing_inputs_at_a_terminal_are_prompted()
    {
        using var tool = Installation();
        AddAcme(tool);

        using var run = tool.Another(stdin: "acme.commerce\nmax-orders\nnumber\n", stdinRedirected: false);
        var exit = run.Run("product", "feature", "add");

        Assert.Equal(0, exit);
        Assert.Contains("Type (switch, number or text): ", run.Stderr);
        using var db = Open(tool);
        Assert.Equal("max-orders", Assert.Single(db.FeatureDefinitions).Name);
    }

    // ---- Display name

    [Fact]
    public void Display_name_edited()
    {
        using var tool = Installation();
        AddAcme(tool);
        using (var feature = tool.Another())
        {
            Assert.Equal(0, feature.Run("product", "feature", "add", "acme.commerce", "max-orders", "--type", "number"));
        }

        string[] keysBefore;
        using (var db = Open(tool))
        {
            keysBefore = [.. db.SigningKeys.Select(k => k.SigningKeyId).OrderBy(i => i)];
        }

        using var run = tool.Another();
        var exit = run.Run("product", "rename", "acme.commerce", "--name", "Acme Commerce Pro");

        Assert.Equal(0, exit);
        Assert.Equal("", run.Stdout);
        using var after = Open(tool);
        var product = Assert.Single(after.Products);
        Assert.Equal("acme.commerce", product.ProductId);
        Assert.Equal("Acme Commerce Pro", product.DisplayName);
        Assert.Equal("max-orders", Assert.Single(after.FeatureDefinitions).Name);
        Assert.Equal(keysBefore, after.SigningKeys.Select(k => k.SigningKeyId).OrderBy(i => i).ToArray());

        using var list = tool.Another();
        Assert.Equal(0, list.Run("product", "list"));
        Assert.Contains("Name: Acme Commerce Pro", list.Stdout);
    }

    [Fact]
    public void Rename_unknown_product_is_refused()
    {
        using var tool = Installation();
        AddAcme(tool);
        var before = Snapshot(tool);

        using var run = tool.Another();
        var exit = run.Run("product", "rename", "acme.nothing", "--name", "Nothing");

        Assert.Equal(1, exit);
        Assert.Contains("Unknown product acme.nothing", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Rename_missing_inputs()
    {
        using var tool = Installation();
        AddAcme(tool);

        using var nobody = tool.Another();
        Assert.Equal(1, nobody.Run("product", "rename"));
        Assert.Contains("<product-id>", nobody.Stderr);
        Assert.Contains("--name", nobody.Stderr);

        using var terminal = tool.Another(stdin: "acme.commerce\nAcme Pro\n", stdinRedirected: false);
        Assert.Equal(0, terminal.Run("product", "rename"));
        using var db = Open(tool);
        Assert.Equal("Acme Pro", Assert.Single(db.Products).DisplayName);
    }

    // ---- Listing

    [Fact]
    public void Products_listed()
    {
        using var tool = Installation();
        AddAcme(tool);
        using (var shipping = tool.Another(variables: WithPassphrase))
        {
            Assert.Equal(0, shipping.Run("product", "add", "acme.shipping", "--name", "Acme Shipping"));
        }

        foreach (var args in new[]
        {
            new[] { "acme.commerce", "max-orders", "number" },
            new[] { "acme.commerce", "ecommerce", "switch" },
            new[] { "acme.commerce", "region", "text" },
        })
        {
            using var feature = tool.Another();
            Assert.Equal(0, feature.Run("product", "feature", "add", args[0], args[1], "--type", args[2]));
        }

        using (var db = Open(tool))
        {
            foreach (var reference in new[] { "AAAAAAAAAA", "BBBBBBBBBB", "CCCCCCCCCC" })
            {
                db.Licenses.Add(new License { Reference = reference, ProductId = "acme.commerce", CreatedUtc = tool.Clock.GetUtcNow() });
            }

            db.Licenses.Add(new License { Reference = "DDDDDDDDDD", ProductId = "acme.shipping", CreatedUtc = tool.Clock.GetUtcNow() });
            db.SaveChanges();
        }

        using var run = tool.Another();
        var exit = run.Run("product", "list");

        Assert.Equal(0, exit);
        Assert.Contains($"Using {Path.GetFullPath(tool.HomeInstallation)}", run.Stderr);
        var expected = string.Join(
            NewLine,
            "acme.commerce",
            "  Name: Acme Commerce",
            "  Licenses: 3",
            "  Features:",
            "    ecommerce (switch)",
            "    max-orders (number)",
            "    region (text)",
            "",
            "acme.shipping",
            "  Name: Acme Shipping",
            "  Licenses: 1",
            "  Features: none",
            "");
        Assert.Equal(expected, run.Stdout);
    }

    [Fact]
    public void Products_are_listed_ordered_by_product_id()
    {
        using var tool = Installation(shared: true);
        foreach (var id in new[] { "zeta.app", "acme.shipping", "acme.commerce" })
        {
            using var add = tool.Another();
            Assert.Equal(0, add.Run("product", "add", id, "--name", id));
        }

        using var run = tool.Another();
        Assert.Equal(0, run.Run("product", "list"));

        var ids = run.Stdout.Split(NewLine).Where(l => l.Length > 0 && !l.StartsWith(' ')).ToArray();
        Assert.Equal(new[] { "acme.commerce", "acme.shipping", "zeta.app" }, ids);
    }

    [Fact]
    public void No_products_lists_nothing_and_says_so_on_stderr()
    {
        using var tool = Installation();

        using var run = tool.Another();
        var exit = run.Run("product", "list");

        Assert.Equal(0, exit);
        Assert.Equal("", run.Stdout);
        Assert.Contains("No products", run.Stderr);
    }

    // ---- No passphrase for rename, feature add and list

    [Fact]
    public void Rename_feature_add_and_list_never_read_a_passphrase_source()
    {
        using var tool = Installation();
        AddAcme(tool);

        // --passphrase-stdin is given and stdin holds a line; no hidden keys are scripted, so a prompt would throw.
        string[][] commands =
        [
            ["product", "rename", "acme.commerce", "--name", "Acme Pro"],
            ["product", "feature", "add", "acme.commerce", "max-orders", "--type", "number"],
            ["product", "list"],
        ];
        foreach (var args in commands)
        {
            using var run = tool.Another(stdin: "unread\n", variables: WithPassphrase);
            Assert.Equal(0, run.Run([.. args, "--passphrase-stdin"]));
            Assert.Equal("unread", run.Environment.Console.ReadLine());
        }
    }

    // ---- Settings

    [Fact]
    public void Commands_refuse_when_the_settings_are_missing()
    {
        using var tool = Installation();
        using (var db = Open(tool))
        {
            db.Settings.RemoveRange(db.Settings.Where(s => s.Name == "SigningKeyMode"));
            db.SaveChanges();
        }

        var before = Snapshot(tool);
        using var run = tool.Another(variables: WithPassphrase);
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce");

        Assert.Equal(1, exit);
        Assert.Contains("settings are missing or damaged", run.Stderr);
        Assert.Equal(before, Snapshot(tool));
    }

    [Fact]
    public void Commands_refuse_on_an_installation_left_empty_by_an_interrupted_setup()
    {
        using var tool = new ScriptedTool();
        using (Tool.Installation.Open(tool.HomeInstallation))
        {
        }

        using var run = tool.Another();
        var exit = run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce");

        Assert.Equal(1, exit);
        Assert.Contains("settings are missing or damaged", run.Stderr);
    }

    [Theory]
    [InlineData("SigningKeyMode", "Both")]
    [InlineData("StandbyKeys", "yes")]
    [InlineData("KeyPrefix", "lower case")]
    public void Settings_with_an_unreadable_value_are_refused(string name, string value)
    {
        using var tool = Installation();
        using (var db = Open(tool))
        {
            db.Settings.Single(s => s.Name == name).Value = value;
            db.SaveChanges();
        }

        using var db2 = Open(tool);
        var refusal = Assert.Throws<ToolRefusal>(() => InstallationSettings.Load(db2));
        Assert.Contains("settings are missing or damaged", refusal.Message);
    }

    [Theory]
    [InlineData(false, true, "")]
    [InlineData(true, false, "ACME")]
    public void Settings_round_trip(bool shared, bool standby, string prefix)
    {
        using var tool = Installation(shared, standby);
        using var db = Open(tool);
        var loaded = InstallationSettings.Load(db);

        Assert.Equal(shared, loaded.Shared);
        Assert.Equal(standby, loaded.StandbyKeys);
        Assert.Equal("LIC", loaded.Prefix);

        using var other = ScriptedTool.Installed(new SetupAnswers(Shared: shared, Standby: standby, Prefix: prefix == "" ? "none" : prefix));
        using var otherDb = Open(other);
        Assert.Equal(prefix, InstallationSettings.Load(otherDb).Prefix);
    }

    // ---- Shared clock, group help

    [Fact]
    public void Another_shares_the_clock_of_its_tool()
    {
        using var tool = Installation(shared: true);
        tool.Clock.Advance(TimeSpan.FromDays(3));

        using var run = tool.Another();
        Assert.Equal(run.Clock, tool.Clock);
        Assert.Equal(0, run.Run("product", "add", "acme.commerce", "--name", "Acme Commerce"));

        using var db = Open(tool);
        Assert.Equal(tool.Clock.GetUtcNow(), Assert.Single(db.Products).CreatedUtc);
        Assert.Equal(new DateTimeOffset(2026, 1, 18, 9, 30, 0, TimeSpan.Zero), tool.Clock.GetUtcNow());
    }

    [Theory]
    [InlineData("folder", "show")]
    [InlineData("product", "add")]
    [InlineData("product", "feature", "add")]
    public void A_command_group_run_with_no_subcommand_shows_its_help(params string[] expectedCommands)
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run(expectedCommands[..^1]);

        Assert.Equal(2, exit);
        Assert.Equal("", tool.Stdout);
        Assert.Contains("Required command was not provided.", tool.Stderr);
        Assert.Contains("Commands:", tool.Stderr);
        Assert.Contains($"ucl {string.Join(' ', expectedCommands[..^1])} [command]", tool.Stderr);
        Assert.Contains(expectedCommands[^1], tool.Stderr);
    }

    [Fact]
    public void A_leaf_command_usage_error_does_not_show_help()
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run("product", "list", "--bogus");

        Assert.Equal(2, exit);
        Assert.DoesNotContain("Commands:", tool.Stderr);
    }
}
