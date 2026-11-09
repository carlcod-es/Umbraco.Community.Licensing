namespace Umbraco.Community.Licensing.Tool;

/// <summary>Thrown by the question helper when the vendor answers <c>/cancel</c>; the menu loop catches it and shows its menu.</summary>
internal sealed class ActionCancelledException() : Exception("Action cancelled.");

/// <summary>Thrown when input ends (the terminal closed); <see cref="ToolApp.Run"/> ends the session with exit 1.</summary>
internal sealed class EndOfInputException() : Exception("End of input.");

/// <summary>The one-line report of an unexpected failure (PDR-0053): the error's own message, never its type or stack.</summary>
internal static class Failure
{
    /// <summary>
    /// False for failures the tool cannot meaningfully continue from; these go to the backstop in
    /// <see cref="ToolApp"/>, which ends the session. (A stack overflow cannot be caught at all.)
    /// </summary>
    public static bool IsContinuable(Exception exception) =>
        exception is not (ActionCancelledException or EndOfInputException or OutOfMemoryException or AccessViolationException);

    /// <summary>The message on one line, with no type name, stack or inner exception.</summary>
    public static string OneLine(Exception exception)
    {
        var text = string.Join(' ', exception.Message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return text.Length == 0 ? "an unexpected error." : text;
    }
}

/// <summary>
/// Thrown when an installation cannot be used: its database cannot be opened, holds a migration this tool does
/// not know, or lacks a setting the tool needs. At startup <see cref="ToolApp.Run"/> writes the message on stderr and
/// exits 1 before the session opens; inside an action it is an ordinary action failure (PDR-0053).
/// </summary>
internal sealed class InstallationRefusedException(string message) : Exception(message);
