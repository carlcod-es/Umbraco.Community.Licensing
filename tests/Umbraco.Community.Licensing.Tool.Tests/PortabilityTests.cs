using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// Moving an installation and Records can move to another database (vendor-tool-setup), as whole sessions. The records are read with a
/// plain SQLite connection and the library alone: nothing here uses the tool's own readers.
/// </summary>
public class PortabilityTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    public void Dispose() => _disposables.ForEach(d => d.Dispose());

    public static TheoryData<bool> Modes => new() { false, true };

    private LicenseKit Kit(bool shared)
    {
        var kit = new LicenseKit(shared);
        _disposables.Add(kit);
        return kit;
    }

    private SessionTestHost NewHost()
    {
        var host = new SessionTestHost();
        _disposables.Add(host);
        return host;
    }

    /// <summary>Copies every file of <paramref name="source"/> into a folder of its own at <paramref name="target"/>.</summary>
    private static void CopyFolder(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
    }

    /// <summary>The Issue answers of <see cref="LicenseKit.Issue"/>, for a host that is not a kit's: product 1, the passphrase, no saved key, Quit.</summary>
    private static ScriptedConsole IssueSession() =>
        Sign(new ScriptedConsole().Line("2").Line("1").Line("1").Line("2027-03-01T23:59:59Z").Line("").Line("")
            .Line("on").Line("500").Line("").Line("").Line("ORD-2001").Line("moved@example.com"));

    /// <summary>
    /// The copy is started from a current folder that has nothing to do with the source, issues with the same passphrase, and
    /// stores nothing that names the source folder.
    /// </summary>
    private void MoveAndIssue(LicenseKit source, bool sameOperatingSystemLayout)
    {
        source.Issue(orderRef: "ORD-1001", email: "owner@example.com");
        var sourceFolder = source.Host.HomeInstallation;
        var other = NewHost();
        CopyFolder(sourceFolder, other.HomeInstallation);
        Assert.NotEqual(sourceFolder, other.HomeInstallation);
        Assert.NotEqual(other.Home, other.Cwd);
        Assert.DoesNotContain(Path.GetFileName(source.Host.Home), other.Cwd);

        var console = IssueSession();
        var exit = other.Run(console, offer: Startup.Offer.Default);

        Assert.True(exit == 0, console.Err + console.Out);
        Assert.Equal("", console.Err);
        var key = KeyStringOf(console);
        Assert.Contains("ORD-2001", PlainStore.Cells(PlainStore.DatabaseFile(other.HomeInstallation)).Select(c => c.Value));

        // Every product, license and key of the old machine is there, and the new key verifies against the copied key.
        using var connection = PlainStore.Open(PlainStore.DatabaseFile(other.HomeInstallation));
        Assert.Single(PlainStore.Query(connection, "SELECT 1 FROM Products"));
        Assert.Equal(2, PlainStore.Query(connection, "SELECT 1 FROM Licenses").Count);
        Assert.Equal(2, PlainStore.Query(connection, "SELECT 1 FROM IssueEntries").Count);
        var keys = PlainStore.Query(connection, "SELECT PublicKey FROM SigningKeys").Select(r => (string)r[0]!).ToList();
        Assert.NotEmpty(keys);
        var trusted = TrustedSigningKeys.Create(keys.Select(SigningPublicKey.Parse));
        var result = new LicenseEvaluator(source.ProductId, trusted, new FakeTimeProvider()).Evaluate(key);
        Assert.True(result.IsLicensed, result.ToString());

        var cells = PlainStore.Cells(PlainStore.DatabaseFile(other.HomeInstallation));
        Assert.Empty(PlainStore.Holding(cells, PlainStore.LocalNames(sourceFolder, source.Host.Home, source.Host.Cwd, other.HomeInstallation, other.Home, other.Cwd)));
        if (sameOperatingSystemLayout)
            return;

        // As far as one system allows the other: no stored value is shaped like a path of either kind, and the PEMs have \n line endings only.
        Assert.DoesNotContain(cells, c => c.Value.Contains(":\\", StringComparison.Ordinal) || c.Value.Contains("/Users/", StringComparison.Ordinal) || c.Value.Contains("/home/", StringComparison.Ordinal));
        Assert.All(cells.Where(c => c.Column == "EncryptedPrivateKeyPem"), c => Assert.DoesNotContain('\r', c.Value));
    }

    // The real move between operating systems (macOS to Windows, Windows to Linux) is checked by hand in task 13.5; these two tests
    // are what one operating system allows: a copy into another home with an unrelated current folder, and nothing stored that
    // is specific to the source machine or its kind of path.

    [Theory]
    [MemberData(nameof(Modes))]
    public void NewLaptop_TheCopiedFolderWorksFromAnotherHome_AcceptsTheSamePassphrase_AndStoresNoSourcePath(bool shared) =>
        MoveAndIssue(Kit(shared), sameOperatingSystemLayout: true);

    [Theory]
    [MemberData(nameof(Modes))]
    public void MovedToAnotherOperatingSystem_NoStoredValueIsSpecificToTheSourceSystem_AndTheCopyIssuesWithTheSamePassphrase(bool shared) =>
        MoveAndIssue(Kit(shared), sameOperatingSystemLayout: false);

    // --- Records can move to another database

    /// <summary>The product's second product, with feature definitions, added through a session.</summary>
    private static void AddSecondProduct(LicenseKit kit)
    {
        var seed = new ScriptedConsole().Line("1").Line("1").Line("acme.search").Line("Acme Search");
        if (!kit.Shared)
            seed.Hidden(Words);
        kit.Run(seed.Line("0").Line("0"));
        using var db = kit.Db();
        db.FeatureDefinitions.Add(new Data.FeatureDefinition { ProductId = "acme.search", Name = "synonyms", Type = Data.FeatureType.Switch, AddedUtc = Utc(2026, 11, 8) });
        db.SaveChanges();
    }

    private LicenseKit BuiltInstallation(bool shared)
    {
        var kit = Kit(shared);
        AddSecondProduct(kit);
        var key = kit.Issue(displayName: "Acme Shop EU", vendorTag: "reseller#7", note: "Gold plan", shipping: "on", orderRef: "ORD-1001", email: "owner@example.com");
        kit.Run(Sign(ReissueQuestions(new ScriptedConsole(), key, maxOrders: "750", orderRef: "ORD-1002")), ClockAt(Utc(2026, 11, 12)));
        kit.Run(Sign(ResendQuestions(new ScriptedConsole(), key)), ClockAt(Utc(2026, 11, 13)));
        kit.Issue(orderRef: "ORD-1003", email: "other@example.com", maxOrders: "5");
        kit.ReplaceActiveKey(); // a replaced key beside the active one, and the standby the setup made
        return kit;
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void RecordsHoldNoLocalPath_NoStoredValueNamesTheFolderTheHomeOrTheMachine(bool shared)
    {
        var kit = BuiltInstallation(shared);
        var folder = kit.Host.HomeInstallation;

        var cells = PlainStore.Cells(PlainStore.DatabaseFile(folder));

        // The scan reads every table, the EF history and lock tables too, and finds the values it should.
        using (var connection = PlainStore.Open(PlainStore.DatabaseFile(folder)))
        {
            var tables = PlainStore.Tables(connection);
            foreach (var expected in new[] { "Settings", "Products", "FeatureDefinitions", "SigningKeys", "KeyReleases", "Licenses", "IssueEntries", "__EFMigrationsHistory", "__EFMigrationsLock" })
                Assert.Contains(expected, tables);
            foreach (var filled in new[] { "Settings", "Products", "FeatureDefinitions", "SigningKeys", "Licenses", "IssueEntries", "__EFMigrationsHistory" })
                Assert.Contains(cells, c => c.Table == filled);
        }

        Assert.Contains(cells, c => c.Table == "Licenses" && c.Column == "CustomerEmail" && c.Value == "owner@example.com");
        Assert.Contains(cells, c => c.Table == "IssueEntries" && c.Column == "OrderRef" && c.Value == "ORD-1002");
        Assert.Contains(cells, c => c.Table == "SigningKeys" && c.Column == "EncryptedPrivateKeyPem" && c.Value.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal));
        Assert.Empty(PlainStore.Holding(cells, PlainStore.LocalNames(folder, kit.Host.Home, kit.Host.Cwd)));
        // The control: the same scan does find a path when one is there.
        Assert.NotEmpty(PlainStore.Holding([new PlainStore.Cell("T", "C", "x " + folder.ToUpperInvariant().Replace('/', '\\'))], PlainStore.LocalNames(folder)));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void RecordsCompleteWithoutTheFolder_EveryRecordIsThere_AndALicenseIsReissuedFromThemAlone(bool shared)
    {
        var kit = BuiltInstallation(shared);
        // Only the database file: nothing else of the folder.
        var alone = Path.Combine(Path.GetTempPath(), "ucl-alone-" + Guid.NewGuid());
        Directory.CreateDirectory(alone);
        _disposables.Add(new Cleanup(alone));
        var file = Path.Combine(alone, "records.db");
        File.Copy(PlainStore.DatabaseFile(kit.Host.HomeInstallation), file);

        using var connection = PlainStore.Open(file);
        long Count(string table) => (long)PlainStore.Query(connection, $"SELECT COUNT(*) FROM {table}")[0][0]!;
        Assert.Equal(2, Count("Products"));
        Assert.Equal(5, Count("FeatureDefinitions"));
        Assert.Equal(2, Count("Licenses")); // both of the first product
        Assert.Equal(4, Count("IssueEntries"));
        Assert.Equal(2, Count("Licenses WHERE CustomerEmail IS NOT NULL"));
        Assert.Equal(1, Count("IssueEntries WHERE OrderRef = 'ORD-1002'"));
        // Per product an active and a standby key, and for the first the replaced one and its new active key; shared: one set of those.
        Assert.Equal(shared ? 3 : 5, Count("SigningKeys"));
        Assert.Equal(1, Count("Settings WHERE Name = 'PassphraseHash'"));
        Assert.True(Count("SigningKeys WHERE EncryptedPrivateKeyPem LIKE '-----BEGIN ENCRYPTED PRIVATE KEY-----%'") == Count("SigningKeys"));

        // Reissue the first license from the stored values and the passphrase.
        var license = PlainStore.Query(connection, "SELECT Reference, ProductId FROM Licenses WHERE CustomerEmail = 'owner@example.com'").Single();
        var reference = (string)license[0]!;
        var product = (string)license[1]!;
        var prefix = (string)PlainStore.Query(connection, "SELECT Value FROM Settings WHERE Name = 'Prefix'")[0][0]!;
        var entry = PlainStore.Query(
            connection,
            "SELECT ExpiresUtc, DisplayName, VendorTag, FeaturesJson FROM IssueEntries WHERE Reference = $r ORDER BY Id DESC LIMIT 1",
            ("$r", reference)).Single();
        var expires = DateTime.Parse((string)entry[0]!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var features = FeatureList.Empty;
        using (var json = JsonDocument.Parse((string)entry[3]!))
        {
            foreach (var property in json.RootElement.EnumerateObject())
            {
                features = property.Value.ValueKind switch
                {
                    JsonValueKind.True => features.With(property.Name, true),
                    JsonValueKind.Number => features.With(property.Name, property.Value.GetDecimal()),
                    _ => features.With(property.Name, property.Value.GetString()!),
                };
            }
        }

        var keyRows = PlainStore.Query(
            connection,
            "SELECT SigningKeyId, PublicKey, EncryptedPrivateKeyPem, Role FROM SigningKeys WHERE ProductId = $p OR ProductId IS NULL",
            ("$p", product));
        var active = Assert.Single(keyRows, r => (string)r[3]! == "active");
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromEncryptedPem((string)active[2]!, Encoding.ASCII.GetBytes(Words));
        using var privateKey = SigningPrivateKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());
        Assert.Equal((string)active[0]!, privateKey.SigningKeyId);

        var issued = new LicenseIssuer(prefix.Length == 0 ? KeyPrefix.None : KeyPrefix.Of(prefix), ClockAt(Utc(2026, 12, 2))).Issue(
            new LicenseRequest
            {
                Product = product,
                Reference = reference,
                Expiry = LicenseExpiry.At(expires),
                DisplayName = entry[1] as string,
                VendorTag = entry[2] as string,
                Features = features,
            },
            privateKey);

        var trusted = TrustedSigningKeys.Create(keyRows.Select(r => SigningPublicKey.Parse((string)r[1]!)));
        var result = new LicenseEvaluator(product, trusted, ClockAt(Utc(2026, 12, 2))).Evaluate(issued.KeyString);
        Assert.True(result.IsLicensed, result.ToString());
        Assert.Equal(reference, issued.Reference.Replace("LIC-", "").Replace("-", ""));
        Assert.EndsWith(reference[5..], result.License!.Reference);
        Assert.Equal("Acme Shop EU", result.License.DisplayName);
        Assert.Equal("reseller#7", result.License.VendorTag);
        Assert.Equal(expires, result.License.Expires!.Value.UtcDateTime);
        Assert.Equal(750, result.GetNumber("max-orders"));
        Assert.True(result.HasSwitch("shipping"));
        Assert.Equal("Gold plan", result.GetText("note"));
    }

    private sealed class Cleanup(string folder) : IDisposable
    {
        public void Dispose() => Directory.Delete(folder, recursive: true);
    }
}
