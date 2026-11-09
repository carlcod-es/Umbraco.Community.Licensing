namespace Umbraco.Community.Licensing.Tool.Tests;

public class QuestionsTests
{
    private static string? Digits(string s) => s.All(char.IsAsciiDigit) && s.Length > 0 ? null : "Digits only.";

    private static (Questions Q, ScriptedConsole C) Make(FakeTimeProvider? clock = null) =>
        MakeWith(new ScriptedConsole(clock: clock));

    private static (Questions Q, ScriptedConsole C) MakeWith(ScriptedConsole c) => (new Questions(c), c);

    /// <summary>A stub menu with one action that asks two questions and writes only at the end.</summary>
    private static int StubMenu(ToolEnvironment env, List<string> written)
    {
        var q = new Questions(env.Console);
        env.Console.WriteLine("Licenses menu");
        try
        {
            var product = q.AskText("Product", "an ID", s => s.Length > 0 ? null : "A product is required.");
            var email = q.AskText("Email", "an address", s => s.Contains('@') ? null : "An email has an @.");
            written.Add(product.Value + "/" + email.Value);
        }
        catch (ActionCancelledException)
        {
            env.Console.WriteLine("Cancelled.");
        }

        env.Console.WriteLine("Licenses menu");
        return 0;
    }

    [Fact]
    public void BadAnswer_NamesRuleAndAsksSameQuestionAgain_KeepingEarlierAnswer()
    {
        var c = new ScriptedConsole().Line("acme.commerce").Line("nope").Line("a@b.com");
        var written = new List<string>();
        var env = new ToolEnvironment(c, new FakeTimeProvider(), "h", "c", 1);

        ToolApp.Run([], env, e => StubMenu(e, written));

        Assert.Equal(["acme.commerce/a@b.com"], written);
        Assert.Contains("An email has an @.", c.Out);
        Assert.Equal(2, CountOf(c.Out, "Email ("));
        Assert.Equal(1, CountOf(c.Out, "Product ("));
    }

    [Fact]
    public void ProposedValue_AcceptedByEmptyAnswer_AndShown()
    {
        var (q, c) = MakeWith(new ScriptedConsole().Line(""));

        var answer = q.AskText("Prefix", "letters", _ => "never", proposed: "LIC");

        Assert.Equal("LIC", answer.Value);
        Assert.Contains("Enter for LIC", c.Out);
        Assert.Contains("/cancel to cancel", c.Out);
    }

    [Fact]
    public void Answers_AreTrimmed()
    {
        var (q, _) = MakeWith(new ScriptedConsole().Line("  123  "));

        Assert.Equal("123", q.AskText("N", "digits", Digits).Value);
    }

    [Fact]
    public void ActionCancelled_StubWritesNothingAndMenuIsShownAgain()
    {
        var c = new ScriptedConsole().Line("acme.commerce").Line("/CANCEL");
        var written = new List<string>();
        var env = new ToolEnvironment(c, new FakeTimeProvider(), "h", "c", 1);

        var exit = ToolApp.Run([], env, e => StubMenu(e, written));

        Assert.Equal(0, exit);
        Assert.Empty(written);
        Assert.Equal(2, CountOf(c.Out, "Licenses menu"));
        Assert.Contains("Cancelled.", c.Out);
    }

    [Fact]
    public void CancelAtHiddenPrompt_CancelsAndZeroesTheBytes()
    {
        var c = new ScriptedConsole().Hidden("/Cancel");
        var q = new Questions(c);

        Assert.Throws<ActionCancelledException>(() => q.AskHidden("Passphrase", "15 to 256 characters", (_, _) => "never"));
        Assert.All(c.HiddenGiven[0].Raw, ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public void RemoveRefusedWhereNotAllowed_AskedAgain()
    {
        var (q, c) = MakeWith(new ScriptedConsole().Line("/remove").Line("5"));

        var answer = q.AskText("N", "digits", Digits);

        Assert.Equal("5", answer.Value);
        Assert.False(answer.Removed);
        Assert.Contains(Questions.RemoveNotAllowed, c.Out);
        Assert.DoesNotContain("/remove to remove", c.Out);
    }

    [Fact]
    public void RemoveAllowed_IsReturnedAndNamedInTheHint()
    {
        var (q, c) = MakeWith(new ScriptedConsole().Line("/REMOVE"));

        var answer = q.AskText("Vendor tag", "text", _ => null, proposed: "old", allowRemove: true);

        Assert.True(answer.Removed);
        Assert.Contains("/remove to remove", c.Out);
    }

    [Fact]
    public void RemoveRefused_AtChoiceAndYesNo()
    {
        var c = new ScriptedConsole().Line("/remove").Line("2").Line("/remove").Line("n");
        var q = new Questions(c);

        Assert.Equal("b", q.AskChoice("Product", [new("a", "A"), new("b", "B")]).Id);
        Assert.False(q.AskYesNo("Sure?", proposed: true));
        Assert.Equal(2, CountOf(c.Out, Questions.RemoveNotAllowed));
    }

    [Fact]
    public void Choice_AnsweredByNumberOrIdAnyCase_AndEmptyTakesProposed()
    {
        var list = new Choice[] { new("acme.commerce", "Acme Commerce"), new("acme.forms", "Acme Forms") };
        var c = new ScriptedConsole().Line("2").Line("ACME.COMMERCE").Line("3").Line("").Line("0").Line("");
        var q = new Questions(c);

        Assert.Equal("acme.forms", q.AskChoice("P", list).Id);
        Assert.Equal("acme.commerce", q.AskChoice("P", list).Id);
        Assert.Equal("acme.forms", q.AskChoice("P", list, proposedId: "acme.forms").Id);
        Assert.Contains("  1 Acme Commerce", c.Out);
        Assert.Contains("Enter for acme.forms", c.Out);
    }

    [Fact]
    public void Choice_OutOfRange_NamesRuleAndAsksAgain()
    {
        var c = new ScriptedConsole().Line("9").Line("1");
        var q = new Questions(c);

        Assert.Equal("a", q.AskChoice("P", [new("a", "A")]).Id);
        Assert.Contains("Answer a number from 1 to 1, or an ID.", c.Out);
    }

    [Theory]
    [InlineData(true, "[Y/n]", "", true)]
    [InlineData(false, "[y/N]", "", false)]
    [InlineData(false, "[y/N]", "YES", true)]
    [InlineData(true, "[Y/n]", "No", false)]
    public void YesNo_ShowsProposedInCapitals(bool proposed, string shown, string answer, bool expected)
    {
        var c = new ScriptedConsole().Line(answer);

        Assert.Equal(expected, new Questions(c).AskYesNo("Sure?", proposed));
        Assert.Contains(shown, c.Out);
    }

    [Fact]
    public void YesNo_BadAnswer_AskedAgain()
    {
        var c = new ScriptedConsole().Line("maybe").Line("y");

        Assert.True(new Questions(c).AskYesNo("Sure?", false));
        Assert.Contains("Answer y or n.", c.Out);
    }

    [Fact]
    public void Cancel_AtEveryQuestionKind()
    {
        var c = new ScriptedConsole().Line("/cancel").Line("/cancel").Line("/cancel");
        var q = new Questions(c);

        Assert.Throws<ActionCancelledException>(() => q.AskText("T", "text", _ => null));
        Assert.Throws<ActionCancelledException>(() => q.AskChoice("C", [new("a", "A")]));
        Assert.Throws<ActionCancelledException>(() => q.AskYesNo("Y", true));
    }

    [Fact]
    public void Hidden_EchoesNothing_HandlesBackspace_AndIsNotTrimmed()
    {
        var c = new ScriptedConsole().Hidden("  pasx\bs ");
        var q = new Questions(c);

        var bytes = q.AskHidden("Passphrase", "15 to 256 characters", (_, _) => null);

        Assert.Equal("  pass ", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.All(c.HiddenGiven[0].Raw, ch => Assert.Equal('\0', ch));
        Assert.DoesNotContain("pass", c.Out);
        Assert.DoesNotContain("*", c.Out);
    }

    [Fact]
    public void Hidden_RefusedAnswer_NamesRuleAsksAgainAndZeroesRefused()
    {
        var c = new ScriptedConsole().Hidden("short").Hidden("long enough passphrase");
        var q = new Questions(c);

        var bytes = q.AskHidden("Passphrase", "15 to 256 characters", PassphraseLike);

        Assert.Equal(22, bytes.Length);
        Assert.Contains("At least 15 characters.", c.Out);
        Assert.All(c.HiddenGiven[0].Raw, ch => Assert.Equal('\0', ch));
        Assert.All(c.HiddenGiven[1].Raw, ch => Assert.Equal('\0', ch));
    }

    // Mirrors the passphrase rule of ADR-0019: U+0020 to U+007E only, 15 to 256 long, no space at either end.
    private static string? PassphraseLike(ReadOnlySpan<char> held, int length)
    {
        foreach (var ch in held)
            if (ch is < ' ' or > '~')
                return "Printable ASCII only.";
        if (length < 15)
            return "At least 15 characters.";
        if (length > 256)
            return "At most 256 characters.";
        if (held[0] == ' ' || held[^1] == ' ')
            return "No space at either end.";
        return null;
    }

    [Fact]
    public void Hidden_NonAsciiKeyIsKeptAsTyped_SoTheRuleRefusesIt()
    {
        var c = new ScriptedConsole().Hidden("Grüße aus München 2026").Hidden("Gruesse aus Muenchen 2026");
        var q = new Questions(c);
        var seen = new List<string>();

        var bytes = q.AskHidden("Passphrase", "15 to 256 characters", (held, length) =>
        {
            seen.Add(held.ToString() + "|" + length);
            return PassphraseLike(held, length);
        });

        Assert.Equal("Grüße aus München 2026|22", seen[0]);
        Assert.Contains("Printable ASCII only.", c.Out);
        Assert.Equal("Gruesse aus Muenchen 2026", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.All(c.HiddenGiven[0].Raw, ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public void Hidden_MoreThanTheBufferIsStillCountedAndSeenAsTooLong()
    {
        var c = new ScriptedConsole().Hidden(new string('a', 300)).Hidden(new string('a', 256));
        var q = new Questions(c);
        var lengths = new List<int>();

        var bytes = q.AskHidden("Passphrase", "15 to 256 characters", (held, length) =>
        {
            lengths.Add(length);
            Assert.True(held.Length <= HiddenAnswer.Capacity);
            return PassphraseLike(held, length);
        });

        Assert.Equal(300, lengths[0]);
        Assert.Contains("At most 256 characters.", c.Out);
        Assert.Equal(256, bytes.Length);
    }

    [Fact]
    public void HiddenAnswer_BackspaceAcrossTheBufferBoundary_KeepsCountAndBufferConsistent()
    {
        using var a = new HiddenAnswer();
        for (var i = 0; i < 260; i++)
            a.Press(ConsoleKey.A, 'a');
        Assert.Equal(260, a.Length);
        Assert.Equal(HiddenAnswer.Capacity, a.Chars.Length);

        for (var i = 0; i < 3; i++)
            a.Press(ConsoleKey.Backspace, '\b');
        Assert.Equal(257, a.Length);
        Assert.Equal(257, a.Chars.Length);

        a.Press(ConsoleKey.Backspace, '\b');
        Assert.Equal(256, a.Length);
        Assert.Equal(256, a.Chars.Length);
        Assert.Equal('\0', a.Raw[256]);

        a.Press(ConsoleKey.B, 'b');
        Assert.Equal('b', a.Chars[^1]);
    }

    [Fact]
    public void HiddenAnswer_NonCharacterKeysAreIgnored_AndDisposeZeroes()
    {
        var a = new HiddenAnswer();
        a.Press(ConsoleKey.LeftArrow, '\0');
        a.Press(ConsoleKey.Tab, '\t');
        a.Press(ConsoleKey.Escape, '\u001b');
        a.Press(ConsoleKey.X, 'x');
        a.Press(ConsoleKey.Backspace, '\b');
        a.Press(ConsoleKey.Backspace, '\b');
        a.Press(ConsoleKey.Y, 'ü');

        Assert.Equal("ü", a.Chars.ToString());
        Assert.True(a.Press(ConsoleKey.Enter, '\r'));

        a.Dispose();
        Assert.All(a.Raw, ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public void HiddenAnswer_NonAsciiBecomesUtf8Bytes_SoALaterCheckFindsItWrong()
    {
        using var a = new HiddenAnswer();
        foreach (var ch in "aü")
            a.Press(ConsoleKey.NoName, ch);

        Assert.Equal([(byte)'a', 0xC3, 0xBC], a.ToBytes());
    }

    [Theory]
    [InlineData('\u0004')] // Ctrl+D on Unix
    [InlineData('\u001A')] // Ctrl+Z on Windows
    public void HiddenAnswer_EndOfInputKeyOnAnEmptyAnswer_IsEndOfInput(char key)
    {
        using var a = new HiddenAnswer();

        Assert.True(a.Press(ConsoleKey.D, key));

        Assert.True(a.EndOfInput);
        Assert.Equal(0, a.Length);
    }

    [Fact]
    public void HiddenAnswer_EndOfInputKeyAfterOtherKeys_IsIgnored()
    {
        using var a = new HiddenAnswer();
        a.Press(ConsoleKey.A, 'a');

        Assert.False(a.Press(ConsoleKey.D, '\u0004'));

        Assert.False(a.EndOfInput);
        Assert.Equal(1, a.Length);
    }

    [Fact]
    public void EndOfInputKeyAtTheHiddenPrompt_ThrowsEndOfInput_AndZeroesTheAnswer()
    {
        var c = new ScriptedConsole().Hidden("\u0004");
        var q = new Questions(c);

        Assert.Throws<EndOfInputException>(() => q.AskHidden("Passphrase", "any text", (_, _) => null));
        Assert.All(Assert.Single(c.HiddenGiven).Raw, ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public void EndOfInput_ThrowsAtLineAndHiddenQuestions()
    {
        var q = new Questions(new ScriptedConsole());

        Assert.Throws<EndOfInputException>(() => q.AskText("T", "text", _ => null));
        Assert.Throws<EndOfInputException>(() => q.AskHidden("P", "any text", (_, _) => null));
    }

    [Fact]
    public void AnswerRead_RaisedForEachCompletedLineAndHiddenAnswer_NotAtEndOfInput()
    {
        var c = new ScriptedConsole().Line("a").Hidden("b");
        var raised = 0;
        c.AnswerRead += () => raised++;

        c.ReadLine();
        c.ReadHidden();
        c.ReadLine();

        Assert.Equal(2, raised);
    }

    [Fact]
    public void AnswerReadHandlerThrowing_DisposesTheHiddenAnswer_AndRethrows()
    {
        var c = new ScriptedConsole().Hidden("secret");
        c.AnswerRead += () => throw new InvalidOperationException("handler");

        Assert.Throws<InvalidOperationException>(() => c.ReadHidden());

        var given = Assert.Single(c.HiddenGiven);
        Assert.All(given.Raw, ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public void ScriptedConsole_AdvancesFakeClockBeforeAnAnswer_FiringTimers()
    {
        var clock = new FakeTimeProvider();
        var c = new ScriptedConsole(clock: clock).Line("a", TimeSpan.FromMinutes(5));
        var fired = 0;
        using var timer = clock.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(3), Timeout.InfiniteTimeSpan);
        var start = clock.GetUtcNow();

        c.ReadLine();

        Assert.Equal(1, fired);
        Assert.Equal(start.AddMinutes(5), clock.GetUtcNow());
    }

    [Fact]
    public void FakeTimeProvider_RestartedTimerFiresOnlyAtItsNewDueTime()
    {
        var clock = new FakeTimeProvider();
        var fired = 0;
        using var timer = clock.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(3), Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromMinutes(2));
        timer.Change(TimeSpan.FromMinutes(3), Timeout.InfiniteTimeSpan);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(0, fired);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, fired);
    }

    private static int CountOf(string text, string part)
    {
        var n = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
            n++;
        return n;
    }
}
