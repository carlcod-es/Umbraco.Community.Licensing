using Xunit.Sdk;

namespace Umbraco.Community.Licensing.Core.Tests.Examples;

/// <summary>
/// Issues every key string of docs/license-examples.md and prints it (ADR-0005). Explicit: never part of a default run.
/// Run from the repository root (any OS): <c>dotnet run --project tests/Umbraco.Community.Licensing.Core.Tests -- -explicit only -method "*ExampleKeyGenerator*" -showLiveOutput</c>.
/// Paste the C# block into <see cref="ExampleKeyStrings"/> and the doc block into docs/license-examples.md.
/// </summary>
public class ExampleKeyGenerator(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public void PrintExampleKeys()
    {
        var keys = ExampleKeyFactory.Build();

        output.WriteLine("// C# constants");
        foreach (var (name, key) in keys)
        {
            output.WriteLine($"public const string {name} = \"{key}\";");
        }

        output.WriteLine("// Documentation");
        foreach (var (name, key) in keys)
        {
            output.WriteLine($"{name}  {key}");
        }
    }
}
