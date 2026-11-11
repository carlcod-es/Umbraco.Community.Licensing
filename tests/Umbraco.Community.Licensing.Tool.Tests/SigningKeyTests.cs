using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Signing keys menu, status and adding a standby key (vendor-tool-signing-keys), as whole sessions through <c>ToolApp.Run</c>.</summary>
public class SigningKeyTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Commerce = "acme.commerce";
    private const string Shipping = "acme.shipping";
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private void Run(ScriptedConsole console)
    {
        var exit = _host.Run(console, null, Startup.Offer.Default);
        Assert.True(exit == 0, console.Err + console.Out);
        Assert.Equal("", console.Err);
    }

    // Main menu: 1 Products (1 Add), 3 Signing keys (1 Status, 2 Add a standby key); 0 leaves each menu.
    private static ScriptedConsole AddProduct(ScriptedConsole c, string id, string name) => c.Line("1").Line(id).Line(name);

    private static ScriptedConsole Status(ScriptedConsole c, string onlyWithout = "") => c.Line("1").Line(onlyWithout);

    private ToolDbContext Db() => Installation.OpenExisting(_host.HomeInstallation).OpenDb();

    private List<SigningKeyRecord> Keys()
    {
        using var db = Db();
        return db.SigningKeys.AsNoTracking().ToList();
    }

    /// <summary>Setup without standby keys, then Products with both products added: each has only its signing key. Ends in the Signing keys menu.</summary>
    private static ScriptedConsole PerProductWithoutStandby() =>
        AddProduct(AddProduct(new ScriptedConsole().SetUp(shared: false, standby: false).Line("1"), Commerce, "Acme Commerce"), Shipping, "Acme Shipping")
            .Line("0").Line("3");

    [Fact]
    public void StandbyAddedLater_PerProduct_CreatesTheStandbyNotYetInARelease_AndTheProductLeavesTheWithoutList()
    {
        // Choosing the product by ID; the session holds the passphrase from setup, so none is asked. Then status (all), then status (without a standby).
        var console = PerProductWithoutStandby().Line("2").Line(Commerce).Line("1").Line("").Line("1").Line("y").Line("0").Line("0");

        Run(console);

        var keys = Keys();
        var active = Assert.Single(keys, k => k.Role == KeyRole.Active && k.ProductId == Commerce);
        var standby = Assert.Single(keys, k => k.Role == KeyRole.Standby);
        Assert.Equal(Commerce, standby.ProductId);
        Assert.NotEqual(active.SigningKeyId, standby.SigningKeyId);
        Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----\n", standby.EncryptedPrivateKeyPem);
        using (KeyProtection.Decrypt(standby.EncryptedPrivateKeyPem, System.Text.Encoding.ASCII.GetBytes(Words)))
        { }
        using (var db = Db())
            Assert.Empty(db.KeyReleases.ToList());
        Assert.Equal(KeyRole.Active, Assert.Single(keys, k => k.ProductId == Shipping).Role);

        Assert.Contains($"Added standby key {standby.SigningKeyId} to the signing key of {Commerce}.", console.Out);
        Assert.Contains($"It is not yet in a release for {Commerce}.", console.Out);
        Assert.Contains($"  Standby key: {standby.SigningKeyId} - not yet in a release for {Commerce}", console.Out);
        // The without-standby list (the last status): shipping only.
        var last = console.Out[console.Out.LastIndexOf("Only those without a standby key?", StringComparison.Ordinal)..];
        Assert.Contains(Shipping, last);
        Assert.DoesNotContain(Commerce, last);
        Assert.Equal(2, console.HiddenGiven.Count); // setup only
    }

    [Fact]
    public void StandbyAddedLater_WorksWhileTheStandbySettingIsOff_AndLeavesTheSettingAlone()
    {
        _host.SetUpInstallation(shared: false, standby: false);
        // Standby setting is off: adding later still works, and the setting does not change.
        var console = AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce").Hidden(Words)
            .Line("0").Line("3").Line("2").Line("1").Line("0").Line("0");

        Run(console);

        Assert.Equal(KeyRole.Standby, Assert.Single(Keys(), k => k.Role == KeyRole.Standby).Role);
        using var db = Db();
        Assert.Equal("false", db.Settings.Single(s => s.Name == SettingNames.StandbyKeys).Value);
        Assert.Single(console.HiddenGiven); // asked once: the session held none
    }

    [Fact]
    public void ProductsWithoutAStandbyKey_ShowsOnlyTheProductWithout()
    {
        var console = PerProductWithoutStandby().Line("2").Line(Commerce).Line("1").Line("y").Line("0").Line("0");

        Run(console);

        var output = console.Out;
        var last = output[output.LastIndexOf("Only those without a standby key?", StringComparison.Ordinal)..];
        Assert.Contains($"{Shipping} - Acme Shipping", last);
        Assert.Contains("  Standby key: none", last);
        Assert.DoesNotContain($"{Commerce} - ", last);
    }

    [Fact]
    public void ProductsWithoutAStandbyKey_WhenEveryProductHasOne_SaysSo()
    {
        _host.SetUpInstallation(shared: false, standby: true);
        var console = AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce").Hidden(Words)
            .Line("0").Line("3").Line("1").Line("y").Line("0").Line("0");

        Run(console);

        Assert.Contains(SigningKeyActions.AllHaveStandby, console.Out);
    }

    [Fact]
    public void Status_ListsEveryProductWithItsKeys_AndAsksNoPassphrase()
    {
        _host.SetUpInstallation(shared: false, standby: true);
        var console = AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce").Hidden(Words)
            .Line("0").Line("3").Line("1").Line("").Line("0").Line("0");

        Run(console);

        var keys = Keys();
        Assert.Contains($"{Commerce} - Acme Commerce", console.Out);
        Assert.Contains($"  Signing key: {keys.Single(k => k.Role == KeyRole.Active).SigningKeyId}", console.Out);
        Assert.Contains($"  Standby key: {keys.Single(k => k.Role == KeyRole.Standby).SigningKeyId} - not yet in a release for {Commerce}", console.Out);
        Assert.Single(console.HiddenGiven); // Add a product's only
    }

    [Fact]
    public void Status_InANewSession_AsksNoPassphrase()
    {
        _host.SetUpInstallation(shared: true);
        var console = new ScriptedConsole().Line("3").Line("1").Line("").Line("0").Line("0");

        Run(console);

        Assert.Empty(console.HiddenGiven);
        Assert.DoesNotContain("Passphrase (", console.Out);
    }

    [Fact]
    public void SharedStandbyReleasedForOneProduct_StatusShowsItNotYetInARelease_ForTheOther()
    {
        _host.SetUpInstallation(shared: true, standby: true);
        var products = AddProduct(AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce"), Shipping, "Acme Shipping").Line("0").Line("0").Line("0");
        Run(products);

        // Release confirmed (Signing keys > Release confirmed) is a later block; the row it would write is added directly.
        // The end-to-end variant belongs with that block.
        var standby = Keys().Single(k => k.Role == KeyRole.Standby);
        using (var db = Db())
        {
            db.KeyReleases.Add(new KeyRelease { SigningKeyId = standby.SigningKeyId, ProductId = Commerce, ConfirmedUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        var console = new ScriptedConsole().Line("3").Line("1").Line("").Line("0").Line("0");
        Run(console);

        Assert.Contains($"  Standby key: {standby.SigningKeyId} - not yet in a release for {Shipping}", console.Out);
        Assert.DoesNotContain($"not yet in a release for {Commerce}", console.Out);
        Assert.Contains($"  Products: {Commerce}, {Shipping}", console.Out);
        Assert.Empty(console.HiddenGiven);
    }

    [Fact]
    public void StandbyAddedLater_Shared_AddsTheSharedStandby_NotInAReleaseForAnyProduct()
    {
        _host.SetUpInstallation(shared: true, standby: false);
        var console = AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce")
            .Line("0").Line("3").Line("2").Line("1").Hidden(Words).Line("1").Line("").Line("0").Line("0");

        Run(console);

        var keys = Keys();
        var standby = Assert.Single(keys, k => k.Role == KeyRole.Standby);
        Assert.Null(standby.ProductId);
        Assert.Equal(2, keys.Count);
        using (var db = Db())
            Assert.Empty(db.KeyReleases.ToList());
        Assert.Contains("It is not yet in a release for any product.", console.Out);
        Assert.Contains($"  Standby key: {standby.SigningKeyId} - not yet in a release for {Commerce}", console.Out);
        Assert.Single(console.HiddenGiven); // a new session holds no passphrase: asked after the question
        Assert.True(console.Out.IndexOf("Signing key (", StringComparison.Ordinal) < console.Out.IndexOf("Passphrase (", StringComparison.Ordinal));
    }

    [Fact]
    public void StandbyAddedLater_CancelAtTheList_CreatesNothing_AndAsksNoPassphrase()
    {
        _host.SetUpInstallation(shared: false, standby: false);
        var console = AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce").Hidden(Words)
            .Line("0").Line("3").Line("2").Line("/cancel").Line("0").Line("0");

        Run(console);

        Assert.Single(Keys());
        Assert.Contains("Cancelled.", console.Out);
        Assert.Single(console.HiddenGiven);
    }

    [Fact]
    public void StandbyAddedLater_CancelAtThePassphrase_CreatesNothing()
    {
        _host.SetUpInstallation(shared: true, standby: false);
        var console = new ScriptedConsole().Line("3").Line("2").Line("1").Hidden("/cancel").Line("0").Line("0");

        Run(console);

        Assert.Single(Keys());
        Assert.Contains("Cancelled.", console.Out);
        Assert.All(console.HiddenGiven, h => Assert.All(h.Raw, c => Assert.Equal('\0', c)));
    }

    [Fact]
    public void StandbyAddedLater_NothingToAdd_SaysSo_AndAsksNoPassphrase()
    {
        _host.SetUpInstallation(shared: true, standby: true);
        var console = new ScriptedConsole().Line("3").Line("2").Line("0").Line("0");

        Run(console);

        Assert.Contains(SigningKeyActions.NothingToAdd, console.Out);
        Assert.Empty(console.HiddenGiven);
        Assert.Equal(2, Keys().Count); // the shared key pair from setup, nothing more
    }

    // --- Damaged scopes (follow-ups rows 123, 124): refused as ActiveKeys.Find refuses (an InstallationRefusedException, never a raw LINQ exception).
    // Inside an action it is continuable (PDR-0053): the entry fails and the menu is shown again.

    private static SigningKeyRecord Record(string id, KeyRole role, string? product = Commerce) =>
        new() { SigningKeyId = id, ProductId = product, PublicKey = "p", EncryptedPrivateKeyPem = "e", Role = role, CreatedUtc = DateTime.UtcNow };

    [Fact]
    public void Roles_FindsTheActiveAndStandbyKeyOfTheScope()
    {
        var keys = new[] { Record("a", KeyRole.Active), Record("s", KeyRole.Standby), Record("x", KeyRole.Active, Shipping), Record("g", KeyRole.Active, null) };

        var (active, standby) = ActiveKeys.Roles(keys, Commerce);
        Assert.Equal("a", active.SigningKeyId);
        Assert.Equal("s", standby!.SigningKeyId);
        Assert.Null(ActiveKeys.Roles(keys, Shipping).Standby);
        Assert.Equal("g", ActiveKeys.Roles(keys, null).Active.SigningKeyId);
    }

    [Theory]
    [InlineData(0, 0, "no active signing key for the product acme.commerce")]
    [InlineData(2, 0, "more than one active signing key for the product acme.commerce")]
    [InlineData(1, 2, "more than one standby signing key for the product acme.commerce")]
    public void Roles_ZeroOrSeveralActiveKeys_OrSeveralStandbyKeys_AreADamagedInstallation(int actives, int standbys, string message)
    {
        var keys = Enumerable.Range(0, actives).Select(i => Record($"a{i}", KeyRole.Active))
            .Concat(Enumerable.Range(0, standbys).Select(i => Record($"s{i}", KeyRole.Standby))).ToList();

        var ex = Assert.Throws<InstallationRefusedException>(() => ActiveKeys.Roles(keys, Commerce));
        Assert.Contains(message, ex.Message);
        Assert.Contains("looks damaged", ex.Message);
    }

    [Fact]
    public void Roles_SharedScopeWithNoActiveKey_NamesTheSharedKey()
    {
        var ex = Assert.Throws<InstallationRefusedException>(() => ActiveKeys.Roles([Record("a", KeyRole.Active)], null));
        Assert.Contains("no active signing key for the shared signing key", ex.Message);
    }

    // --- The in-transaction re-check of Add a standby key (row 125): the state can change between the questions and the write.

    private (SigningKeyRecord Target, KeyProtection.NewKey Made) SeedActiveKey()
    {
        _host.SetUpInstallation(shared: false, standby: false);
        Run(AddProduct(new ScriptedConsole().Line("1"), Commerce, "Acme Commerce").Hidden(Words).Line("0").Line("0"));
        var made = KeyProtection.CreateEncrypted(System.Text.Encoding.ASCII.GetBytes(Words), SessionTestHost.Iterations);
        return (Keys().Single(), made);
    }

    [Fact]
    public void CommitStandby_WhenTheKeyIsNoLongerActive_RefusesAndWritesNothing()
    {
        var (target, made) = SeedActiveKey();
        using var db = Db();
        db.SigningKeys.Single().Role = KeyRole.Replaced;
        db.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() => SigningKeyActions.CommitStandby(db, target, made, DateTime.UtcNow, "The signing key of acme.commerce"));

        Assert.Contains("no longer the active key", ex.Message);
        Assert.Single(Keys());
    }

    [Fact]
    public void CommitStandby_WhenAStandbyAppearedMeanwhile_RefusesAndWritesNothing()
    {
        var (target, made) = SeedActiveKey();
        using var db = Db();
        db.SigningKeys.Add(Record("other", KeyRole.Standby));
        db.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() => SigningKeyActions.CommitStandby(db, target, made, DateTime.UtcNow, "The signing key of acme.commerce"));

        Assert.Contains("already has a standby key", ex.Message);
        Assert.Equal(2, Keys().Count);
    }

    // --- A damaged scope inside a whole session (row 129): the entry fails, the menu is shown again, Quit exits 0.

    [Theory]
    [InlineData(0, "no active signing key for the product acme.commerce")]
    [InlineData(2, "more than one active signing key for the product acme.commerce")]
    public void DamagedScope_FailsTheEntry_ShowsTheMenuAgain_AndTheSessionQuitsCleanly(int actives, string message)
    {
        SeedActiveKey();
        using (var db = Db())
        {
            if (actives == 0)
                db.SigningKeys.Single().Role = KeyRole.Replaced;
            else
                db.SigningKeys.Add(Record("second-active", KeyRole.Active));
            db.SaveChanges();
        }

        // Status asks its question first, then reads the keys; Switch reads them before asking anything.
        var console = new ScriptedConsole().Line("3").Line("1").Line("").Line("3").Line("0").Line("0");

        Run(console);

        Assert.Contains("Signing keys - Status failed:", console.Out);
        Assert.Contains("Signing keys - Switch failed:", console.Out);
        Assert.Contains(message, console.Out);
        Assert.Contains("looks damaged", console.Out);
        Assert.Empty(console.HiddenGiven);
    }
}
