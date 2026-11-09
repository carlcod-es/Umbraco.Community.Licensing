namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// One menu entry: a submenu (<see cref="Children"/>) or an action found by <see cref="Key"/> in the
/// <see cref="ActionTable"/>. The tree, numbers and order are design.md "Menu session".
/// </summary>
internal sealed record MenuNode(string Label, string? Key = null, IReadOnlyList<MenuNode>? Children = null)
{
    public bool IsSubmenu => Children is not null;
}

/// <summary>The menu tree and the action keys later sections register against.</summary>
internal static class Menus
{
    public const string FolderInUse = "settings.folder";
    public const string NewInstallationLabel = "New installation in the current folder";
    public const string SetUpHere = "settings.new.setup";
    public const string RestoreHere = "settings.new.restore";
    public const string BackupRestore = "backup.restore";

    public static IReadOnlyList<MenuNode> Main { get; } =
    [
        new("Products", Children:
        [
            new("Add a product", "products.add"),
            new("Rename a product", "products.rename"),
            new("Add a feature", "products.feature"),
            new("List products", "products.list"),
        ]),
        new("Licenses", Children:
        [
            new("Issue", "licenses.issue"),
            new("Reissue", "licenses.reissue"),
            new("Renew", "licenses.renew"),
            new("Resend", "licenses.resend"),
            new("Find and list", "licenses.find"),
            new("Show a license", "licenses.show"),
            new("Customer details", "licenses.customer"),
        ]),
        new("Signing keys", Children:
        [
            new("Status", "keys.status"),
            new("Add a standby key", "keys.standby"),
            new("Switch", "keys.switch"),
            new("Trusted keys", "keys.trusted"),
            new("Release confirmed", "keys.release"),
            new("Reissues sent", "keys.reissues"),
            new("Export by signing key", "keys.export"),
        ]),
        new("Backup", Children:
        [
            new("Export", "backup.export"),
            new("Restore into the current folder", BackupRestore),
        ]),
        new("Settings", Children:
        [
            new("Folder in use", FolderInUse),
            new("Prefix", "settings.prefix"),
            new("Idle lock", "settings.idle"),
            new(NewInstallationLabel, Children:
            [
                new("Set up", SetUpHere),
                new("Restore from a backup", RestoreHere),
            ]),
        ]),
    ];

    /// <summary>Every action key in the tree.</summary>
    public static IEnumerable<string> Keys(IReadOnlyList<MenuNode>? nodes = null)
    {
        foreach (var node in nodes ?? Main)
        {
            if (node.Key is not null)
                yield return node.Key;
            if (node.Children is not null)
                foreach (var key in Keys(node.Children))
                    yield return key;
        }
    }
}

/// <summary>
/// What each menu entry does, by key. Every entry starts as a stub; a section replaces its
/// entries with <see cref="Register"/> and the menu loop is untouched.
/// </summary>
internal sealed class ActionTable
{
    private readonly Dictionary<string, Action<Session>> _actions = new(StringComparer.Ordinal);

    /// <summary>The table the tool runs with: stubs everywhere except what is implemented.</summary>
    public static ActionTable Default()
    {
        var table = new ActionTable();
        table.Register(Menus.FolderInUse, session => session.WriteFolderInUse());
        SettingsActions.Register(table);
        ProductActions.Register(table);
        return table;
    }

    /// <summary>Sets the action for a key, replacing a stub. An action that throws <see cref="ActionCancelledException"/> returns to its menu.</summary>
    public void Register(string key, Action<Session> action) => _actions[key] = action;

    public void Run(string key, string label, Session session)
    {
        if (_actions.TryGetValue(key, out var action))
            action(session);
        else
            session.Console.WriteLine($"{label} is not available yet.");
    }
}
