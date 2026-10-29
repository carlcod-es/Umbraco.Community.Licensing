using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// Sets <c>secure_delete</c> on and <c>journal_mode</c> to DELETE on every connection EF opens, so a
/// cleared value leaves no copy in free pages or a WAL file (ADR-0013, PDR-0033).
/// </summary>
internal sealed class ConnectionPragmas : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Apply(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
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
