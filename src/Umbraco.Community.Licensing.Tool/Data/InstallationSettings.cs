namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// The choices made at setup, read and written as typed values over the <c>Settings</c> rows.
/// <see cref="KeyProtection"/> owns the passphrase check rows.
/// </summary>
/// <param name="Prefix">The key prefix; empty means none.</param>
/// <param name="Shared">One signing key shared across all products (otherwise one per product); never changes after setup.</param>
/// <param name="StandbyKeys">Each signing key gets a standby key.</param>
internal sealed record InstallationSettings(string Prefix, bool Shared, bool StandbyKeys)
{
    private const string PrefixName = "KeyPrefix";
    private const string ModeName = "SigningKeyMode";
    private const string StandbyName = "StandbyKeys";

    private const string PerProductValue = "PerProduct";
    private const string SharedValue = "Shared";

    /// <summary>Adds the settings rows to <paramref name="db"/> for the caller to save (setup saves them with the rest of its work).</summary>
    public void AddTo(ToolDbContext db) =>
        db.Settings.AddRange(
            new Setting { Name = PrefixName, Value = Prefix },
            new Setting { Name = ModeName, Value = Shared ? SharedValue : PerProductValue },
            new Setting { Name = StandbyName, Value = StandbyKeys ? "true" : "false" });

    /// <summary>Reads the settings.</summary>
    /// <exception cref="ToolRefusal">A row is missing or holds a value setup never writes (also a database left empty by a setup that was killed).</exception>
    public static InstallationSettings Load(ToolDbContext db)
    {
        var rows = db.Settings
            .Where(s => s.Name == PrefixName || s.Name == ModeName || s.Name == StandbyName)
            .ToDictionary(s => s.Name, s => s.Value);

        if (rows.TryGetValue(PrefixName, out var prefix)
            && rows.TryGetValue(ModeName, out var mode)
            && rows.TryGetValue(StandbyName, out var standby)
            && (prefix.Length == 0 || IsPrefix(prefix))
            && mode is PerProductValue or SharedValue
            && standby is "true" or "false")
        {
            return new InstallationSettings(prefix, mode == SharedValue, standby == "true");
        }

        throw new ToolRefusal("This installation's settings are missing or damaged. Nothing changed.");
    }

    private static bool IsPrefix(string value)
    {
        try
        {
            _ = KeyPrefix.Of(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
