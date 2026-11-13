using System.Text.Json;
using Acme.ContactForm.Licensing;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>
/// Evaluates every committed demo key (the package's embedded Demo/demo-keys.json) as the sample product would, so a
/// library change that alters how a key reads fails here, naming the entry.
/// </summary>
public class DemoKeyTests
{
    private const string UntrustedKeyId = "3gpjfm6DKNU";

    private static readonly string[] ExpectedNames =
    [
        "Valid: full plan", "Valid: small plan", "Valid: another domain", "Valid: expiring soon", "Expired",
        "No key", "Damaged key", "Another product's key", "Untrusted signer", "Edited key",
    ];

    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();

    public static TheoryData<string> EntryNames => [.. Entries.Select(e => e.Name)];

    private static LicenseEvaluator EvaluatorAt(DateTimeOffset now) => new(
        ContactFormProduct.ProductId,
        ContactFormProduct.TrustedSigningKeys,
        new DemoKeyFile.FixedTimeProvider(now));

    private static DateTimeOffset ClockFor(DemoKeyEntry entry) =>
        entry.DemoDate is { } date ? DemoKeyFile.ParseInstant(date) : DemoKeyFile.FixedClock;

    private static DemoKeyEntry Entry(string name) => Entries.Single(e => e.Name == name);

    [Fact]
    public void The_ten_entries() => Assert.Equal(ExpectedNames, Entries.Select(e => e.Name));

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Every_entry_yields_its_state(string name)
    {
        var entry = Entry(name);
        var result = EvaluatorAt(ClockFor(entry)).Evaluate(entry.Key);

        Assert.True(entry.State == result.State.ToString(), $"{name}: the file says {entry.State} but the library reports {result.State}.");
        Assert.Equal(entry.ProductVerified, result.License is not null);
        Assert.Equal(entry.Product, result.License?.Product ?? result.ClaimedProduct);
        Assert.Equal(entry.Expires, result.License?.Expires is { } e ? DemoKeyFile.FormatInstant(e) : null);

        var actual = result.License?.Features.ToDictionary(f => f.Name, f => DemoKeyFile.ToJson(f.Value).GetRawText()) ?? [];
        var expected = entry.Features.ToDictionary(f => f.Key, f => f.Value.GetRawText());
        Assert.Equal(expected.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));
    }

    [Fact]
    public void Every_entry_has_a_description()
    {
        Assert.All(Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Description), e.Name));
    }

    [Fact]
    public void Expiring_soon_at_its_demo_date()
    {
        var entry = Entry("Valid: expiring soon");
        Assert.NotNull(entry.DemoDate);
        var result = EvaluatorAt(ClockFor(entry)).Evaluate(entry.Key);

        Assert.Equal(LicenseState.Valid, result.State);
        var remaining = result.License!.Expires!.Value - ClockFor(entry);
        Assert.Equal(14, remaining.Days);
    }

    [Fact]
    public void Only_the_expiring_entry_has_a_demo_date() =>
        Assert.Equal(["Valid: expiring soon"], Entries.Where(e => e.DemoDate is not null).Select(e => e.Name));

    [Fact]
    public void The_untrusted_key_is_not_trusted()
    {
        var trusted = ContactFormProduct.TrustedSigningKeys;
        Assert.False(trusted.Contains(UntrustedKeyId));
        Assert.Equal(UntrustedKeyId, DemoKeyFiles.LoadPublic("untrusted.public.txt").SigningKeyId);

        var entry = Entry("Untrusted signer");
        var result = EvaluatorAt(ClockFor(entry)).Evaluate(entry.Key);
        Assert.Equal(LicenseState.SigningKeyNotRecognised, result.State);
        Assert.Equal(UntrustedKeyId, SigningKeyIdOf(entry.Key!));
    }

    [Fact]
    public void Not_supported_not_demonstrated()
    {
        Assert.DoesNotContain(Entries, e => e.State == nameof(LicenseState.NotSupported));

        var readme = File.ReadAllText(Path.Combine(DemoKeyFiles.SamplesRoot, "README.md"));
        var start = readme.IndexOf("## Demo keys", StringComparison.Ordinal);
        Assert.True(start >= 0, "samples/README.md has no 'Demo keys' section.");
        var next = readme.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var section = next < 0 ? readme[start..] : readme[start..next];
        Assert.Contains("NotSupported", section);
        Assert.Contains("not demonstrated", section);
        Assert.All(ExpectedNames, n => Assert.Contains(n, section));
    }

    /// <summary>The signing key ID (the payload's <c>k</c> claim) read from the key's middle part, as the library routes on it.</summary>
    private static string SigningKeyIdOf(string key)
    {
        var payload = key.Split('.')[1];
        var json = System.Buffers.Text.Base64Url.DecodeFromChars(payload);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("k").GetString()!;
    }
}
