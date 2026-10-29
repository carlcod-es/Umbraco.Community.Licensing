using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class ErrorHandlingTests
{
    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("sqlite")]
    [InlineData("update")]
    public void Expected_failure_prints_the_message_without_a_stack_trace_and_exits_1(string kind)
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run("stub-throw", kind);

        Assert.Equal(1, exit);
        Assert.Contains($"boom-{kind}", tool.Stderr);
        Assert.DoesNotContain(" at ", tool.Stderr);
        Assert.Equal("", tool.Stdout);
    }

    [Fact]
    public void Other_exceptions_are_not_swallowed()
    {
        using var tool = new ScriptedTool();

        // Not caught by the host: System.CommandLine reports it as an unhandled exception (with the stack trace).
        var exit = tool.Run("stub-throw", "other");

        Assert.NotEqual(0, exit);
        Assert.Contains("InvalidOperationException", tool.Stderr);
    }

    [Fact]
    public void Database_update_failure_prints_the_inner_database_message()
    {
        using var tool = new ScriptedTool();

        var exit = tool.Run("stub-throw", "update-inner");

        Assert.Equal(1, exit);
        Assert.Contains("boom-inner", tool.Stderr);
        Assert.DoesNotContain("See the inner exception", tool.Stderr);
    }

    internal static Exception Make(string kind) => kind switch
    {
        "io" => new IOException("boom-io"),
        "access" => new UnauthorizedAccessException("boom-access"),
        "sqlite" => new SqliteException("boom-sqlite", 1),
        "update" => new DbUpdateException("boom-update"),
        "update-inner" => new DbUpdateException("See the inner exception for details.", new SqliteException("boom-inner", 19)),
        _ => new InvalidOperationException("boom-other"),
    };
}
