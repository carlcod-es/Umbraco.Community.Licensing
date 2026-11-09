using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>Licenses &gt; Issue (vendor-tool-licenses), as whole sessions through <see cref="ToolApp.Run(string[], ToolEnvironment, Func{ToolEnvironment, int})"/>, in both signing-key modes.</summary>
public class LicenseIssueTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private const string Id = "acme.commerce";
    private const string Name = "Acme Commerce";
    private const string Expiry = "2027-03-01T23:59:59Z";
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    public static TheoryData<bool> Modes => new() { false, true };

    private int Run(ScriptedConsole console, ActionTable? actions = null, FakeTimeProvider? clock = null)
    {
        var exit = _host.Run(console, actions, Startup.Offer.Default, clock);
        Assert.True(exit == 0, console.Err + console.Out);
        return exit;
    }

    /// <summary>The Licenses menu is 2 in the main menu; there 1 is Issue.</summary>
    private static ScriptedConsole Quit(ScriptedConsole c) => c.Line("0").Line("0");

    /// <summary>
    /// An installation with product <c>acme.commerce</c> (switch ecommerce, number max-orders, text note) made by an earlier
    /// session, so the session under test holds no passphrase. Returns a console with the Licenses menu open.
    /// </summary>
    private ScriptedConsole Open(bool shared, bool features = true)
    {
        _host.SetUpInstallation(shared: shared);
        var seed = new ScriptedConsole().Line("1").Line("1").Line(Id).Line(Name);
        if (!shared)
            seed.Hidden(Words);
        Run(seed.Line("0").Line("0"));

        if (features)
        {
            using var db = Db();
            db.FeatureDefinitions.AddRange(
                Define("ecommerce", FeatureType.Switch), Define("max-orders", FeatureType.Number), Define("note", FeatureType.Text));
            db.SaveChanges();
        }

        return new ScriptedConsole().Line("2");
    }

    private static FeatureDefinition Define(string name, FeatureType type) =>
        new() { ProductId = Id, Name = name, Type = type, AddedUtc = new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc) };

    /// <summary>Chooses Issue and the product and answers the questions up to the summary. The defaults are the "License issued" scenario.</summary>
    private static ScriptedConsole Questions(
        ScriptedConsole c,
        string expiry = Expiry,
        string displayName = "",
        string vendorTag = "",
        string ecommerce = "on",
        string maxOrders = "500",
        string note = "",
        string orderRef = "ORD-1042",
        string email = "owner@example.com") =>
        c.Line("1").Line("1").Line(expiry).Line(displayName).Line(vendorTag).Line(ecommerce).Line(maxOrders).Line(note).Line(orderRef).Line(email);

    /// <summary>The summary answered yes, the passphrase (when the session holds none), and no to saving the key.</summary>
    private static ScriptedConsole Sign(ScriptedConsole c, bool passphrase = true)
    {
        c.Line("");
        if (passphrase)
            c.Hidden(Words);
        return c.Line("");
    }

    private ToolDbContext Db() => Installation.OpenExisting(_host.HomeInstallation).OpenDb();

    private List<LicenseRecord> Licenses()
    {
        using var db = Db();
        return db.Licenses.AsNoTracking().OrderBy(l => l.CreatedUtc).ThenBy(l => l.Reference).ToList();
    }

    private List<IssueEntry> Entries()
    {
        using var db = Db();
        return db.IssueEntries.AsNoTracking().OrderBy(e => e.Id).ToList();
    }

    private static string KeyStringOf(ScriptedConsole console)
    {
        var lines = SessionTestHost.Lines(console.Out);
        var shown = Array.FindIndex(lines, l => l.Contains("The key string is shown once", StringComparison.Ordinal));
        Assert.True(shown >= 0, console.Out);
        return lines[shown + 1];
    }

    private static void AssertZeroed(ScriptedConsole console) =>
        Assert.All(console.HiddenGiven, h => Assert.All(h.Raw, c => Assert.Equal('\0', c)));

    private static void AssertNoPassphraseAsked(ScriptedConsole console)
    {
        Assert.DoesNotContain("Passphrase (", console.Out);
        Assert.Empty(console.HiddenGiven);
    }

    private void AssertNothingStored()
    {
        Assert.Empty(Licenses());
        Assert.Empty(Entries());
    }

    /// <summary>The key string verified by the library, trusting the public keys of the product (its own, or the shared ones).</summary>
    private LicenseResult Evaluate(string keyString, bool shared, FakeTimeProvider? clock = null)
    {
        using var db = Db();
        var keys = db.SigningKeys.AsNoTracking().ToList().Where(k => shared ? k.ProductId is null : k.ProductId == Id);
        var trusted = TrustedSigningKeys.Create(keys.Select(k => SigningPublicKey.Parse(k.PublicKey)));
        return new LicenseEvaluator(Id, trusted, clock ?? new FakeTimeProvider()).Evaluate(keyString);
    }

    // --- License issued

    [Theory]
    [MemberData(nameof(Modes))]
    public void LicenseIssued_ShowsTheKeyOnce_StoresTheRecordAndIssueEntry_AndTheKeyVerifies(bool shared)
    {
        var console = Quit(Sign(Questions(Open(shared))));

        Assert.Equal(0, Run(console));

        var key = KeyStringOf(console);
        Assert.Equal(1, SessionTestHost.Count(console.Out, key)); // shown once
        var license = Assert.Single(Licenses());
        var entry = Assert.Single(Entries());
        Assert.Equal(10, license.Reference.Length);
        Assert.Equal(Id, license.ProductId);
        Assert.Equal("owner@example.com", license.CustomerEmail);
        Assert.Equal("owner@example.com", license.CustomerEmailKey);
        Assert.Equal(DateTimeKind.Utc, license.CreatedUtc.Kind);
        Assert.Equal(license.Reference, entry.Reference);
        Assert.Equal(IssueKind.Issue, entry.Kind);
        Assert.Equal(new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc), entry.IssuedUtc);
        Assert.Equal(new DateTime(2027, 3, 1, 23, 59, 59, DateTimeKind.Utc), entry.ExpiresUtc);
        Assert.Null(entry.DisplayName);
        Assert.Null(entry.VendorTag);
        Assert.Equal("""{"ecommerce":true,"max-orders":500}""", entry.FeaturesJson);
        Assert.Equal("ORD-1042", entry.OrderRef);
        Assert.Equal("ord-1042", entry.OrderRefKey);
        Assert.StartsWith($"LIC-{license.Reference[..5]}-{license.Reference[5..]}-", entry.KeyIdentifier);
        Assert.StartsWith(entry.KeyIdentifier + ".", key);

        using (var db = Db())
        {
            var active = Assert.Single(db.SigningKeys.AsNoTracking().ToList(), k => k.Role == KeyRole.Active && (shared ? k.ProductId is null : k.ProductId == Id));
            Assert.Equal(active.SigningKeyId, entry.SigningKeyId);
        }

        var result = Evaluate(key, shared);
        Assert.True(result.IsLicensed, result.State.ToString());
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.Equal(500m, result.GetNumber("max-orders"));
        Assert.Equal(entry.ExpiresUtc, result.License!.Expires!.Value.UtcDateTime);

        Assert.Contains("Summary", console.Out);
        Assert.Contains("Product: acme.commerce - Acme Commerce", console.Out);
        Assert.Contains("Expiry: 2027-03-01T23:59:59Z", console.Out);
        Assert.Single(console.HiddenGiven);
        AssertZeroed(console);
    }

    [Fact]
    public void LicenseIssued_SummaryComesBeforeThePassphrase_AndQuestionsInTheOrderOfTheDesign()
    {
        var console = Quit(Sign(Questions(Open(shared: false))));

        Run(console);

        var text = console.Out;
        string[] order =
        [
            "Product (", "Expiry (", "Display name (", "Vendor tag (", "Feature ecommerce (switch) (", "Feature max-orders (number) (",
            "Feature note (text) (", "Order reference (", "Customer email (", "Summary", "Sign this license? [Y/n]", "Passphrase (", "Save the key to a file? [y/N]",
        ];
        var at = -1;
        foreach (var marker in order)
        {
            var next = text.IndexOf(marker, at + 1, StringComparison.Ordinal);
            Assert.True(next > at, $"'{marker}' is missing or out of order in:\n{text}");
            at = next;
        }
    }

    [Fact]
    public void LicenseIssued_WithDisplayNameVendorTagAndText_RecordsThemInTheKey()
    {
        var console = Quit(Sign(Questions(Open(shared: false), displayName: "Acme Shop EU", vendorTag: "reseller#7", note: "Gold plan")));

        Run(console);

        var entry = Assert.Single(Entries());
        Assert.Equal("Acme Shop EU", entry.DisplayName);
        Assert.Equal("reseller#7", entry.VendorTag);
        using var json = JsonDocument.Parse(entry.FeaturesJson);
        Assert.Equal("Gold plan", json.RootElement.GetProperty("note").GetString());

        var result = Evaluate(KeyStringOf(console), shared: false);
        Assert.True(result.IsLicensed);
        Assert.Equal("Acme Shop EU", result.License!.DisplayName);
        Assert.Equal("reseller#7", result.License.VendorTag);
        Assert.Equal("Gold plan", result.GetText("note"));
    }

    [Fact]
    public void LicenseIssued_Perpetual_StoresNoExpiry_AndTheKeyDoesNotExpire()
    {
        var console = Quit(Sign(Questions(Open(shared: false), expiry: "PERPETUAL")));

        Run(console);

        Assert.Null(Assert.Single(Entries()).ExpiresUtc);
        Assert.Contains("Expiry: perpetual", console.Out);
        var result = Evaluate(KeyStringOf(console), shared: false);
        Assert.True(result.License!.IsPerpetual);
    }

    [Fact]
    public void LicenseIssued_ADateAlone_ExpiresAtTheEndOfThatDayUtc()
    {
        var console = Quit(Sign(Questions(Open(shared: false), expiry: "2027-03-01")));

        Run(console);

        Assert.Equal(new DateTime(2027, 3, 1, 23, 59, 59, DateTimeKind.Utc), Assert.Single(Entries()).ExpiresUtc);
    }

    [Fact]
    public void LicenseIssued_UsesTheInstallationsCurrentPrefix_AndKeepsItPerEntry()
    {
        var console = Open(shared: false);
        // Settings (5) > Prefix (2): a new prefix, then Back to the main menu and into Licenses.
        console.Line("0").Line("5").Line("2").Line("ACME").Line("0").Line("2");
        Quit(Sign(Questions(console)));

        Run(console);

        var entry = Assert.Single(Entries());
        Assert.StartsWith("ACME-", entry.KeyIdentifier);
        Assert.StartsWith("ACME-", KeyStringOf(console));
        Assert.Equal(10, Assert.Single(Licenses()).Reference.Length);
    }

    // --- Optional inputs skipped

    [Theory]
    [MemberData(nameof(Modes))]
    public void OptionalInputsSkipped_SummaryShowsNone_AndTheLicenseIsIssuedWithoutThem(bool shared)
    {
        var console = Quit(Sign(Questions(Open(shared), ecommerce: "", maxOrders: "", orderRef: "", email: "")));

        Run(console);

        foreach (var line in new[] { "Display name: (none)", "Vendor tag: (none)", "Features: (none)", "Customer email: (none)", "Order reference: (none)" })
            Assert.Contains(line, console.Out);
        var license = Assert.Single(Licenses());
        Assert.Null(license.CustomerEmail);
        Assert.Null(license.CustomerEmailKey);
        var entry = Assert.Single(Entries());
        Assert.Null(entry.DisplayName);
        Assert.Null(entry.VendorTag);
        Assert.Null(entry.OrderRef);
        Assert.Null(entry.OrderRefKey);
        Assert.Equal("{}", entry.FeaturesJson);
        Assert.True(Evaluate(KeyStringOf(console), shared).IsLicensed);
    }

    // --- Feature value outside its type

    [Theory]
    [InlineData("lots")]
    [InlineData("-1")]
    [InlineData("1.23456")]
    [InlineData("1,000")]
    public void FeatureValueOutsideItsType_NamesTheNumberRule_AndAsksThatFeatureAgain(string wrong)
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line(wrong).Line("500").Line("").Line("").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Contains("A number is a number, zero or more", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Feature max-orders (number) ("));
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Feature ecommerce (switch) ("));
        Assert.Equal("""{"ecommerce":true,"max-orders":500}""", Assert.Single(Entries()).FeaturesJson);
    }

    [Fact]
    public void FeatureValueOutsideItsType_SwitchAnswerThatIsNotOnOrOff_IsAskedAgain()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("maybe").Line("on").Line("").Line("").Line("").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Contains(TypedValues.SwitchRule, console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Feature ecommerce (switch) ("));
    }

    [Fact]
    public void FeatureValueOutsideItsType_TextFeatureWithAnEmoji_IsAskedAgain()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("").Line("").Line("star \u2B50").Line("fine").Line("").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Equal(2, SessionTestHost.Count(console.Out, "Feature note (text) ("));
        Assert.Contains(TypedValues.CheckTextFeature("note", "star \u2B50")!, console.Out);
        Assert.Equal("""{"note":"fine"}""", Assert.Single(Entries()).FeaturesJson);
    }

    // --- Not signed at the summary

    [Fact]
    public void NotSignedAtTheSummary_StoresNothing_AsksNoPassphrase_AndShowsTheLicensesMenu()
    {
        var console = Open(shared: false);
        Questions(console).Line("n");
        Quit(console);

        Run(console);

        AssertNothingStored();
        AssertNoPassphraseAsked(console);
        Assert.Contains(LicenseActions.NotSigned, console.Out);
        Assert.DoesNotContain("The key string is shown once", console.Out);
        var afterNo = console.Out[console.Out.IndexOf(LicenseActions.NotSigned, StringComparison.Ordinal)..];
        Assert.Contains("Licenses", afterNo);
        Assert.Contains("1 Issue", afterNo);
    }

    // --- Key saved to a file

    [Fact]
    public void KeySavedToAFile_FileBytesEqualTheKeyString_NoBom_NoLineEnding()
    {
        var console = Open(shared: false);
        Questions(console).Line("").Hidden(Words).Line("y").Line("acme-1042.txt");
        Quit(console);

        Run(console);

        var key = KeyStringOf(console);
        var path = Path.Combine(_host.Cwd, "acme-1042.txt");
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.ASCII.GetBytes(key), bytes);
        Assert.All(bytes, b => Assert.InRange(b, (byte)0x21, (byte)0x7E));
        Assert.Contains($"Saved the key to {path}", console.Out);
        Assert.Equal(1, SessionTestHost.Count(console.Out, key)); // the file name and message do not repeat the key
    }

    [Fact]
    public void KeySavedToAFile_AnExistingFile_IsAskedAgain_AndUnchanged_AsIsAFolderOrAMissingFolder()
    {
        var existing = Path.Combine(_host.Cwd, "taken.txt");
        File.WriteAllText(existing, "keep me");
        Directory.CreateDirectory(Path.Combine(_host.Cwd, "keys"));
        var console = Open(shared: false);
        Questions(console).Line("").Hidden(Words).Line("y")
            .Line("taken.txt").Line("keys").Line(Path.Combine("nowhere", "k.txt")).Line("fresh.txt");
        Quit(console);

        Run(console);

        Assert.Equal("keep me", File.ReadAllText(existing));
        Assert.Contains($"The file {existing} already exists and is not replaced", console.Out);
        Assert.Contains(" is a folder. Give a file name.", console.Out);
        Assert.Contains("does not exist", console.Out);
        Assert.False(Directory.Exists(Path.Combine(_host.Cwd, "nowhere")));
        Assert.Equal(KeyStringOf(console), File.ReadAllText(Path.Combine(_host.Cwd, "fresh.txt")));
        Assert.Single(Licenses());
    }

    [Fact]
    public void KeySavedToAFile_CancelledAtThePath_KeepsTheLicense_AndSaysTheKeyWasNotSaved()
    {
        var console = Open(shared: false);
        Questions(console).Line("").Hidden(Words).Line("y").Line("/cancel");
        Quit(console);

        Run(console);

        Assert.Single(Licenses());
        Assert.Contains(LicenseActions.NotSaved, console.Out);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
    }

    [Fact]
    public void KeySavedToAFile_NotOffered_WhenTheAnswerIsNo_WritesNothing()
    {
        var console = Quit(Sign(Questions(Open(shared: false))));

        Run(console);

        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
    }

    // --- No key string stored

    [Theory]
    [MemberData(nameof(Modes))]
    public void KeyStringsAreNotStored_NoFileOfTheFolderHoldsTheKeyString(bool shared)
    {
        var console = Quit(Sign(Questions(Open(shared))));

        Run(console);

        var key = Encoding.ASCII.GetBytes(KeyStringOf(console));
        var payload = Encoding.ASCII.GetBytes(KeyStringOf(console).Split('.')[1]);
        foreach (var file in Directory.GetFiles(_host.HomeInstallation, "*", SearchOption.AllDirectories))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            Assert.False(bytes.AsSpan().IndexOf(key) >= 0, file);
            Assert.False(bytes.AsSpan().IndexOf(payload) >= 0, file);
        }
    }

    // --- Wrong passphrase

    [Fact]
    public void WrongPassphrase_IsSaidToBeWrong_AndAskedAgain_NothingStoredUntilItIsRight()
    {
        var console = Open(shared: false);
        Questions(console).Line("").Hidden("not the right words").Hidden(Words).Line("");
        Quit(console);

        Run(console);

        Assert.Equal(1, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Passphrase ("));
        Assert.Single(Licenses());
        Assert.Equal(2, console.HiddenGiven.Count);
        AssertZeroed(console);
        Assert.True(Evaluate(KeyStringOf(console), shared: false).IsLicensed);
    }

    [Fact]
    public void WrongPassphrase_ThreeTimes_CancelsTheIssue_NothingStored_NoKeyShown()
    {
        var console = Open(shared: true);
        Questions(console).Line("").Hidden("wrong one 1111111").Hidden("wrong one 2222222").Hidden("wrong one 3333333");
        Quit(console);

        Run(console);

        Assert.Equal(3, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        AssertNothingStored();
        Assert.DoesNotContain("The key string is shown once", console.Out);
        AssertZeroed(console);
    }

    // --- Switch given as off

    [Theory]
    [MemberData(nameof(Modes))]
    public void SwitchGivenAsOff_IsLeftOutOfTheKeyAndTheEntry(bool shared)
    {
        var console = Quit(Sign(Questions(Open(shared), ecommerce: "OFF")));

        Run(console);

        Assert.Equal("""{"max-orders":500}""", Assert.Single(Entries()).FeaturesJson);
        Assert.Contains("Features: max-orders = 500", console.Out);
        Assert.DoesNotContain("ecommerce (on)", console.Out);
        var result = Evaluate(KeyStringOf(console), shared);
        Assert.False(result.HasSwitch("ecommerce"));
        Assert.Equal(500m, result.GetNumber("max-orders"));
    }

    // --- Bad answer asked again

    [Fact]
    public void BadAnswerAskedAgain_ANonexistentDate_NamesTheExpiryRule_AndKeepsTheProduct()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line("2027-02-30").Line(Expiry).Line("").Line("").Line("on").Line("500").Line("").Line("").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Equal(1, SessionTestHost.Count(console.Out, TypedValues.ExpiryRuleMessage));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Expiry ("));
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Product (")); // acme.commerce kept
        Assert.Equal(Id, Assert.Single(Licenses()).ProductId);
    }

    [Theory]
    [InlineData("2020-01-01", "earlier than the current time")]
    [InlineData("tomorrow", "The expiry must be")]
    [InlineData("2027-03-01T23:59:59", "The expiry must be")]
    [InlineData("2027-03-01T23:59:59.5Z", "The expiry must be")]
    [InlineData("", "The expiry must be")]
    public void BadAnswerAskedAgain_ExpiryOutsideTheRule_IsRefused_ThenAccepted(string wrong, string message)
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(wrong).Line(Expiry).Line("").Line("").Line("").Line("").Line("").Line("").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Contains(message, console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Expiry ("));
        Assert.Single(Licenses());
    }

    [Fact]
    public void BadAnswerAskedAgain_VendorTagAndDisplayNameOutsideTheirRules_AreAskedAgain()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("Bad ⭐ name").Line("Good name").Line("bad tag").Line("good-tag").Line("").Line("").Line("").Line("").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Equal(2, SessionTestHost.Count(console.Out, "Display name ("));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Vendor tag ("));
        var entry = Assert.Single(Entries());
        Assert.Equal("Good name", entry.DisplayName);
        Assert.Equal("good-tag", entry.VendorTag);
    }

    // --- Action cancelled

    [Fact]
    public void ActionCancelled_AtTheCustomerEmail_SignsAndStoresNothing_AndShowsTheLicensesMenu()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line("500").Line("").Line("ORD-1042").Line("/cancel");
        Quit(console);

        Run(console);

        AssertNothingStored();
        AssertNoPassphraseAsked(console);
        var afterCancel = console.Out[console.Out.IndexOf("Cancelled.", StringComparison.Ordinal)..];
        Assert.Contains("1 Issue", afterCancel);
        Assert.DoesNotContain("Summary", console.Out);
    }

    // --- Several jobs in one session

    [Theory]
    [MemberData(nameof(Modes))]
    public void SeveralJobsInOneSession_AddAProduct_AddAFeature_IssueThree_AndQuit(bool shared)
    {
        _host.SetUpInstallation(shared: shared);
        var console = new ScriptedConsole().Line("1").Line("1").Line(Id).Line(Name);
        if (!shared)
            console.Hidden(Words);
        console.Line("3").Line(Id).Line("ecommerce").Line("switch")
            .Line("3").Line(Id).Line("max-orders").Line("number")
            .Line("0").Line("2");
        for (var i = 0; i < 3; i++)
        {
            console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line((100 * (i + 1)).ToString()).Line($"ORD-{i}").Line("").Line("");
            if (shared && i == 0)
                console.Hidden(Words); // the first signing asks; the session then holds the passphrase
            console.Line("");
        }

        Quit(console);

        Assert.Equal(0, Run(console));

        var licenses = Licenses();
        Assert.Equal(3, licenses.Count);
        Assert.Equal(3, licenses.Select(l => l.Reference).Distinct().Count());
        Assert.Equal(3, Entries().Count);
        Assert.Equal(["ORD-0", "ORD-1", "ORD-2"], Entries().Select(e => e.OrderRef).Order(StringComparer.Ordinal));
        Assert.Single(console.HiddenGiven); // the passphrase is asked once in the session
        Assert.Equal("", console.Err);
        AssertZeroed(console);
    }

    // --- Customer email and order reference values

    [Theory]
    [InlineData("jürgen@müller.de")] // one character
    [InlineData("jürgen@müller.de")] // letter and combining mark
    public void AccentedEmailAddress_IsStoredExactlyAsTyped_WithTheMatchKeyAlongside(string email)
    {
        var console = Quit(Sign(Questions(Open(shared: false), email: email)));

        Run(console);

        var license = Assert.Single(Licenses());
        Assert.Equal(email, license.CustomerEmail);
        Assert.Equal(TypedValues.MatchKey(email), license.CustomerEmailKey);
        Assert.Equal("jürgen@müller.de", license.CustomerEmailKey); // both forms give the same key
    }

    [Fact]
    public void EmailOutsideTheRule_IsNamedAndAskedAgain_StoringNothingUntilRight()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line("500").Line("").Line("")
            .Line("owner").Line("a@b@example.com").Line("owner @example.com").Line("Owner@Example.com");
        Quit(Sign(console));

        Run(console);

        Assert.Equal(3, SessionTestHost.Count(console.Out, TypedValues.EmailRule));
        Assert.Equal(4, SessionTestHost.Count(console.Out, "Customer email ("));
        var license = Assert.Single(Licenses());
        Assert.Equal("Owner@Example.com", license.CustomerEmail);
        Assert.Equal("owner@example.com", license.CustomerEmailKey);
    }

    [Theory]
    [InlineData("#1042")]
    [InlineData("INV-2026-0042")]
    [InlineData("ORD 1042")]
    [InlineData("UMB:1042")]
    [InlineData("pi_3NqXk2LkdIwHu7ix0yJ8")]
    public void OrderReferencesFromShops_AreStoredExactlyAsTyped(string orderRef)
    {
        var console = Quit(Sign(Questions(Open(shared: false), orderRef: orderRef)));

        Run(console);

        var entry = Assert.Single(Entries());
        Assert.Equal(orderRef, entry.OrderRef);
        Assert.Equal(TypedValues.MatchKey(orderRef), entry.OrderRefKey);
    }

    [Fact]
    public void OrderReferenceOutsideTheRule_IsNamedAndAskedAgain()
    {
        var console = Open(shared: false);
        console.Line("1").Line("1").Line(Expiry).Line("").Line("").Line("on").Line("500").Line("")
            .Line("ORD-1042 ⭐").Line(new string('1', 65)).Line("ORD-1042").Line("");
        Quit(Sign(console));

        Run(console);

        Assert.Equal(3, SessionTestHost.Count(console.Out, "Order reference ("));
        Assert.Contains(TypedValues.CheckOrderReference("ORD-1042 \u2B50")!, console.Out);
        Assert.Contains(TypedValues.CheckOrderReference(new string('1', 65))!, console.Out);
        Assert.Equal("ORD-1042", Assert.Single(Entries()).OrderRef);
    }

    // --- Checks before the summary and a damaged installation

    [Fact]
    public void ARequestTheLibraryRefuses_IsRefusedBeforeTheSummary_NamingTheField_StoringNothing()
    {
        var console = Open(shared: false, features: false);
        using (var db = Db())
        {
            for (var i = 1; i <= 51; i++) // more than Add a feature allows (PDR-0054): an installation from before the limit
                db.FeatureDefinitions.Add(Define($"f{i:00}", FeatureType.Number));
            db.SaveChanges();
        }

        console.Line("1").Line("1").Line(Expiry).Line("").Line("");
        for (var i = 1; i <= 51; i++)
            console.Line("1");
        console.Line("").Line("");
        Quit(console);

        Run(console);

        Assert.Contains("[features] A license may have at most 50 features.", console.Out);
        Assert.DoesNotContain("Summary", console.Out);
        AssertNothingStored();
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void NoProducts_SaysSo_AndAsksNothing()
    {
        _host.SetUpInstallation();
        var console = Quit(new ScriptedConsole().Line("2").Line("1"));

        Run(console);

        Assert.Contains(ProductActions.NoProducts, console.Out);
        AssertNothingStored();
    }

    [Fact]
    public void ActiveKey_PerProduct_NoneOrSeveral_FailsAsADamagedInstallation_AndTheSessionContinues()
    {
        foreach (var damage in new[] { "none", "several" })
        {
            using var host = new SessionTestHost();
            host.SetUpInstallation(shared: false);
            var seed = new ScriptedConsole().Line("1").Line("1").Line(Id).Line(Name).Hidden(Words).Line("0").Line("0");
            Assert.Equal(0, host.Run(seed, offer: Startup.Offer.Default));
            using (var db = Installation.OpenExisting(host.HomeInstallation).OpenDb())
            {
                if (damage == "none")
                {
                    foreach (var key in db.SigningKeys.Where(k => k.ProductId == Id && k.Role == KeyRole.Active))
                        key.Role = KeyRole.Replaced;
                }
                else
                {
                    foreach (var key in db.SigningKeys.Where(k => k.ProductId == Id && k.Role == KeyRole.Standby))
                        key.Role = KeyRole.Active;
                }

                db.SaveChanges();
            }

            var console = Quit(new ScriptedConsole().Line("2").Line("1").Line("1"));

            Assert.Equal(0, host.Run(console, offer: Startup.Offer.Default));

            Assert.Contains("Licenses - Issue failed:", console.Out);
            Assert.Contains("looks damaged", console.Out);
            Assert.DoesNotContain("Expiry (", console.Out);
            using var check = Installation.OpenExisting(host.HomeInstallation).OpenDb();
            Assert.Empty(check.IssueEntries.ToList());
        }
    }

    [Fact]
    public void ActiveKey_Shared_IsTheKeyWithNoProduct_NotAProductsKey()
    {
        var console = Open(shared: true);
        using (var db = Db())
        {
            foreach (var key in db.SigningKeys.Where(k => k.ProductId == null && k.Role == KeyRole.Active))
                key.Role = KeyRole.Replaced;
            db.SaveChanges();
        }

        Quit(console.Line("1").Line("1"));

        Run(console);

        Assert.Contains("Licenses - Issue failed:", console.Out);
        Assert.Contains("no active signing key for the shared signing key", console.Out);
        AssertNothingStored();
    }
}
