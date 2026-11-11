using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Settings &gt; Prefix and Settings &gt; Idle lock (vendor-tool-setup: Changing the prefix, Idle lock), as whole sessions.</summary>
public class SettingsTests : IDisposable
{
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private int Run(ScriptedConsole console, ActionTable? actions = null, FakeTimeProvider? clock = null) =>
        _host.Run(console, actions, Startup.Offer.Default, clock);

    private string Installed(string prefix = "", string idleLock = "")
    {
        var folder = _host.SetUpInstallation(prefix: prefix, idleLock: idleLock);
        return folder;
    }

    private static string Setting(string folder, string name)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return db.Settings.AsNoTracking().Single(s => s.Name == name).Value;
    }

    // Main menu 5 is Settings; there 2 is Prefix and 3 is Idle lock; 0 leaves each menu.
    private static ScriptedConsole OpenPrefix(ScriptedConsole c) => c.Line("5").Line("2");
    private static ScriptedConsole OpenIdle(ScriptedConsole c) => c.Line("5").Line("3");
    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    private static string Snapshot(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return JsonSerializer.Serialize(new object[]
        {
            db.Products.AsNoTracking().OrderBy(x => x.ProductId).ToList(),
            db.SigningKeys.AsNoTracking().OrderBy(x => x.SigningKeyId).ToList(),
            db.Licenses.AsNoTracking().OrderBy(x => x.Reference).ToList(),
            db.IssueEntries.AsNoTracking().OrderBy(x => x.Id).ToList(),
            db.KeyReleases.AsNoTracking().ToList(),
        });
    }

    private static void SeedRecords(string folder)
    {
        var at = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        using var db = Installation.OpenExisting(folder).OpenDb();
        db.Products.Add(new Product { ProductId = "shop", DisplayName = "Shop", CreatedUtc = at });
        db.SigningKeys.Add(new SigningKeyRecord
        {
            SigningKeyId = "key1", ProductId = "shop", PublicKey = "pub", EncryptedPrivateKeyPem = "pem", Role = KeyRole.Active, CreatedUtc = at,
        });
        db.Licenses.Add(new LicenseRecord { Reference = "8F3AKM7RXB", ProductId = "shop", CreatedUtc = at });
        db.IssueEntries.Add(new IssueEntry
        {
            Reference = "8F3AKM7RXB", Kind = IssueKind.Issue, IssuedUtc = at, FeaturesJson = "{}", KeyIdentifier = "LIC-8F3AK-M7RXB", SigningKeyId = "key1",
        });
        db.SaveChanges();
    }

    private static void AssertNoPassphraseAsked(ScriptedConsole console)
    {
        Assert.Empty(console.HiddenGiven);
        Assert.DoesNotContain("typed hidden", console.Out);
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void PrefixChanged_ShowsTheCurrentPrefix_StoresTheNewOne_AsksNoPassphrase_AndStatesWhatChanges()
    {
        var folder = Installed();
        SeedRecords(folder);
        var before = Snapshot(folder);
        var console = Quit(OpenPrefix(new ScriptedConsole()).Line("ACME").Line("0"));

        Assert.Equal(0, Run(console));

        Assert.Equal("ACME", Setting(folder, SettingNames.Prefix));
        Assert.Contains("Prefix: LIC", console.Out);
        Assert.Contains("Enter for LIC", console.Out);
        Assert.Contains("/cancel to cancel", console.Out);
        Assert.Contains("New issues and reissues use the new prefix. Existing licenses keep theirs until reissued.", console.Out);
        AssertNoPassphraseAsked(console);
        Assert.Equal(before, Snapshot(folder));
    }

    [Fact]
    public void PrefixChanged_ToNone_ThenBackToLic_ShowsNoneAsTheCurrentPrefix()
    {
        var folder = Installed(prefix: "none");
        Assert.Equal("", Setting(folder, SettingNames.Prefix));
        var console = new ScriptedConsole();
        OpenPrefix(console).Line("LIC").Line("0");
        OpenPrefix(console).Line("NONE").Line("0");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Equal("", Setting(folder, SettingNames.Prefix));
        Assert.Contains("Prefix: none", console.Out);
        Assert.Contains("Enter for none", console.Out);
        Assert.Contains("Prefix is now LIC.", console.Out);
        Assert.Contains("Prefix is now none.", console.Out);
        Assert.Contains("Prefix: LIC", console.Out);
    }

    [Fact]
    public void PrefixUnchanged_WhenEnterKeepsIt_WritesNothingAndSaysSo()
    {
        var folder = Installed();
        var console = Quit(OpenPrefix(new ScriptedConsole()).Line(""));

        Assert.Equal(0, Run(console));

        Assert.Equal("LIC", Setting(folder, SettingNames.Prefix));
        Assert.Contains("Unchanged.", console.Out);
        Assert.DoesNotContain("Existing licenses keep theirs", console.Out);
    }

    [Fact]
    public void InvalidPrefixOnChange_NamesTheRule_AsksAgain_AndTheEnteredValidOneIsStored()
    {
        var folder = Installed();
        var console = Quit(OpenPrefix(new ScriptedConsole()).Line("acme-shop").Line("ACME").Line("0"));

        Assert.Equal(0, Run(console));

        Assert.Equal("ACME", Setting(folder, SettingNames.Prefix));
        Assert.Contains(TypedValues.PrefixRule, console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "New prefix ("));
    }

    [Fact]
    public void InvalidPrefixOnChange_ThenCancel_LeavesThePrefixUnchanged()
    {
        var folder = Installed();
        var console = Quit(OpenPrefix(new ScriptedConsole()).Line("acme-shop").Line("/cancel"));

        Assert.Equal(0, Run(console));

        Assert.Equal("LIC", Setting(folder, SettingNames.Prefix));
        Assert.Contains(TypedValues.PrefixRule, console.Out);
        Assert.Contains("Cancelled.", console.Out);
    }

    [Fact]
    public void IdleLockChanged_AsksNoPassphrase_TheCurrentSessionKeepsTheEarlierTime_AndANewSessionUsesTheNewOne()
    {
        var folder = Installed();
        Assert.Equal("15", Setting(folder, SettingNames.IdleLock));
        var clock = new FakeTimeProvider();
        byte[]? held = null;
        var heldAfterSix = false;
        var heldAfterSixteen = true;
        var listCalls = 0;
        var actions = ActionTable.Default();
        actions.Register("products.add", s =>
        {
            held = System.Text.Encoding.ASCII.GetBytes(SessionTestHost.SetupPassphrase);
            s.Lock.Hold(held);
        });
        actions.Register("products.list", s =>
        {
            if (++listCalls == 1)
                heldAfterSix = s.Lock.IsHeld;
            else
                heldAfterSixteen = s.Lock.IsHeld;
        });
        var console = new ScriptedConsole(clock: clock).Line("1").Line("1").Line("0");
        OpenIdle(console).Line("5").Line("0");
        console
            .Line("1", TimeSpan.FromMinutes(6)).Line("4")   // six minutes after the change: 15 still applies
            .Line("4", TimeSpan.FromMinutes(16))             // sixteen minutes more: locked
            .Line("0").Line("0");

        Assert.Equal(0, Run(console, actions, clock));

        Assert.Equal("5", Setting(folder, SettingNames.IdleLock));
        Assert.Contains("Idle lock: 15 minutes", console.Out);
        Assert.Contains("Idle lock is now 5 minutes. The new idle lock applies from the next session.", console.Out);
        AssertNoPassphraseAsked(console);
        Assert.True(heldAfterSix);
        Assert.False(heldAfterSixteen);

        TimeSpan? next = TimeSpan.Zero;
        var again = ActionTable.Default();
        again.Register("products.list", s => next = s.Lock.IdleTime);
        var second = new ScriptedConsole().Line("1").Line("4").Line("0").Line("0");
        Assert.Equal(0, Run(second, again));
        Assert.Equal(TimeSpan.FromMinutes(5), next);
    }

    [Fact]
    public void IdleLockChanged_ToNever_AndBack_ShowsTheCurrentValueEachTime()
    {
        var folder = Installed(idleLock: "never");
        Assert.Equal("never", Setting(folder, SettingNames.IdleLock));
        var console = new ScriptedConsole();
        OpenIdle(console).Line("30").Line("0");
        OpenIdle(console).Line("NEVER").Line("0");
        Quit(console);

        Assert.Equal(0, Run(console));

        Assert.Equal("never", Setting(folder, SettingNames.IdleLock));
        Assert.Contains("Idle lock: never", console.Out);
        Assert.Contains("Enter for never", console.Out);
        Assert.Contains("Idle lock is now 30 minutes.", console.Out);
        Assert.Contains("Idle lock is now never.", console.Out);
    }

    [Fact]
    public void IdleLockUnchanged_WhenEnterKeepsIt_WritesNothingAndSaysSo()
    {
        var folder = Installed();
        var console = Quit(OpenIdle(new ScriptedConsole()).Line(""));

        Assert.Equal(0, Run(console));

        Assert.Equal("15", Setting(folder, SettingNames.IdleLock));
        Assert.Contains("Enter for 15", console.Out);
        Assert.Contains("Unchanged.", console.Out);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("ten")]
    public void IdleLockOutsideTheRule_InSettings_NamesTheRuleAndAsksAgain(string typed)
    {
        var folder = Installed();
        var console = Quit(OpenIdle(new ScriptedConsole()).Line(typed).Line("20").Line("0"));

        Assert.Equal(0, Run(console));

        Assert.Equal("20", Setting(folder, SettingNames.IdleLock));
        Assert.Contains(TypedValues.IdleLockRule, console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "New idle lock ("));
    }

    [Fact]
    public void IdleLockOutsideTheRule_ThenCancel_LeavesItUnchanged()
    {
        var folder = Installed();
        var console = Quit(OpenIdle(new ScriptedConsole()).Line("ten").Line("/cancel"));

        Assert.Equal(0, Run(console));

        Assert.Equal("15", Setting(folder, SettingNames.IdleLock));
        Assert.Contains("Cancelled.", console.Out);
    }

    [Fact]
    public void DamagedIdleLockSetting_AtOpen_IsRefusedOnStderr_NamingAnotherFolder()
    {
        var folder = Installed();
        Damage(folder);

        var console = new ScriptedConsole();
        Assert.Equal(1, Run(console));

        Assert.Contains("looks damaged. Restore a backup into another folder.", console.Err);
        Assert.DoesNotContain("from a backup", console.Err);
        Assert.Equal("", console.Out);
    }

    [Fact]
    public void DamagedIdleLockSetting_InSettings_IsOneFailureLine_AndTheSessionContinues()
    {
        var folder = Installed();
        var actions = ActionTable.Default();
        actions.Register("products.add", _ => Damage(folder));
        var console = new ScriptedConsole().Line("1").Line("1").Line("0");
        // The failure returns to Settings, where Prefix still works.
        OpenIdle(console).Line("2").Line("").Line("0").Line("0");
        Assert.Equal(0, Run(console, actions));

        Assert.Contains("Settings - Idle lock failed: The idle lock setting in", console.Out);
        Assert.Contains("looks damaged. Restore a backup into another folder.", console.Out);
        Assert.Equal("ten", Setting(folder, SettingNames.IdleLock));
        Assert.Contains("Unchanged.", console.Out);
        Assert.Equal("", console.Err);
    }

    private static void Damage(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        db.Settings.Single(s => s.Name == SettingNames.IdleLock).Value = "ten";
        db.SaveChanges();
    }
}
