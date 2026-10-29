using System.CommandLine;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Wires commands to the environment. Commands are built from a <see cref="ToolHost"/> and run through
/// <see cref="SetAction"/>, which supplies the <see cref="ToolContext"/> and maps refusals to exit code 1.
/// </summary>
/// <param name="environment">The outside world.</param>
/// <param name="globals">The global options.</param>
/// <param name="runSetup">
/// Runs setup when the person accepts the offer made by <see cref="ToolContext.UseInstallation"/>; it
/// creates the installation or throws <see cref="ToolRefusal"/>. Defaults to <see cref="SetupUnavailable"/>.
/// </param>
internal sealed class ToolHost(ToolEnvironment environment, GlobalOptions globals, Action<ToolContext>? runSetup = null)
{
    private readonly Action<ToolContext> _runSetup = runSetup ?? SetupUnavailable;

    public ToolEnvironment Environment { get; } = environment;

    /// <summary>Sets the command's action. Return <see cref="ExitCodes.Done"/> or throw <see cref="ToolRefusal"/>.</summary>
    public void SetAction(Command command, Func<ToolContext, int> run) =>
        command.SetAction(parseResult =>
        {
            try
            {
                return run(new ToolContext(Environment, parseResult, globals, _runSetup));
            }
            catch (ToolRefusal refusal)
            {
                Environment.Console.Error.WriteLine(refusal.Message);
                return ExitCodes.Refused;
            }
        });

    // Placeholder until tasks.md 6.1 supplies setup.
    private static void SetupUnavailable(ToolContext context) =>
        throw new ToolRefusal("Setup is not available yet.");
}
