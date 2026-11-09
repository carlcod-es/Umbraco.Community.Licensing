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

    /// <summary>A console whose standard output fails when the closing message of setup is written.</summary>
    private sealed class FailsAtClosingMessage : ScriptedConsole
    {
        public override void Write(string text)
        {
            if (text.Contains("Setup is complete", StringComparison.Ordinal))
                throw new IOException("The pipe is closed.");
            base.Write(text);
        }
    }

    [Fact]
    public void SetUpFailingAfterThePassphraseIsHeld_ReleasesIt_RemovesWhatItMade_AndShowsTheOfferAgain()
    {
        // The closing message is written as the session takes the folder, after Setup.Run returned with the passphrase held.
        var heldWhenOfferedAgain = true;
        var calls = 0;
        var offer = new Startup.Offer(
            s =>
            {
                if (++calls > 1)
                    heldWhenOfferedAgain = s.Lock.IsHeld;
                return Setup.Run(s);
            },
            Startup.Offer.Stubs.Restore);
        var console = new FailsAtClosingMessage().SetUp().Line("1").Line("/cancel").Line("0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        Assert.Contains("Set up a new installation failed: The pipe is closed.", console.Out);
        Assert.Equal(2, calls);
        Assert.False(heldWhenOfferedAgain);
        Assert.False(Directory.Exists(_host.HomeInstallation));
        Assert.Equal(3, SessionTestHost.Lines(console.Out).Count(l => l == "What now?"));
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void SetUpFailingBeforeAnythingIsHeld_CreatesNothing_HoldsNothing()
    {
        // .ucl is a file, so the folder cannot be made.
        File.WriteAllText(_host.HomeInstallation, "in the way");
        var heldAfter = true;
        var offer = new Startup.Offer(
            s =>
            {
                try { return Setup.Run(s); }
                finally { heldAfter = s.Lock.IsHeld; }
            },
            Startup.Offer.Stubs.Restore);
        var console = new ScriptedConsole().SetUp().Line("0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        Assert.Contains("Set up a new installation failed: ", console.Out);
        Assert.False(heldAfter);
        Assert.Equal("in the way", File.ReadAllText(_host.HomeInstallation));
        Assert.Equal(2, SessionTestHost.Lines(console.Out).Count(l => l == "What now?"));
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void NewInstallationTheSessionCannotUse_IsAFailedSetUp_NothingHeld_NothingLeft_OfferShownAgain()
    {
        // A setup stub that omits the IdleLock setting: the session refuses the new database after the offer returned.
        var stub = Path.Combine(_host.Home, "stub");
        var heldAtOffer = new List<bool>();
        var offer = new Startup.Offer(
            s =>
            {
                heldAtOffer.Add(s.Lock.IsHeld);
                SessionTestHost.MakeInstallation(stub, idleLock: null);
                s.Lock.Hold(Passphrase());
                return new Provisioned(stub, () => Directory.Delete(stub, recursive: true));
            },
            Startup.Offer.Stubs.Restore);
        var console = Script("1", "1", "0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        var failed = SessionTestHost.Lines(console.Out).First(l => l.Contains("Set up a new installation failed: ", StringComparison.Ordinal));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Set up a new installation failed: "));
        Assert.Contains("idle lock setting", failed);
        Assert.Equal([false, false], heldAtOffer);
        Assert.False(Directory.Exists(stub));
        Assert.Equal(3, SessionTestHost.Lines(console.Out).Count(l => l == "What now?"));
        Assert.DoesNotContain("Main menu", console.Out);
        AssertNoTechnicalDetail(console.Out);
    }

    [Fact]
    public void RealSetUpWhoseNewInstallationIsRefusedByTheSession_RemovesTheFolderItMade()
    {
        var console = new ScriptedConsole().SetUp().Line("0");

        var exit = _host.Run(
            console,
            offer: Startup.Offer.Default,
            readIdleTime: _ => throw new InstallationRefusedException("The new database is refused."));

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        Assert.Contains("Set up a new installation failed: The new database is refused.", console.Out);
        Assert.False(Directory.Exists(_host.HomeInstallation));
        Assert.DoesNotContain("Main menu", console.Out);
        // A folder the session refuses is never announced as complete (follow-ups row 64).
        Assert.DoesNotContain("Setup is complete", console.Out);
        Assert.DoesNotContain("Next steps", console.Out);
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

    [Fact]
    public void BackstopWithStandardOutputClosed_StillExitsWith1()
    {
        var exit = ToolApp.Run(
            [],
            new ToolEnvironment(new ClosedOutputConsole(), new FakeTimeProvider(), _host.Home, _host.Cwd, 1000),
            _ => throw new InvalidOperationException("boom"));

        Assert.Equal(1, exit);
    }

    /// <summary>A terminal whose standard output has been closed: every write fails.</summary>
    private sealed class ClosedOutputConsole : ToolConsole
    {
        public override bool PersonPresent => true;
        public override void Write(string text) => throw new IOException("The pipe is closed.");
        public override void WriteError(string text) => throw new IOException("The pipe is closed.");
        protected override string? ReadLineCore() => null;
        protected override HiddenAnswer? ReadHiddenCore() => null;
    }
}
