using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

public class DemoSigningKeyTests
{
    [Fact]
    public void WarningBesideThePrivateKey()
    {
        foreach (var file in DemoKeyFiles.AllFiles)
        {
            Assert.StartsWith("// " + DemoKeyFiles.Warning, DemoKeyFiles.Read(file).TrimStart(), StringComparison.Ordinal);
        }

        var privateFiles = Directory.EnumerateFiles(DemoKeyFiles.KeysFolder, "*", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("PRIVATE KEY", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(privateFiles);
        foreach (var file in privateFiles)
        {
            var text = File.ReadAllText(file).TrimStart();
            Assert.StartsWith("// " + DemoKeyFiles.Warning, text, StringComparison.Ordinal);
            Assert.True(
                text.IndexOf(DemoKeyFiles.Warning, StringComparison.Ordinal) < text.IndexOf("PRIVATE KEY", StringComparison.Ordinal),
                file + ": warning must come before the key");
        }
    }

    [Fact]
    public void NotTheLibrarysExampleKey()
    {
        using var demo = DemoKeyFiles.LoadPrivate("demo.pem");
        using var untrusted = DemoKeyFiles.LoadPrivate("untrusted.pem");

        string[] exampleIds = ["Nb_sm4Fxh5c", "MoA_5TxIpJs"];
        Assert.DoesNotContain(demo.SigningKeyId, exampleIds);
        Assert.DoesNotContain(untrusted.SigningKeyId, exampleIds);
        Assert.NotEqual(demo.SigningKeyId, untrusted.SigningKeyId);
    }

    [Theory]
    [InlineData("demo")]
    [InlineData("untrusted")]
    public void PemAndPublicLineShareOneKeyId(string name)
    {
        using var privateKey = DemoKeyFiles.LoadPrivate(name + ".pem");
        var publicKey = DemoKeyFiles.LoadPublic(name + ".public.txt");

        Assert.Equal(publicKey.SigningKeyId, privateKey.SigningKeyId);
        Assert.Equal(publicKey, privateKey.PublicKey);
    }

    [Fact]
    public void NoPrivateKeyInThePackage()
    {
        var package = Path.Combine(DemoKeyFiles.SamplesRoot, "Acme.ContactForm");
        Assert.True(Directory.Exists(package));

        // bin, obj and node_modules are build output or restored dependencies, not package source; they are skipped.
        var offenders = WalkSource(package)
            .Where(f => File.ReadAllText(f).Contains("PRIVATE KEY", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(offenders);

        // The built package assembly folder (the referenced Acme.ContactForm.dll output) must hold no .pem either.
        var built = Path.GetDirectoryName(typeof(DemoKeyFiles).Assembly.Location)!;
        var assemblyFolder = Path.GetDirectoryName(System.Reflection.Assembly.Load("Acme.ContactForm").Location)!;
        Assert.Empty(Directory.EnumerateFiles(assemblyFolder, "*.pem", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(built, "*.pem", SearchOption.TopDirectoryOnly));
        var packageBin = Path.Combine(package, "bin");
        if (Directory.Exists(packageBin))
        {
            Assert.Empty(Directory.EnumerateFiles(packageBin, "*.pem", SearchOption.AllDirectories));
        }
    }

    private static IEnumerable<string> WalkSource(string folder)
    {
        string[] skipped = ["bin", "obj", "node_modules"];
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            yield return file;
        }

        foreach (var dir in Directory.EnumerateDirectories(folder).Where(d => !skipped.Contains(Path.GetFileName(d))))
        {
            foreach (var file in WalkSource(dir))
            {
                yield return file;
            }
        }
    }
}
