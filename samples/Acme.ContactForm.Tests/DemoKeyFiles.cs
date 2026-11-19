using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

/// <summary>Locates and reads the committed demo signing keys (samples/demo-signing-keys/).</summary>
internal static class DemoKeyFiles
{
    public const string Warning =
        "PUBLIC, DEMO-ONLY KEY. It is published in this repository. Never trust it in a real product.";

    public static readonly string[] AllFiles =
        ["demo.pem", "demo.public.txt", "untrusted.pem", "untrusted.public.txt", "README.md"];

    /// <summary>The folder holding samples/Acme.ContactForm.slnx, found by walking up from the test's base directory.</summary>
    public static string SamplesRoot { get; } = FindSamplesRoot();

    public static string KeysFolder => Path.Combine(SamplesRoot, "demo-signing-keys");

    public static string Read(string fileName) => File.ReadAllText(Path.Combine(KeysFolder, fileName));

    /// <summary>Loads a private key from a committed PEM; the warning before the BEGIN line is ignored by the library.</summary>
    public static SigningPrivateKey LoadPrivate(string fileName) => SigningPrivateKey.FromPem(Read(fileName));

    /// <summary>
    /// Parses a committed public line file. <see cref="SigningPublicKey.Parse"/> has no comment syntax, so lines
    /// starting with <c>//</c> (PDR-0056) are dropped here, as a product reading the file must do.
    /// </summary>
    public static SigningPublicKey LoadPublic(string fileName)
    {
        var lines = Read(fileName)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith("//", StringComparison.Ordinal))
            .ToArray();
        return SigningPublicKey.Parse(Assert.Single(lines));
    }

    private static string FindSamplesRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Acme.ContactForm.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("samples/Acme.ContactForm.slnx not found above " + AppContext.BaseDirectory);
    }
}
