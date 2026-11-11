using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>Products menu (vendor-tool-products), as whole sessions through <see cref="ToolApp.Run(string[], ToolEnvironment, Func{ToolEnvironment, int})"/>, in both signing-key modes.</summary>
public class ProductTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Id = "acme.commerce";
    private const string Name = "Acme Commerce";
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private int Run(ScriptedConsole console, ActionTable? actions = null)
    {
        var exit = _host.Run(console, actions, Startup.Offer.Default);
        Assert.True(exit == 0, console.Err + console.Out);
        return exit;
    }

    // Main menu 1 is Products; there 1 Add, 2 Rename, 3 Add a feature, 4 List; 0 leaves each menu.
    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    /// <summary>Setup (so the session holds the passphrase) and the Products menu open.</summary>
    private static ScriptedConsole SetUpThenProducts(bool shared, bool standby = true) =>
        new ScriptedConsole().SetUp(shared: shared, standby: standby).Line("1");

    /// <summary>An installation made by an earlier session, then a new session with the Products menu open: it holds no passphrase.</summary>
    private ScriptedConsole OpenProducts(bool shared, bool standby = true)
    {
        _host.SetUpInstallation(shared: shared, standby: standby);
        return new ScriptedConsole().Line("1");
    }

    private static ScriptedConsole Add(ScriptedConsole c, string id, string name) => c.Line("1").Line(id).Line(name);

    private static ScriptedConsole AddFeature(ScriptedConsole c, string product, string feature, string type) =>
        c.Line("3").Line(product).Line(feature).Line(type);

    private ToolDbContext Db() => Installation.OpenExisting(_host.HomeInstallation).OpenDb();

    private List<Product> Products()
    {
        using var db = Db();
        return db.Products.AsNoTracking().OrderBy(p => p.ProductId).ToList();
    }

    private List<SigningKeyRecord> Keys()
    {
        using var db = Db();
        return db.SigningKeys.AsNoTracking().ToList();
    }

    private List<FeatureDefinition> Features()
    {
        using var db = Db();
        return db.FeatureDefinitions.AsNoTracking().OrderBy(f => f.Name).ToList();
    }

    private static void AssertZeroed(ScriptedConsole console) =>
        Assert.All(console.HiddenGiven, h => Assert.All(h.Raw, c => Assert.Equal('\0', c)));

    // --- Adding a product

    [Fact]
    public void ProductAdded_PerProduct_RightAfterSetup_CreatesItsSigningKeyAndStandbyKey_WithoutAskingThePassphraseAgain()
    {
        var console = Quit(Add(SetUpThenProducts(shared: false), Id, Name));

        Assert.Equal(0, Run(console));

        var product = Assert.Single(Products());
        Assert.Equal(Id, product.ProductId);
        Assert.Equal(Name, product.DisplayName);
        Assert.Equal(DateTimeKind.Utc, product.CreatedUtc.Kind);
        var keys = Keys();
        Assert.Equal(2, keys.Count);
        var active = Assert.Single(keys, k => k.Role == KeyRole.Active);
        var standby = Assert.Single(keys, k => k.Role == KeyRole.Standby);
        Assert.Equal(Id, active.ProductId);
        Assert.Equal(Id, standby.ProductId);
        Assert.NotEqual(active.SigningKeyId, standby.SigningKeyId);
        Assert.All(keys, k => Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----\n", k.EncryptedPrivateKeyPem));
        Assert.All(keys, k => Assert.NotEmpty(k.PublicKey));
        using (KeyProtection.Decrypt(active.EncryptedPrivateKeyPem, System.Text.Encoding.ASCII.GetBytes(Words)))
        { }

        using var db = Db();
        Assert.Empty(db.KeyReleases.ToList()); // a product added later starts with no release marks
        Assert.Equal(2, console.HiddenGiven.Count); // the two setup entries only
        Assert.DoesNotContain($"Passphrase (", console.Out);
        Assert.Contains("Created its signing key and its standby key.", console.Out);
        Assert.Contains("Products", console.Out);
        Assert.Equal("", console.Err);
        AssertZeroed(console);
    }

    [Fact]
    public void ProductAdded_PerProduct_InANewSession_AsksThePassphrase_AfterTheLastQuestion()
    {
        var console = Quit(Add(OpenProducts(shared: false), Id, Name).Hidden(Words));

        Assert.Equal(0, Run(console));

        Assert.Equal(2, Keys().Count(k => k.ProductId == Id));
        var out_ = console.Out;
        Assert.True(out_.IndexOf("Display name (", StringComparison.Ordinal) < out_.IndexOf("Passphrase (", StringComparison.Ordinal));
        Assert.Single(console.HiddenGiven);
        AssertZeroed(console);
    }

    [Fact]
    public void ProductAdded_PerProduct_StandbyKeysOff_CreatesOnlyTheSigningKey()
    {
        var console = Quit(Add(SetUpThenProducts(shared: false, standby: false), Id, Name));

        Assert.Equal(0, Run(console));

        var key = Assert.Single(Keys());
        Assert.Equal(KeyRole.Active, key.Role);
        Assert.Equal(Id, key.ProductId);
        Assert.Contains("Created its signing key.", console.Out);
        Assert.DoesNotContain("standby key.", console.Out);
    }

    [Fact]
    public void ProductAdded_Shared_UsesTheSharedKey_CreatesNoKey_AndAsksNoPassphrase()
    {
        var console = Quit(Add(OpenProducts(shared: true), Id, Name));

        Assert.Equal(0, Run(console));

        Assert.Equal(Id, Assert.Single(Products()).ProductId);
        var keys = Keys();
        Assert.Equal(2, keys.Count); // the shared key and its standby, from setup
        Assert.All(keys, k => Assert.Null(k.ProductId));
        Assert.Empty(console.HiddenGiven);
        Assert.DoesNotContain("typed hidden", console.Out);
        Assert.Contains("It uses the shared signing key.", console.Out);
    }

    [Fact]
    public void ProductAdded_CancelAtThePassphrase_StoresNothing_AndReturnsToTheProductsMenu()
    {
        var console = Quit(Add(OpenProducts(shared: false), Id, Name).Hidden("/cancel"));

        Assert.Equal(0, Run(console));

        Assert.Empty(Products());
        Assert.Empty(Keys());
        Assert.Contains("Cancelled.", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Add a product")); // Products menu shown again
        AssertZeroed(console);
    }

    [Fact]
    public void ProductAdded_ThreeWrongPassphrases_StoreNothing_AndReturnToTheProductsMenu()
    {
        var wrong = "not the right words";
        var console = Quit(Add(OpenProducts(shared: false), Id, Name).Hidden(wrong).Hidden(wrong).Hidden(wrong));

        Assert.Equal(0, Run(console));

        Assert.Empty(Products());
        Assert.Empty(Keys());
        Assert.Equal(3, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        Assert.Contains("Cancelled.", console.Out);
        AssertZeroed(console);
    }

    [Fact]
    public void ProductAdded_AWrongPassphraseThenTheRightOne_StoresTheProduct()
    {
        var console = Quit(Add(OpenProducts(shared: false), Id, Name).Hidden("not the right words").Hidden(Words));

        Assert.Equal(0, Run(console));

        Assert.Single(Products());
        Assert.Equal(1, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateProductId_IsRefusedNamingIt_AsksAgain_AndChangesNothing(bool shared)
    {
        var console = SetUpThenProducts(shared);
        Add(console, Id, Name);
        console.Line("1").Line(Id).Line("/cancel"); // a second time
        var after = Quit(console);

        Assert.Equal(0, Run(after));

        Assert.Single(Products());
        Assert.Equal(2, Keys().Count); // shared: setup's two; per-product: the product's two, none added by the second try
        Assert.Contains($"The product ID {Id} is already in use.", console.Out);
        Assert.Equal(3, SessionTestHost.Count(console.Out, "Product ID ("));
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Display name ("));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidProductId_IsRefusedNamingTheRule_AndAskedAgainWithTheValidOneStored(bool shared)
    {
        var console = Quit(SetUpThenProducts(shared).Line("1").Line("Acme Commerce").Line(Id).Line(Name));

        Assert.Equal(0, Run(console));

        Assert.Equal(Id, Assert.Single(Products()).ProductId);
        Assert.Contains("A product ID must be two dot-separated parts", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Product ID ("));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisplayNameInAnyScript_IsStoredExactlyAsTyped(bool shared)
    {
        const string name = "コマース Müller – EU";
        var console = Quit(Add(SetUpThenProducts(shared), Id, name));

        Assert.Equal(0, Run(console));

        Assert.Equal(name, Assert.Single(Products()).DisplayName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisplayNameOutsideTheRule_IsRefusedNamingTheRule_AsksAgainKeepingTheProductId_AndNothingIsStoredOnCancel(bool shared)
    {
        var console = Quit(SetUpThenProducts(shared).Line("1").Line(Id).Line("Acme Commerce™").Line("/cancel"));

        Assert.Equal(0, Run(console));

        Assert.Empty(Products());
        Assert.Equal(shared ? 2 : 0, Keys().Count); // only setup's shared keys
        Assert.Contains("A display name contains a character that is not allowed: U+2122", console.Out);
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Product ID ("));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Display name ("));
        Assert.Contains("Cancelled.", console.Out);
    }

    [Fact]
    public void DisplayNameRefused_ThenAValidOne_StoresTheProduct()
    {
        var console = Quit(SetUpThenProducts(shared: false).Line("1").Line(Id).Line("Acme Commerce™").Line(Name));

        Assert.Equal(0, Run(console));

        Assert.Equal(Name, Assert.Single(Products()).DisplayName);
        Assert.Equal(2, Keys().Count);
    }

    [Fact]
    public void ProductAdded_WhenTheProductAlreadyHasKeys_FailsInsideTheTransaction_StoresNothing_AndTheSessionCarriesOn()
    {
        // One active key per product is enforced in code (a unique index cannot, SQLite NULLs being distinct).
        var folder = _host.SetUpInstallation(shared: false);
        using (var db = Installation.OpenExisting(folder).OpenDb())
        {
            // A key row without its product breaks the foreign key, so the check in code is what stands behind it; switch the key off for this seed only.
            db.Database.OpenConnection();
            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
            db.SigningKeys.Add(new SigningKeyRecord
            {
                SigningKeyId = "stray", ProductId = Id, PublicKey = "pub", EncryptedPrivateKeyPem = "pem", Role = KeyRole.Active,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.SaveChanges();
        }

        var console = Quit(Add(new ScriptedConsole().Line("1"), Id, Name).Hidden(Words).Line("4"));

        Assert.Equal(0, Run(console));

        Assert.Empty(Products());
        Assert.Single(Keys()); // only the stray one
        Assert.Contains("already has signing keys", console.Out);
        Assert.Contains("Products - Add a product failed:", console.Out);
        Assert.Contains("There are no products yet.", console.Out); // the session went on and List works
    }

    [Fact]
    public void ProductAdded_WithDamagedKeySettings_FailsAsAnActionFailure_AndTheSessionCarriesOn()
    {
        var folder = _host.SetUpInstallation(shared: true);
        using (var db = Installation.OpenExisting(folder).OpenDb())
        {
            db.Settings.Remove(db.Settings.Single(s => s.Name == SettingNames.StandbyKeys));
            db.SaveChanges();
        }

        var console = Quit(Add(new ScriptedConsole().Line("1"), Id, Name).Line("4"));

        Assert.Equal(0, Run(console));

        Assert.Empty(Products());
        Assert.Contains("Products - Add a product failed:", console.Out);
        Assert.Contains("There are no products yet.", console.Out);
    }

    // --- Feature definitions

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FeatureAdded_IsStoredWithItsType_AndShownInTheList(bool shared)
    {
        var console = SetUpThenProducts(shared);
        Add(console, Id, Name);
        AddFeature(console, "1", "max-orders", "number");
        AddFeature(console, Id, "beta", "switch");
        AddFeature(console, "1", "region", "3");
        console.Line("4");
        Quit(console);

        Assert.Equal(0, Run(console));

        var features = Features();
        Assert.Equal(["beta", "max-orders", "region"], features.Select(f => f.Name));
        Assert.Equal([FeatureType.Switch, FeatureType.Number, FeatureType.Text], features.Select(f => f.Type));
        Assert.All(features, f => Assert.Equal(Id, f.ProductId));
        Assert.All(features, f => Assert.Equal(DateTimeKind.Utc, f.AddedUtc.Kind));
        Assert.Contains("Added number feature max-orders to acme.commerce.", console.Out);
        Assert.Contains("  Features: beta (switch), max-orders (number), region (text)", console.Out);
        Assert.Equal("", console.Err);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FeatureAlreadyDefined_IsRefusedNamingTheFeatureAndItsType_AsksAgain_AndChangesNothing(bool shared)
    {
        var console = SetUpThenProducts(shared);
        Add(console, Id, Name);
        AddFeature(console, "1", "max-orders", "number");
        console.Line("3").Line("1").Line("max-orders").Line("min-orders").Line("text"); // retyping is refused; another name is accepted
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Contains("The feature max-orders is already defined as number.", console.Out);
        Assert.Equal(2, Features().Count);
        Assert.Equal(FeatureType.Number, Features().Single(f => f.Name == "max-orders").Type);
        Assert.Equal(FeatureType.Text, Features().Single(f => f.Name == "min-orders").Type);
        Assert.Equal(3, SessionTestHost.Count(console.Out, "Feature name ("));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FeatureLimitReached_IsRefusedAtTheProduct_NamingTheLimit_StoresNothing_AndShowsTheProductsMenu(bool shared)
    {
        var console = OpenProducts(shared);
        SeedProductWithFeatures(ProductActions.MaxFeatures);
        console.Line("3").Line("1");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Contains("already defines 50 features", console.Out);
        Assert.Equal(0, SessionTestHost.Count(console.Out, "Feature name ("));
        Assert.Equal(ProductActions.MaxFeatures, Features().Count);
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "Products"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FeatureLimitNotReached_AProductWith49Definitions_AcceptsA50th(bool shared)
    {
        var console = OpenProducts(shared);
        SeedProductWithFeatures(ProductActions.MaxFeatures - 1);
        AddFeature(console, "1", "the-last", "switch");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Equal(ProductActions.MaxFeatures, Features().Count);
        Assert.Contains("Added switch feature the-last to acme.commerce.", console.Out);
        Assert.DoesNotContain("already defines", console.Out);
    }

    private void SeedProductWithFeatures(int count)
    {
        var now = DateTime.UtcNow;
        using var db = Db();
        db.Products.Add(new Product { ProductId = Id, DisplayName = Name, CreatedUtc = now });
        for (var i = 0; i < count; i++)
            db.FeatureDefinitions.Add(new FeatureDefinition { ProductId = Id, Name = $"feature-{i:00}", Type = FeatureType.Switch, AddedUtc = now });
        db.SaveChanges();
    }

    [Fact]
    public void FeatureAlreadyDefined_AsTheSameType_IsRefusedToo_AndCancelStoresNothingMore()
    {
        var console = SetUpThenProducts(shared: true);
        Add(console, Id, Name);
        AddFeature(console, "1", "max-orders", "number");
        console.Line("3").Line("1").Line("max-orders").Line("/cancel");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Contains("The feature max-orders is already defined as number.", console.Out);
        Assert.Single(Features());
    }

    [Fact]
    public void InvalidFeatureName_IsRefusedNamingTheRule_AndAskedAgain()
    {
        var console = SetUpThenProducts(shared: true);
        Add(console, Id, Name);
        console.Line("3").Line("1").Line("Max Orders").Line("max-orders").Line("1");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Contains("The feature name \"Max Orders\" is not valid", console.Out);
        Assert.Equal("max-orders", Assert.Single(Features()).Name);
    }

    [Fact]
    public void AddFeature_CancelAtTheTypeQuestion_StoresNothing()
    {
        var console = SetUpThenProducts(shared: true);
        Add(console, Id, Name);
        console.Line("3").Line("1").Line("max-orders").Line("/cancel");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Empty(Features());
        Assert.Contains("Cancelled.", console.Out);
    }

    [Fact]
    public void TheProductsMenu_HasNoEntryToRemoveRenameOrRetypeAFeature()
    {
        var console = Quit(SetUpThenProducts(shared: true));

        Assert.Equal(0, Run(console));

        var transcript = console.Out.Replace("\r\n", "\n");
        var menu = transcript[transcript.LastIndexOf("\nProducts\n", StringComparison.Ordinal)..];
        Assert.DoesNotContain("emove", menu);
        Assert.DoesNotContain("etype", menu);
        Assert.Contains("  3 Add a feature", menu);
    }

    // --- Display name edits

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisplayNameEdited_ShowsTheCurrentName_StoresTheNewOne_AndKeepsIdAndFeatures(bool shared)
    {
        var console = SetUpThenProducts(shared);
        Add(console, Id, Name);
        AddFeature(console, "1", "max-orders", "number");
        console.Line("2").Line("1").Line("Acme Commerce Pro");
        console.Line("4");
        Quit(console);

        Assert.Equal(0, Run(console));

        var product = Assert.Single(Products());
        Assert.Equal(Id, product.ProductId);
        Assert.Equal("Acme Commerce Pro", product.DisplayName);
        Assert.Equal("max-orders", Assert.Single(Features()).Name);
        Assert.Contains("Display name: Acme Commerce", console.Out);
        Assert.Contains("Enter for Acme Commerce", console.Out);
        Assert.Contains("acme.commerce is now Acme Commerce Pro.", console.Out);
        Assert.Contains("acme.commerce - Acme Commerce Pro", console.Out);
        Assert.DoesNotContain("Passphrase (", console.Out[console.Out.IndexOf("Display name: ", StringComparison.Ordinal)..]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisplayNameChangeRefused_NamesTheRule_AsksAgain_AndTheProductKeepsItsName(bool shared)
    {
        var console = SetUpThenProducts(shared);
        Add(console, Id, Name);
        console.Line("2").Line(Id).Line("Acme ⭐ Pro").Line("/cancel");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Equal(Name, Assert.Single(Products()).DisplayName);
        Assert.Contains("A display name contains a character that is not allowed: U+2B50", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "New display name ("));
        Assert.Contains("Cancelled.", console.Out);
    }

    [Fact]
    public void DisplayNameUnchanged_WhenEnterKeepsIt_WritesNothingAndSaysSo()
    {
        var console = SetUpThenProducts(shared: true);
        Add(console, Id, Name);
        console.Line("2").Line("1").Line("");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Equal(Name, Assert.Single(Products()).DisplayName);
        Assert.Contains("Unchanged.", console.Out);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    public void RenameAndAddFeature_WithNoProducts_SayThereAreNone(string entry)
    {
        var console = Quit(SetUpThenProducts(shared: true).Line(entry));

        Assert.Equal(0, Run(console));

        Assert.Contains("There are no products yet.", console.Out);
        Assert.Equal("", console.Err);
    }

    // --- Listing

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductsListed_ShowIdDisplayNameFeaturesAndLicenseCounts_InOrder(bool shared)
    {
        var console = SetUpThenProducts(shared);
        Add(console, "acme.shipping", "Acme Shipping");
        Add(console, Id, Name);
        AddFeature(console, Id, "max-orders", "number");
        AddFeature(console, Id, "beta", "switch");
        // Licenses come in section 8; rows are inserted here for the count.
        console.Line("0").Line("0"); // leave the session, add licenses, then list in a second session
        Assert.Equal(0, Run(console));

        using (var db = Db())
        {
            var at = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            db.Licenses.AddRange(
                new LicenseRecord { Reference = "8F3AKM7RXB", ProductId = Id, CreatedUtc = at },
                new LicenseRecord { Reference = "9G4BLN8SYC", ProductId = Id, CreatedUtc = at });
            db.SaveChanges();
        }

        var list = Quit(new ScriptedConsole().Line("1").Line("4"));
        Assert.Equal(0, Run(list));

        var start = list.Out.IndexOf("acme.commerce - Acme Commerce", StringComparison.Ordinal); // the answer is not echoed, so the first line shares the prompt
        Assert.True(start >= 0, list.Out);
        var lines = SessionTestHost.Lines(list.Out[start..]);
        Assert.Equal("  Features: beta (switch), max-orders (number)", lines[1]);
        Assert.Equal("  Licenses: 2", lines[2]);
        Assert.Equal("acme.shipping - Acme Shipping", lines[3]);
        Assert.Equal("  Features: none", lines[4]);
        Assert.Equal("  Licenses: 0", lines[5]);
    }

    [Fact]
    public void ProductsListed_WhenThereAreNone_SaysSo()
    {
        var console = Quit(SetUpThenProducts(shared: false).Line("4"));

        Assert.Equal(0, Run(console));

        Assert.Contains("There are no products yet.", console.Out);
    }

    [Fact]
    public void StoredValues_HoldNoLocalPath()
    {
        var console = Quit(Add(SetUpThenProducts(shared: false), Id, Name));
        Assert.Equal(0, Run(console));

        var text = string.Join('\n', Products().Select(p => p.DisplayName + p.ProductId))
            + string.Join('\n', Keys().Select(k => k.PublicKey + k.SigningKeyId));
        Assert.DoesNotContain(_host.Home, text);
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), text);
    }
}
