using System.Reflection;

namespace Acme.ContactForm.Tests;

public class AssemblySmokeTests
{
    [Fact]
    public void PackageAssembly_Loads()
    {
        Assembly assembly = Assembly.Load("Acme.ContactForm");

        Assert.Equal("Acme.ContactForm", assembly.GetName().Name);
    }
}
