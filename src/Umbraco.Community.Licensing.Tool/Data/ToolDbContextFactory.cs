using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the context without a host. Used only at design time. It points at an
/// in-memory database, so no command (<c>migrations add</c> never connects; <c>database update</c> would only touch
/// memory) can create a file in the working directory.
/// </summary>
internal sealed class ToolDbContextFactory : IDesignTimeDbContextFactory<ToolDbContext>
{
    public ToolDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ToolDbContext>().UseSqlite("Data Source=:memory:").Options);
}
