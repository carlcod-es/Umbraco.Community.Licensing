using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Changing the prefix (vendor-tool-setup): the scenario "Prefix changed", as whole sessions in both signing-key modes.</summary>
public class PrefixChangeTests : IDisposable
{
    private LicenseKit? _kit;

    public void Dispose() => _kit?.Dispose();

    public static TheoryData<bool> Modes => new() { false, true };

    [Theory]
    [MemberData(nameof(Modes))]
    public void PrefixChanged_NewKeysStartWithTheNewPrefix_TheEarlierRecordIsUnchanged_AndAReissueOfItTakesTheNewPrefix(bool shared)
    {
        var kit = _kit = new LicenseKit(shared, prefix: "LIC");
        var first = kit.Issue(orderRef: "ORD-1001");
        var license = Assert.Single(kit.Licenses());
        var display = $"{license.Reference[..5]}-{license.Reference[5..]}";
        Assert.StartsWith($"LIC-{display}-", first);
        var earlierEntry = Assert.Single(kit.Entries());
        Assert.StartsWith($"LIC-{display}-", earlierEntry.KeyIdentifier);

        // Settings (5) > Prefix (2): ACME; Back; Quit.
        kit.Run(new ScriptedConsole().Line("5").Line("2").Line("ACME").Line("0").Line("0"));
        var second = kit.Issue(orderRef: "ORD-1002", email: "other@example.com");

        var licenses = kit.Licenses();
        var other = Assert.Single(licenses, l => l.Reference != license.Reference);
        var entries = kit.Entries();
        Assert.StartsWith($"ACME-{other.Reference[..5]}-{other.Reference[5..]}-", second);
        Assert.StartsWith($"ACME-{other.Reference[..5]}-{other.Reference[5..]}-", entries.Single(e => e.Reference == other.Reference).KeyIdentifier);

        // The record of the earlier license, and its entry, are as they were.
        var stored = Assert.Single(licenses, l => l.Reference == license.Reference);
        Assert.Equal((license.ProductId, license.CustomerEmail, license.CustomerEmailKey, license.CreatedUtc), (stored.ProductId, stored.CustomerEmail, stored.CustomerEmailKey, stored.CreatedUtc));
        var kept = Assert.Single(entries, e => e.Reference == license.Reference);
        Assert.Equal(
            (earlierEntry.Id, earlierEntry.Kind, earlierEntry.IssuedUtc, earlierEntry.ExpiresUtc, earlierEntry.FeaturesJson, earlierEntry.OrderRef, earlierEntry.KeyIdentifier, earlierEntry.SigningKeyId),
            (kept.Id, kept.Kind, kept.IssuedUtc, kept.ExpiresUtc, kept.FeaturesJson, kept.OrderRef, kept.KeyIdentifier, kept.SigningKeyId));

        // A reissue of the earlier license (pasting its old key) starts ACME-<ref>-.
        var console = kit.Run(Sign(ReissueQuestions(new ScriptedConsole(), first)));
        var reissued = KeyStringOf(console);
        Assert.StartsWith($"ACME-{display}-", reissued);
        Assert.StartsWith($"ACME-{display}-", kit.Entries().Last(e => e.Reference == license.Reference).KeyIdentifier);
        Assert.True(kit.Evaluate(reissued).IsLicensed);
    }
}
