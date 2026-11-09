using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Guided setup (vendor-tool-setup: Guided setup, Passphrase rule, Idle lock, Folder location), as whole sessions.</summary>
public class SetupTests : IDisposable
{
    private const string Words = SessionTestHost.SetupPassphrase;
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static byte[] Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    private static Dictionary<string, string> Settings(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return db.Settings.AsNoTracking().ToDictionary(s => s.Name, s => s.Value);
    }

    private static List<SigningKeyRecord> Keys(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return db.SigningKeys.AsNoTracking().ToList();
    }

    private int Run(ScriptedConsole console, ActionTable? actions = null) => _host.Run(console, actions, Startup.Offer.Default);

    private static void AssertZeroed(ScriptedConsole console) =>
        Assert.All(console.HiddenGiven, h => Assert.All(h.Raw, c => Assert.Equal('\0', c)));

    private static int Lines(string transcript, string line) => SessionTestHost.Lines(transcript).Count(l => l == line);

    /// <summary>Quit after the menu shows: Quit at the main menu.</summary>
    private static ScriptedConsole ThenQuit(ScriptedConsole console) => console.Line("0");

    [Fact]
    public void SetupWithDefaults_PerProduct_CreatesTheInstallationInDotUcl_AndShowsTheClosingMessageThenTheMenu()
    {
        TimeSpan? idle = null;
        var actions = ActionTable.Default();
        actions.Register("products.add", s => idle = s.Lock.IdleTime);
        // Setup, then Products > Add a product (a stub that reads the idle lock), Back, Quit.
        var console = ThenQuit(new ScriptedConsole().SetUp().Line("1").Line("1").Line("0"));

        var exit = Run(console, actions);

        Assert.Equal(0, exit);
        Assert.Equal("", console.Err);
        var folder = _host.HomeInstallation;
        var settings = Settings(folder);
        Assert.Equal("LIC", settings[SettingNames.Prefix]);
        Assert.Equal("per-product", settings[SettingNames.SigningKeyMode]);
        Assert.Equal("true", settings[SettingNames.StandbyKeys]);
        Assert.Equal("15", settings[SettingNames.IdleLock]);
        Assert.Empty(Keys(folder));
        Assert.Equal(TimeSpan.FromMinutes(15), idle);

        var check = Assert.IsType<PassphraseCheck>(ReadCheck(folder));
        Assert.Equal(SessionTestHost.Iterations, check.Iterations);
        Assert.True(check.Matches(Bytes(Words)));

        var out_ = console.Out;
        // Every question shows what it accepts and its proposed value.
        Assert.Contains("Folder for the installation", out_);
        Assert.Contains($"  1 {folder} (.ucl in your home folder)", out_);
        Assert.Contains($"  2 {_host.Cwd} (the current folder)", out_);
        Assert.Contains("Enter for .ucl", out_);
        Assert.Contains("Key prefix (", out_);
        Assert.Contains("Enter for LIC", out_);
        Assert.Contains("(recommended)", out_);
        Assert.Contains("[Y/n]", out_);
        Assert.Contains("Idle lock in minutes (", out_);
        Assert.Contains("Enter for 15", out_);
        Assert.Contains("typed hidden;", out_);

        // The closing message, then Using, then the menu.
        var closing = out_.IndexOf("Setup is complete", StringComparison.Ordinal);
        var using_ = out_.IndexOf($"Using {folder}", StringComparison.Ordinal);
        var menu = out_.IndexOf("Main menu", StringComparison.Ordinal);
        Assert.True(closing >= 0 && closing < using_ && using_ < menu);
        var message = out_[closing..using_];
        Assert.Contains($"  {folder}", message);
        Assert.Contains("private signing keys, encrypted", message);
        Assert.Contains("license records", message);
        Assert.Contains("customer emails and order references, which are not encrypted", message);
        Assert.Contains("Back this folder up", message);
        Assert.Contains("OneDrive", message);
        Assert.Contains("iCloud Drive", message);
        Assert.Contains("Dropbox", message);
        Assert.Contains("not a complete list", message);
        Assert.Contains("password manager", message);
        Assert.Contains("apart from your backups", message);
        Assert.Contains("Products > Add a product (this creates its signing key under the", message);
        Assert.Contains("passphrase you just set; it is not asked again in this session", message);
        Assert.All(SessionTestHost.Lines(message).Where(l => l != $"  {folder}"), l => Assert.True(l.Length <= 80, l));
        Assert.Contains("Signing keys > Trusted keys", message);
        Assert.DoesNotContain("~", out_);
        AssertZeroed(console);
    }

    [Fact]
    public void SetupWithDefaults_Shared_CreatesTheSharedKeyAndItsStandby_AndTheNextStepsDifferByMode()
    {
        var console = ThenQuit(new ScriptedConsole().SetUp(shared: true));

        var exit = Run(console);

        Assert.Equal(0, exit);
        var folder = _host.HomeInstallation;
        Assert.Equal("shared", Settings(folder)[SettingNames.SigningKeyMode]);
        var keys = Keys(folder).OrderBy(k => k.Role).ToList();
        Assert.Equal([KeyRole.Active, KeyRole.Standby], keys.Select(k => k.Role));
        Assert.All(keys, k =>
        {
            Assert.Null(k.ProductId);
            Assert.Equal(DateTimeKind.Utc, k.CreatedUtc.Kind);
            Assert.Equal(new FakeTimeProvider().GetUtcNow().UtcDateTime, k.CreatedUtc);
            Assert.DoesNotContain('\r', k.EncryptedPrivateKeyPem);
            // Encrypted under the passphrase just set, and the public key line is that key's.
            using var opened = KeyProtection.Decrypt(k.EncryptedPrivateKeyPem, Bytes(Words));
            Assert.Equal(k.SigningKeyId, opened.SigningKeyId);
            Assert.Equal(k.PublicKey, opened.PublicKey.Export());
        });
        Assert.NotEqual(keys[0].SigningKeyId, keys[1].SigningKeyId);

        var out_ = console.Out;
        Assert.Contains("The shared signing key and its standby key are created.", out_);
        Assert.Contains("Products > Add a product (the shared signing key is already created)", out_);
        Assert.DoesNotContain("this creates its signing key", out_);
        Assert.Contains("Signing keys > Trusted keys", out_);
        var message = out_[out_.IndexOf("Setup is complete", StringComparison.Ordinal)..out_.IndexOf($"Using {folder}", StringComparison.Ordinal)];
        Assert.All(SessionTestHost.Lines(message).Where(l => l != $"  {folder}"), l => Assert.True(l.Length <= 80, l));
    }

    [Fact]
    public void SetupInSharedMode_WithoutStandby_CreatesOnlyTheActiveSharedKey()
    {
        var console = ThenQuit(new ScriptedConsole().SetUp(shared: true, standby: false));

        Assert.Equal(0, Run(console));

        var key = Assert.Single(Keys(_host.HomeInstallation));
        Assert.Equal(KeyRole.Active, key.Role);
        Assert.Equal("false", Settings(_host.HomeInstallation)[SettingNames.StandbyKeys]);
        Assert.Contains("The shared signing key is created.", console.Out);
    }

    [Fact]
    public void SetupWithAPrefixAndIdleLockOfTheVendorsOwn_StoresThem_AndNoneIsTheEmptyPrefix()
    {
        var acme = ThenQuit(new ScriptedConsole().SetUp(prefix: "ACME", idleLock: "never"));
        Assert.Equal(0, Run(acme));
        var settings = Settings(_host.HomeInstallation);
        Assert.Equal("ACME", settings[SettingNames.Prefix]);
        Assert.Equal("never", settings[SettingNames.IdleLock]);
        Directory.Delete(_host.HomeInstallation, recursive: true);

        var none = ThenQuit(new ScriptedConsole().SetUp(prefix: "None", idleLock: "1440"));
        Assert.Equal(0, Run(none));
        settings = Settings(_host.HomeInstallation);
        Assert.Equal("", settings[SettingNames.Prefix]);
        Assert.Equal("1440", settings[SettingNames.IdleLock]);
    }

    [Fact]
    public void SetupFromTheStartupOffer_HoldsThePassphrase_SoTheNextKeyCreatingActionAsksNone_InBothModes()
    {
        foreach (var shared in new[] { false, true })
        {
            if (Directory.Exists(_host.HomeInstallation))
                Directory.Delete(_host.HomeInstallation, recursive: true);
            var created = 0;
            var actions = ActionTable.Default();
            actions.Register("products.add", s =>
            {
                KeyProtection.WithPassphrase(s, held => KeyProtection.CreateEncrypted(held, s.Environment.Pbkdf2Iterations));
                created++;
            });
            var console = ThenQuit(new ScriptedConsole().SetUp(shared: shared).Line("1").Line("1").Line("0"));

            var exit = Run(console, actions);

            Assert.Equal(0, exit);
            Assert.Equal(1, created);
            Assert.Equal(0, SessionTestHost.Count(console.Out, "Passphrase ("));
            Assert.Equal(1, SessionTestHost.Count(console.Out, "Choose a passphrase ("));
            Assert.Equal(1, SessionTestHost.Count(console.Out, "Type it again ("));
        }
    }

    [Fact]
    public void SetupFromTheStartupOffer_PassphraseIsForgottenWhenTheIdleLockPasses()
    {
        var clock = new FakeTimeProvider();
        var actions = ActionTable.Default();
        actions.Register("products.add", s => KeyProtection.WithPassphrase(s, _ => { }));
        // Idle 5 minutes; Products > Add after 6 idle minutes asks for the passphrase again.
        var console = new ScriptedConsole(clock: clock).SetUp(idleLock: "5")
            .Line("1", TimeSpan.FromMinutes(6)).Line("1").Hidden(Words).Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, actions, Startup.Offer.Default, clock));

        Assert.Equal(1, SessionTestHost.Count(console.Out, "Passphrase ("));
    }

    [Fact]
    public void InvalidPrefix_NamesTheRule_AsksAgain_AndNothingIsCreatedUntilSetupCompletes()
    {
        var console = new ScriptedConsole().ChooseSetUp().Hidden(Words).Hidden(Words)
            .Line("acme-shop").Line("/cancel").Line("0");

        Assert.Equal(0, Run(console));

        Assert.Equal(1, SessionTestHost.Count(console.Out, TypedValues.PrefixRule));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Key prefix ("));
        Assert.False(Directory.Exists(_host.HomeInstallation));

        var again = ThenQuit(new ScriptedConsole().ChooseSetUp().Hidden(Words).Hidden(Words)
            .Line("acme-shop").Line("acme").Line("ACME").Line("").Line("").Line(""));
        Assert.Equal(0, Run(again));
        Assert.Equal(2, SessionTestHost.Count(again.Out, TypedValues.PrefixRule));
        Assert.Equal("ACME", Settings(_host.HomeInstallation)[SettingNames.Prefix]);
    }

    [Fact]
    public void PassphrasesDiffer_SaysSo_AsksForBothAgain_AndZeroesBothEntries()
    {
        var console = ThenQuit(new ScriptedConsole().ChooseSetUp()
            .Hidden(Words).Hidden(Words + "x")
            .Hidden(Words).Hidden(Words)
            .AnswerRest());

        Assert.Equal(0, Run(console));

        Assert.Equal(1, SessionTestHost.Count(console.Out, Setup.PassphrasesDiffer));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Choose a passphrase ("));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Type it again ("));
        Assert.True(ReadCheck(_host.HomeInstallation)!.Matches(Bytes(Words)));
        Assert.Equal(4, console.HiddenGiven.Count);
        AssertZeroed(console);
    }

    public static TheoryData<string, string> RefusedPassphrases => new()
    {
        { "Passphrase too short", "short pass" },
        { "Passphrase with an accented letter", "Grüße aus München 2026" },
        { "Passphrase with a trailing space", "correct horse battery " },
        { "Passphrase with a leading space", " correct horse battery" },
        { "Passphrase too long", new string('a', 257) },
    };

    [Theory]
    [MemberData(nameof(RefusedPassphrases))]
    public void RefusedPassphrase_NamesTheRule_AsksAgain_AndTheRefusedBytesAreZeroed(string scenario, string typed)
    {
        _ = scenario;
        var console = ThenQuit(new ScriptedConsole().ChooseSetUp()
            .Hidden(typed)
            .Hidden(Words).Hidden(Words)
            .AnswerRest());

        Assert.Equal(0, Run(console));

        var expected = scenario switch
        {
            "Passphrase too short" => TypedValues.PassphraseTooShort,
            "Passphrase with an accented letter" => TypedValues.PassphraseCharacters,
            "Passphrase too long" => TypedValues.PassphraseTooLong,
            _ => TypedValues.PassphraseEdgeSpace,
        };
        Assert.Equal(1, SessionTestHost.Count(console.Out, expected));
        // The second prompt is not asked for a refused first entry.
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Choose a passphrase ("));
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Type it again ("));
        Assert.True(ReadCheck(_host.HomeInstallation)!.Matches(Bytes(Words)));
        AssertZeroed(console);
    }

    [Fact]
    public void PassphraseRuleMessages_NameTheirRule_InTheSpecsWords()
    {
        Assert.Contains("at least 15 characters", TypedValues.PassphraseTooShort);
        Assert.Contains("at most 256 characters", TypedValues.PassphraseTooLong);
        Assert.Contains("only letters A to Z, digits, spaces and the symbols on a standard keyboard", TypedValues.PassphraseCharacters);
        Assert.Contains("cannot start or end with a space", TypedValues.PassphraseEdgeSpace);
    }

    [Fact]
    public void PassphraseOfWords_IsAccepted_AndSetupHoldsItsCheck()
    {
        var console = ThenQuit(new ScriptedConsole().SetUp(passphrase: "correct horse battery staple"));

        Assert.Equal(0, Run(console));

        Assert.DoesNotContain(TypedValues.PassphraseTooShort, console.Out);
        Assert.DoesNotContain(TypedValues.PassphraseCharacters, console.Out);
        Assert.True(ReadCheck(_host.HomeInstallation)!.Matches(Bytes("correct horse battery staple")));
    }

    [Fact]
    public void IdleLockOutsideTheRule_AtSetup_NamesTheRule_AndAsksAgain()
    {
        var console = ThenQuit(new ScriptedConsole().ChooseSetUp().Hidden(Words).Hidden(Words)
            .Line("").Line("").Line("")
            .Line("0").Line("1441").Line("ten").Line("-1").Line("5"));

        Assert.Equal(0, Run(console));

        Assert.Equal(4, SessionTestHost.Count(console.Out, TypedValues.IdleLockRule));
        Assert.Equal(5, SessionTestHost.Count(console.Out, "Idle lock in minutes ("));
        Assert.Equal("5", Settings(_host.HomeInstallation)[SettingNames.IdleLock]);
    }

    [Fact]
    public void FolderAlreadyHoldsAnInstallation_SetupRefuses_NamingIt_ChangesNothing_AndShowsTheOfferAgain()
    {
        var existing = Path.Combine(_host.Home, "existing");
        SessionTestHost.MakeInstallation(existing);
        var before = File.ReadAllBytes(Path.Combine(existing, Startup.DatabaseFileName));
        // The folder is fixed here (the way Settings will call setup), so the refusal comes at the folder.
        var offer = new Startup.Offer(s => Setup.Run(s, existing), Startup.Offer.Stubs.Restore);
        var console = new ScriptedConsole().Line("1").Line("0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Contains(Setup.RefusedFolderMessage(Path.GetFullPath(existing)), console.Out);
        Assert.Equal(0, SessionTestHost.Count(console.Out, "Choose a passphrase"));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(existing, Startup.DatabaseFileName)));
        Assert.Equal(2, Lines(console.Out, "What now?"));
        Assert.False(Directory.Exists(_host.HomeInstallation));
    }

    /// <summary>A console that runs <paramref name="onPrompt"/> when the idle lock question, the last, is asked.</summary>
    private sealed class RacingConsole(Action onPrompt) : ScriptedConsole
    {
        public override void Write(string text)
        {
            base.Write(text);
            if (text.StartsWith("Idle lock in minutes", StringComparison.Ordinal))
                onPrompt();
        }
    }

    [Fact]
    public void FolderGainsAnInstallationDuringSetup_IsRefusedBeforeCreating_AndTheOtherInstallationIsUntouched()
    {
        byte[]? other = null;
        var console = new RacingConsole(() =>
        {
            SessionTestHost.MakeInstallation(_host.HomeInstallation, passphrase: "another passphrase entirely");
            other = File.ReadAllBytes(Path.Combine(_host.HomeInstallation, Startup.DatabaseFileName));
        });
        ThenQuit(console.SetUp());

        var exit = Run(console);

        Assert.Equal(0, exit);
        Assert.Contains(Setup.RefusedFolderMessage(_host.HomeInstallation), console.Out);
        Assert.Equal(other, File.ReadAllBytes(Path.Combine(_host.HomeInstallation, Startup.DatabaseFileName)));
        Assert.DoesNotContain("Setup is complete", console.Out);
        Assert.DoesNotContain("Main menu", console.Out);
        AssertZeroed(console);
    }

    public static TheoryData<int> QuestionsInOrder => new() { 0, 1, 2, 3, 4, 5, 6 };

    [Theory]
    [MemberData(nameof(QuestionsInOrder))]
    public void SetupCancelled_AtEachQuestionInTurn_CreatesNothing_AndLeavesNoFolderBehind(int cancelAt)
    {
        // Folder, passphrase, passphrase again, prefix, mode, standby, idle lock.
        var console = new ScriptedConsole().Line("1");
        string[] normal = ["", Words, Words, "", "", "", ""];
        for (var i = 0; i < normal.Length; i++)
        {
            var answer = i == cancelAt ? "/cancel" : normal[i];
            if (i is 1 or 2)
                console.Hidden(answer);
            else
                console.Line(answer);
            if (i == cancelAt)
                break;
        }

        var heldAfter = true;
        var offer = new Startup.Offer(
            s =>
            {
                try { return Setup.Run(s); }
                catch (ActionCancelledException) { heldAfter = s.Lock.IsHeld; throw; }
            },
            Startup.Offer.Stubs.Restore);
        console.Line("0");

        var exit = _host.Run(console, offer: offer);

        Assert.Equal(0, exit);
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Cancelled."));
        Assert.Equal(2, Lines(console.Out, "What now?"));
        Assert.False(heldAfter);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Home)); // no .ucl left behind
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
        Assert.DoesNotContain("Main menu", console.Out);
        Assert.DoesNotContain("Setup is complete", console.Out);
        AssertZeroed(console);
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void SetupCancelled_ForTheCurrentFolder_LeavesItsOtherFilesAlone()
    {
        File.WriteAllText(Path.Combine(_host.Cwd, "notes.txt"), "mine");
        var console = new ScriptedConsole().ChooseSetUp("2").Hidden(Words).Line("/cancel").Line("0");

        Assert.Equal(0, _host.Run(console, offer: Startup.Offer.Default));

        Assert.Equal(["notes.txt"], Directory.GetFileSystemEntries(_host.Cwd).Select(Path.GetFileName));
        Assert.Empty(Directory.GetFileSystemEntries(_host.Home));
    }

    [Fact]
    public void DefaultFolderCreated_WhenThereIsNoDotUcl_AndTheVendorAcceptsTheProposedFolder()
    {
        Assert.False(Directory.Exists(_host.HomeInstallation));
        var console = ThenQuit(new ScriptedConsole().SetUp());

        Assert.Equal(0, Run(console));

        Assert.True(Directory.Exists(_host.HomeInstallation));
        Assert.True(File.Exists(Path.Combine(_host.HomeInstallation, Startup.DatabaseFileName)));
        Assert.Contains($"Using {_host.HomeInstallation}", console.Out);
        Assert.Empty(Directory.GetFileSystemEntries(_host.Cwd));
    }

    [Fact]
    public void DefaultFolderOnWindows_ShownPathEqualsTheFullPathUnderTheInjectedHome()
    {
        // A home like C:\Users\carl: here, a folder tree of that shape under the temp root.
        var home = Path.Combine(Path.GetDirectoryName(_host.Home)!, "Users", "carl");
        Directory.CreateDirectory(home);
        var expected = Path.GetFullPath(Path.Combine(home, ".ucl"));
        var console = ThenQuit(new ScriptedConsole().SetUp());

        var exit = ToolApp.Run(
            [],
            new ToolEnvironment(console, new FakeTimeProvider(), home, _host.Cwd, SessionTestHost.Iterations),
            env => MenuLoop.Run(env, ActionTable.Default(), Startup.Offer.Default));

        Assert.Equal(0, exit);
        Assert.Contains($"  1 {expected} (.ucl in your home folder)", console.Out);
        Assert.Contains($"  {expected}{Environment.NewLine}", console.Out); // the closing message
        Assert.Contains($"Using {expected}", console.Out);
        Assert.DoesNotContain("~", console.Out);
        Assert.True(File.Exists(Path.Combine(expected, Startup.DatabaseFileName)));
        Assert.False(File.Exists(Path.Combine(home, Startup.DatabaseFileName)));
    }

    [Fact]
    public void StartedInTheHomeFolder_OnlyDotUclIsOffered_NotTheHomeFolderItself_ForSetup()
    {
        _host.Cwd = _host.Home;
        var console = ThenQuit(new ScriptedConsole().SetUp());

        Assert.Equal(0, Run(console));

        // Only one folder is listed, and it is .ucl in full (the scripted answers echo nothing, so the first line follows the prompt).
        Assert.Equal(1, SessionTestHost.Count(console.Out, "(.ucl in your home folder)"));
        Assert.Contains($"  1 {_host.HomeInstallation} (.ucl in your home folder)", console.Out);
        Assert.DoesNotContain("  2 ", console.Out.Split("Folder for the installation")[1].Split("Choose a passphrase")[0]);
        Assert.DoesNotContain("(the current folder)", console.Out);
        Assert.True(File.Exists(Path.Combine(_host.HomeInstallation, Startup.DatabaseFileName)));
        Assert.False(File.Exists(Path.Combine(_host.Home, Startup.DatabaseFileName)));
    }

    [Fact]
    public void CurrentFolderWithOtherFiles_SetupThere_CreatesTheInstallation_AndLeavesTheFilesUntouched()
    {
        File.WriteAllText(Path.Combine(_host.Cwd, "notes.txt"), "mine");
        Directory.CreateDirectory(Path.Combine(_host.Cwd, "sub"));
        File.WriteAllText(Path.Combine(_host.Cwd, "sub", "a.txt"), "also mine");
        var console = ThenQuit(new ScriptedConsole().SetUp(folder: "2"));

        Assert.Equal(0, Run(console));

        Assert.True(Installation.Holds(_host.Cwd));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(_host.Cwd, "notes.txt")));
        Assert.Equal("also mine", File.ReadAllText(Path.Combine(_host.Cwd, "sub", "a.txt")));
        Assert.Contains($"Using {_host.Cwd}", console.Out);
        Assert.False(Directory.Exists(_host.HomeInstallation));
        Assert.Contains("Main menu", console.Out);
    }

    [Fact]
    public void RecordsOfANewInstallation_HoldNoLocalPath()
    {
        var console = ThenQuit(new ScriptedConsole().SetUp(shared: true));
        Assert.Equal(0, Run(console));

        var folder = _host.HomeInstallation;
        var values = Settings(folder).Values.Concat(Keys(folder).SelectMany(k => new[] { k.PublicKey, k.EncryptedPrivateKeyPem, k.SigningKeyId })).ToList();
        Assert.All(values, v =>
        {
            Assert.DoesNotContain(folder, v, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(_host.Home, v, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.MachineName, v, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void TheProductionEntry_OffersRealSetup_AtTheStartupOffer()
    {
        var console = ThenQuit(new ScriptedConsole().SetUp());

        var exit = ToolApp.Run([], new ToolEnvironment(console, new FakeTimeProvider(), _host.Home, _host.Cwd, SessionTestHost.Iterations));

        Assert.Equal(0, exit);
        Assert.DoesNotContain("is not available yet", console.Out);
        Assert.True(Installation.Holds(_host.HomeInstallation));
        Assert.Contains("Main menu", console.Out);
    }

    [Fact]
    public void SetUpInstallationHelper_MakesAnInstallationThroughASession_ForLaterSections()
    {
        var folder = _host.SetUpInstallation(shared: true, standby: false, idleLock: "30");

        Assert.Equal(_host.HomeInstallation, folder);
        Assert.Equal("30", Settings(folder)["IdleLock"]);
        Assert.Single(Keys(folder));
    }

    private static PassphraseCheck? ReadCheck(string folder)
    {
        using var db = Installation.OpenExisting(folder).OpenDb();
        return PassphraseCheck.Read(db);
    }

    // Folder offer: shared with restore (10.2), whose own session test comes with it.

    [Fact]
    public void FolderOffer_StartedInTheHomeFolder_IsOnlyDotUcl_ForSetupAndRestoreAlike()
    {
        var environment = new ToolEnvironment(new ScriptedConsole(), new FakeTimeProvider(), _host.Home, _host.Home, SessionTestHost.Iterations);

        var choices = FolderOffer.Choices(environment);

        var only = Assert.Single(choices);
        Assert.Equal(FolderOffer.HomeId, only.Id);
        Assert.StartsWith(_host.HomeInstallation, only.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void FolderOffer_ElsewhereOffersDotUclFirst_ThenTheCurrentFolder_BothInFull()
    {
        var environment = new ToolEnvironment(new ScriptedConsole(), new FakeTimeProvider(), _host.Home, _host.Cwd, SessionTestHost.Iterations);

        var choices = FolderOffer.Choices(environment);

        Assert.Equal([FolderOffer.HomeId, FolderOffer.CurrentId], choices.Select(c => c.Id));
        Assert.StartsWith(_host.HomeInstallation, choices[0].Label, StringComparison.Ordinal);
        Assert.StartsWith(_host.Cwd, choices[1].Label, StringComparison.Ordinal);
    }

    [Fact]
    public void FolderOffer_CurrentFolderThatIsDotUcl_IsOfferedOnce()
    {
        Directory.CreateDirectory(_host.HomeInstallation);
        var environment = new ToolEnvironment(new ScriptedConsole(), new FakeTimeProvider(), _host.Home, _host.HomeInstallation, SessionTestHost.Iterations);

        Assert.Single(FolderOffer.Choices(environment));
    }

    [Fact]
    public void FolderOffer_CurrentFolderReachedThroughALinkToHome_IsStillTheHomeFolder()
    {
        var link = Path.Combine(Path.GetDirectoryName(_host.Home)!, "home-link");
        try
        {
            Directory.CreateSymbolicLink(link, _host.Home);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be made here.");
            return;
        }

        var environment = new ToolEnvironment(new ScriptedConsole(), new FakeTimeProvider(), _host.Home, link, SessionTestHost.Iterations);

        Assert.Single(FolderOffer.Choices(environment));
        Assert.True(Startup.SamePath(link, _host.Home));
        Assert.DoesNotContain(Path.GetFullPath(link), Startup.Resolve(environment).Checked);
    }

    [Fact]
    public void SamePath_IgnoresATrailingSeparator_AndDistinguishesOtherFolders()
    {
        Assert.True(Startup.SamePath(_host.Home, _host.Home + Path.DirectorySeparatorChar));
        Assert.False(Startup.SamePath(_host.Home, _host.Cwd));
    }
}
