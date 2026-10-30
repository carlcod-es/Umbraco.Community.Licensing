using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class PrefixTests
{
    private static ToolDbContext Open(ScriptedTool tool) => Installation.Open(Path.GetFullPath(tool.HomeInstallation));

    /// <summary>A run with no passphrase anywhere and a stdin that fails when read.</summary>
    private static (int Exit, ScriptedTool Run) Prefix(ScriptedTool tool, params string[] args)
    {
        var run = tool.Another(stdinReader: new UnreadableReader());
        return (run.Run(["prefix", .. args]), run);
    }

    private static string Setting(ScriptedTool tool, string name)
    {
        using var db = Open(tool);
        return db.Settings.Single(s => s.Name == name).Value;
    }

    /// <summary>Everything except the prefix row: licenses, entries, keys and the other settings.</summary>
    private static string Records(ScriptedTool tool)
    {
        using var db = Open(tool);
        var licenses = db.Licenses.AsEnumerable().OrderBy(l => l.Reference)
            .Select(l => $"{l.Reference}|{l.ProductId}|{l.CustomerEmail}|{l.CreatedUtc:O}");
        var entries = db.IssueEntries.AsEnumerable().OrderBy(e => e.Id)
            .Select(e => $"{e.Id}|{e.Reference}|{e.Kind}|{e.IssuedUtc:O}|{e.ExpiresUtc:O}|{e.DisplayName}|{e.VendorTag}|{e.FeaturesJson}|{e.OrderRef}|{e.KeyIdentifier}|{e.SigningKeyId}");
        var keys = db.SigningKeys.AsEnumerable().OrderBy(k => k.SigningKeyId)
            .Select(k => $"{k.SigningKeyId}|{k.ProductId}|{k.PublicKey}|{k.EncryptedPrivateKeyPem}|{k.Role}|{k.CreatedUtc:O}|{k.ReplacedUtc:O}|{k.ReplacedReason}");
        var settings = db.Settings.AsEnumerable().Where(s => s.Name != "KeyPrefix").OrderBy(s => s.Name)
            .Select(s => $"{s.Name}={s.Value}");
        return string.Join('\n', licenses.Concat(entries).Concat(keys).Concat(settings));
    }

    private static string Issue(ScriptedTool tool)
    {
        var (exit, run) = LicenseIssueTests.Run(tool, "license", "issue", "acme.commerce", "--expires", "2027-03-01T23:59:59Z");
        using (run)
        {
            Assert.True(exit == 0, run.Stderr);
            return LicenseIssueTests.KeyOf(run);
        }
    }

    [Fact]
    public void Show_prints_the_prefix_to_stdout()
    {
        using var tool = ScriptedTool.Installed();

        var (exit, run) = Prefix(tool, "show");
        using (run)
        {
            Assert.Equal(0, exit);
            Assert.Equal("LIC" + System.Environment.NewLine, run.Stdout);
            Assert.Contains("Using ", run.Stderr);
        }
    }

    [Fact]
    public void Show_prints_none_when_there_is_no_prefix()
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Prefix: "none"));

        var (exit, run) = Prefix(tool, "show");
        using (run)
        {
            Assert.Equal(0, exit);
            Assert.Equal("none" + System.Environment.NewLine, run.Stdout);
        }
    }

    [Fact]
    public void Prefix_changed()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var oldKey = Issue(tool);
        var oldReference = oldKey[4..15];
        var before = Records(tool);

        var (exit, run) = Prefix(tool, "set", "ACME");
        using (run)
        {
            Assert.Equal(0, exit);
            Assert.Equal("", run.Stdout);
            Assert.Contains("Prefix changed from LIC to ACME.", run.Stderr);
            Assert.Contains("New issues and reissues use ACME-. Existing licenses keep their prefix until reissued.", run.Stderr);
        }

        Assert.Equal("ACME", Setting(tool, "KeyPrefix"));
        Assert.Equal(before, Records(tool));

        var newKey = Issue(tool);
        Assert.StartsWith("ACME-", newKey);
        using (var db = Open(tool))
        {
            Assert.Equal(oldKey[..oldKey.IndexOf('.')], db.IssueEntries.AsEnumerable().First().KeyIdentifier);
        }

        var (reissueExit, reissue) = LicenseIssueTests.Run(tool, "license", "reissue", "ACME-" + oldReference, "--expires", "perpetual");
        using (reissue)
        {
            Assert.True(reissueExit == 0, reissue.Stderr);
            Assert.StartsWith($"ACME-{oldReference}-", LicenseIssueTests.KeyOf(reissue));
        }

        using var check = Open(tool);
        Assert.Equal(oldKey[..oldKey.IndexOf('.')], check.IssueEntries.AsEnumerable().First().KeyIdentifier);
    }

    [Fact]
    public void Earlier_prefix_typed()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var key = Issue(tool);
        var reference = key[4..15];
        Assert.StartsWith("LIC-", key);
        (var changed, var change) = Prefix(tool, "set", "ACME");
        change.Dispose();
        Assert.Equal(0, changed);

        var earlier = tool.Another();
        Assert.Equal(1, earlier.Run("license", "show", "LIC-" + reference));
        Assert.Contains("Unknown license LIC-" + reference, earlier.Stderr);
        Assert.Contains("The configured prefix is ACME", earlier.Stderr);
        Assert.Equal("", earlier.Stdout);

        var bare = tool.Another();
        Assert.Equal(0, bare.Run("license", "show", reference));
        Assert.Contains(reference, bare.Stdout);

        var current = tool.Another();
        Assert.Equal(0, current.Run("license", "show", "ACME-" + reference));
    }

    [Fact]
    public void Earlier_prefix_typed_when_the_prefix_is_removed()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var reference = Issue(tool)[4..15];
        (var changed, var change) = Prefix(tool, "set", "none");
        change.Dispose();
        Assert.Equal(0, changed);

        var earlier = tool.Another();
        Assert.Equal(1, earlier.Run("license", "show", "LIC-" + reference));
        Assert.Contains("No prefix is configured", earlier.Stderr);
        Assert.Equal(0, tool.Another().Run("license", "show", reference));
    }

    [Theory]
    [InlineData("acme-shop")]
    [InlineData("lic")]
    [InlineData("WAYTOOLONGPREFIXX")]
    [InlineData("AC ME")]
    public void Invalid_prefix_on_change(string typed)
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        Issue(tool);
        var before = Records(tool);

        var (exit, run) = Prefix(tool, "set", typed);
        using (run)
        {
            Assert.Equal(1, exit);
            Assert.Contains(PrefixInput.RuleText, run.Stderr);
            Assert.Contains("The prefix is unchanged.", run.Stderr);
            Assert.Equal("", run.Stdout);
        }

        Assert.Equal("LIC", Setting(tool, "KeyPrefix"));
        Assert.Equal(before, Records(tool));
    }

    [Fact]
    public void Setting_the_prefix_to_none_and_back()
    {
        using var tool = ScriptedTool.Installed();

        var (noneExit, none) = Prefix(tool, "set", "NONE");
        using (none)
        {
            Assert.Equal(0, noneExit);
            Assert.Contains("Prefix changed from LIC to none.", none.Stderr);
            Assert.Contains("use no prefix", none.Stderr);
        }

        Assert.Equal("", Setting(tool, "KeyPrefix"));

        var (backExit, back) = Prefix(tool, "set", "LIC");
        using (back)
        {
            Assert.Equal(0, backExit);
        }

        Assert.Equal("LIC", Setting(tool, "KeyPrefix"));
    }

    [Fact]
    public void Setting_the_same_prefix_changes_nothing()
    {
        using var tool = ScriptedTool.Installed();

        var (exit, run) = Prefix(tool, "set", "LIC");
        using (run)
        {
            Assert.Equal(0, exit);
            Assert.Contains("already LIC. Nothing changed.", run.Stderr);
        }
    }

    [Fact]
    public void Set_needs_no_passphrase_and_never_changes_the_mode()
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Shared: true));
        var before = Records(tool);

        // Stdin fails when read; the hidden-key queue is empty; no UCL_PASSPHRASE; nobody asked.
        var (exit, run) = Prefix(tool, "set", "ACME");
        using (run)
        {
            Assert.Equal(0, exit);
            Assert.DoesNotContain("assphrase", run.Stderr);
        }

        Assert.Equal("ACME", Setting(tool, "KeyPrefix"));
        Assert.Equal("Shared", Setting(tool, "SigningKeyMode"));
        Assert.Equal(before, Records(tool));
    }

    [Fact]
    public void Missing_prefix_at_a_terminal_is_asked_for()
    {
        using var tool = ScriptedTool.Installed();

        using var run = tool.Another(stdin: "ACME\n", stdinRedirected: false);
        Assert.Equal(0, run.Run("prefix", "set"));
        Assert.Contains("Key prefix (LIC, your own, or none): ", run.Stderr);
        Assert.Equal("ACME", Setting(tool, "KeyPrefix"));
    }

    [Fact]
    public void Missing_prefix_with_nobody_present_is_refused_naming_it()
    {
        using var tool = ScriptedTool.Installed();

        var (exit, run) = Prefix(tool, "set");
        using (run)
        {
            Assert.Equal(1, exit);
            Assert.Contains("Missing <prefix>", run.Stderr);
        }

        Assert.Equal("LIC", Setting(tool, "KeyPrefix"));
    }

    [Fact]
    public void Prefix_commands_refuse_without_an_installation()
    {
        using var tool = new ScriptedTool();

        Assert.Equal(1, tool.Run("prefix", "show"));
        Assert.Contains("No installation found", tool.Stderr);
    }

    private sealed class UnreadableReader : TextReader
    {
        public override int Peek() => throw new InvalidOperationException("stdin was read");

        public override int Read() => throw new InvalidOperationException("stdin was read");

        public override string ReadToEnd() => throw new InvalidOperationException("stdin was read");

        public override string ReadLine() => throw new InvalidOperationException("stdin was read");
    }
}
