namespace Umbraco.Community.Licensing.Tool;

/// <summary>Process exit codes (design.md Command surface).</summary>
internal static class ExitCodes
{
    /// <summary>The action completed.</summary>
    public const int Done = 0;

    /// <summary>The action was refused and changed nothing.</summary>
    public const int Refused = 1;

    /// <summary>The command line could not be understood.</summary>
    public const int Usage = 2;
}
