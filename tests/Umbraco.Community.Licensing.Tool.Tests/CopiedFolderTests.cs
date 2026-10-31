using System.Text;
using Microsoft.Data.Sqlite;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class CopiedFolderTests
{
    private static string Ok(ScriptedTool tool, params string[] args)
    {
        var (exit, run) = LicenseIssueTests.Run(tool, args);
        using (run)
        {
            Assert.True(exit == 0, run.Stderr);
            return LicenseIssueTests.KeyOf(run);
        }
    }

    private static string Display(string reference) => $"LIC-{reference[..5]}-{reference[5..]}";

    private static bool Holds(byte[] bytes, string text) =>
        bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0
        || bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(text)) >= 0
        || bytes.AsSpan().IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Copied_folder(bool shared)
    {
        using var tool = LicenseIssueTests.WithProducts(shared);
        var (featureExit, feature) = LicenseIssueTests.Run(tool, "product", "feature", "add", "acme.commerce", "shipping", "--type", "switch");
        using (feature)
        {
            Assert.True(featureExit == 0, feature.Stderr);
        }

        var keys = new List<string>();

        keys.Add(Ok(tool, "license", "issue", "acme.commerce", "--expires", "2027-03-01T23:59:59Z",
            "--feature", "ecommerce=on", "--feature", "max-orders=500", "--order-ref", "ORD-CF-1",
            "--email", "owner@example.com"));
        tool.Clock.Advance(TimeSpan.FromMinutes(1));
        keys.Add(Ok(tool, "license", "issue", "acme.commerce", "--expires", "perpetual",
            "--order-ref", "ORD-CF-2", "--email", "other@example.com"));

        // Both licenses were issued by this run, so the references name them in order of creation via their entries.
        string[] references;
        using (var db = Installation.Open(Path.GetFullPath(tool.HomeInstallation)))
        {
            references = db.IssueEntries.AsEnumerable().OrderBy(e => e.IssuedUtc).Select(e => e.Reference).Distinct().ToArray();
        }

        Assert.Equal(2, references.Length);
        var withFeatures = Display(references[0]);
        var perpetual = Display(references[1]);

        keys.Add(Ok(tool, "license", "reissue", withFeatures, "--feature", "shipping=on", "--expires", "2027-03-01T23:59:59Z"));
        keys.Add(Ok(tool, "license", "renew", withFeatures, "--expires", "2028-03-01T23:59:59Z", "--order-ref", "ORD-CF-3"));
        keys.Add(Ok(tool, "license", "resend", perpetual));

        Assert.Equal(5, keys.Count);
        Assert.Equal(5, keys.Distinct().Count());

        // Closes pooled connections so every page, and any journal, is with the files.
        SqliteConnection.ClearAllPools();
        var folder = Path.GetFullPath(tool.HomeInstallation);
        var contents = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(f =>
        {
            using var file = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            file.CopyTo(memory);
            return (Name: Path.GetRelativePath(folder, f), Bytes: memory.ToArray());
        }).ToList();
        Assert.NotEmpty(contents);

        // Positive control: the scan sees values the database does store.
        Assert.Contains(contents, f => Holds(f.Bytes, "ORD-CF-3"));
        Assert.Contains(contents, f => Holds(f.Bytes, "owner@example.com"));
        foreach (var reference in references)
        {
            Assert.Contains(contents, f => Holds(f.Bytes, reference));
        }

        foreach (var (name, bytes) in contents)
        {
            foreach (var key in keys)
            {
                Assert.False(Holds(bytes, key), $"{name} holds a key string");
                // The signed part alone (after the identifier) must not be there either.
                Assert.False(Holds(bytes, key[(key.IndexOf('.') + 1)..]), $"{name} holds a key's signed part");
            }
        }
    }
}
