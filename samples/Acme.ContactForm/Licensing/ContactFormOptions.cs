namespace Acme.ContactForm.Licensing;

/// <summary>The site owner's settings, bound to the <c>ContactForm</c> configuration section.</summary>
public sealed class ContactFormOptions
{
    public const string SectionName = "ContactForm";

    /// <summary>The setting name the dashboard tells a site owner to use.</summary>
    public const string LicenseKeySettingName = "ContactForm:LicenseKey";

    /// <summary>The same setting as an environment variable.</summary>
    public const string LicenseKeyEnvironmentVariable = "ContactForm__LicenseKey";

    public string? LicenseKey { get; set; }

    public DemoOptions Demo { get; set; } = new();

    public sealed class DemoOptions
    {
        /// <summary>Turns the demo picker and demo clock on (<c>ContactForm:Demo:Enabled</c>). Off by default.</summary>
        public bool Enabled { get; set; }
    }
}
