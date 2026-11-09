using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// Reads an installation's database as any other program would (PDR-0046): a plain SQLite connection, no EF Core, no tool code.
/// Every table in sqlite_master, the EF history and lock tables included (follow-ups: "6.4 scan includes __EF* tables").
/// </summary>
internal static class PlainStore
{
    /// <summary><paramref name="Encoded"/>: the cell is a key, PEM, hash or salt, so the machine-name scan skips it (see <see cref="EncodedColumns"/>).</summary>
    internal sealed record Cell(string Table, string Column, string Value, bool Encoded = false);

    /// <summary>
    /// The columns that hold encoded data (Base64, PEM or hash text), named, not guessed from content. The machine-name token scan skips them:
    /// a 3-character name can pass the boundary test inside Base64 by chance (about 1e-4 to 1e-3 per run on a host with such a name). The path needles still scan them.
    /// </summary>
    public static readonly IReadOnlySet<string> EncodedColumns = new HashSet<string>(StringComparer.Ordinal)
    {
        "SigningKeys.SigningKeyId",
        "SigningKeys.PublicKey",
        "SigningKeys.EncryptedPrivateKeyPem",
        "Licenses.CustomerEmailKey",
        "IssueEntries.OrderRefKey",
        "IssueEntries.SigningKeyId",
        "KeyReleases.SigningKeyId",
    };

    /// <summary>The Settings rows whose Value is encoded (Base64 salt and hash): <see cref="SettingNames.PassphraseSalt"/> and <see cref="SettingNames.PassphraseHash"/>.</summary>
    public static readonly IReadOnlySet<string> EncodedSettings = new HashSet<string>(StringComparer.Ordinal) { SettingNames.PassphraseSalt, SettingNames.PassphraseHash };

    public static string DatabaseFile(string folder) => Path.Combine(folder, Startup.DatabaseFileName);

    public static SqliteConnection Open(string databaseFile)
    {
        var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return connection;
    }

    public static List<string> Tables(SqliteConnection connection) =>
        Query(connection, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name").Select(r => (string)r[0]!).ToList();

    /// <summary>Every column of every row of every table, as text (a blob as its bytes read as Latin-1, so each byte is one character).</summary>
    public static List<Cell> Cells(string databaseFile)
    {
        using var connection = Open(databaseFile);
        var cells = new List<Cell>();
        foreach (var table in Tables(connection))
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table}\"";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var rowStart = cells.Count;
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    if (value is DBNull)
                        continue;
                    var text = value switch
                    {
                        byte[] bytes => Encoding.Latin1.GetString(bytes),
                        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
                    };
                    var column = reader.GetName(i);
                    cells.Add(new Cell(table, column, text, EncodedColumns.Contains($"{table}.{column}")));
                }

                // A Settings row is Name and Value: the Value is encoded only for the passphrase rows.
                if (table == "Settings" && cells.Skip(rowStart).FirstOrDefault(c => c.Column == "Name") is { } name && EncodedSettings.Contains(name.Value))
                    for (var i = rowStart; i < cells.Count; i++)
                        if (cells[i].Column == "Value")
                            cells[i] = cells[i] with { Encoded = true };
            }
        }

        return cells;
    }

    public static List<object?[]> Query(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>A path as written on either kind of system: as given, with / and with \.</summary>
    public static IEnumerable<string> PathForms(string path)
    {
        yield return path;
        yield return path.Replace('\\', '/');
        yield return path.Replace('/', '\\');
    }

    /// <summary>
    /// The machine name is matched as a whole token (non-alphanumeric or text boundaries each side, any case), never as a substring.
    /// A name shorter than this is not scanned at all, by rule (even as a token it would be vacuous), and the tests record that skip.
    /// Encoded cells (<see cref="EncodedColumns"/>) are not scanned for it either, so the check cannot hit Base64 by chance: no residual flake.
    /// </summary>
    public const int MinMachineNameLength = 3;

    public static bool HoldsMachineName(string value) => HoldsToken(value, Environment.MachineName);

    internal static bool HoldsToken(string value, string name)
    {
        if (name.Length < MinMachineNameLength)
            return false;
        for (var at = value.IndexOf(name, StringComparison.OrdinalIgnoreCase); at >= 0; at = value.IndexOf(name, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + name.Length;
            if ((at == 0 || !char.IsLetterOrDigit(value[at - 1])) && (end == value.Length || !char.IsLetterOrDigit(value[end])))
                return true;
        }

        return false;
    }

    /// <summary>Cells holding any of <paramref name="needles"/> in any case, or (unless <see cref="Cell.Encoded"/>) the machine name as a token (<see cref="HoldsMachineName"/>), as "table.column".</summary>
    public static List<string> Holding(IEnumerable<Cell> cells, IEnumerable<string> needles) => Holding(cells, needles, Environment.MachineName);

    internal static List<string> Holding(IEnumerable<Cell> cells, IEnumerable<string> needles, string machineName)
    {
        var wanted = needles.Where(n => n.Length > 0).Distinct().ToList();
        return [.. cells.Where(c => (!c.Encoded && HoldsToken(c.Value, machineName)) || wanted.Any(n => c.Value.Contains(n, StringComparison.OrdinalIgnoreCase))).Select(c => $"{c.Table}.{c.Column}")];
    }

    /// <summary>The local things no stored value may name: each folder given (as written on either kind of system) and the real home folder. The machine name is checked by <see cref="Holding"/> itself.</summary>
    public static List<string> LocalNames(params string[] folders)
    {
        var names = new List<string>();
        foreach (var folder in folders.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            if (folder.Length > 0)
                names.AddRange(PathForms(folder));
        }

        return names;
    }

    /// <summary>Every file under the folder, read whole while the tool may hold it open.</summary>
    public static List<(string Path, byte[] Bytes)> FolderFiles(string folder)
    {
        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(f => (f, ReadShared(f))).ToList();
        Assert.Contains(files, f => Path.GetFileName(f.f) == Startup.DatabaseFileName);
        return files;
    }

    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
