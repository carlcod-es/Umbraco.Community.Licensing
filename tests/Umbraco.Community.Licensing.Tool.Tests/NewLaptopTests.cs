namespace Umbraco.Community.Licensing.Tool.Tests;

public class NewLaptopTests
{
    [Fact]
    public void New_laptop()
    {
        using var oldLaptop = LicenseIssueTests.WithProducts(shared: false);

        // The folder is copied to another home's .ucl; the tool runs from an unrelated current directory.
        using var newLaptop = new ScriptedTool();
        var target = newLaptop.HomeInstallation;
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(oldLaptop.HomeInstallation))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        var elsewhere = Path.Combine(newLaptop.Environment.Home, "..", "somewhere-unrelated");
        Directory.CreateDirectory(elsewhere);
        Assert.NotEqual(Path.GetFullPath(target), Path.GetFullPath(elsewhere));

        using var run = newLaptop.Another(
            variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = ScriptedTool.DefaultPassphrase });
        var unrelated = run.Environment with { CurrentDirectory = Path.GetFullPath(elsewhere) };
        var exit = ToolApp.Run(
            ["license", "issue", "acme.commerce", "--expires", "2027-03-01", "--feature", "ecommerce=on"],
            unrelated);

        Assert.Equal(0, exit);
        Assert.Contains($"Using {Path.GetFullPath(target)}", run.Stderr);
        var key = LicenseIssueTests.KeyOf(run);

        // The key verifies against the public keys held in the copied folder, as on the old machine.
        var result = LicenseIssueTests.Evaluate(newLaptop, "acme.commerce", key, shared: false);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.True(result.HasSwitch("ecommerce"));
    }

    [Fact]
    public void New_laptop_with_another_passphrase_is_refused()
    {
        using var oldLaptop = LicenseIssueTests.WithProducts(shared: false);
        using var newLaptop = new ScriptedTool();
        Directory.CreateDirectory(newLaptop.HomeInstallation);
        foreach (var file in Directory.GetFiles(oldLaptop.HomeInstallation))
        {
            File.Copy(file, Path.Combine(newLaptop.HomeInstallation, Path.GetFileName(file)));
        }

        using var run = newLaptop.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = "a different passphrase" });
        var exit = run.Run("license", "issue", "acme.commerce", "--expires", "perpetual");

        Assert.Equal(1, exit);
        Assert.Contains("Wrong passphrase", run.Stderr);
        Assert.Equal("", run.Stdout);
    }
}
