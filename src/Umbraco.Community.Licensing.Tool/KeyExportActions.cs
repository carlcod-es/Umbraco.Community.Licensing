using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Signing keys &gt; Export by signing key (vendor-tool-signing-keys, PDR-0035, ADR-0015): a CSV of the issue entries one key signed, to a
/// file the vendor names. The reference column is the display form the vendor sees elsewhere, under the installation's prefix. Needs no passphrase; the file holds no key string, which the database does not hold either.
/// </summary>
internal static class KeyExportActions
{
    internal const string NoKeys = "There are no signing keys to export for.";
    internal const string Header = "reference,key_identifier,product,issued_utc,expires_utc";

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static void Register(ActionTable table) => table.Register("keys.export", Export);

    internal static void Export(Session session)
    {
        var console = session.Console;
        List<SigningKeyRecord> keys;
        using (var db = session.OpenDb())
            keys = [.. db.SigningKeys.AsNoTracking().ToList()
                .OrderBy(k => k.ProductId is null ? "" : k.ProductId, StringComparer.Ordinal)
                .ThenBy(k => k.CreatedUtc).ThenBy(k => k.SigningKeyId, StringComparer.Ordinal)];

        if (keys.Count == 0)
        {
            console.WriteLine(NoKeys);
            return;
        }

        var choice = session.Questions.AskChoice(
            "Signing key",
            [.. keys.Select(k => new Choice(k.SigningKeyId, $"{k.SigningKeyId} - {TrustedKeyActions.RoleOf(k)} - {(k.ProductId is null ? "shared" : k.ProductId)}"))]);

        var prefix = LicenseActions.ReadPrefix(session);
        var formatPrefix = prefix.Value.Length == 0 ? null : prefix.Value;
        List<Row> rows;
        using (var db = session.OpenDb())
        {
            var entries = db.IssueEntries.AsNoTracking().Where(e => e.SigningKeyId == choice.Id).ToList();
            var references = entries.Select(e => e.Reference).Distinct().ToList();
            var products = db.Licenses.AsNoTracking().Where(l => references.Contains(l.Reference)).Select(l => new { l.Reference, l.ProductId }).ToList()
                .ToDictionary(l => l.Reference, l => l.ProductId, StringComparer.Ordinal);
            rows = [.. entries.OrderBy(e => e.IssuedUtc).ThenBy(e => e.Id)
                .Select(e => new Row(KeyIdentifierFormat.DisplayReference(formatPrefix, e.Reference), e.KeyIdentifier, products.GetValueOrDefault(e.Reference) ?? "", e.IssuedUtc, e.ExpiresUtc))];
        }

        var files = new NamedFiles(session.Environment, session.Questions);
        var path = files.AskAndWrite("File to export to", stream => WriteCsv(stream, rows));
        console.WriteLine($"Exported {rows.Count} row{(rows.Count == 1 ? "" : "s")} for signing key {choice.Id} to {path}.");
    }

    internal sealed record Row(string Reference, string KeyIdentifier, string Product, DateTime IssuedUtc, DateTime? ExpiresUtc);

    /// <summary>RFC 4180: UTF-8 without a byte order mark, CRLF after every line, the header first, <c>perpetual</c> for no expiry.</summary>
    internal static void WriteCsv(Stream stream, IEnumerable<Row> rows)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };
        writer.WriteLine(Header);
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',', [
                Field(row.Reference),
                Field(row.KeyIdentifier),
                Field(row.Product),
                Field(Time(row.IssuedUtc)),
                Field(row.ExpiresUtc is { } e ? Time(e) : TypedValues.PerpetualWord),
            ]));
        }
    }

    private static string Time(DateTime utc) => utc.ToString(TimeFormat, CultureInfo.InvariantCulture);

    /// <summary>A CSV field: quoted when it holds a comma, quote, CR or LF, with quotes doubled.</summary>
    internal static string Field(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
