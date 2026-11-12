using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Backup &gt; Export (vendor-tool-backup, ADR-0015), as whole sessions through <c>ToolApp.Run</c>.</summary>
public class BackupExportTests : IDisposable
{
    private static readonly DateTime Taken = Utc(2026, 11, 8, 9, 30, 15);

    private readonly LicenseKit _kit = new(shared: false);

    public void Dispose() => _kit.Dispose();

    private string InHome(params string[] parts) => Path.Combine([_kit.Host.Home, .. parts]);

    /// <summary>Main menu 4 (Backup), 1 (Export), the path answers, then Back and Quit.</summary>
    private ScriptedConsole Export(params string[] paths)
    {
        var console = new ScriptedConsole().Line("4").Line("1");
        foreach (var path in paths)
            console.Line(path);
        return _kit.Run(Quit(console), ClockAt(Taken));
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void BackupTaken_WritesTheZip_ReportsPathAndTime_AsksNoPassphrase_AndHoldsTheTwoEntries()
    {
        var target = InHome("Backups", "ucl-2026-10-28.zip");
        Directory.CreateDirectory(InHome("Backups"));

        var console = Export(target);

        Assert.True(File.Exists(target));
        Assert.Contains($"Backup written to {target}, taken 2026-11-08 09:30:15 UTC.", console.Out);
        Assert.DoesNotContain("Passphrase", console.Out);

        using var zip = ZipFile.OpenRead(target);
        Assert.Equal(["manifest.json", "ucl.db"], zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        using var manifest = JsonDocument.Parse(ReadEntry(zip.GetEntry("manifest.json")!));
        var root = manifest.RootElement;
        using var db = _kit.Db();
        Assert.Equal(1, root.GetProperty("format").GetInt32());
        Assert.Equal(ToolApp.Version, root.GetProperty("toolVersion").GetString());
        Assert.Equal(db.Database.GetMigrations().Last(), root.GetProperty("lastMigration").GetString());
        Assert.Equal(Taken, root.GetProperty("takenUtc").GetDateTime().ToUniversalTime());
        Assert.EndsWith("Z", root.GetProperty("takenUtc").GetString());
    }

    [Fact]
    public void BackupFileAlreadyExists_SaysSo_AsksAnotherName_AndLeavesTheFileUnchanged()
    {
        Directory.CreateDirectory(InHome("Backups"));
        var existing = InHome("Backups", "ucl-2026-10-28.zip");
        byte[] bytes = [1, 2, 3, 4];
        File.WriteAllBytes(existing, bytes);
        var other = InHome("Backups", "other.zip");

        var console = Export(existing, other);

        Assert.Contains($"The file {existing} already exists", console.Out);
        Assert.Equal(bytes, File.ReadAllBytes(existing));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void BackupFolderMissing_Declined_NamesTheFolder_AsksAgain_AndCreatesNoFolder()
    {
        var missing = InHome("Dropbx");
        var wrong = Path.Combine(missing, "ucl-2026-11-08.zip");
        Directory.CreateDirectory(InHome("Backups"));
        var right = InHome("Backups", "ucl-2026-11-08.zip");

        var console = Export(wrong, "n", right);

        Assert.Contains($"The folder {missing} does not exist.", console.Out);
        Assert.Contains("Create it? [Y/n]", console.Out);
        Assert.False(Directory.Exists(missing));
        Assert.True(File.Exists(right));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "File to back up to"));
    }

    [Fact]
    public void BackupFolderMissing_Created_WithEveryMissingLevel_AndTheZipWritten()
    {
        var folder = InHome("Backups", "2026", "Nov");
        var target = Path.Combine(folder, "ucl-2026-11-08.zip");

        var console = Export(target, "y");

        Assert.Contains($"The folder {folder} does not exist.", console.Out);
        Assert.True(Directory.Exists(InHome("Backups", "2026")));
        Assert.True(File.Exists(target));
        Assert.Contains($"Backup written to {target}, taken", console.Out);
    }

    [Fact]
    public void BackupFolderMissing_EnterTakesTheProposedYes()
    {
        var target = InHome("Made", "b.zip");

        Export(target, "");

        Assert.True(File.Exists(target));
    }

    [Fact]
    public void BackupFolderMissing_Cancelled_EndsTheActionAndCreatesNothing()
    {
        var folder = InHome("Never");

        var console = Export(Path.Combine(folder, "b.zip"), "/cancel");

        Assert.False(Directory.Exists(folder));
        Assert.DoesNotContain("Backup written", console.Out);
    }

    [Fact]
    public void HomeFolderTypedAsTilde_WritesUnderHome_AndReportsTheFullPath()
    {
        Directory.CreateDirectory(InHome("Backups"));

        var console = Export("~/Backups/ucl-2026-11-08.zip");

        var target = InHome("Backups", "ucl-2026-11-08.zip");
        Assert.True(File.Exists(target));
        Assert.Contains($"Backup written to {target}, taken", console.Out);
        Assert.DoesNotContain("written to ~", console.Out);
    }

    [Fact]
    public void HomeFolderTypedAsTilde_Alone_IsAFolder_AndAsksAgain()
    {
        var console = Export("~", "b.zip");

        Assert.Contains($"{_kit.Host.Home} is a folder. Give a file name.", console.Out);
        Assert.True(File.Exists(Path.Combine(_kit.Host.Cwd, "b.zip")));
    }

    [Fact]
    public void HomeFolderTypedAsTildeOnWindows_BackslashIsRead_AsHome()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only: ~\\ is the home folder only there.");
        Directory.CreateDirectory(InHome("Backups"));

        var console = Export(@"~\Backups\w.zip");

        Assert.True(File.Exists(InHome("Backups", "w.zip")));
        Assert.Contains($"Backup written to {InHome("Backups", "w.zip")}, taken", console.Out);
    }

    [Fact]
    public void TildeBackslashOutsideWindows_IsAName_NotTheHomeFolder()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "A backslash separates folders on Windows.");

        Export(@"~\w.zip");

        Assert.True(File.Exists(Path.Combine(_kit.Host.Cwd, @"~\w.zip")));
        Assert.False(File.Exists(InHome("w.zip")));
    }

    [Fact]
    public void TildeInAName_IsLeftAsTyped_AndReadFromTheCurrentFolder()
    {
        var target = Path.Combine(_kit.Host.Cwd, "~backups-x.zip");

        var console = Export("~backups-x.zip");

        Assert.True(File.Exists(target));
        Assert.Contains($"Backup written to {target}, taken", console.Out);
    }

    [Fact]
    public void RelativePath_IsReadFromTheCurrentFolder()
    {
        var console = Export("rel.zip");

        Assert.True(File.Exists(Path.Combine(_kit.Host.Cwd, "rel.zip")));
        Assert.False(File.Exists(InHome("rel.zip")));
    }

    [Fact]
    public void ExistingFileTypedWithTilde_IsNeverReplaced()
    {
        byte[] bytes = [9, 8, 7];
        File.WriteAllBytes(InHome("keep.zip"), bytes);

        var console = Export("~/keep.zip", "other.zip");

        Assert.Equal(bytes, File.ReadAllBytes(InHome("keep.zip")));
        Assert.Contains($"The file {InHome("keep.zip")} already exists", console.Out);
    }

    [Fact]
    public void TheZip_HoldsTheEncryptedPrivateKeyPems_OfTheLiveInstallation()
    {
        var target = Path.Combine(_kit.Host.Cwd, "backup.zip");
        Export(target);

        var extracted = Path.Combine(_kit.Host.Cwd, "extracted.db");
        using (var zip = ZipFile.OpenRead(target))
            zip.GetEntry("ucl.db")!.ExtractToFile(extracted);

        using var liveDb = _kit.Db();
        var live = liveDb.SigningKeys.AsNoTracking().ToList().ToDictionary(k => k.SigningKeyId, k => k.EncryptedPrivateKeyPem);
        Assert.NotEmpty(live);

        var connectionString = new SqliteConnectionStringBuilder { DataSource = extracted, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SigningKeyId, EncryptedPrivateKeyPem FROM SigningKeys";
        using var reader = command.ExecuteReader();
        var zipped = new Dictionary<string, string>();
        while (reader.Read())
            zipped[reader.GetString(0)] = reader.GetString(1);

        Assert.Equal(live, zipped);
        Assert.All(zipped.Values, pem => Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----", pem));
    }

    [Fact]
    public void Export_LeavesNoTempSnapshot_AndTheLiveDatabaseUnchanged()
    {
        var live = Path.Combine(_kit.Host.HomeInstallation, "ucl.db");
        var before = File.ReadAllBytes(live);

        Export(Path.Combine(_kit.Host.Cwd, "backup.zip"));

        Assert.Equal(["ucl.db"], Directory.GetFiles(_kit.Host.HomeInstallation).Select(Path.GetFileName));
        Assert.Equal(before, File.ReadAllBytes(live));
    }

    [Fact]
    public void ExportFailingMidWay_LeavesNoZipAndNoTempSnapshot_SaysOneLine_AndTheSessionReachesQuit()
    {
        var target = Path.Combine(_kit.Host.Cwd, "backup.zip");
        var actions = ActionTable.Default();
        actions.Register("backup.export", s => BackupExportActions.Export(s, afterSnapshot: () => throw new IOException("The disk is full.")));
        var console = Quit(new ScriptedConsole().Line("4").Line("1").Line(target));

        var exit = _kit.Host.Run(console, actions, Startup.Offer.Default, ClockAt(Taken));

        Assert.Equal(0, exit);
        Assert.Contains("Backup - Export failed: The disk is full.", console.Out);
        Assert.False(File.Exists(target));
        Assert.Equal(["ucl.db"], Directory.GetFiles(_kit.Host.HomeInstallation).Select(Path.GetFileName));
    }
}
