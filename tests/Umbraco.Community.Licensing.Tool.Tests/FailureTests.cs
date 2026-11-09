namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Unexpected failure in a session (PDR-0053): a failed action returns to its menu, a failure outside one ends the session.</summary>
public class FailureTests : IDisposable
{
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static ScriptedConsole Script(params string[] answers)
    {
        var console = new ScriptedConsole();
        foreach (var a in answers)
            console.Line(a);
        return console;
    }

    private static byte[] Passphrase()
    {
        var pinned = GC.AllocateArray<byte>(8, pinned: true);
        System.Text.Encoding.ASCII.GetBytes("abcdefgh", pinned);
        return pinned;
    }

    private static bool AllZero(byte[] bytes) => bytes.All(b => b == 0);

    private static void AssertNoTechnicalDetail(string transcript)
    {
        Assert.DoesNotContain("Exception", transcript);
        Assert.DoesNotContain("System.", transcript);
        Assert.DoesNotContain("   at ", transcript);
    }

    [Fact]
    public void ActionFailsUnexpectedly_OneLine_NothingOnStderr_MenuShownAgain_PassphraseStillHeld()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var held = Passphrase();
        var heldAtList = false;
        var actions = new ActionTable();
        actions.Register("backup.export", s =>
        {
            s.Lock.Hold(held);
            s.Console.WriteLine("Writing the backup...");
            throw new IOException("There is not enough space on the disk.\r\nTry again.");
        });
        actions.Register("products.list", s => heldAtList = s.Lock.IsHeld && !AllZero(held));
        SessionTestHost.MakeInstallation(_host.Cwd);
        // Backup, Export (fails), Back, Products, List, Back, Quit.
        var console = Script("4", "1", "0", "1", "4", "0", "0");

        var exit = _host.Run(console, actions);

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        Assert.Equal(1, SessionTestHost.Count(console.Out, " failed: "));
        Assert.Contains("Backup - Export failed: There is not enough space on the disk. Try again.", console.Out);
        // Backup was shown again after the failure, and the session carried on.
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "Backup"));
        Assert.True(heldAtList);
        Assert.True(AllZero(held)); // zeroed when the session ended
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void SetUpFailsAtTheOffer_OneLine_OfferShownAgain_AndRestoreFailsTheSame()
    {
        var offer = new Startup.Offer(
            _ => throw new IOException("Cannot write to the folder."),
            _ => throw new UnauthorizedAccessException("Access denied."));
        var console = Script("1", "2", "0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        var lines = SessionTestHost.Lines(console.Out);
        Assert.Contains("Set up a new installation failed: Cannot write to the folder.", console.Out);
        Assert.Contains("Restore from a backup failed: Access denied.", console.Out);
        Assert.Equal(3, lines.Count(l => l == "What now?"));
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void FailureAtTheOffer_DoesNotTouchTheLock()
    {
        byte[]? held = null;
        SessionLock? sessionLock = null;
        var offer = new Startup.Offer(
            s =>
            {
                held = Passphrase();
                s.Lock.Hold(held);
                sessionLock = s.Lock;
                throw new IOException("disk full");
            },
            Startup.Offer.Stubs.Restore);

        var exit = _host.Run(Script("1", "0"), offer: offer);

        Assert.Equal(0, exit);
        Assert.True(AllZero(held!)); // only the session's end zeroed it
        Assert.NotNull(sessionLock);
    }

    [Fact]
    public void PassphraseHeldAtTheFailure_IsNotZeroed_UntilTheSessionEnds()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var held = Passphrase();
        var zeroedAfterFailure = true;
        var actions = new ActionTable();
        actions.Register("products.add", s => { s.Lock.Hold(held); throw new IOException("x"); });
        actions.Register("products.list", s => zeroedAfterFailure = AllZero(held));

        _host.Run(Script("1", "1", "4", "0", "0"), actions);

        Assert.False(zeroedAfterFailure);
    }

    [Fact]
    public void FailureOutsideAnAction_OneLine_ExitOne_ZeroesThePassphrase_NothingOnStderr()
    {
        var held = Passphrase();
        var console = new ScriptedConsole();

        var exit = ToolApp.Run(
            [],
            new ToolEnvironment(console, new FakeTimeProvider(), _host.Home, _host.Cwd, 1000),
            env =>
            {
                using var session = new Session(env);
                session.Lock.Hold(held);
                throw new InvalidOperationException("The database is damaged.");
            });

        Assert.Equal(1, exit);
        Assert.True(AllZero(held));
        Assert.Equal("", console.Err);
        Assert.Equal("The session ended: The database is damaged.", SessionTestHost.Lines(console.Out).Single(l => l.Length > 0));
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void FatalFailureInAnAction_GoesToTheBackstop_AndTheMenuLoopsSessionIsDisposed()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var held = Passphrase();
        var actions = new ActionTable();
        actions.Register("products.add", s => { s.Lock.Hold(held); throw new OutOfMemoryException("Out of memory."); });
        var console = Script("1", "1", "0", "0");

        var exit = _host.Run(console, actions);

        Assert.Equal(1, exit);
        Assert.True(AllZero(held));
        Assert.Equal("", console.Err);
        Assert.Contains("The session ended: Out of memory.", console.Out);
        Assert.DoesNotContain(" failed: ", console.Out);
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void CancelAndEndOfInput_KeepTheirHandling()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var actions = new ActionTable();
        actions.Register("products.add", _ => throw new ActionCancelledException());
        actions.Register("products.list", _ => throw new EndOfInputException());

        var cancelled = Script("1", "1", "4");
        var exit = _host.Run(cancelled, actions);

        Assert.Equal(1, exit);
        Assert.Contains("Cancelled.", cancelled.Out);
        Assert.DoesNotContain(" failed: ", cancelled.Out);
        Assert.DoesNotContain("The session ended", cancelled.Out);
    }

    [Fact]
    public void AMessageWithNoText_StillGivesAReadableLine()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var actions = new ActionTable();
        actions.Register("products.add", _ => throw new IOException(""));
        var console = Script("1", "1", "0", "0");

        _host.Run(console, actions);

        Assert.Contains("Products - Add a product failed: an unexpected error.", console.Out);
    }
}
