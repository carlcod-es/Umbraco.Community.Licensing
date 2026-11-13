using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Licensing;

/// <summary>The first thing to do for each license state, worded for the administrator or site owner reading the dashboard.</summary>
public static class FirstActions
{
    /// <summary>The first action for the state, or null when there is nothing to do (<see cref="LicenseState.Valid"/>).</summary>
    /// <remarks>No default branch: a new <see cref="LicenseState"/> member makes this fail to compile.</remarks>
    // CS8524 only asks for a branch for undefined numeric values; a missing named member is still an error (CS8509).
#pragma warning disable CS8524
    public static string? For(LicenseState state) => state switch
    {
        LicenseState.Missing =>
            $"Install the license key in the {ContactFormOptions.LicenseKeySettingName} setting. If you do not have it, ask the site owner for it.",
        LicenseState.Unreadable =>
            $"Paste the license key again into the {ContactFormOptions.LicenseKeySettingName} setting. What is there is not a license key, or it is damaged.",
        LicenseState.WrongProduct =>
            "Move this key to the product it names. It is not a key for this product.",
        LicenseState.SigningKeyNotRecognised =>
            "Update this product to its latest version. If that does not help, ask the vendor.",
        LicenseState.NotVerified =>
            "Paste the license key again, exactly as it was issued. If that does not help, ask the vendor.",
        LicenseState.NotSupported =>
            "Update this product to its latest version, which can read this key. If that does not help, ask the vendor.",
        LicenseState.Expired =>
            "Renew the license.",
        LicenseState.Valid => null,
    };
#pragma warning restore CS8524
}
