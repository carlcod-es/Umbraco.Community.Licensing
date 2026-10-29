using System.CommandLine;

namespace Umbraco.Community.Licensing.Tool.Commands;

/// <summary>The <c>folder</c> command group.</summary>
internal static class FolderCommands
{
    public static Command Build(ToolHost host)
    {
        var show = new Command("show", "Show the vendor folder in use.");
        host.SetAction(show, context =>
        {
            context.Console.Out.WriteLine(context.UseInstallation());
            return ExitCodes.Done;
        });

        return new Command("folder", "Work with the vendor folder.") { show };
    }
}
