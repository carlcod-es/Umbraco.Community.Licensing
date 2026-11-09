using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// Sets <c>secure_delete = ON</c> and <c>journal_mode = DELETE</c> each time a connection opens (ADR-0013), so a
/// cleared value leaves no copy in a free page or a WAL file. Run on every open, not once per file: secure_delete
/// is per connection.
/// </summary>
internal sealed class SqlitePragmas : DbConnectionInterceptor
{
    public static SqlitePragmas Instance { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Apply(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection);
        return Task.CompletedTask;
    }

    private static void Apply(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA secure_delete = ON; PRAGMA journal_mode = DELETE;";
        command.ExecuteNonQuery();
    }
}
