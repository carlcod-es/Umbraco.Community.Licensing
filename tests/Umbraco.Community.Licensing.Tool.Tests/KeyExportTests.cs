using System.Text;
using Umbraco.Community.Licensing.Tool.Data;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Signing keys &gt; Export by signing key (vendor-tool-signing-keys, ADR-0015), as whole sessions through <c>ToolApp.Run</c>.</summary>
public class KeyExportTests : IDisposable
{
    private readonly LicenseKit _kit = new(shared: false);

    public void Dispose() => _kit.Dispose();

    private string ActiveKeyId()
    {
        using var db = _kit.Db();
        return db.SigningKeys.Single(k => k.Role == KeyRole.Active).SigningKeyId;
    }

    /// <summary>Main menu 3 (Signing keys), 7 (Export by signing key), the key, then the path answers, then Quit.</summary>
    private ScriptedConsole Export(string keyId, params string[] paths)
    {
        var console = new ScriptedConsole().Line("3").Line("7").Line(keyId);
        foreach (var path in paths)
            console.Line(path);
        return _kit.Run(Quit(console));
    }

    private string PathOf(string name) => Path.Combine(_kit.Host.Cwd, name);

    private static string Shown(string reference) => $"LIC-{reference[..5]}-{reference[5..]}";

    private static string[] Rows(string file) => File.ReadAllText(file).Split("\r\n");

    // --- Leak export

    [Fact]
    public void LeakExport_HoldsOnlyTheLeakedKeysRows_WithTheRightColumns_AndNoKeyString()
    {
        var keys = new List<string>();
        var a1 = _kit.Issue(orderRef: "ORD-1");
        keys.Add(a1);
        keys.Add(KeyStringOf(_kit.Run(Sign(ReissueQuestions(new ScriptedConsole(), a1, maxOrders: "750")))));
        keys.Add(_kit.Issue(orderRef: "ORD-2", email: "other@example.com", expiry: "2028-01-31T12:30:00Z"));
        var leaked = ActiveKeyId();
        var entries = _kit.Entries();
        Assert.Equal(3, entries.Count);

        var replacement = _kit.ReplaceActiveKey(ReplacedReason.Leak);
        keys.Add(_kit.Issue(orderRef: "ORD-3", email: "third@example.com")); // signed by the replacement
        Assert.Equal(replacement, _kit.Entries()[3].SigningKeyId);

        var console = Export(leaked, "leak.csv");

        var file = PathOf("leak.csv");
        Assert.Contains($"Exported 3 rows for signing key {leaked} to {file}.", console.Out);
        var lines = Rows(file);
        Assert.Equal(["reference,key_identifier,product,issued_utc,expires_utc"], lines.Take(1));
        Assert.Equal("", lines[^1]); // CRLF after the last row
        var data = lines[1..^1];
        Assert.Equal(3, data.Length);
        for (var i = 0; i < 3; i++)
        {
            var e = entries[i];
            Assert.Equal($"{Shown(e.Reference)},{e.KeyIdentifier},acme.commerce,{e.IssuedUtc:yyyy-MM-dd'T'HH:mm:ss'Z'},{e.ExpiresUtc:yyyy-MM-dd'T'HH:mm:ss'Z'}", data[i]);
        }

        Assert.Equal(entries[0].Reference, entries[1].Reference);
        Assert.NotEqual(entries[0].Reference, entries[2].Reference);
        Assert.DoesNotContain(_kit.Entries()[3].Reference, File.ReadAllText(file));
        Assert.NotEqual(0xEF, File.ReadAllBytes(file)[0]); // no byte order mark
        var text = File.ReadAllText(file);
        Assert.Equal(4, keys.Distinct().Count());
        foreach (var key in keys)
        {
            Assert.DoesNotContain(key, text);
            Assert.DoesNotContain(key[(key.IndexOf('.') + 1)..][..24], text);
        }

        Assert.DoesNotContain("Passphrase (", console.Out);
        Assert.Empty(console.HiddenGiven);
    }

    [Fact]
    public void KeyList_NamesEveryKeyWithItsRoleAndProduct_AndTheChosenKeysRowsAreAlone()
    {
        _kit.Issue();
        var replaced = ActiveKeyId();
        var fresh = _kit.ReplaceActiveKey(ReplacedReason.Leak);
        _kit.Issue(orderRef: "ORD-9");

        var console = Export(fresh, "fresh.csv");

        Assert.Contains($"{replaced} - replaced (leak)", console.Out);
        Assert.Contains($"{fresh} - active - acme.commerce", console.Out);
        Assert.Contains(" - standby - acme.commerce", console.Out);
        var data = Rows(PathOf("fresh.csv"))[1..^1];
        Assert.Single(data);
        Assert.StartsWith(Shown(_kit.Entries()[1].Reference) + ",", data[0]);
    }

    [Fact]
    public void KeyList_AfterTheReissuesAreSent_NoLongerSaysTheLeakedKeyIsListedAsTrusted()
    {
        _kit.Issue();
        var leaked = ActiveKeyId();
        var fresh = _kit.ReplaceActiveKey(ReplacedReason.Leak);
        using (var db = _kit.Db())
        {
            db.SigningKeys.Single(k => k.SigningKeyId == leaked).ReissuesSentUtc = Utc(2026, 12, 2);
            db.SaveChanges();
        }

        var console = Export(fresh, "fresh.csv");

        Assert.Contains($"{leaked} - replaced (leak), reissues sent - acme.commerce", console.Out);
        Assert.DoesNotContain("LEAKED", console.Out);
    }

    [Fact]
    public void KeyWithNoEntries_WritesTheHeaderOnly_AndSaysZeroRows()
    {
        var console = Export(ActiveKeyId(), "none.csv");

        Assert.Equal("reference,key_identifier,product,issued_utc,expires_utc\r\n", File.ReadAllText(PathOf("none.csv")));
        Assert.Contains("Exported 0 rows for signing key", console.Out);
    }

    [Fact]
    public void SupersededAndExpiredEntries_AreIncluded_AndNoExpiryIsPerpetual()
    {
        _kit.Seed((IssueKind.Issue, Utc(2024, 1, 1), Utc(2024, 12, 31, 23, 59, 59)), (IssueKind.Reissue, Utc(2024, 6, 1, 8, 0, 5), null));

        Export(ActiveKeyId(), "rows.csv");

        var data = Rows(PathOf("rows.csv"))[1..^1];
        Assert.Equal(
            [
                "LIC-8F3AK-M7RXB,LIC-8F3AK-M7RXB-ABCD,acme.commerce,2024-01-01T00:00:00Z,2024-12-31T23:59:59Z",
                "LIC-8F3AK-M7RXB,LIC-8F3AK-M7RXB-ABCD,acme.commerce,2024-06-01T08:00:05Z,perpetual",
            ],
            data);
    }

    [Fact]
    public void ReferenceColumn_IsTheReferenceAsPrintedAtIssue()
    {
        var console = new ScriptedConsole().Line("2").Line("1").Line("1").Line("2027-03-01T23:59:59Z").Line("").Line("").Line("on").Line("500").Line("").Line("").Line("ORD-1").Line("owner@example.com");
        var issue = _kit.Run(Sign(console));
        var printed = System.Text.RegularExpressions.Regex.Match(issue.Out, @"Issued (\S+?)\. The key string").Groups[1].Value;
        Assert.Matches("^LIC-[0-9A-Z]{5}-[0-9A-Z]{5}$", printed);

        Export(ActiveKeyId(), "ref.csv");

        Assert.StartsWith(printed + ",", Rows(PathOf("ref.csv"))[1]);
    }

    [Fact]
    public void ReferenceColumn_HasNoPrefix_WhenThePrefixIsNone()
    {
        using var kit = new LicenseKit(shared: false, prefix: "none");
        kit.Seed((IssueKind.Issue, Utc(2024, 1, 1), null));
        using var db = kit.Db();
        var keyId = db.SigningKeys.Single(k => k.Role == KeyRole.Active).SigningKeyId;

        kit.Run(Quit(new ScriptedConsole().Line("3").Line("7").Line(keyId).Line("n.csv")));

        Assert.StartsWith("8F3AK-M7RXB,", Rows(Path.Combine(kit.Host.Cwd, "n.csv"))[1]);
    }

    // --- Export file already exists

    [Fact]
    public void ExportFileAlreadyExists_AsksForAnotherName_AndLeavesTheFileUnchanged()
    {
        var existing = PathOf("taken.csv");
        var original = new byte[] { 0x00, 0x41, 0xFF, 0x0D, 0x0A };
        File.WriteAllBytes(existing, original);
        _kit.Issue();

        var console = Export(ActiveKeyId(), "taken.csv", "other.csv");

        Assert.Equal(original, File.ReadAllBytes(existing));
        Assert.Contains($"The file {existing} already exists and is not replaced. Give another name.", console.Out);
        Assert.Equal(2, Rows(PathOf("other.csv")).Length - 1); // header and one row
        Assert.Contains($"to {PathOf("other.csv")}.", console.Out);
    }

    [Fact]
    public void MissingParentFolder_Declined_IsAskedAgain_AndNoFolderIsCreated()
    {
        var console = Export(ActiveKeyId(), Path.Combine("nowhere", "k.csv"), "n", "k.csv");

        Assert.Contains($"The folder {PathOf("nowhere")} does not exist.", console.Out);
        Assert.False(Directory.Exists(PathOf("nowhere")));
        Assert.True(File.Exists(PathOf("k.csv")));
    }

    [Fact]
    public void MissingParentFolder_Created_ThenTheFileIsWritten()
    {
        var console = Export(ActiveKeyId(), Path.Combine("a", "b", "k.csv"), "y");

        Assert.True(File.Exists(Path.Combine(PathOf("a"), "b", "k.csv")));
        Assert.Contains($"to {Path.Combine(PathOf("a"), "b", "k.csv")}.", console.Out);
    }

    [Fact]
    public void HomeFolderTypedAsTilde_WritesUnderHome()
    {
        var console = Export(ActiveKeyId(), "~/k.csv");

        var file = Path.Combine(_kit.Host.Home, "k.csv");
        Assert.True(File.Exists(file));
        Assert.Contains($"to {file}.", console.Out);
    }

    [Fact]
    public void PathThatIsAFolder_IsAskedAgain()
    {
        Directory.CreateDirectory(PathOf("out"));

        var console = Export(ActiveKeyId(), "out", "k.csv");

        Assert.Contains($"{PathOf("out")} is a folder. Give a file name.", console.Out);
        Assert.True(File.Exists(PathOf("k.csv")));
        Assert.Empty(Directory.GetFileSystemEntries(PathOf("out")));
    }

    [Fact]
    public void CancelAtThePath_WritesNothing_AndReturnsToTheSigningKeysMenu()
    {
        var console = new ScriptedConsole().Line("3").Line("7").Line(ActiveKeyId()).Line("/cancel").Line("1").Line("").Line("0").Line("0");
        _kit.Run(console);

        Assert.Empty(Directory.GetFiles(_kit.Host.Cwd));
        var after = console.Out[console.Out.LastIndexOf("File to export to", StringComparison.Ordinal)..];
        Assert.Contains("Status", after); // the Signing keys menu is shown again, and Status (1) ran
    }

    // --- CSV fields

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("a\rb", "\"a\rb\"")]
    [InlineData("a\nb", "\"a\nb\"")]
    [InlineData("", "")]
    public void CsvField_IsQuotedOnlyWhenItHoldsACommaQuoteCrOrLf(string value, string expected) =>
        Assert.Equal(expected, KeyExportActions.Field(value));

    [Fact]
    public void WriteCsv_QuotesAProductWithAComma_AndWritesUtf8WithoutABom()
    {
        using var stream = new MemoryStream();
        KeyExportActions.WriteCsv(stream, [new KeyExportActions.Row("R", "K", "café, \"x\"", Utc(2026, 1, 2, 3, 4, 5), null)]);

        var bytes = stream.ToArray();
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("reference,key_identifier,product,issued_utc,expires_utc\r\nR,K,\"café, \"\"x\"\"\",2026-01-02T03:04:05Z,perpetual\r\n", Encoding.UTF8.GetString(bytes));
    }
}
