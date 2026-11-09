using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// Signing keys &gt; Trusted keys, Release confirmed and Reissues sent (vendor-tool-signing-keys), as whole sessions through
/// <c>ToolApp.Run</c>. The Signing keys menu is main menu 3: Status 1, Switch 3, Trusted keys 4, Release confirmed 5, Reissues sent 6.
/// </summary>
public class TrustedKeysTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Commerce = "acme.commerce";
    private const string Shipping = "acme.shipping";
    private const string Tax = "acme.tax";
    private static readonly DateTime Now = new(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc);
    private readonly SessionTestHost _host = new();
    private readonly List<LicenseKit> _kits = [];

    public void Dispose()
    {
        _host.Dispose();
        _kits.ForEach(k => k.Dispose());
    }

    private ToolDbContext Db() => Installation.OpenExisting(_host.HomeInstallation).OpenDb();

    private List<SigningKeyRecord> Keys()
    {
        using var db = Db();
        return db.SigningKeys.AsNoTracking().ToList();
    }

    private List<KeyRelease> Releases()
    {
        using var db = Db();
        return db.KeyReleases.AsNoTracking().ToList();
    }

    private ScriptedConsole Run(ScriptedConsole console)
    {
        var exit = _host.Run(console, null, Startup.Offer.Default);
        Assert.True(exit == 0, console.Err + console.Out);
        Assert.Equal("", console.Err);
        return console;
    }

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

    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    private static ScriptedConsole Trusted(string product) => Quit(new ScriptedConsole().Line("3").Line("4").Line(product));

    private static ScriptedConsole Release(string product, string answer) => Quit(new ScriptedConsole().Line("3").Line("5").Line(product).Line(answer));

    private static ScriptedConsole Reissues(string keyId, string answer) => Quit(new ScriptedConsole().Line("3").Line("6").Line(keyId).Line(answer));

    private static ScriptedConsole Status() => Quit(new ScriptedConsole().Line("3").Line("1").Line(""));

    /// <summary>Switch in the Signing keys menu (the key, or 1 in shared mode; the reason; yes to the warning; the passphrase), then Quit.</summary>
    private static ScriptedConsole Switch(string? product, string reason) =>
        Quit(new ScriptedConsole().Line("3").Line("3").Line(product ?? "1").Line(reason).Line("y").Hidden(Words));

    /// <summary>The comment and key lines printed by the last Trusted keys in a transcript, as (comment, key line) pairs.</summary>
    internal static List<(string Comment, string Line)> Printed(string transcript)
    {
        var lines = SessionTestHost.Lines(transcript);
        var start = Array.FindLastIndex(lines, l => l.Contains(TrustedKeyActions.CommentPrefix + "Trusted public keys for", StringComparison.Ordinal));
        Assert.True(start >= 0, transcript);

        var pairs = new List<(string, string)>();
        for (var i = start + 1; i + 1 < lines.Length && lines[i].StartsWith(TrustedKeyActions.CommentPrefix, StringComparison.Ordinal); i += 2)
            pairs.Add((lines[i], lines[i + 1]));
        return pairs;
    }

    private static string Id(string keyLine) => keyLine[..keyLine.IndexOf('.')];

    private static string[] Ids(List<(string Comment, string Line)> printed) => [.. printed.Select(p => Id(p.Line)).Order(StringComparer.Ordinal)];

    private static string[] Sorted(params string[] ids) => [.. ids.Order(StringComparer.Ordinal)];

    /// <summary>Per-product, standby on: A active, S standby. Returns their IDs; A is then switched out with <paramref name="reason"/> (confirming the warning), leaving S active and a new standby T.</summary>
    private (string A, string S, string T) Rotated(string reason)
    {
        Seed(shared: false, standby: true, Commerce);
        var before = Keys();
        var a = before.Single(k => k.Role == KeyRole.Active).SigningKeyId;
        var s = before.Single(k => k.Role == KeyRole.Standby).SigningKeyId;
        Run(Switch(Commerce, reason));
        var t = Keys().Single(k => k.Role == KeyRole.Standby).SigningKeyId;
        Assert.NotEqual(s, t);
        return (a, s, t);
    }

    // --- Trusted public keys per product

    [Fact]
    public void AfterRoutineRotation_TheListHoldsAS_AndT_EachPrecededByItsRole()
    {
        var (a, s, t) = Rotated("routine");
        var console = Run(Trusted(Commerce));

        var printed = Printed(console.Out);

        Assert.Equal("// ", TrustedKeyActions.CommentPrefix);
        Assert.All(printed, p =>
        {
            Assert.StartsWith("//", p.Comment, StringComparison.Ordinal);
            Assert.False(p.Line.StartsWith('/'));
        });
        Assert.Equal([(TrustedKeyActions.CommentPrefix + "active", s), (TrustedKeyActions.CommentPrefix + "standby", t), (TrustedKeyActions.CommentPrefix + "replaced (routine)", a)],
            printed.Select(p => (p.Comment, Id(p.Line))));
        var records = Keys();
        Assert.All(printed, p => Assert.Equal(records.Single(k => k.SigningKeyId == Id(p.Line)).PublicKey, p.Line));
    }

    [Fact]
    public void DuringALeak_TheListHoldsA_MarkedLeaked_S_AndT()
    {
        var (a, s, t) = Rotated("leak");
        var console = Run(Trusted(Commerce));

        var printed = Printed(console.Out);

        Assert.Equal(Sorted(a, s, t), Ids(printed));
        var leaked = Assert.Single(printed, p => Id(p.Line) == a);
        Assert.Contains("replaced (leak)", leaked.Comment);
        Assert.Contains("LEAKED", leaked.Comment);
    }

    [Fact]
    public void LossReplacedKey_StaysListed_AsReplacedLoss()
    {
        var (a, _, _) = Rotated("loss");

        var printed = Printed(Run(Trusted(Commerce)).Out);

        Assert.Equal(TrustedKeyActions.CommentPrefix + "replaced (loss)", Assert.Single(printed, p => Id(p.Line) == a).Comment);
    }

    [Fact]
    public void AfterTheReissuesAreSent_TheListHoldsSAndTOnly_AndNoPassphraseIsAsked()
    {
        var (a, s, t) = Rotated("leak");
        var reissues = Run(Reissues(a, "y"));

        var printed = Printed(Run(Trusted(Commerce)).Out);

        Assert.Equal(Sorted(s, t), Ids(printed));
        Assert.Equal(Now, Keys().Single(k => k.SigningKeyId == a).ReissuesSentUtc);
        Assert.Empty(reissues.HiddenGiven);
        Assert.Contains($"Reissues for {a} are marked as sent", reissues.Out);
    }

    [Fact]
    public void ReissuesSent_ListsOnlyKeysReplacedForALeakWaitingForTheirReissues_AndSaysSoWhenThereAreNone()
    {
        var (a, _, _) = Rotated("routine");

        var console = Run(new ScriptedConsole().Line("3").Line("6").Line("0").Line("0"));

        Assert.Contains(TrustedKeyActions.NoLeakedKeys, console.Out);
        Assert.DoesNotContain(a, console.Out);
        Assert.All(Keys(), k => Assert.Null(k.ReissuesSentUtc));
    }

    [Fact]
    public void ReissuesSent_ListsTheLeakedKeyWithItsProduct_AndDecliningWritesNothing()
    {
        var (a, _, _) = Rotated("leak");

        var console = Run(Reissues(a, "n"));

        Assert.Contains($"{a} - replaced 2026-11-08, the signing key of {Commerce}", console.Out);
        Assert.Contains(TrustedKeyActions.ReissuesDeclined, console.Out);
        Assert.All(Keys(), k => Assert.Null(k.ReissuesSentUtc));
        var transcript = console.Out.Replace("\r\n", "\n");
        Assert.Contains("Signing keys\n  1 Status", transcript[transcript.LastIndexOf(TrustedKeyActions.ReissuesDeclined, StringComparison.Ordinal)..]);
        Assert.Empty(console.HiddenGiven);
    }

    [Fact]
    public void SharedMode_ReissuesSentNamesTheSharedKey_AndTheKeyLeavesTheListOfEveryProduct()
    {
        Seed(shared: true, standby: true, Commerce, Shipping);
        var a = Keys().Single(k => k.Role == KeyRole.Active).SigningKeyId;
        Run(Switch(null, "leak"));

        var reissues = Run(Reissues(a, "y"));

        Assert.Contains($"{a} - replaced 2026-11-08, the shared signing key", reissues.Out);
        Assert.DoesNotContain(a, Printed(Run(Trusted(Commerce)).Out).Select(p => Id(p.Line)));
        Assert.DoesNotContain(a, Printed(Run(Trusted(Shipping)).Out).Select(p => Id(p.Line)));
    }

    // --- Release confirmed

    [Fact]
    public void ReleaseConfirmed_PerProduct_MarksEveryListedKey_AndStatusNoLongerShowsTheStandbyAsNotYetInARelease()
    {
        Seed(shared: false, standby: true, Commerce, Shipping);
        // Seeding the second product in per-product mode asks no passphrase: the first session held it for both.
        var keys = Keys();
        var commerceKeys = keys.Where(k => k.ProductId == Commerce).Select(k => k.SigningKeyId).ToArray();
        var console = Run(Release(Commerce, "y"));

        var rows = Releases();
        Assert.Equal(Sorted(commerceKeys), rows.Select(r => r.SigningKeyId).Order(StringComparer.Ordinal).ToArray());
        Assert.All(rows, r =>
        {
            Assert.Equal(Commerce, r.ProductId);
            Assert.Equal(Now, r.ConfirmedUtc);
        });
        Assert.Empty(console.HiddenGiven);
        Assert.Contains("Marked 2 keys as in a release for acme.commerce.", console.Out);

        var status = Run(Status()).Out;
        var standbyCommerce = keys.Single(k => k.ProductId == Commerce && k.Role == KeyRole.Standby).SigningKeyId;
        var standbyShipping = keys.Single(k => k.ProductId == Shipping && k.Role == KeyRole.Standby).SigningKeyId;
        Assert.Contains($"  Standby key: {standbyCommerce}", SessionTestHost.Lines(status));
        Assert.Contains($"  Standby key: {standbyShipping} - not yet in a release for {Shipping}", status);
    }

    [Fact]
    public void ReleaseConfirmed_SharedModeWithTwoProducts_MarksTheListForTheChosenProductOnly()
    {
        Seed(shared: true, standby: true, Commerce, Shipping);
        var keys = Keys();
        var standby = keys.Single(k => k.Role == KeyRole.Standby).SigningKeyId;

        Run(Release(Commerce, "y"));

        var rows = Releases();
        Assert.Equal(2, rows.Count); // the shared active and standby keys, for acme.commerce
        Assert.All(rows, r => Assert.Equal(Commerce, r.ProductId));
        Assert.Equal(Sorted([.. keys.Select(k => k.SigningKeyId)]), rows.Select(r => r.SigningKeyId).Order(StringComparer.Ordinal).ToArray());

        var status = Run(Status()).Out;
        Assert.Contains($"  Standby key: {standby} - not yet in a release for {Shipping}", status);
        Assert.DoesNotContain($"not yet in a release for {Commerce}", status);
        Assert.DoesNotContain($"{Shipping}, {Commerce}", status);
    }

    [Fact]
    public void ReleaseConfirmed_ShowsTheSameListAsTrustedKeys_BeforeAsking_AndAsksNoPassphrase()
    {
        Seed(shared: false, standby: true, Commerce);

        var trusted = Printed(Run(Trusted(Commerce)).Out);
        var console = Run(Release(Commerce, "y"));

        Assert.Equal(trusted, Printed(console.Out));
        Assert.True(console.Out.IndexOf("Does a release of acme.commerce trust this list?", StringComparison.Ordinal) > console.Out.IndexOf(trusted[^1].Line, StringComparison.Ordinal));
        Assert.Empty(console.HiddenGiven);
    }

    [Fact]
    public void ReleaseConfirmed_Declined_WritesNothing_AndShowsTheSigningKeysMenu()
    {
        Seed(shared: false, standby: true, Commerce);

        var console = Run(Release(Commerce, "n"));

        Assert.Empty(Releases());
        Assert.Contains(TrustedKeyActions.ReleaseDeclined, console.Out);
        var after = console.Out.Replace("\r\n", "\n");
        Assert.Contains("Signing keys\n  1 Status", after[after.LastIndexOf(TrustedKeyActions.ReleaseDeclined, StringComparison.Ordinal)..]);
    }

    [Fact]
    public void ReleaseConfirmed_MarksOnlyTheListedKeysThatLackARow_AndKeepsTheEarlierRow()
    {
        Seed(shared: false, standby: true, Commerce);
        var standby = Keys().Single(k => k.Role == KeyRole.Standby).SigningKeyId;
        var earlier = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var db = Db())
        {
            db.KeyReleases.Add(new KeyRelease { SigningKeyId = standby, ProductId = Commerce, ConfirmedUtc = earlier });
            db.SaveChanges();
        }

        var console = Run(Release(Commerce, "y"));

        Assert.Equal(2, Releases().Count);
        Assert.Equal(earlier, Releases().Single(r => r.SigningKeyId == standby).ConfirmedUtc);
        Assert.Contains("Marked 1 key as in a release for acme.commerce.", console.Out);
    }

    [Fact]
    public void ReleaseConfirmed_WhenEveryListedKeyIsAlreadyMarked_SaysSo_AndAsksNothing()
    {
        Seed(shared: false, standby: true, Commerce);
        Run(Release(Commerce, "y"));

        var console = Run(new ScriptedConsole().Line("3").Line("5").Line(Commerce).Line("0").Line("0"));

        Assert.Contains(TrustedKeyActions.AlreadyInRelease(Commerce), console.Out);
        Assert.DoesNotContain("trust this list?", console.Out);
        Assert.Equal(2, Releases().Count);
    }

    [Fact]
    public void SharedModeWithThreeProducts_EachProductHasItsOwnRelease()
    {
        Seed(shared: true, standby: true, Commerce, Shipping, Tax);
        var standby = Keys().Single(k => k.Role == KeyRole.Standby).SigningKeyId;

        Run(Release(Commerce, "y"));
        Run(Release(Tax, "y"));

        Assert.Equal([Commerce, Tax], Releases().Where(r => r.SigningKeyId == standby).Select(r => r.ProductId).Order(StringComparer.Ordinal));
        Assert.Contains($"  Standby key: {standby} - not yet in a release for {Shipping}", Run(Status()).Out);
    }

    [Fact]
    public void SharedMode_AProductAddedAfterTheConfirmation_HasNoRowsAndTheStandbyIsNotYetInItsRelease()
    {
        // Row 92 of follow-ups: the check that can fail only in shared mode, where one confirmation covers a key for several products.
        Seed(shared: true, standby: true, Commerce, Shipping);
        var standby = Keys().Single(k => k.Role == KeyRole.Standby).SigningKeyId;
        Run(Release(Commerce, "y"));
        Run(Release(Shipping, "y"));
        Assert.Equal(4, Releases().Count);

        AddProducts(shared: true, Tax);

        Assert.DoesNotContain(Releases(), r => r.ProductId == Tax);
        var status = Run(Status()).Out;
        Assert.Contains($"  Standby key: {standby} - not yet in a release for {Tax}", status);
        Assert.DoesNotContain($"for {Commerce}", status);
        Assert.DoesNotContain($"for {Shipping}", status);
    }

    // --- No passphrase for reading

    [Fact]
    public void NoPassphraseForReading_TrustedKeysInAFreshSessionAsksNone()
    {
        Seed(shared: false, standby: true, Commerce);

        var console = Run(Trusted(Commerce));

        Assert.Empty(console.HiddenGiven);
        Assert.DoesNotContain("Passphrase (", console.Out);
        Assert.Equal(2, Printed(console.Out).Count);
    }

    [Fact]
    public void SharedMode_TrustedKeysForAnyProductPrintsTheSharedKeys()
    {
        Seed(shared: true, standby: true, Commerce, Shipping);

        var commerce = Printed(Run(Trusted(Commerce)).Out);
        var shipping = Printed(Run(Trusted(Shipping)).Out);

        Assert.Equal(commerce, shipping);
        Assert.Equal(Sorted([.. Keys().Select(k => k.SigningKeyId)]), Ids(commerce));
        Assert.Contains("(the shared signing keys)", Run(Trusted(Commerce)).Out);
    }

    [Fact]
    public void ProductChosenByNumber_AndNoProducts_SaysSo()
    {
        Seed(shared: false, standby: false);
        Assert.Contains(ProductActions.NoProducts, Run(new ScriptedConsole().Line("3").Line("4").Line("0").Line("0")).Out);

        AddProducts(shared: false, Commerce);
        Assert.Single(Printed(Run(Trusted("1")).Out));
    }

    // --- The re-check inside the transaction

    [Fact]
    public void CommitRelease_WhenTheListChangedSinceItWasShown_RefusesAndInsertsNothing()
    {
        Seed(shared: false, standby: true, Commerce);
        var shown = Keys().Select(k => k.SigningKeyId).ToArray();
        using (var db = Db())
        {
            db.SigningKeys.Add(new SigningKeyRecord
            {
                SigningKeyId = "old", ProductId = Commerce, PublicKey = "p", EncryptedPrivateKeyPem = "e", Role = KeyRole.Replaced,
                ReplacedReason = ReplacedReason.Routine, ReplacedUtc = Now, CreatedUtc = Now,
            });
            db.SaveChanges();
        }

        using var change = Db();
        using var transaction = change.Database.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => TrustedKeyActions.CommitRelease(change, Commerce, Commerce, shown, Now));

        Assert.Contains("changed since the list was shown", ex.Message);
        transaction.Rollback();
        Assert.Empty(Releases());
    }

    [Fact]
    public void CommitReissuesSent_WhenAlreadySentOrNotALeak_RefusesAndWritesNothing()
    {
        var (a, s, _) = Rotated("leak");
        using (var db = Db())
        {
            TrustedKeyActions.CommitReissuesSent(db, a, Now);
        }

        var sentAt = Now.AddDays(1);
        using var again = Db();
        Assert.Throws<InvalidOperationException>(() => TrustedKeyActions.CommitReissuesSent(again, a, sentAt));
        Assert.Throws<InvalidOperationException>(() => TrustedKeyActions.CommitReissuesSent(again, s, sentAt));
        Assert.Equal(Now, Keys().Single(k => k.SigningKeyId == a).ReissuesSentUtc);
        Assert.Null(Keys().Single(k => k.SigningKeyId == s).ReissuesSentUtc);
    }

    // --- LicenseEvaluator trusting the printed lines

    private LicenseKit Kit()
    {
        var kit = new LicenseKit(shared: false);
        _kits.Add(kit);
        return kit;
    }

    /// <summary>The printed key lines parsed the way a site would: comment lines skipped, each other line one public key.</summary>
    private static TrustedSigningKeys TrustPrinted(string transcript) =>
        TrustedSigningKeys.Create(Printed(transcript).Select(p => SigningPublicKey.Parse(p.Line)));

    private static LicenseResult Evaluate(TrustedSigningKeys trusted, string key) =>
        new LicenseEvaluator(LicenseKitProduct, trusted, new FakeTimeProvider()).Evaluate(key);

    private const string LicenseKitProduct = "acme.commerce";

    [Fact]
    public void LicenseEvaluatorTrustingThePrintedLines_AcceptsEveryListedKey_AndReportsTheWithdrawnKeyNotRecognised()
    {
        var kit = Kit();
        var keyA = kit.Issue();
        var a = kit.Keys().Single(k => k.Role == KeyRole.Active).SigningKeyId;
        kit.Run(LicenseKit.Quit(new ScriptedConsole().Line("3").Line("3").Line(Commerce).Line("leak").Line("y").Hidden(Words)));
        var keyS = kit.Issue();
        var s = kit.Keys().Single(k => k.Role == KeyRole.Active).SigningKeyId;
        Assert.NotEqual(a, s);

        // During the leak: A, S and T are printed; keys signed by A and by S are both accepted.
        var during = TrustPrinted(kit.Run(Trusted(Commerce)).Out);
        Assert.Equal(3, during.SigningKeyIds.Count);
        Assert.True(Evaluate(during, keyA).IsLicensed);
        Assert.True(Evaluate(during, keyS).IsLicensed);

        // After the reissues are sent A is withdrawn: its key is not recognised, S still valid.
        kit.Run(Reissues(a, "y"));
        var after = TrustPrinted(kit.Run(Trusted(Commerce)).Out);
        Assert.Equal(2, after.SigningKeyIds.Count);
        Assert.False(after.Contains(a));
        Assert.Equal(LicenseState.SigningKeyNotRecognised, Evaluate(after, keyA).State);
        Assert.True(Evaluate(after, keyS).IsLicensed);

        // Row 120: the installation still trusts the withdrawn key, so a key it signed still chooses its license.
        var show = kit.Run(LicenseKit.Quit(new ScriptedConsole().Line("2").Line("6").Line(keyA)));
        Assert.Contains("LIC-", show.Out);
        Assert.Contains("Product: acme.commerce", show.Out);
        Assert.DoesNotContain("not issued by this installation", show.Out);
    }
}

file static class KitKeys
{
    public static List<SigningKeyRecord> Keys(this LicenseKit kit)
    {
        using var db = kit.Db();
        return db.SigningKeys.AsNoTracking().ToList();
    }
}
