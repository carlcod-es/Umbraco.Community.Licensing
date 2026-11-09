namespace Umbraco.Community.Licensing.Tool;

/// <summary>An answer to a text question: a value, or the vendor asked to remove it (<c>/remove</c>).</summary>
internal readonly record struct Answer(string Value, bool Removed = false);

/// <summary>One entry of a numbered list: the ID the vendor may type and the text shown beside its number.</summary>
internal sealed record Choice(string Id, string Label);

/// <summary>A rule over a hidden answer: the characters held and the true length typed; the rule broken, or null.</summary>
internal delegate string? HiddenRule(ReadOnlySpan<char> held, int length);

/// <summary>
/// Asks the questions of an action (ADR-0020). A refused answer names its rule and the same
/// question is asked again, so earlier answers held by the caller are kept. <c>/cancel</c> throws
/// <see cref="ActionCancelledException"/>; end of input throws <see cref="EndOfInputException"/>.
/// </summary>
internal sealed class Questions(ToolConsole console)
{
    internal const string Cancel = "/cancel";
    internal const string Remove = "/remove";

    internal const string RemoveNotAllowed = "/remove is not accepted at this question.";

    /// <summary>
    /// Asks for text. <paramref name="rule"/> returns the rule broken, or null when the answer is
    /// fine. An empty answer takes <paramref name="proposed"/> when there is one, else goes to the rule.
    /// </summary>
    public Answer AskText(
        string question,
        string accepts,
        Func<string, string?> rule,
        string? proposed = null,
        bool allowRemove = false)
    {
        while (true)
        {
            var hint = Hint(accepts, proposed is null ? null : $"Enter for {proposed}", allowRemove);
            console.Write($"{question} ({hint}): ");
            var answer = ReadLine().Trim();

            if (Same(answer, Cancel))
                throw new ActionCancelledException();
            if (Same(answer, Remove))
            {
                if (allowRemove)
                    return new Answer("", Removed: true);
                console.WriteLine(RemoveNotAllowed);
                continue;
            }

            if (answer.Length == 0 && proposed is not null)
                return new Answer(proposed);

            var broken = rule(answer);
            if (broken is null)
                return new Answer(answer);
            console.WriteLine(broken);
        }
    }

    /// <summary>Asks for one entry of a numbered list, answered by its number or its ID (any case).</summary>
    public Choice AskChoice(string question, IReadOnlyList<Choice> choices, string? proposedId = null)
    {
        var proposed = proposedId is null ? null : choices.FirstOrDefault(c => Same(c.Id, proposedId));
        var accepts = $"a number from 1 to {choices.Count}, or an ID";
        while (true)
        {
            for (var i = 0; i < choices.Count; i++)
                console.WriteLine($"  {i + 1} {choices[i].Label}");

            var hint = Hint(accepts, proposed is null ? null : $"Enter for {proposed.Id}", allowRemove: false);
            console.Write($"{question} ({hint}): ");
            var answer = ReadLine().Trim();

            if (Same(answer, Cancel))
                throw new ActionCancelledException();
            if (Same(answer, Remove))
            {
                console.WriteLine(RemoveNotAllowed);
                continue;
            }

            if (answer.Length == 0 && proposed is not null)
                return proposed;

            if (int.TryParse(answer, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
                && number >= 1 && number <= choices.Count)
                return choices[number - 1];

            var byId = choices.FirstOrDefault(c => Same(c.Id, answer));
            if (byId is not null && answer.Length > 0)
                return byId;

            console.WriteLine($"Answer {accepts}.");
        }
    }

    /// <summary>Asks yes or no; the proposed answer is shown in capitals and taken by an empty answer.</summary>
    public bool AskYesNo(string question, bool proposed)
    {
        var options = proposed ? "Y/n" : "y/N";
        while (true)
        {
            console.Write($"{question} [{options}] (Enter for {(proposed ? "yes" : "no")}; {Cancel} to cancel): ");
            var answer = ReadLine().Trim();

            if (Same(answer, Cancel))
                throw new ActionCancelledException();
            if (Same(answer, Remove))
            {
                console.WriteLine(RemoveNotAllowed);
                continue;
            }

            if (answer.Length == 0)
                return proposed;
            if (Same(answer, "y") || Same(answer, "yes"))
                return true;
            if (Same(answer, "n") || Same(answer, "no"))
                return false;
            console.WriteLine("Answer y or n.");
        }
    }

    /// <summary>
    /// Asks at the hidden prompt, nothing echoed and the answer not trimmed. <paramref name="rule"/>
    /// sees the characters held and the true length typed (which can exceed them) and returns the
    /// rule broken, or null. The caller owns the returned pinned bytes and zeroes them. The typed
    /// characters are zeroed on every path: accepted, refused, <c>/cancel</c>, end of input.
    /// </summary>
    public byte[] AskHidden(string question, HiddenRule rule)
    {
        while (true)
        {
            console.Write($"{question} (typed hidden; {Cancel} to cancel): ");
            using var answer = console.ReadHidden() ?? throw new EndOfInputException();

            if (answer.Is(Cancel))
                throw new ActionCancelledException();

            var broken = rule(answer.Chars, answer.Length);
            if (broken is null)
                return answer.ToBytes();

            console.WriteLine(broken);
        }
    }

    private string ReadLine() => console.ReadLine() ?? throw new EndOfInputException();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Hint(string accepts, string? proposed, bool allowRemove)
    {
        var parts = new List<string> { accepts };
        if (proposed is not null)
            parts.Add(proposed);
        parts.Add($"{Cancel} to cancel");
        if (allowRemove)
            parts.Add($"{Remove} to remove it");
        return string.Join("; ", parts);
    }
}
