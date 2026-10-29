using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model. The folder is resolved at runtime, so this
/// points at a file that is never opened.
/// </summary>
internal sealed class ToolDbContextFactory : IDesignTimeDbContextFactory<ToolDbContext>
{
    public ToolDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ToolDbContext>().UseSqlite("Data Source=design-time-only.db").Options);
}
