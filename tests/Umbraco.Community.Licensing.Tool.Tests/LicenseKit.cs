using System.Text;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>
/// An installation of its own with one product (switches ecommerce and shipping, number max-orders, text note), for the tests of the
/// actions that choose an existing license (Reissue, Renew, Resend). Whole sessions through the scripted console; the Licenses menu is 2.
/// </summary>
internal sealed class LicenseKit : IDisposable
{
    public const string Words = SessionTestHost.SetupPassphrase;
    public const string Reference = "LIC-8F3AK-M7RXB";

    public LicenseKit(bool shared, string productId = "acme.commerce", string name = "Acme Commerce", string prefix = "")
    {
        Shared = shared;
        ProductId = productId;
        Host.SetUpInstallation(shared: shared, prefix: prefix);

        var seed = new ScriptedConsole().Line("1").Line("1").Line(productId).Line(name);
        if (!shared)
            seed.Hidden(Words);
        Run(seed.Line("0").Line("0"));

        using var db = Db();
        foreach (var (feature, type) in new[] { ("ecommerce", FeatureType.Switch), ("max-orders", FeatureType.Number), ("note", FeatureType.Text), ("shipping", FeatureType.Switch) })
            db.FeatureDefinitions.Add(new FeatureDefinition { ProductId = productId, Name = feature, Type = type, AddedUtc = Utc(2026, 11, 8) });
        db.SaveChanges();
    }

    public SessionTestHost Host { get; } = new();
    public bool Shared { get; }
    public string ProductId { get; }

    public void Dispose() => Host.Dispose();

    public static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    public static FakeTimeProvider ClockAt(DateTime utc) => new(new DateTimeOffset(utc));

    public ScriptedConsole Run(ScriptedConsole console, FakeTimeProvider? clock = null)
    {
        var exit = Host.Run(console, offer: Startup.Offer.Default, clock: clock);
        Assert.True(exit == 0, console.Err + console.Out);
        return console;
    }

    public ToolDbContext Db() => Installation.OpenExisting(Host.HomeInstallation).OpenDb();

    public List<LicenseRecord> Licenses()
    {
        using var db = Db();
        return db.Licenses.AsNoTracking().OrderBy(l => l.CreatedUtc).ThenBy(l => l.Reference).ToList();
    }

    public List<IssueEntry> Entries()
    {
        using var db = Db();
        return db.IssueEntries.AsNoTracking().OrderBy(e => e.Id).ToList();
    }

    /// <summary>Quit: Back from the Licenses menu, then Quit.</summary>
    public static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    /// <summary>The summary answered yes, the passphrase (the session holds none), no to saving the key, then Quit.</summary>
    public static ScriptedConsole Sign(ScriptedConsole c) => Quit(c.Line("").Hidden(Words).Line(""));

    /// <summary>Issue with the "License issued" answers by default; returns the key string shown. Every call is a session of its own.</summary>
    public string Issue(
        string expiry = "2027-03-01T23:59:59Z",
        string displayName = "",
        string vendorTag = "",
        string ecommerce = "on",
        string maxOrders = "500",
        string note = "",
        string shipping = "",
        string orderRef = "ORD-1001",
        string email = "owner@example.com",
        FakeTimeProvider? clock = null)
    {
        var console = new ScriptedConsole().Line("2").Line("1").Line("1").Line(expiry).Line(displayName).Line(vendorTag)
            .Line(ecommerce).Line(maxOrders).Line(note).Line(shipping).Line(orderRef).Line(email);
        return KeyStringOf(Run(Sign(console), clock));
    }

    /// <summary>The Reissue questions in order; an empty answer keeps the current value.</summary>
    public static ScriptedConsole ReissueQuestions(
        ScriptedConsole c,
        string license,
        string expiry = "",
        string displayName = "",
        string vendorTag = "",
        string ecommerce = "",
        string maxOrders = "",
        string note = "",
        string shipping = "",
        string orderRef = "",
        string email = "") =>
        c.Line("2").Line("2").Line(license).Line(expiry).Line(displayName).Line(vendorTag)
            .Line(ecommerce).Line(maxOrders).Line(note).Line(shipping).Line(orderRef).Line(email);

    /// <summary>The Renew questions in order.</summary>
    public static ScriptedConsole RenewQuestions(ScriptedConsole c, string license, string expiry = "", string orderRef = "", string email = "") =>
        c.Line("2").Line("3").Line(license).Line(expiry).Line(orderRef).Line(email);

    public static ScriptedConsole ResendQuestions(ScriptedConsole c, string license) => c.Line("2").Line("4").Line(license);

    public static string KeyStringOf(ScriptedConsole console)
    {
        var lines = SessionTestHost.Lines(console.Out);
        var shown = Array.FindIndex(lines, l => l.Contains("The key string is shown once", StringComparison.Ordinal));
        Assert.True(shown >= 0, console.Out);
        return lines[shown + 1];
    }

    /// <summary>The key string verified by the library, trusting every key the installation holds for the product, whatever its role.</summary>
    public LicenseResult Evaluate(string keyString, FakeTimeProvider? clock = null)
    {
        using var db = Db();
        var keys = db.SigningKeys.AsNoTracking().ToList().Where(k => Shared ? k.ProductId is null : k.ProductId == ProductId);
        var trusted = TrustedSigningKeys.Create(keys.Select(k => SigningPublicKey.Parse(k.PublicKey)));
        return new LicenseEvaluator(ProductId, trusted, clock ?? new FakeTimeProvider()).Evaluate(keyString);
    }

    public static void AssertNoPassphraseAsked(ScriptedConsole console)
    {
        Assert.DoesNotContain("Passphrase (", console.Out);
        Assert.Empty(console.HiddenGiven);
    }

    public static void AssertZeroed(ScriptedConsole console) =>
        Assert.All(console.HiddenGiven, h => Assert.All(h.Raw, c => Assert.Equal('\0', c)));

    /// <summary>
    /// Moves the active key of the product (or the shared one) to replaced for <paramref name="reason"/> and makes a new active key under the
    /// passphrase, as Signing keys &gt; Switch will (section 9). Returns the new key's ID.
    /// </summary>
    public string ReplaceActiveKey(ReplacedReason reason = ReplacedReason.Routine)
    {
        using var db = Db();
        var old = db.SigningKeys.Single(k => k.Role == KeyRole.Active && (Shared ? k.ProductId == null : k.ProductId == ProductId));
        old.Role = KeyRole.Replaced;
        old.ReplacedUtc = Utc(2026, 12, 1);
        old.ReplacedReason = reason;

        var made = KeyProtection.CreateEncrypted(Encoding.ASCII.GetBytes(Words), SessionTestHost.Iterations);
        db.SigningKeys.Add(new SigningKeyRecord
        {
            SigningKeyId = made.SigningKeyId,
            ProductId = Shared ? null : ProductId,
            PublicKey = made.PublicKey,
            EncryptedPrivateKeyPem = made.EncryptedPrivateKeyPem,
            Role = KeyRole.Active,
            CreatedUtc = Utc(2026, 12, 1),
        });
        db.SaveChanges();
        return made.SigningKeyId;
    }

    /// <summary>Stores a license <see cref="Reference"/> with the given issue entries (kind, issued, expires; null expires is perpetual), signed by the active key.</summary>
    public void Seed(params (IssueKind Kind, DateTime Issued, DateTime? Expires)[] entries)
    {
        using var db = Db();
        var key = db.SigningKeys.AsNoTracking().Single(k => k.Role == KeyRole.Active && (Shared ? k.ProductId == null : k.ProductId == ProductId));
        db.Licenses.Add(new LicenseRecord
        {
            Reference = "8F3AKM7RXB",
            ProductId = ProductId,
            CustomerEmail = "owner@example.com",
            CustomerEmailKey = "owner@example.com",
            CreatedUtc = entries[0].Issued,
        });
        foreach (var (kind, issued, expires) in entries)
        {
            db.IssueEntries.Add(new IssueEntry
            {
                Reference = "8F3AKM7RXB",
                Kind = kind,
                IssuedUtc = issued,
                ExpiresUtc = expires,
                FeaturesJson = "{}",
                KeyIdentifier = "LIC-8F3AK-M7RXB-ABCD",
                SigningKeyId = key.SigningKeyId,
            });
        }

        db.SaveChanges();
    }
}
