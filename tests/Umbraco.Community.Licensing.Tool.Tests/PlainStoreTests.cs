using Microsoft.Data.Sqlite;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>The machine-name matcher of <see cref="PlainStore"/> proves itself: it must hold a real name and must not hit encoded text.</summary>
public class PlainStoreTests
{
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("on abc now", "abc")]
    [InlineData("/abc/x", "abc")]
    [InlineData("a-abc-b", "abc")]
    [InlineData("ABC", "abc")]
    [InlineData("host abc", "ABC")]
    [InlineData("xabcx abc", "abc")]
    [InlineData("xabcx abc end", "abc")]
    [InlineData("abcx abc", "abc")]
    public void HoldsToken_FindsTheNameAsAToken(string value, string name) => Assert.True(PlainStore.HoldsToken(value, name));

    [Theory]
    [InlineData("xabcx", "abc")]
    [InlineData("QUJDabcREVG", "abc")]
    [InlineData("abcx", "abc")]
    [InlineData("xabc", "abc")]
    [InlineData("1abc2 xabc abcx", "abc")]
    [InlineData("", "abc")]
    public void HoldsToken_IgnoresTheNameInsideAnAlphanumericRun(string value, string name) => Assert.False(PlainStore.HoldsToken(value, name));

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    [InlineData("")]
    public void HoldsToken_DoesNotScanANameShorterThanTheMinimum(string name)
    {
        Assert.True(name.Length < PlainStore.MinMachineNameLength);
        Assert.False(PlainStore.HoldsToken(name + " " + name, name));
    }

    [Fact]
    public void Holding_ReportsACellHoldingTheMachineNameAsAToken()
    {
        var name = Environment.MachineName;
        Assert.SkipWhen(name.Length < PlainStore.MinMachineNameLength, "machine name is too short to scan");

        var cells = new[] { new PlainStore.Cell("Products", "DisplayName", $"built on {name}!") };

        Assert.Equal(["Products.DisplayName"], PlainStore.Holding(cells, []));
    }

    [Fact]
    public void Holding_ReportsAnyTextColumn_ButNotEncodedOnes()
    {
        var cells = new[]
        {
            new PlainStore.Cell("Products", "DisplayName", "see abc here"),
            new PlainStore.Cell("SigningKeys", "PublicKey", "abc", Encoded: true),
            new PlainStore.Cell("Settings", "Value", "abc", Encoded: true),
            new PlainStore.Cell("Settings", "Value", "xabcx"),
        };

        Assert.Equal(["Products.DisplayName"], PlainStore.Holding(cells, [], "abc"));
    }

    [Fact]
    public void Holding_StillScansEncodedCellsForPathNeedles()
    {
        var cells = new[] { new PlainStore.Cell("SigningKeys", "PublicKey", "x /home/me/store y", Encoded: true) };

        Assert.Equal(["SigningKeys.PublicKey"], PlainStore.Holding(cells, ["/home/me/store"], "abc"));
    }

    [Fact]
    public void Holding_SkipsAMachineNameShorterThanTheMinimum()
    {
        Assert.Empty(PlainStore.Holding([new PlainStore.Cell("T", "C", "ab ab")], [], "ab"));
    }

    [Fact]
    public void EncodedColumns_AreRealColumnsOfARealDatabase_AndEncodedSettingsAreRealSettingsRows()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ucl-plainstore-" + Guid.NewGuid());
        try
        {
            SessionTestHost.MakeInstallation(folder, passphrase: SessionTestHost.SetupPassphrase);
            using var connection = PlainStore.Open(PlainStore.DatabaseFile(folder));

            var missing = PlainStore.EncodedColumns
                .Where(named =>
                {
                    var (table, column) = (named[..named.IndexOf('.')], named[(named.IndexOf('.') + 1)..]);
                    return !PlainStore.Query(connection, $"PRAGMA table_info(\"{table}\")").Any(r => (string)r[1]! == column);
                })
                .ToList();
            Assert.Empty(missing);

            var settings = PlainStore.Query(connection, "SELECT Name FROM Settings").Select(r => (string)r[0]!).ToList();
            Assert.All(PlainStore.EncodedSettings, name => Assert.Contains(name, settings));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }
}
