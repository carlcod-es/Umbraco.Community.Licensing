using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>
/// Creates the demo and untrusted signing pairs with the library's <see cref="SigningKeyPair"/> and writes
/// samples/demo-signing-keys/ (ADR-0028). Explicit: never part of a default run. Regenerating rotates the keys, so the
/// demo keys signed with them must be regenerated too. It refuses to overwrite existing files unless
/// <c>DEMO_KEYS_OVERWRITE=1</c> is set.
/// Run from the repository root (any OS): <c>dotnet run --project samples/Acme.ContactForm.Tests -- -explicit only -method "*DemoSigningKeyGenerator*" -showLiveOutput</c>.
/// To rotate deliberately, set the environment variable <c>DEMO_KEYS_OVERWRITE=1</c> for that command.
/// </summary>
public class DemoSigningKeyGenerator(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public void WriteDemoSigningKeys()
    {
        var folder = DemoKeyFiles.KeysFolder;
        var targets = new[] { "demo.pem", "demo.public.txt", "untrusted.pem", "untrusted.public.txt" }
            .Select(n => Path.Combine(folder, n))
            .ToArray();

        var overwrite = Environment.GetEnvironmentVariable("DEMO_KEYS_OVERWRITE") == "1";
        var existing = targets.Where(File.Exists).ToArray();
        if (existing.Length > 0 && !overwrite)
        {
            throw new InvalidOperationException(
                "Refusing to overwrite the committed demo keys (" + string.Join(", ", existing.Select(Path.GetFileName)) +
                "). Set DEMO_KEYS_OVERWRITE=1 to rotate them deliberately, then regenerate the demo keys.");
        }

        Directory.CreateDirectory(folder);
        Write("demo", "Demo signer");
        Write("untrusted", "Untrusted signer");
    }

    private void Write(string name, string role)
    {
        using var pair = SigningKeyPair.Create();
        var header = $"// {DemoKeyFiles.Warning}\n// Role: {role}.\n";

        File.WriteAllText(Path.Combine(DemoKeyFiles.KeysFolder, name + ".pem"), header + pair.PrivateKey.ExportPem());
        File.WriteAllText(Path.Combine(DemoKeyFiles.KeysFolder, name + ".public.txt"), header + pair.PublicKey.Export() + "\n");

        output.WriteLine($"{name}: {pair.SigningKeyId}");
    }
}
