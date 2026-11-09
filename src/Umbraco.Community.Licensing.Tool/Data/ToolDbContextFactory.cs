using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the context without a host. Used only at design time; it
/// names a file that is never created, since adding a migration does not connect.
/// </summary>
internal sealed class ToolDbContextFactory : IDesignTimeDbContextFactory<ToolDbContext>
{
    public ToolDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ToolDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
