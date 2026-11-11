using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// The session passphrase scenarios of vendor-tool-signing-keys and the idle lock of vendor-tool-setup, through Issue, as whole
/// sessions through <see cref="ToolApp"/>. The idle time is the one setup stored (15 minutes, or never); the fake clock is advanced before answers.
/// </summary>
public class SessionPassphraseTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Id = "acme.commerce";
    private const string Expiry = "2027-03-01T23:59:59Z";
    private static readonly TimeSpan Minutes10 = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Minutes16 = TimeSpan.FromMinutes(16);
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private void Run(ScriptedConsole console, FakeTimeProvider? clock = null)
    {
        var exit = _host.Run(console, offer: Startup.Offer.Default, clock: clock);
        Assert.True(exit == 0, console.Err + console.Out);
    }

    /// <summary>An installation in per-product mode (idle lock as given; empty is the proposed 15) with the product, made by earlier sessions.</summary>
    private void Prepare(string idleLock = "")
    {
        _host.SetUpInstallation(idleLock: idleLock);
        Run(new ScriptedConsole().Line("1").Line("1").Line(Id).Line("Acme Commerce").Hidden(Words).Line("0").Line("0"));
        using var db = Db();
        db.FeatureDefinitions.Add(new FeatureDefinition { ProductId = Id, Name = "ecommerce", Type = Data.FeatureType.Switch, AddedUtc = new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc) });
        db.SaveChanges();
    }

    private ToolDbContext Db() => Installation.OpenExisting(_host.HomeInstallation).OpenDb();

    /// <summary>Issue from the Licenses menu: product 1, the questions to the summary.</summary>
    private static ScriptedConsole IssueQuestions(ScriptedConsole c, string orderRef = "ORD-1", TimeSpan advanceAtEmail = default) =>
        c.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line(orderRef).Line("owner@example.com", advanceAtEmail);

    /// <summary>The summary answered yes, the passphrase when asked, and no to saving the key.</summary>
    private static ScriptedConsole Sign(ScriptedConsole c, bool passphrase)
    {
        c.Line("");
        if (passphrase)
            c.Hidden(Words);
        return c.Line("");
    }

    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    private static int Prompts(ScriptedConsole c) => SessionTestHost.Count(c.Out, "Passphrase (");

    [Fact]
    public void PassphraseAskedOncePerSession()
    {
        _host.SetUpInstallation();
        var c = new ScriptedConsole().Line("1").Line("1").Line(Id).Line("Acme Commerce").Hidden(Words).Line("0").Line("2");
        Sign(IssueQuestions(c, "ORD-1"), passphrase: false);
        Sign(IssueQuestions(c, "ORD-2"), passphrase: false);
        Quit(c);

        Run(c);

        Assert.Equal(1, Prompts(c));
        Assert.Single(c.HiddenGiven);
        using var db = Db();
        Assert.Equal(2, db.IssueEntries.Count());
    }

    [Fact]
    public void ProductAddedStraightAfterSetup_AsksNoPassphrase_AndTheKeyOpensWithTheSetupPassphrase()
    {
        var c = new ScriptedConsole().SetUp(shared: false).Line("1").Line("1").Line(Id).Line("Acme Commerce").Line("0").Line("0");

        _host.Run(c, offer: Startup.Offer.Default);

        Assert.True(c.Err.Length == 0, c.Err + c.Out);
        // The two hidden answers are setup's own, the passphrase and its confirmation; the product add asks nothing.
        Assert.Equal(2, c.HiddenGiven.Count);
        using var db = Db();
        var keys = db.SigningKeys.AsNoTracking().Where(k => k.ProductId == Id).ToList();
        Assert.Equal(2, keys.Count); // active and standby
        foreach (var key in keys)
        {
            using var opened = ECDsa.Create();
            opened.ImportFromEncryptedPem(key.EncryptedPrivateKeyPem, Encoding.ASCII.GetBytes(Words));
        }
    }

    [Fact]
    public void NothingKeptAfterQuitting()
    {
        Prepare();
        var first = new ScriptedConsole().Line("2");
        Quit(Sign(IssueQuestions(first, "ORD-1"), passphrase: true));
        Run(first);
        Assert.Equal(1, Prompts(first));
        LicenseKit.AssertZeroed(first);

        var second = new ScriptedConsole().Line("2");
        Quit(Sign(IssueQuestions(second, "ORD-2"), passphrase: true));
        Run(second);

        Assert.Equal(1, Prompts(second));
        Assert.Single(second.HiddenGiven);
    }

    [Fact]
    public void LockedAfterIdleTime()
    {
        Prepare();
        var clock = new FakeTimeProvider();
        var c = new ScriptedConsole(clock: clock).Line("2");
        Sign(IssueQuestions(c, "ORD-1"), passphrase: true);
        c.Line("1", Minutes16).Line("1").Line(Expiry).Line("").Line("").Line("on").Line("ORD-2").Line("owner@example.com");
        Sign(c, passphrase: true);
        Quit(c);

        Run(c, clock);

        Assert.Equal(2, Prompts(c));
        Assert.Equal(2, c.HiddenGiven.Count);
        using var db = Db();
        Assert.Equal(2, db.IssueEntries.Count());
    }

    [Fact]
    public void IdleLockNever_AnHourBetweenIssues_AsksOnlyForTheFirst()
    {
        Prepare(idleLock: "never");
        var clock = new FakeTimeProvider();
        var c = new ScriptedConsole(clock: clock).Line("2");
        Sign(IssueQuestions(c, "ORD-1"), passphrase: true);
        c.Line("1", TimeSpan.FromHours(1)).Line("1").Line(Expiry).Line("").Line("").Line("on").Line("ORD-2").Line("owner@example.com");
        Sign(c, passphrase: false);
        Quit(c);

        Run(c, clock);

        Assert.Equal(1, Prompts(c));
        using var db = Db();
        Assert.Equal(2, db.IssueEntries.Count());
    }

    [Fact]
    public void MenuChoicesKeepTheSessionUnlocked()
    {
        Prepare();
        var clock = new FakeTimeProvider();
        var c = new ScriptedConsole(clock: clock).Line("2");
        Sign(IssueQuestions(c, "ORD-1"), passphrase: true);
        // Back to main, Products, back to main: thirty minutes in all, a choice at least every ten.
        c.Line("0", Minutes10).Line("1", Minutes10).Line("0", Minutes10).Line("2", Minutes10);
        Sign(IssueQuestions(c, "ORD-2"), passphrase: false);
        Quit(c);

        Run(c, clock);

        Assert.Equal(1, Prompts(c));
        using var db = Db();
        Assert.Equal(2, db.IssueEntries.Count());
    }

    [Fact]
    public void IdleLockFiringBetweenAnIssuesQuestions_AsksAfterTheSummary_AndKeepsTheAnswers()
    {
        Prepare();
        var clock = new FakeTimeProvider();
        var c = new ScriptedConsole(clock: clock).Line("2");
        Sign(IssueQuestions(c, "ORD-1"), passphrase: true);
        // The idle time passes before the last answer of the second issue; the summary comes first, then the passphrase.
        Sign(IssueQuestions(c, "ORD-2", advanceAtEmail: Minutes16), passphrase: true);
        Quit(c);

        Run(c, clock);

        Assert.Equal(2, Prompts(c));
        var out2 = c.Out[(c.Out.LastIndexOf("Summary", StringComparison.Ordinal))..];
        Assert.True(out2.IndexOf("Summary", StringComparison.Ordinal) < out2.IndexOf("Passphrase (", StringComparison.Ordinal), out2);
        using var db = Db();
        var entries = db.IssueEntries.AsNoTracking().OrderBy(e => e.Id).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("ORD-2", entries[1].OrderRef);
        Assert.Equal("owner@example.com", db.Licenses.AsNoTracking().OrderBy(l => l.CreatedUtc).Last().CustomerEmail);
    }
}
