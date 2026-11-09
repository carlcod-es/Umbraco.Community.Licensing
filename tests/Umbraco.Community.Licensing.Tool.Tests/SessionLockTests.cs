namespace Umbraco.Community.Licensing.Tool.Tests;

public class SessionLockTests : IDisposable
{
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(10);
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static byte[] Passphrase()
    {
        var pinned = GC.AllocateArray<byte>(8, pinned: true);
        System.Text.Encoding.ASCII.GetBytes("abcdefgh", pinned);
        return pinned;
    }

    private static bool AllZero(byte[] bytes) => bytes.All(b => b == 0);

    private static string Read(SessionLock l)
    {
        var text = "";
        l.TryUse(p => text = System.Text.Encoding.ASCII.GetString(p));
        return text;
    }

    private static (SessionLock Lock, FakeTimeProvider Clock, byte[] Held) Held(TimeSpan? idle)
    {
        var clock = new FakeTimeProvider();
        var l = new SessionLock(clock);
        l.SetIdleTime(idle);
        var held = Passphrase();
        l.Hold(held);
        return (l, clock, held);
    }

    [Fact]
    public void IdleTimePassesWithNoAnswer_TimerZeroesTheHandedInArray_AndDropsIt()
    {
        var (l, clock, held) = Held(Idle);

        clock.Advance(Idle - TimeSpan.FromSeconds(1));
        Assert.True(l.IsHeld);
        Assert.False(AllZero(held));

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(AllZero(held));
        Assert.False(l.TryUse(_ => Assert.Fail("nothing should be held")));
    }

    [Fact]
    public void SetAside_HidesTheArray_ButKeepsItUnzeroed_AndRestoreGivesItBack()
    {
        var (l, _, held) = Held(Idle);

        l.SetAside();

        Assert.False(AllZero(held));
        Assert.False(l.IsHeld);
        Assert.False(l.TryUse(_ => Assert.Fail("a set-aside passphrase must not be used")));
        l.Restore();
        Assert.True(l.IsHeld);
        Assert.Equal("abcdefgh", Read(l));
    }

    [Fact]
    public void SetAside_TheTimerZeroesIt_AndRestoreGivesNothing()
    {
        var (l, clock, held) = Held(Idle);
        l.SetAside();

        clock.Advance(Idle);

        Assert.True(AllZero(held));
        l.Restore();
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void SetAside_TheTimerFiresAtTheLastAnswerPlusIdle_NotIdleFromTheSetAside()
    {
        var (l, clock, held) = Held(Idle);
        clock.Advance(Idle / 2);
        l.SetAside();

        clock.Advance(Idle / 2); // exactly last answer + idle; no answer or use since

        Assert.True(AllZero(held));
        l.Restore();
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void Restore_TheTimerFiresAtTheLastAnswerPlusIdle_NotIdleFromTheRestore()
    {
        var (l, clock, held) = Held(Idle);
        l.SetAside();
        clock.Advance(Idle / 2);
        l.Restore();
        Assert.False(AllZero(held));

        clock.Advance(Idle / 2); // exactly last answer + idle; no answer or use since

        Assert.True(AllZero(held));
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void SetAside_AnswersKeepCountingIdleTime_AndRestoreAfterTheIdleTimeZeroesIt()
    {
        var (l, clock, held) = Held(Idle);
        l.SetAside();
        var justUnder = Idle - TimeSpan.FromSeconds(1);

        clock.Advance(justUnder);
        l.Touch(); // an answer in setup restarts the idle time
        Assert.False(AllZero(held));
        clock.Advance(justUnder);
        l.Touch();
        Assert.False(AllZero(held));
        l.Restore();
        Assert.Equal("abcdefgh", Read(l));

        l.SetAside();
        clock.Advance(Idle);
        l.Restore(); // the timer is not relied on: the give-back finds the idle time passed
        Assert.True(AllZero(held));
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void SetAside_AnAnswerAfterTheIdleTime_ZeroesIt_EvenWhenTheTimerHasNotRun()
    {
        var late = new LateClock(); // its timers never fire: only the check at the answer can lock
        var l = new SessionLock(late);
        l.SetIdleTime(Idle);
        var held = Passphrase();
        l.Hold(held);
        l.SetAside();

        late.Move(Idle);
        Assert.False(AllZero(held));
        l.Touch();

        Assert.True(AllZero(held));
        l.Restore();
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void SetAside_NeverIdleTime_KeepsItUntilDiscarded()
    {
        var (l, clock, held) = Held(null);
        l.SetAside();
        clock.Advance(TimeSpan.FromDays(30));
        Assert.False(AllZero(held));

        l.Discard();

        Assert.True(AllZero(held));
        l.Restore();
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void Discard_ZeroesTheSetAsidePassphrase_AndLeavesWhatIsHeld()
    {
        var (l, _, aside) = Held(Idle);
        l.SetAside();
        var fresh = Passphrase();
        l.Hold(fresh);

        l.Discard();

        Assert.True(AllZero(aside));
        Assert.False(AllZero(fresh));
        Assert.True(l.IsHeld);
    }

    [Fact]
    public void Restore_ZeroesWhatWasHeldMeanwhile_AndBringsTheSetAsideBack()
    {
        var (l, _, aside) = Held(Idle);
        l.SetAside();
        var meanwhile = Passphrase();
        l.Hold(meanwhile);

        l.Restore();

        Assert.True(AllZero(meanwhile));
        Assert.False(AllZero(aside));
        Assert.True(l.IsHeld);
    }

    [Fact]
    public void Dispose_ZeroesTheSetAsidePassphrase()
    {
        var (l, _, held) = Held(Idle);
        l.SetAside();

        l.Dispose();

        Assert.True(AllZero(held));
    }

    [Fact]
    public void EachAnswer_RestartsTheTimer()
    {
        var (l, clock, held) = Held(Idle);

        for (var i = 0; i < 5; i++)
        {
            clock.Advance(Idle - TimeSpan.FromSeconds(1));
            Assert.True(l.IsHeld);
            l.Touch();
        }

        Assert.Equal("abcdefgh", Read(l));
        Assert.False(AllZero(held));

        clock.Advance(Idle);
        Assert.True(AllZero(held));
    }

    [Fact]
    public void MenuChoices_RestartTheTimer_ThroughTheSession_AndQuitZeroesThem()
    {
        var clock = new FakeTimeProvider();
        var start = clock.GetUtcNow();
        var justUnder = Idle - TimeSpan.FromSeconds(1);
        byte[]? held = null;
        var heldAtList = false;
        var actions = new ActionTable();
        actions.Register("products.add", s =>
        {
            held = Passphrase();
            s.Lock.Hold(held);
        });
        actions.Register("products.list", s => heldAtList = s.Lock.IsHeld);
        SessionTestHost.MakeInstallation(_host.Cwd);
        // Products, add (holds), then four menu choices and Quit, each just under the idle time after the one before.
        var console = new ScriptedConsole(clock: clock)
            .Line("1")
            .Line("1")
            .Line("2", justUnder)
            .Line("3", justUnder)
            .Line("4", justUnder)
            .Line("0", justUnder)
            .Line("0", justUnder);

        // The idle time comes from the installation's Settings; the seam gives ten minutes here.
        var exit = RunWithIdle(console, actions, clock, Idle);

        Assert.Equal(0, exit);
        Assert.True(heldAtList);
        Assert.Equal(5 * justUnder, clock.GetUtcNow() - start);
        Assert.True(AllZero(held!));
    }

    [Fact]
    public void IdleTimePassingBetweenMenuChoices_LocksThem()
    {
        var clock = new FakeTimeProvider();
        byte[]? held = null;
        var heldAtList = true;
        var actions = new ActionTable();
        actions.Register("products.add", s =>
        {
            held = Passphrase();
            s.Lock.Hold(held);
        });
        actions.Register("products.list", s => heldAtList = s.Lock.IsHeld);
        SessionTestHost.MakeInstallation(_host.Cwd);
        var console = new ScriptedConsole(clock: clock)
            .Line("1").Line("1").Line("4", Idle).Line("0").Line("0");

        RunWithIdle(console, actions, clock, Idle);

        Assert.False(heldAtList);
        Assert.True(AllZero(held!));
    }

    private int RunWithIdle(ScriptedConsole console, ActionTable actions, FakeTimeProvider clock, TimeSpan? idle) =>
        ToolApp.Run(
            [],
            new ToolEnvironment(console, clock, _host.Home, _host.Cwd, 1000),
            env => MenuLoop.Run(env, actions, Startup.Offer.Stubs, _ => idle));

    [Fact]
    public void AnswerAfterTheIdleTime_WhenTheTimerHasNotRun_FindsNothingHeld()
    {
        // A clock whose timers never fire stands for a late timer: only the use-time check can lock.
        var late = new LateClock();
        var l = new SessionLock(late);
        l.SetIdleTime(Idle);
        var held = Passphrase();
        l.Hold(held);

        late.Move(Idle);

        Assert.False(AllZero(held));
        Assert.False(l.TryUse(_ => Assert.Fail("must not hand out")));
        Assert.True(AllZero(held));
    }

    [Fact]
    public void Never_KeepsTheBytes_ForALongAdvance_AndSetsNoTimer()
    {
        var (l, clock, held) = Held(null);

        clock.Advance(TimeSpan.FromDays(30));
        l.Touch();
        clock.Advance(TimeSpan.FromDays(30));

        Assert.True(l.IsHeld);
        Assert.Equal("abcdefgh", Read(l));
        Assert.False(AllZero(held));
    }

    [Fact]
    public void Hold_ZeroesThePassphraseHeldBefore()
    {
        var (l, _, first) = Held(Idle);
        var second = Passphrase();

        l.Hold(second);

        Assert.True(AllZero(first));
        Assert.False(AllZero(second));
    }

    [Fact]
    public void Release_ZeroesAndDrops_AndALaterHoldWorks()
    {
        var (l, clock, held) = Held(Idle);

        l.Release();

        Assert.True(AllZero(held));
        Assert.False(l.IsHeld);
        var again = Passphrase();
        l.Hold(again);
        clock.Advance(Idle);
        Assert.True(AllZero(again));
    }

    [Fact]
    public void Dispose_ZeroesThem_AndNothingIsHeldAfter()
    {
        var (l, _, held) = Held(Idle);

        l.Dispose();

        Assert.True(AllZero(held));
        var late = Passphrase();
        l.Hold(late);
        Assert.True(AllZero(late));
        Assert.False(l.IsHeld);
    }

    [Fact]
    public void ChangingTheIdleTime_AppliesFromThen()
    {
        var (l, clock, held) = Held(Idle);

        clock.Advance(Idle - TimeSpan.FromSeconds(1));
        l.SetIdleTime(TimeSpan.FromMinutes(1));
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(AllZero(held));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(AllZero(held));
    }

    [Fact]
    public void SetIdleTime_RefusesZeroOrNegative()
    {
        var l = new SessionLock(new FakeTimeProvider());

        Assert.Throws<ArgumentOutOfRangeException>(() => l.SetIdleTime(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => l.SetIdleTime(TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void Quit_ZeroesThem()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        byte[]? held = null;
        var actions = new ActionTable();
        actions.Register("products.add", s => { held = Passphrase(); s.Lock.Hold(held); });

        var exit = _host.Run(new ScriptedConsole().Line("1").Line("1").Line("0").Line("0"), actions);

        Assert.Equal(0, exit);
        Assert.True(AllZero(held!));
    }

    [Fact]
    public void EndOfInput_ZeroesThem()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        byte[]? held = null;
        var actions = new ActionTable();
        actions.Register("products.add", s => { held = Passphrase(); s.Lock.Hold(held); });

        var exit = _host.Run(new ScriptedConsole().Line("1").Line("1"), actions);

        Assert.Equal(1, exit);
        Assert.True(AllZero(held!));
    }

    [Fact]
    public void AnActionThatThrows_KeepsThemHeld_AndZeroesThemWhenTheSessionEnds()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        byte[]? held = null;
        var actions = new ActionTable();
        actions.Register("products.add", s =>
        {
            held = Passphrase();
            s.Lock.Hold(held);
            throw new InvalidOperationException("boom");
        });

        // The failure is reported and the menu shown again; the session then ends at end of input (exit 1).
        // Run through ToolApp.Run, so nothing relies on the test runner catching an escaping exception.
        var exit = _host.Run(new ScriptedConsole().Line("1").Line("1"), actions);

        Assert.Equal(1, exit);
        Assert.True(AllZero(held!));
    }

    [Fact]
    public void MovingToARestoredInstallation_ZeroesThem_AndTakesItsIdleTime()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var other = Path.Combine(_host.Home, "restored");
        SessionTestHost.MakeInstallation(other);
        byte[]? held = null;
        TimeSpan? idleAfter = null;
        var actions = new ActionTable();
        actions.Register("products.add", s => { held = Passphrase(); s.Lock.Hold(held); });
        actions.Register("settings.prefix", s =>
        {
            s.ReadIdleTime = _ => TimeSpan.FromMinutes(3);
            s.UseFolder(other);
            idleAfter = s.Lock.IdleTime;
            Assert.False(s.Lock.IsHeld);
        });

        _host.Run(new ScriptedConsole().Line("1").Line("1").Line("0").Line("5").Line("2").Line("1").Line("0").Line("0"), actions);

        Assert.True(AllZero(held!));
        Assert.Equal(TimeSpan.FromMinutes(3), idleAfter);
    }

    [Fact]
    public void SetupAtTheOffer_KeepsThePassphraseJustSet_AndTakesTheNewIdleTime()
    {
        var created = Path.Combine(_host.Home, ".ucl");
        byte[]? held = null;
        SessionLock? sessionLock = null;
        var offer = new Startup.Offer(s =>
        {
            SessionTestHost.MakeInstallation(created);
            s.ReadIdleTime = _ => TimeSpan.FromMinutes(7);
            held = Passphrase();
            s.Lock.Hold(held);
            sessionLock = s.Lock;
            return created;
        }, Startup.Offer.Stubs.Restore);
        var observed = "";
        var actions = new ActionTable();
        actions.Register("products.add", s => observed = Read(s.Lock));

        _host.Run(new ScriptedConsole().Line("1").Line("1").Line("1").Line("0").Line("0"), actions, offer);

        Assert.Equal("abcdefgh", observed);
        Assert.Equal(TimeSpan.FromMinutes(7), sessionLock!.IdleTime);
        Assert.True(AllZero(held!)); // zeroed only when the session ended
    }

    [Fact]
    public void RestoreAtTheOffer_DropsAnyPassphrase()
    {
        var created = Path.Combine(_host.Home, ".ucl");
        byte[]? held = null;
        var offer = new Startup.Offer(Startup.Offer.Stubs.SetUp, s =>
        {
            SessionTestHost.MakeInstallation(created);
            held = Passphrase();
            s.Lock.Hold(held);
            return created;
        });
        var observed = true;
        var actions = new ActionTable();
        actions.Register("products.add", s => observed = s.Lock.IsHeld);

        _host.Run(new ScriptedConsole().Line("2").Line("1").Line("1").Line("0").Line("0"), actions, offer);

        Assert.False(observed);
        Assert.True(AllZero(held!));
    }

    /// <summary>A clock whose timers never fire, so a test can show the use-time check alone.</summary>
    private sealed class LateClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Move(TimeSpan by) => _now += by;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new NeverTimer();

        private sealed class NeverTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
