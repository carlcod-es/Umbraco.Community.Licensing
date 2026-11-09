using Umbraco.Community.Licensing.Tool.Data;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Key strings are not stored (vendor-tool-licenses): every file of the folder is scanned for every key string shown, as whole sessions.</summary>
public class KeyStringScanTests : IDisposable
{
    private readonly List<LicenseKit> _kits = [];

    public void Dispose() => _kits.ForEach(k => k.Dispose());

    public static TheoryData<bool> Modes => new() { false, true };

    private static bool InAnyFile(List<(string Path, byte[] Bytes)> files, string text)
    {
        var needle = System.Text.Encoding.UTF8.GetBytes(text);
        return files.Any(f => f.Bytes.AsSpan().IndexOf(needle) >= 0);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void CopiedFolder_AfterIssueReissueRenewAndResend_NoFileHoldsAnyKeyString(bool shared)
    {
        var kit = new LicenseKit(shared);
        _kits.Add(kit);
        var keys = new List<string>();

        var a1 = kit.Issue(orderRef: "ORD-1001", displayName: "Acme Shop EU");
        keys.Add(a1);
        keys.Add(KeyStringOf(kit.Run(Sign(ReissueQuestions(new ScriptedConsole(), a1, maxOrders: "750")))));
        var a3 = KeyStringOf(kit.Run(Sign(ResendQuestions(new ScriptedConsole(), a1))));
        keys.Add(a3);
        var b1 = kit.Issue(orderRef: "ORD-1002", email: "other@example.com");
        keys.Add(b1);
        keys.Add(KeyStringOf(kit.Run(Sign(RenewQuestions(new ScriptedConsole(), b1, expiry: "2028-03-01T23:59:59Z")), ClockAt(Utc(2026, 11, 20)))));
        keys.Add(KeyStringOf(kit.Run(Sign(ResendQuestions(new ScriptedConsole(), a3)))));

        Assert.Equal(6, keys.Count);
        Assert.Equal(6, keys.Distinct().Count());
        Assert.Equal(6, kit.Entries().Count);
        var files = PlainStore.FolderFiles(kit.Host.HomeInstallation);

        foreach (var key in keys)
        {
            Assert.False(InAnyFile(files, key), "A file holds a whole key string.");
            // The part after the key identifier is the payload and signature; none of it is kept either.
            var rest = key[(key.IndexOf('.') + 1)..];
            Assert.False(InAnyFile(files, rest), "A file holds the payload and signature of a key string.");
            Assert.False(InAnyFile(files, rest[..24]), "A file holds the start of a key string's payload.");
        }

        // Positive control: the scan finds what is stored, the key identifier that starts each key string.
        foreach (var entry in kit.Entries())
            Assert.True(InAnyFile(files, entry.KeyIdentifier));
        Assert.True(InAnyFile(files, "owner@example.com"));
    }
}
