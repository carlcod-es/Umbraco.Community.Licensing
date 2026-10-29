using System.Reflection;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class VersionTests
{
    /// <summary>Version.props is imported by this test project too, so $(Version) is embedded as metadata at build.</summary>
    internal static string RepoVersion { get; } = typeof(VersionTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "RepoVersion").Value!;

    [Fact]
    public void Tool_informational_version_equals_Version_props_without_revision_suffix()
    {
        var toolAssembly = typeof(Program).Assembly;
        var actual = toolAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.DoesNotContain('+', actual);
        Assert.Equal(RepoVersion, actual);
    }
}
