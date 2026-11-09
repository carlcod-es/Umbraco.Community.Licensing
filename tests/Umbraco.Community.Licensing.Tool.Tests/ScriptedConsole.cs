using System.Text;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// The scripted console: queued answers (lines and hidden answers), each optionally preceded by a
/// fake-clock advance, and separate transcripts of stdout and stderr. An empty queue is end of input.
/// </summary>
internal sealed class ScriptedConsole : ToolConsole
{
    private readonly Queue<(object Answer, TimeSpan Advance)> _answers = new();
    private readonly StringBuilder _out = new();
    private readonly StringBuilder _err = new();
    private readonly FakeTimeProvider? _clock;

    public ScriptedConsole(bool inputRedirected = false, bool outputRedirected = false, FakeTimeProvider? clock = null)
    {
        InputRedirected = inputRedirected;
        OutputRedirected = outputRedirected;
        _clock = clock;
    }

    public bool InputRedirected { get; }
    public bool OutputRedirected { get; }
    public override bool PersonPresent => !InputRedirected && !OutputRedirected;

    /// <summary>Everything written to stdout.</summary>
    public string Out => _out.ToString();

    /// <summary>Everything written to stderr.</summary>
    public string Err => _err.ToString();

    /// <summary>Hidden answers returned so far, so a test can check their buffers were zeroed.</summary>
    public List<HiddenAnswer> HiddenGiven { get; } = [];

    /// <summary>Queues a line answer, advancing the fake clock by <paramref name="advanceBefore"/> first.</summary>
    public ScriptedConsole Line(string answer, TimeSpan advanceBefore = default)
    {
        _answers.Enqueue((answer, advanceBefore));
        return this;
    }

    /// <summary>Queues a hidden answer; typed Backspace characters (<c>\b</c>) erase the character before them and every other character, non-ASCII included, is typed.</summary>
    public ScriptedConsole Hidden(string typed, TimeSpan advanceBefore = default)
    {
        _answers.Enqueue((new HiddenTyped(typed), advanceBefore));
        return this;
    }

    public override void Write(string text) => _out.Append(text);

    public override void WriteError(string text) => _err.AppendLine(text);

    protected override string? ReadLineCore()
    {
        if (!Next(out var answer))
            return null;
        return answer as string ?? throw new InvalidOperationException("A hidden answer was queued where a line was read.");
    }

    protected override HiddenAnswer? ReadHiddenCore()
    {
        if (!Next(out var answer))
            return null;
        if (answer is not HiddenTyped typed)
            throw new InvalidOperationException("A line answer was queued where a hidden answer was read.");

        // The same key handling as the terminal; only the key source differs.
        var hidden = new HiddenAnswer();
        foreach (var c in typed.Text)
        {
            if (c == '\b')
                hidden.Press(ConsoleKey.Backspace, c);
            else
                hidden.Press(ConsoleKey.NoName, c);
        }

        hidden.Press(ConsoleKey.Enter, '\r');
        HiddenGiven.Add(hidden);
        return hidden;
    }

    private bool Next(out object answer)
    {
        if (_answers.Count == 0)
        {
            answer = "";
            return false;
        }

        var (queued, advance) = _answers.Dequeue();
        if (advance > TimeSpan.Zero)
            (_clock ?? throw new InvalidOperationException("No fake clock given to advance.")).Advance(advance);
        answer = queued;
        return true;
    }

    private sealed record HiddenTyped(string Text);
}
