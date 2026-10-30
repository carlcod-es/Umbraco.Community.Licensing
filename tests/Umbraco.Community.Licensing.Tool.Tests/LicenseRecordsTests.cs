using System.Text;
using Microsoft.Data.Sqlite;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class LicenseRecordsTests
{
    private static readonly string NewLine = System.Environment.NewLine;

    private static ToolDbContext Open(ScriptedTool tool) => Installation.Open(Path.GetFullPath(tool.HomeInstallation));

    private static DateTimeOffset At(string iso) =>
        DateTimeOffset.Parse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();

    private static string Display(string reference) => $"LIC-{reference[..5]}-{reference[5..]}";

    /// <summary>Runs a command with the passphrase available and nobody at a terminal; it must succeed.</summary>
    private static void Ok(ScriptedTool tool, params string[] args)
    {
        var (exit, run) = LicenseIssueTests.Run(tool, args);
        using (run)
        {
            Assert.True(exit == 0, run.Stderr);
        }
    }

    /// <summary>Runs a command that needs no passphrase: none is available anywhere.</summary>
    private static (int Exit, ScriptedTool Run) Read(ScriptedTool tool, params string[] args)
    {
        var run = tool.Another();
        return (run.Run(args), run);
    }

    /// <summary>Issues <c>acme.commerce</c> and returns the displayed reference.</summary>
    private static string Issue(ScriptedTool tool, string product, string issuedAt, params string[] extra)
    {
        tool.Clock.Set(At(issuedAt));
        using var db = Open(tool);
        var before = db.Licenses.Select(l => l.Reference).ToHashSet();
        Ok(tool, ["license", "issue", product, "--expires", "2027-01-15T10:00:00Z", .. extra]);
        db.ChangeTracker.Clear();
        return Display(db.Licenses.AsEnumerable().Single(l => !before.Contains(l.Reference)).Reference);
    }

    /// <summary>
    /// <c>LIC-…</c> issued with order reference ORD-1001 and email owner@example.com, then renewed with ORD-1874.
    /// </summary>
    private static (ScriptedTool Tool, string Reference) Renewed()
    {
        var tool = LicenseIssueTests.WithProducts(shared: false);
        var reference = Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z",
            "--order-ref", "ORD-1001", "--email", "owner@example.com", "--feature", "ecommerce=on");
        tool.Clock.Set(At("2026-12-20T00:00:00Z"));
        Ok(tool, "license", "renew", reference, "--expires", "2028-01-15T10:00:00Z", "--order-ref", "ORD-1874");
        return (tool, reference);
    }

    private static List<IssueEntry> Entries(ScriptedTool tool)
    {
        using var db = Open(tool);
        return [.. db.IssueEntries.OrderBy(e => e.Id)];
    }

    /// <summary>Whether <paramref name="text"/> appears anywhere in <c>ucl.db</c>, as UTF-8 or UTF-16.</summary>
    private static bool DatabaseHolds(ScriptedTool tool, string text)
    {
        // Closes pooled connections so every page is with the file.
        SqliteConnection.ClearAllPools();
        var path = Path.Combine(tool.HomeInstallation, Installation.DatabaseFileName);
        byte[] bytes;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var memory = new MemoryStream())
        {
            file.CopyTo(memory);
            bytes = memory.ToArray();
        }

        return bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0
            || bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(text)) >= 0
            || bytes.AsSpan().IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0;
    }

    private static string[] Rows(ScriptedTool run) =>
        run.Stdout.Split(NewLine, StringSplitOptions.RemoveEmptyEntries);

    // ---- Finding and listing licenses

    [Fact]
    public void Find_by_customer_email()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var first = Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z", "--email", "owner@example.com");
        var second = Issue(tool, "acme.shipping", "2026-01-16T10:00:00Z", "--email", "Owner@Example.com");
        var other = Issue(tool, "acme.commerce", "2026-01-17T10:00:00Z", "--email", "someone@example.com");
        Issue(tool, "acme.commerce", "2026-01-18T10:00:00Z");

        var (exit, run) = Read(tool, "license", "list", "--email", "owner@example.com");
        using (run)
        {
            Assert.Equal(0, exit);
            var rows = Rows(run);
            Assert.Equal(2, rows.Length);
            Assert.StartsWith(first + "\t", rows[0]);
            Assert.StartsWith(second + "\t", rows[1]);
            Assert.DoesNotContain(other, run.Stdout);
        }
    }

    [Fact]
    public void An_email_filter_matches_the_whole_address()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z", "--email", "owner@example.com");

        var (exit, run) = Read(tool, "license", "list", "--email", "owner@example");
        using (run)
        {
            Assert.Equal(0, exit);
            Assert.Equal("", run.Stdout);
            Assert.Contains("No licenses match", run.Stderr);
        }
    }

    [Fact]
    public void Find_by_an_earlier_order_reference()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            Issue(tool, "acme.commerce", "2027-01-01T00:00:00Z", "--order-ref", "ORD-2000");

            var (exit, run) = Read(tool, "license", "list", "--order-ref", "ORD-1001");
            using (run)
            {
                Assert.Equal(0, exit);
                var row = Assert.Single(Rows(run)).Split('\t');
                Assert.Equal([reference, "acme.commerce", "2028-01-15T10:00:00Z", "owner@example.com", "ORD-1874"], row);
            }

            var (_, latest) = Read(tool, "license", "list", "--order-ref", "ORD-1874");
            using (latest)
            {
                Assert.StartsWith(reference + "\t", Assert.Single(Rows(latest)));
            }

            var (_, none) = Read(tool, "license", "list", "--order-ref", "ORD-9999");
            using (none)
            {
                Assert.Equal("", none.Stdout);
            }
        }
    }

    [Fact]
    public void Filters_combine_and_a_reference_is_read_under_the_prefix_rule()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var first = Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z", "--email", "owner@example.com");
        var second = Issue(tool, "acme.shipping", "2026-01-16T10:00:00Z", "--email", "owner@example.com");
        var bare = first[4..];

        foreach (var typed in new[] { first, first.ToLowerInvariant(), bare, bare.Replace("-", ""), first.Replace("-", " ") })
        {
            var (exit, run) = Read(tool, "license", "list", "--reference", typed);
            using (run)
            {
                Assert.Equal(0, exit);
                Assert.StartsWith(first + "\t", Assert.Single(Rows(run)));
            }
        }

        var (_, byProduct) = Read(tool, "license", "list", "--product", "acme.shipping", "--email", "owner@example.com");
        using (byProduct)
        {
            Assert.StartsWith(second + "\t", Assert.Single(Rows(byProduct)));
        }

        var (_, disjoint) = Read(tool, "license", "list", "--product", "acme.shipping", "--reference", first);
        using (disjoint)
        {
            Assert.Equal("", disjoint.Stdout);
        }

        var (_, all) = Read(tool, "license", "list");
        using (all)
        {
            Assert.Equal(2, Rows(all).Length);
        }
    }

    [Fact]
    public void A_reference_under_another_prefix_is_not_found()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var reference = Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z");
        var other = "XYZ-" + reference[4..];

        var (exit, list) = Read(tool, "license", "list", "--reference", other);
        using (list)
        {
            Assert.Equal(0, exit);
            Assert.Equal("", list.Stdout);
        }

        var (showExit, show) = Read(tool, "license", "show", other);
        using (show)
        {
            Assert.Equal(1, showExit);
            Assert.Contains("Unknown license", show.Stderr);
        }
    }

    [Fact]
    public void An_empty_filter_value_matches_nothing()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z", "--email", "owner@example.com", "--order-ref", "ORD-1");

        foreach (var option in new[] { "--email", "--order-ref", "--product", "--reference" })
        {
            var (exit, run) = Read(tool, "license", "list", option, "");
            using (run)
            {
                Assert.Equal(0, exit);
                Assert.Equal("", run.Stdout);
            }
        }
    }

    [Fact]
    public void License_history()
    {
        using var tool = LicenseIssueTests.WithProducts(shared: false);
        var reference = Issue(tool, "acme.commerce", "2026-01-15T10:00:00Z",
            "--order-ref", "ORD-1001", "--feature", "ecommerce=on", "--feature", "max-orders=500", "--display-name", "Acme Ltd");
        tool.Clock.Set(At("2026-06-01T00:00:00Z"));
        Ok(tool, "license", "reissue", reference, "--expires", "suggested", "--feature", "region=eu");
        tool.Clock.Set(At("2026-12-20T00:00:00Z"));
        Ok(tool, "license", "renew", reference, "--expires", "2028-01-15T10:00:00Z", "--order-ref", "ORD-1874");
        var entries = Entries(tool);

        var (exit, run) = Read(tool, "license", "show", reference.ToLowerInvariant());
        using (run)
        {
            Assert.Equal(0, exit);
            var text = run.Stdout;
            Assert.StartsWith(reference + NewLine, text);
            Assert.Contains("Product: acme.commerce", text);
            Assert.Contains("Issue entries: 3", text);
            var third = text.IndexOf("#3 renewal", StringComparison.Ordinal);
            var second = text.IndexOf("#2 reissue", StringComparison.Ordinal);
            var first = text.IndexOf("#1 issue", StringComparison.Ordinal);
            Assert.True(third > 0 && second > third && first > second, "newest first");

            var newest = text[third..second];
            Assert.Contains("Issued: 2026-12-20T00:00:00Z", newest);
            Assert.Contains("Expires: 2028-01-15T10:00:00Z", newest);
            Assert.Contains("Order reference: ORD-1874", newest);
            Assert.Contains($"Key identifier: {entries[2].KeyIdentifier}", newest);
            Assert.Contains($"Signing key ID: {entries[2].SigningKeyId}", newest);
            Assert.Contains("Features: ecommerce=on, max-orders=500, region=eu", newest);

            var middle = text[second..first];
            Assert.Contains("Order reference: none", middle);
            Assert.Contains($"Key identifier: {entries[1].KeyIdentifier}", middle);
            Assert.Contains("Features: ecommerce=on, max-orders=500, region=eu", middle);

            var oldest = text[first..];
            Assert.Contains("Order reference: ORD-1001", oldest);
            Assert.Contains("Display name: Acme Ltd", oldest);
            Assert.Contains("Features: ecommerce=on, max-orders=500", oldest);
            Assert.DoesNotContain("region", oldest);
            Assert.Contains($"Key identifier: {entries[0].KeyIdentifier}", oldest);
        }
    }

    // ---- Customer details editable and clearable

    [Fact]
    public void Renewal_keeps_the_earlier_order_reference()
    {
        var (tool, _) = Renewed();
        using (tool)
        {
            var entries = Entries(tool);
            Assert.Equal(["ORD-1001", "ORD-1874"], entries.Select(e => e.OrderRef));
        }
    }

    [Fact]
    public void Correcting_the_latest_order_reference()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            Assert.True(DatabaseHolds(tool, "ORD-1874"));

            var (exit, run) = Read(tool, "license", "customer", reference, "--order-ref", "ORD-1875");
            using (run)
            {
                Assert.Equal(0, exit);
                Assert.Equal("", run.Stdout);
                Assert.Contains("latest order reference corrected", run.Stderr);
            }

            Assert.Equal(["ORD-1001", "ORD-1875"], Entries(tool).Select(e => e.OrderRef));
            Assert.False(DatabaseHolds(tool, "ORD-1874"));
            Assert.True(DatabaseHolds(tool, "ORD-1875"));
        }
    }

    [Fact]
    public void Clearing_order_references()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            Assert.True(DatabaseHolds(tool, "ORD-1001"));

            var (exit, run) = Read(tool, "license", "customer", reference, "--clear-order-refs");
            using (run)
            {
                Assert.Equal(0, exit);
            }

            Assert.Equal(2, Entries(tool).Count);
            Assert.All(Entries(tool), e => Assert.Null(e.OrderRef));
            using (var db = Open(tool))
            {
                Assert.Single(db.Licenses);
                Assert.Equal("owner@example.com", db.Licenses.Single().CustomerEmail);
            }

            Assert.False(DatabaseHolds(tool, "ORD-1001"));
            Assert.False(DatabaseHolds(tool, "ORD-1874"));
        }
    }

    [Fact]
    public void Erasure_request()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            Assert.True(DatabaseHolds(tool, "owner@example.com"));

            var (exit, run) = Read(tool, "license", "customer", reference, "--clear-email", "--clear-order-refs");
            using (run)
            {
                Assert.Equal(0, exit);
            }

            Assert.False(DatabaseHolds(tool, "owner@example.com"));
            Assert.False(DatabaseHolds(tool, "ORD-1001"));
            Assert.False(DatabaseHolds(tool, "ORD-1874"));
            using var db = Open(tool);
            var license = db.Licenses.Single();
            Assert.Null(license.CustomerEmail);
            Assert.Equal(2, db.IssueEntries.Count());
        }
    }

    [Fact]
    public void An_email_can_be_changed_and_the_earlier_one_leaves_the_database()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            var (exit, run) = Read(tool, "license", "customer", reference.ToLowerInvariant(), "--email", " new@example.org ");
            using (run)
            {
                Assert.Equal(0, exit);
            }

            using (var db = Open(tool))
            {
                Assert.Equal("new@example.org", db.Licenses.Single().CustomerEmail);
            }

            Assert.False(DatabaseHolds(tool, "owner@example.com"));
        }
    }

    [Theory]
    [InlineData("--order-ref", "ORD-1", "--clear-order-refs")]
    [InlineData("--email", "a@example.com", "--clear-email")]
    public void Conflicting_customer_options_are_a_usage_error(string first, string value, string second)
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            var (exit, run) = Read(tool, "license", "customer", reference, first, value, second);
            using (run)
            {
                Assert.Equal(2, exit);
                Assert.Contains("cannot be used together", run.Stderr);
            }

            Assert.Equal(["ORD-1001", "ORD-1874"], Entries(tool).Select(e => e.OrderRef));
        }
    }

    [Fact]
    public void Customer_with_nothing_to_change_or_an_empty_value_is_refused()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            foreach (var (args, message) in new[]
            {
                (new[] { "license", "customer", reference }, "Nothing to change"),
                (["license", "customer", reference, "--email", ""], "use --clear-email"),
                (["license", "customer", reference, "--order-ref", " "], "use --clear-order-refs"),
                (["license", "customer", "LIC-22222-22222", "--clear-email"], "Unknown license"),
                (["license", "customer"], "Missing <reference>"),
            })
            {
                var (exit, run) = Read(tool, args);
                using (run)
                {
                    Assert.Equal(1, exit);
                    Assert.Contains(message, run.Stderr);
                }
            }

            Assert.Equal(["ORD-1001", "ORD-1874"], Entries(tool).Select(e => e.OrderRef));
            using var db = Open(tool);
            Assert.Equal("owner@example.com", db.Licenses.Single().CustomerEmail);
        }
    }

    [Fact]
    public void No_delete()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            foreach (var args in new[]
            {
                new[] { "license", "delete", reference },
                ["license", "remove", reference],
                ["license", "customer", reference, "--delete"],
            })
            {
                var (exit, run) = Read(tool, args);
                using (run)
                {
                    Assert.Equal(2, exit);
                }
            }

            // The strongest customer change leaves the license and every entry.
            var (cleared, clear) = Read(tool, "license", "customer", reference, "--clear-email", "--clear-order-refs");
            clear.Dispose();
            Assert.Equal(0, cleared);
            using var db = Open(tool);
            Assert.Single(db.Licenses);
            Assert.Equal(2, db.IssueEntries.Count());
        }
    }

    // ---- No passphrase for reading

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_passphrase_for_reading(bool redirected)
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            foreach (var args in new[]
            {
                new[] { "license", "list" },
                ["license", "show", reference],
                ["license", "customer", reference, "--order-ref", "ORD-1875"],
            })
            {
                // No UCL_PASSPHRASE; no hidden keys scripted (asking would throw); stdin must stay unread.
                using var run = tool.Another(stdin: "unread\n", stdinRedirected: redirected);
                var exit = run.Run(args);

                Assert.True(exit == 0, run.Stderr);
                Assert.DoesNotContain("assphrase", run.Stderr);
                Assert.Equal("unread", run.Environment.Console.ReadLine());
            }
        }
    }

    [Fact]
    public void Reading_ignores_a_wrong_passphrase_in_the_environment()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            using var run = tool.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = "not the passphrase" });
            Assert.Equal(0, run.Run("license", "show", reference));
        }
    }

    // ---- Follow-ups from the review of tasks.md 8.3

    [Fact]
    public void An_empty_expires_is_missing_and_not_the_suggestion()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            var (exit, run) = LicenseIssueTests.Run(tool, "license", "renew", reference, "--expires", "");
            using (run)
            {
                Assert.Equal(1, exit);
                Assert.Contains("Missing --expires", run.Stderr);
                Assert.Equal("", run.Stdout);
            }

            Assert.Equal(2, Entries(tool).Count);
        }
    }

    [Fact]
    public void An_empty_expires_at_a_terminal_is_asked_for()
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            using var run = tool.Another(stdin: "2029-01-01\n", stdinRedirected: false, hiddenKeys: ScriptedTool.DefaultPassphrase + "\n");
            Assert.Equal(0, run.Run("license", "renew", reference, "--expires", ""));
            Assert.Contains("Expiry [", run.Stderr);
            Assert.Equal(At("2029-01-01T23:59:59Z"), Entries(tool)[2].ExpiresUtc);
        }
    }

    [Theory]
    [InlineData("--display-name", "displayName")]
    [InlineData("--vendor-tag", "vendorTag")]
    public void An_empty_display_name_or_vendor_tag_on_reissue_is_refused_by_the_library(string option, string field)
    {
        var (tool, reference) = Renewed();
        using (tool)
        {
            var (exit, run) = LicenseIssueTests.Run(tool, "license", "reissue", reference, "--expires", "suggested", option, "");
            using (run)
            {
                // Pinned: the empty value reaches the library, which names the field. It does not clear the value.
                Assert.Equal(1, exit);
                Assert.Contains($"{field}:", run.Stderr);
                Assert.Contains("Nothing stored", run.Stderr);
                Assert.Equal("", run.Stdout);
            }

            Assert.Equal(2, Entries(tool).Count);
        }
    }
}
