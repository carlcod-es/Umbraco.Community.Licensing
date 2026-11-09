namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// The only console access in the tool (ADR-0020): read a line, read hidden, write, write error,
/// person present. Tests derive a scripted console from it.
/// </summary>
internal abstract class ToolConsole
{
    /// <summary>
    /// Raised after each completed answer, a line or a hidden answer, never at end of input. The
    /// session lock restarts its idle timer from it (ADR-0021).
    /// </summary>
    public event Action? AnswerRead;

    /// <summary>True when neither standard input nor standard output is redirected (PDR-0050).</summary>
    public abstract bool PersonPresent { get; }

    /// <summary>Reads one line as typed; null is end of input.</summary>
    public string? ReadLine()
    {
        var line = ReadLineCore();
        if (line is not null)
            AnswerRead?.Invoke();
        return line;
    }

    /// <summary>
    /// Reads a hidden answer, echoing nothing. The caller owns the answer and disposes it, which
    /// zeroes it; null is end of input.
    /// </summary>
    public HiddenAnswer? ReadHidden()
    {
        var answer = ReadHiddenCore();
        if (answer is null)
            return null;
        try
        {
            AnswerRead?.Invoke();
        }
        catch
        {
            answer.Dispose(); // a failing handler must not leave the answer unzeroed
            throw;
        }

        return answer;
    }

    /// <summary>Writes text to standard output, with no line ending.</summary>
    public abstract void Write(string text);

    /// <summary>Writes text to standard output followed by a line ending.</summary>
    public void WriteLine(string text = "") => Write(text + Environment.NewLine);

    /// <summary>Writes a line to standard error; used only for refusals before the session opens.</summary>
    public abstract void WriteError(string text);

    protected abstract string? ReadLineCore();

    protected abstract HiddenAnswer? ReadHiddenCore();
}

/// <summary>The terminal: <see cref="Console"/> with <c>ReadLine</c> and <c>ReadKey(intercept: true)</c>.</summary>
internal sealed class TerminalConsole : ToolConsole
{
    public override bool PersonPresent => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public override void Write(string text) => Console.Out.Write(text);

    public override void WriteError(string text) => Console.Error.WriteLine(text);

    protected override string? ReadLineCore() => Console.ReadLine();

    protected override HiddenAnswer? ReadHiddenCore()
    {
        var answer = new HiddenAnswer();
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (answer.Press(key.Key, key.KeyChar))
                    break;
            }

            Console.Out.WriteLine();
            if (answer.EndOfInput)
            {
                // Ctrl+D (Unix) or Ctrl+Z (Windows) on an empty answer is end of input, as at a line prompt.
                answer.Dispose();
                return null;
            }

            return answer;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // A terminal hangup during ReadKey is end of input, like Ctrl+D or Ctrl+Z or a closed input.
            answer.Dispose();
            return null;
        }
        catch
        {
            answer.Dispose();
            throw;
        }
    }
}
