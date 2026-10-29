using System.Text;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The tool's console. Results go to <see cref="Out"/> (stdout); messages and prompts go to
/// <see cref="Error"/> (stderr). Everything is injected so tests script input and capture both streams.
/// </summary>
/// <param name="input">Standard input.</param>
/// <param name="output">Standard output: results only.</param>
/// <param name="error">Standard error: messages and prompts.</param>
/// <param name="inputRedirected">Whether stdin is redirected (nobody at a terminal).</param>
/// <param name="readKey">Reads one key without echo; production uses <c>Console.ReadKey(intercept: true)</c>.</param>
internal sealed class ToolConsole(
    TextReader input,
    TextWriter output,
    TextWriter error,
    bool inputRedirected,
    Func<ConsoleKeyInfo> readKey)
{
    public TextWriter Out { get; } = output;

    public TextWriter Error { get; } = error;

    /// <summary>Whether stdin is redirected, so nobody can answer a prompt.</summary>
    public bool InputRedirected { get; } = inputRedirected;

    /// <summary>The console of the running process.</summary>
    public static ToolConsole FromProcess() => new(
        Console.In,
        Console.Out,
        Console.Error,
        Console.IsInputRedirected,
        () => Console.ReadKey(intercept: true));

    /// <summary>Interactive when stdin is not redirected and <c>--no-input</c> is absent.</summary>
    public bool IsInteractive(bool noInput) => !noInput && !InputRedirected;

    /// <summary>Reads one line of stdin; null at end of input.</summary>
    public string? ReadLine() => input.ReadLine();

    /// <summary>Writes a prompt to stderr, then reads one line of stdin.</summary>
    public string? Prompt(string text)
    {
        Error.Write(text);
        Error.Flush();
        return ReadLine();
    }

    /// <summary>
    /// Writes a prompt to stderr, then reads a line from the keyboard without echoing it. Call it through
    /// <see cref="ToolContext.PromptHidden"/>, which refuses when nobody is present. Stdin is never a source.
    /// </summary>
    /// <exception cref="InvalidOperationException">Stdin is redirected: there are no keys to read.</exception>
    internal string PromptHidden(string text)
    {
        if (InputRedirected)
        {
            throw new InvalidOperationException("A hidden prompt needs a keyboard, not redirected input.");
        }

        Error.Write(text);
        Error.Flush();

        var buffer = new StringBuilder();
        while (true)
        {
            var key = readKey();
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }

        Error.WriteLine();
        return buffer.ToString();
    }
}
