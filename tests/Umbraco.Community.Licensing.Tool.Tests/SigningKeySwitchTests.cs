using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>Signing keys &gt; Switch (vendor-tool-signing-keys), as whole sessions through <c>ToolApp.Run</c>, in both signing-key modes.</summary>
public class SigningKeySwitchTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Commerce = "acme.commerce";
    private const string Shipping = "acme.shipping";
    private const string Tax = "acme.tax";
    private const string Warning = "is not yet in a release for";
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private ToolDbContext Db() => Installation.OpenExisting(_host.HomeInstallation).OpenDb();

    private List<SigningKeyRecord> Keys()
    {
        using var db = Db();
        return db.SigningKeys.AsNoTracking().ToList();
    }

    private int Run(ScriptedConsole console)
    {
        var exit = _host.Run(console, null, Startup.Offer.Default);
        Assert.True(exit == 0, console.Err + console.Out);
        Assert.Equal("", console.Err);
        return exit;
    }

    /// <summary>
    /// Setup, then a session that adds the products, so the session under test holds no passphrase. Per-product products get
    /// their keys from the first session's passphrase (asked once). Returns nothing; the keys are read from the database.
    /// </summary>
    private void Seed(bool shared, bool standby, params string[] products)
    {
        _host.SetUpInstallation(shared: shared, standby: standby);
        AddProducts(shared, products);
    }

    private void AddProducts(bool shared, params string[] products)
    {
        var console = new ScriptedConsole().Line("1");
        for (var i = 0; i < products.Length; i++)
        {
            console.Line("1").Line(products[i]).Line(products[i]);
            if (i == 0 && !shared)
                console.Hidden(Words);
        }

        Run(console.Line("0").Line("0"));
    }

    // Release confirmed (Signing keys > Release confirmed) is a later block; the row it would write is inserted directly.
    private void Release(string signingKeyId, params string[] products)
    {
        using var db = Db();
        foreach (var product in products)
            db.KeyReleases.Add(new KeyRelease { SigningKeyId = signingKeyId, ProductId = product, ConfirmedUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    private SigningKeyRecord Standby(string? product) => Keys().Single(k => k.Role == KeyRole.Standby && k.ProductId == product);

    /// <summary>Main menu 3 (Signing keys), 3 (Switch), the key (the product, or the single shared entry) and the reason.</summary>
    private static ScriptedConsole Switch(ScriptedConsole c, string? product, string reason) =>
        c.Line("3").Line("3").Line(product ?? "1").Line(reason);

    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    private static string WarningLine(ScriptedConsole console) =>
        SessionTestHost.Lines(console.Out).Single(l => l.Contains(Warning, StringComparison.Ordinal));

    private static readonly DateTime Now = new(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc);

    // --- Routine rotation with standby

    [Fact]
    public void RoutineRotationWithStandby_PromotesTheStandby_MakesANewStandby_AndAsksNoConfirmationWhenReleased()
    {
        Seed(shared: false, standby: true, Commerce);
        var before = Keys();
        var oldActive = before.Single(k => k.Role == KeyRole.Active);
        var oldStandby = before.Single(k => k.Role == KeyRole.Standby);
        Release(oldStandby.SigningKeyId, Commerce);
        var console = Quit(Switch(new ScriptedConsole(), Commerce, "routine").Hidden(Words));

        Run(console);

        var keys = Keys();
        Assert.Equal(3, keys.Count);
        var replaced = Assert.Single(keys, k => k.Role == KeyRole.Replaced);
        Assert.Equal(oldActive.SigningKeyId, replaced.SigningKeyId);
        Assert.Equal(ReplacedReason.Routine, replaced.ReplacedReason);
        Assert.Equal(Now, replaced.ReplacedUtc);
        Assert.Equal(oldStandby.SigningKeyId, Assert.Single(keys, k => k.Role == KeyRole.Active).SigningKeyId);
        var newStandby = Assert.Single(keys, k => k.Role == KeyRole.Standby);
        Assert.DoesNotContain(newStandby.SigningKeyId, new[] { oldActive.SigningKeyId, oldStandby.SigningKeyId });
        Assert.Equal(Commerce, newStandby.ProductId);
        using (KeyProtection.Decrypt(newStandby.EncryptedPrivateKeyPem, System.Text.Encoding.ASCII.GetBytes(Words)))
        { }
        using (var db = Db())
            Assert.Empty(db.KeyReleases.Where(r => r.SigningKeyId == newStandby.SigningKeyId).ToList());

        Assert.DoesNotContain("Switch anyway?", console.Out); // the standby is in a release for its product
        Assert.DoesNotContain(Warning, console.Out);
        Assert.Single(console.HiddenGiven); // asked once, after the questions
        Assert.True(console.Out.LastIndexOf("Reason (", StringComparison.Ordinal) < console.Out.IndexOf("Passphrase (", StringComparison.Ordinal));
    }

    [Fact]
    public void NewIssues_AreSignedByTheNewKey_AndVerifyAgainstItsPublicKey()
    {
        Seed(shared: false, standby: true, Commerce);
        var standby = Standby(Commerce);
        Release(standby.SigningKeyId, Commerce);
        using (var db = Db())
        {
            db.FeatureDefinitions.Add(new FeatureDefinition { ProductId = Commerce, Name = "ecommerce", Type = FeatureType.Switch, AddedUtc = Now });
            db.SaveChanges();
        }

        // Switch, back to the main menu, then Licenses > Issue in the same session (the passphrase is held), then quit.
        var console = Switch(new ScriptedConsole(), Commerce, "routine").Hidden(Words).Line("0")
            .Line("2").Line("1").Line("1").Line("2027-03-01T23:59:59Z").Line("").Line("").Line("on").Line("ORD-1").Line("owner@example.com")
            .Line("").Line("");
        Run(Quit(console));

        using var verify = Db();
        var entry = Assert.Single(verify.IssueEntries.AsNoTracking().ToList());
        Assert.Equal(standby.SigningKeyId, entry.SigningKeyId);
        var lines = SessionTestHost.Lines(console.Out);
        var key = lines[Array.FindIndex(lines, l => l.Contains("The key string is shown once", StringComparison.Ordinal)) + 1];
        var trusted = TrustedSigningKeys.Create([SigningPublicKey.Parse(standby.PublicKey)]);
        var result = new LicenseEvaluator(Commerce, trusted, new FakeTimeProvider()).Evaluate(key);
        Assert.True(result.IsLicensed, result.State.ToString());
        Assert.Single(console.HiddenGiven); // the switch asked; the issue used the held passphrase
    }

    // --- Standby not yet in a release

    [Fact]
    public void StandbyNotYetInARelease_WarnsNamingTheProduct_AndSwitchesWhenConfirmed()
    {
        Seed(shared: false, standby: true, Commerce);
        var standby = Standby(Commerce);
        var console = Quit(Switch(new ScriptedConsole(), Commerce, "routine").Line("y").Hidden(Words));

        Run(console);

        Assert.Contains($"The standby key {standby.SigningKeyId} {Warning} {Commerce}.", console.Out);
        Assert.Contains("signed by a key not recognised", console.Out);
        Assert.Equal(standby.SigningKeyId, Assert.Single(Keys(), k => k.Role == KeyRole.Active).SigningKeyId);
        Assert.Equal(3, Keys().Count);
        Assert.True(console.Out.IndexOf("Switch anyway?", StringComparison.Ordinal) < console.Out.IndexOf("Passphrase (", StringComparison.Ordinal));
    }

    // --- Switch with no standby

    [Theory]
    [InlineData("routine")]
    [InlineData("loss")]
    [InlineData("leak")]
    public void SwitchWithNoStandby_WarnsAndConfirms_ThenANewKeyPairBecomesActive_AndNoStandbyIsMade(string reason)
    {
        Seed(shared: false, standby: false, Commerce);
        var oldActive = Assert.Single(Keys());
        var console = Quit(Switch(new ScriptedConsole(), Commerce, reason).Line("y").Hidden(Words));

        Run(console);

        Assert.Contains($"A new signing key {Warning} {Commerce}.", console.Out);
        var keys = Keys();
        Assert.Equal(2, keys.Count);
        var replaced = Assert.Single(keys, k => k.Role == KeyRole.Replaced);
        Assert.Equal(oldActive.SigningKeyId, replaced.SigningKeyId);
        Assert.Equal(Enum.Parse<ReplacedReason>(reason, ignoreCase: true), replaced.ReplacedReason);
        Assert.Equal(Now, replaced.ReplacedUtc);
        var active = Assert.Single(keys, k => k.Role == KeyRole.Active);
        Assert.NotEqual(oldActive.SigningKeyId, active.SigningKeyId);
        Assert.Equal(Commerce, active.ProductId);
        Assert.DoesNotContain(keys, k => k.Role == KeyRole.Standby);
        using var db = Db();
        Assert.Empty(db.KeyReleases.ToList());
    }

    [Fact]
    public void SwitchWithNoStandby_WorksWhileTheStandbySettingIsOn_AndLeavesTheSettingAlone()
    {
        // Setup asked for standby keys, but this product has none (an earlier standby was replaced): the setting does not matter.
        Seed(shared: false, standby: true, Commerce);
        using (var db = Db())
        {
            db.SigningKeys.Remove(db.SigningKeys.Single(k => k.Role == KeyRole.Standby));
            db.SaveChanges();
        }

        Run(Quit(Switch(new ScriptedConsole(), Commerce, "loss").Line("y").Hidden(Words)));

        Assert.DoesNotContain(Keys(), k => k.Role == KeyRole.Standby);
        using var settings = Db();
        Assert.Equal("true", settings.Settings.Single(s => s.Name == SettingNames.StandbyKeys).Value);
    }

    // --- Confirmation declined

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfirmationDeclined_ChangesNothing_CreatesNoKey_AndShowsTheSigningKeysMenu(bool withStandby)
    {
        Seed(shared: false, standby: withStandby, Commerce);
        var before = Keys().Select(k => (k.SigningKeyId, k.Role, k.ReplacedUtc, k.ReplacedReason)).OrderBy(k => k.SigningKeyId).ToList();
        var console = Quit(Switch(new ScriptedConsole(), Commerce, "leak").Line("n"));

        Assert.Equal(0, Run(console));

        Assert.Equal(before, Keys().Select(k => (k.SigningKeyId, k.Role, k.ReplacedUtc, k.ReplacedReason)).OrderBy(k => k.SigningKeyId).ToList());
        Assert.Empty(console.HiddenGiven); // no passphrase asked: the confirmation comes first
        var afterDecline = console.Out[console.Out.IndexOf(SigningKeyActions.SwitchDeclined, StringComparison.Ordinal)..];
        Assert.Contains("Signing keys\n  1 Status", afterDecline.Replace("\r\n", "\n"));
    }

    // --- Shared mode

    [Fact]
    public void SharedStandbyNotReleasedForEveryProduct_NamesTheProductsLackingIt()
    {
        Seed(shared: true, standby: true, Commerce, Shipping);
        var standby = Standby(null);
        Release(standby.SigningKeyId, Commerce);
        var console = Quit(Switch(new ScriptedConsole(), null, "routine").Line("y").Hidden(Words));

        Run(console);

        var line = WarningLine(console);
        Assert.Contains(Shipping, line);
        Assert.DoesNotContain(Commerce, line);
        // The one shared key moved for every product.
        var keys = Keys();
        Assert.Equal(standby.SigningKeyId, Assert.Single(keys, k => k.Role == KeyRole.Active).SigningKeyId);
        Assert.All(keys, k => Assert.Null(k.ProductId));
        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public void SharedStandbyReleasedForEveryProduct_AsksNoConfirmation_AndTheNewStandbyHasNoMarks()
    {
        Seed(shared: true, standby: true, Commerce, Shipping);
        var standby = Standby(null);
        Release(standby.SigningKeyId, Commerce, Shipping);
        var console = Quit(Switch(new ScriptedConsole(), null, "routine").Hidden(Words));

        Run(console);

        Assert.DoesNotContain("Switch anyway?", console.Out);
        Assert.Equal(standby.SigningKeyId, Assert.Single(Keys(), k => k.Role == KeyRole.Active).SigningKeyId);
        var newStandby = Standby(null);
        Assert.NotEqual(standby.SigningKeyId, newStandby.SigningKeyId);
        using var db = Db();
        Assert.Empty(db.KeyReleases.Where(r => r.SigningKeyId == newStandby.SigningKeyId).ToList());
        Assert.Equal(2, db.KeyReleases.Count()); // the standby's own marks stay
    }

    [Fact]
    public void ProductAddedAfterTheStandbyWasReleased_WarnsNamingOnlyTheNewProduct()
    {
        Seed(shared: true, standby: true, Commerce, Shipping);
        var standby = Standby(null);
        Release(standby.SigningKeyId, Commerce, Shipping);
        AddProducts(shared: true, Tax);
        var console = Quit(Switch(new ScriptedConsole(), null, "routine").Line("y").Hidden(Words));

        Run(console);

        var line = WarningLine(console);
        Assert.Contains(Tax, line);
        Assert.DoesNotContain(Commerce, line);
        Assert.DoesNotContain(Shipping, line);
        Assert.Equal(standby.SigningKeyId, Assert.Single(Keys(), k => k.Role == KeyRole.Active).SigningKeyId);
    }

    [Fact]
    public void SharedThreeProducts_NamesEveryProductLackingTheMark_InOrder()
    {
        Seed(shared: true, standby: true, Commerce, Shipping, Tax);
        Release(Standby(null).SigningKeyId, Commerce);
        var console = Quit(Switch(new ScriptedConsole(), null, "routine").Line("n"));

        Run(console);

        Assert.EndsWith($"{Warning} {Shipping}, {Tax}.", WarningLine(console));
    }

    [Fact]
    public void SharedSwitchWithNoStandby_NamesEveryProduct_AndMakesNoStandby()
    {
        Seed(shared: true, standby: false, Commerce, Shipping);
        var console = Quit(Switch(new ScriptedConsole(), null, "loss").Line("y").Hidden(Words));

        Run(console);

        Assert.EndsWith($"{Warning} {Commerce}, {Shipping}.", WarningLine(console));
        var keys = Keys();
        Assert.Equal(2, keys.Count);
        Assert.Single(keys, k => k.Role == KeyRole.Active);
        Assert.Equal(ReplacedReason.Loss, Assert.Single(keys, k => k.Role == KeyRole.Replaced).ReplacedReason);
    }

    // --- Passphrase

    [Fact]
    public void WrongPassphraseThreeTimes_CancelsTheSwitch_AndWritesNothing()
    {
        Seed(shared: false, standby: true, Commerce);
        var before = Keys().Select(k => (k.SigningKeyId, k.Role)).OrderBy(k => k.SigningKeyId).ToList();
        var console = Quit(Switch(new ScriptedConsole(), Commerce, "routine").Line("y")
            .Hidden("wrong one wrong one").Hidden("wrong two wrong two").Hidden("wrong three wrong three"));

        Run(console);

        Assert.Equal(3, console.HiddenGiven.Count);
        Assert.Contains("Cancelled.", console.Out);
        Assert.Equal(before, Keys().Select(k => (k.SigningKeyId, k.Role)).OrderBy(k => k.SigningKeyId).ToList());
        Assert.Contains(Keys(), k => k.Role == KeyRole.Standby);
    }

    [Fact]
    public void ASessionThatHoldsThePassphrase_IsNotAskedAgain()
    {
        _host.SetUpInstallation(shared: true, standby: true);
        // Add a product (shared mode asks no passphrase), then switch twice: the first asks, the second uses the held one.
        var console = new ScriptedConsole().Line("1").Line("1").Line(Commerce).Line("Acme Commerce").Line("0");
        Switch(console, null, "routine").Line("y").Hidden(Words).Line("0");
        Run(Switch(console, null, "routine").Line("y").Line("0").Line("0"));

        Assert.Single(console.HiddenGiven);
        Assert.Equal(2, Keys().Count(k => k.Role == KeyRole.Replaced));
    }

    // --- The re-check inside the transaction

    private (SigningKeyRecord Active, SigningKeyRecord Standby, KeyProtection.NewKey Made) SeedForCommit()
    {
        Seed(shared: false, standby: true, Commerce);
        var keys = Keys();
        var made = KeyProtection.CreateEncrypted(System.Text.Encoding.ASCII.GetBytes(Words), SessionTestHost.Iterations);
        return (keys.Single(k => k.Role == KeyRole.Active), keys.Single(k => k.Role == KeyRole.Standby), made);
    }

    [Fact]
    public void CommitSwitch_WhenTheActiveKeyChangedMeanwhile_RefusesAndWritesNothing()
    {
        var (active, standby, made) = SeedForCommit();
        using var db = Db();
        db.SigningKeys.Single(k => k.SigningKeyId == active.SigningKeyId).Role = KeyRole.Replaced;
        db.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SigningKeyActions.CommitSwitch(db, active, standby, made, ReplacedReason.Routine, Now, "The signing key of acme.commerce"));

        Assert.Contains("no longer the active key", ex.Message);
        Assert.Equal(2, Keys().Count);
    }

    [Fact]
    public void CommitSwitch_WhenTheStandbyChangedMeanwhile_RefusesAndWritesNothing()
    {
        var (active, standby, made) = SeedForCommit();
        using var db = Db();
        db.SigningKeys.Remove(db.SigningKeys.Single(k => k.SigningKeyId == standby.SigningKeyId));
        db.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SigningKeyActions.CommitSwitch(db, active, standby, made, ReplacedReason.Routine, Now, "The signing key of acme.commerce"));

        Assert.Contains("different standby key", ex.Message);
        Assert.Equal(KeyRole.Active, Assert.Single(Keys()).Role);
    }

    [Fact]
    public void CommitSwitch_WhenAStandbyAppearedForANoStandbySwitch_RefusesAndWritesNothing()
    {
        var (active, standby, made) = SeedForCommit();
        using var db = Db();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SigningKeyActions.CommitSwitch(db, active, standby: null, made, ReplacedReason.Loss, Now, "The signing key of acme.commerce"));

        Assert.Contains("different standby key", ex.Message);
        Assert.Equal(2, Keys().Count);
        Assert.Equal(KeyRole.Active, Keys().Single(k => k.SigningKeyId == active.SigningKeyId).Role);
    }

    [Fact]
    public void CommitSwitch_WhenASecondActiveKeyIsInScope_FailsTheLateCheck_AndTheTransactionRollsEverythingBack()
    {
        var (active, standby, made) = SeedForCommit();
        using (var seed = Db())
        {
            seed.SigningKeys.Add(new SigningKeyRecord
            {
                SigningKeyId = "second-active", ProductId = Commerce, PublicKey = "p", EncryptedPrivateKeyPem = "e", Role = KeyRole.Active, CreatedUtc = Now,
            });
            seed.SaveChanges();
        }

        var before = Keys().Select(k => (k.SigningKeyId, k.Role, k.ReplacedReason)).OrderBy(k => k.SigningKeyId).ToList();
        using (var db = Db())
        using (var transaction = db.Database.BeginTransaction())
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                SigningKeyActions.CommitSwitch(db, active, standby, made, ReplacedReason.Routine, Now, "The signing key of acme.commerce"));
            Assert.Contains("exactly one active key", ex.Message);
        } // no Commit: the writes made before the late check are rolled back, as in Switch

        Assert.Equal(before, Keys().Select(k => (k.SigningKeyId, k.Role, k.ReplacedReason)).OrderBy(k => k.SigningKeyId).ToList());
    }

    // --- Shared mode with no products (row 131): nothing to name, so no warning and no confirmation; the switch goes ahead.

    [Fact]
    public void SharedModeWithNoProductsAndNoStandby_SwitchesWithNoConfirmation()
    {
        _host.SetUpInstallation(shared: true, standby: false);
        var oldActive = Assert.Single(Keys());
        var console = Quit(Switch(new ScriptedConsole(), null, "routine").Hidden(Words));

        Run(console);

        Assert.DoesNotContain("Switch anyway?", console.Out);
        Assert.DoesNotContain(Warning, console.Out);
        var keys = Keys();
        Assert.Equal(2, keys.Count);
        Assert.Equal(oldActive.SigningKeyId, Assert.Single(keys, k => k.Role == KeyRole.Replaced).SigningKeyId);
        Assert.NotEqual(oldActive.SigningKeyId, Assert.Single(keys, k => k.Role == KeyRole.Active).SigningKeyId);
    }
}
