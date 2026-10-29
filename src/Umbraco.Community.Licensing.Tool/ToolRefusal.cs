namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// Thrown by a command to refuse: the message goes to stderr, nothing is written to stdout, exit code 1.
/// Commands gather and check their inputs before changing anything, so a refusal changes nothing.
/// </summary>
internal sealed class ToolRefusal(string message) : Exception(message);
