using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// Reads an installation's database as any other program would (PDR-0046): a plain SQLite connection, no EF Core, no tool code.
/// Every table in sqlite_master, the EF history and lock tables included (follow-ups: "6.4 scan includes __EF* tables").
/// </summary>
internal static class PlainStore
{
    internal sealed record Cell(string Table, string Column, string Value);

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
                    cells.Add(new Cell(table, reader.GetName(i), text));
                }
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

    /// <summary>Cells holding any of <paramref name="needles"/> in any case, as "table.column".</summary>
    public static List<string> Holding(IEnumerable<Cell> cells, IEnumerable<string> needles)
    {
        var wanted = needles.Where(n => n.Length > 0).Distinct().ToList();
        return [.. cells.Where(c => wanted.Any(n => c.Value.Contains(n, StringComparison.OrdinalIgnoreCase))).Select(c => $"{c.Table}.{c.Column}")];
    }

    /// <summary>The local things no stored value may name: each folder given (as written on either kind of system), the real home folder and the machine name.</summary>
    public static List<string> LocalNames(params string[] folders)
    {
        var names = new List<string>();
        foreach (var folder in folders.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            if (folder.Length > 0)
                names.AddRange(PathForms(folder));
        }

        // A very short machine name would match base64 text by chance.
        if (Environment.MachineName.Length >= 5)
            names.Add(Environment.MachineName);
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
