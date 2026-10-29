using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;
using Umbraco.Community.Licensing.Tool.Commands;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>Builds the command tree and runs it against a <see cref="ToolEnvironment"/>.</summary>
internal static class ToolApp
{
    /// <summary>Runs the tool. Returns 0 done, 1 refused, 2 usage error.</summary>
    public static int Run(string[] args, ToolEnvironment environment) => Run(args, environment, []);

    /// <summary>Runs the tool with extra commands registered (tests use this for stub commands).</summary>
    internal static int Run(
        string[] args,
        ToolEnvironment environment,
        IEnumerable<Func<ToolHost, Command>> extraCommands,
        Action<ToolContext>? runSetup = null)
    {
        var globals = new GlobalOptions();
        var host = new ToolHost(environment, globals, runSetup);
        var console = environment.Console;

        // A plain Command rather than RootCommand: RootCommand names itself after the executable, which is not "ucl" under `dotnet run` or the test host.
        var root = new Command("ucl", "Umbraco.Community.Licensing vendor console.") { new HelpOption(), new VersionOption() };
        globals.AddTo(root);
        root.Subcommands.Add(SetupCommand.Build(host));
        root.Subcommands.Add(FolderCommands.Build(host));
        UseBareVersion(root, console);
        foreach (var build in extraCommands)
        {
            root.Subcommands.Add(build(host));
        }

        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                console.Error.WriteLine(error.Message);
            }

            return ExitCodes.Usage;
        }

        return parseResult.Invoke(new InvocationConfiguration { Output = console.Out, Error = console.Error });
    }

    /// <summary>
    /// <c>--version</c> prints this assembly's version without the <c>+sha</c> suffix, to stdout.
    /// (System.CommandLine's default reads the entry assembly, which is the test host under test.)
    /// </summary>
    private static void UseBareVersion(Command root, ToolConsole console)
    {
        var assembly = typeof(ToolApp).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        var plus = version.IndexOf('+');
        var bare = plus < 0 ? version : version[..plus];

        foreach (var option in root.Options.OfType<VersionOption>())
        {
            option.Action = new BareVersionAction(bare, console);
        }
    }

    private sealed class BareVersionAction(string version, ToolConsole console) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            console.Out.WriteLine(version);
            return ExitCodes.Done;
        }
    }
}
