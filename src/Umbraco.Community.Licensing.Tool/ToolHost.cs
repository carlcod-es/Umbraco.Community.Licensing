using System.CommandLine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Commands;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Wires commands to the environment. Commands are built from a <see cref="ToolHost"/> and run through
/// <see cref="SetAction"/>, which supplies the <see cref="ToolContext"/> and maps refusals to exit code 1.
/// </summary>
/// <param name="environment">The outside world.</param>
/// <param name="globals">The global options.</param>
/// <param name="runSetup">
/// Runs setup when the person accepts the offer made by <see cref="ToolContext.UseInstallation"/>; it
/// creates the installation or throws <see cref="ToolRefusal"/>. Defaults to <see cref="SetupCommand.Run"/>.
/// </param>
internal sealed class ToolHost(ToolEnvironment environment, GlobalOptions globals, Action<ToolContext>? runSetup = null)
{
    private readonly Action<ToolContext> _runSetup = runSetup ?? SetupCommand.Run;

    public ToolEnvironment Environment { get; } = environment;

    /// <summary>The global options, for command validators.</summary>
    public GlobalOptions Globals { get; } = globals;

    /// <summary>Sets the command's action. Return <see cref="ExitCodes.Done"/> or throw <see cref="ToolRefusal"/>.</summary>
    public void SetAction(Command command, Func<ToolContext, int> run) =>
        command.SetAction(parseResult =>
        {
            try
            {
                return run(new ToolContext(Environment, parseResult, Globals, _runSetup));
            }
            catch (Exception ex) when (ex is ToolRefusal or IOException or UnauthorizedAccessException or SqliteException or DbUpdateException)
            {
                // Expected failures of the folder and database: the message, no stack trace.
                // EF wraps the database error in a generic "see the inner exception" message.
                Environment.Console.Error.WriteLine(ex is DbUpdateException { InnerException: { } inner } ? inner.Message : ex.Message);
                return ExitCodes.Refused;
            }
        });
}
