using System.Reflection;

namespace Umbraco.Community.Licensing.Core.Tests.Examples;

/// <summary>Keeps docs/license-examples.md and the tested key strings identical (ADR-0005).</summary>
public class ExampleDocumentTests
{
    private const string SolutionFile = "Umbraco.Community.Licensing.slnx";

    [Fact]
    public void EveryExampleKeyString_AppearsVerbatimInTheExamplesDocument()
    {
        var document = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "license-examples.md"));
        var constants = typeof(ExampleKeyStrings)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .ToList();

        var missing = constants
            .Where(f => !document.Contains((string)f.GetRawConstantValue()!, StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.NotEmpty(constants);
        Assert.True(
            missing.Count == 0,
            $"{missing.Count} of {constants.Count} example key strings are missing from docs/license-examples.md: {string.Join(", ", missing)}");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"{SolutionFile} was not found above {AppContext.BaseDirectory}.");
    }
}
