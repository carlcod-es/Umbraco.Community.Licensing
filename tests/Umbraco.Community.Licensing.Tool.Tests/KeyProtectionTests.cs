using System.Security.Cryptography;
using Umbraco.Community.Licensing.Tool.Data;
using Xunit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Private keys encrypted with the passphrase (ADR-0014, ADR-0021): the check, encrypt and decrypt, and unlocking.</summary>
public class KeyProtectionTests(ITestOutputHelper output) : IDisposable
{
    private const string Passphrase = "correct horse battery";
    private readonly SessionTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static byte[] Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    /// <summary>The menu keys of the stub actions: Signing keys, then "Add a standby key" and "Status".</summary>
    private static readonly string[] CreateKey = ["3", "2"];

    private static ScriptedConsole Script(params string[] lines)
    {
        var console = new ScriptedConsole();
        foreach (var line in lines)
            console.Line(line);
        return console;
    }

    /// <summary>
    /// A stub action that asks one question, then unlocks, then creates a key and stores it in one step
    /// (the order every real signing action follows); <paramref name="created"/> counts keys it created.
    /// </summary>
    private static ActionTable StubTable(Action? created = null, Action<Session>? status = null)
    {
        var actions = new ActionTable();
        actions.Register("keys.standby", session =>
        {
            var name = session.Questions.AskText("Name", "any text", _ => null);
            KeyProtection.NewKey? key = null;
            KeyProtection.WithPassphrase(session, held =>
                key = KeyProtection.CreateEncrypted(held, session.Environment.Pbkdf2Iterations));
            created?.Invoke();
            using var db = session.OpenDb();
            db.SigningKeys.Add(new SigningKeyRecord
            {
                SigningKeyId = key!.SigningKeyId,
                PublicKey = key.PublicKey,
                EncryptedPrivateKeyPem = key.EncryptedPrivateKeyPem,
                Role = KeyRole.Active,
                CreatedUtc = DateTime.UtcNow,
            });
            db.SaveChanges();
            session.Console.WriteLine($"Created {name.Value}.");
        });
        if (status is not null)
            actions.Register("keys.status", status);
        return actions;
    }

    private List<SigningKeyRecord> StoredKeys()
    {
        using var db = Installation.OpenExisting(_host.Cwd).OpenDb();
        return db.SigningKeys.ToList();
    }

    [Fact]
    public void OpenedByAnotherTool_StoredKeyOpensWithPassphrase_AndNotWithout()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        var console = Script(CreateKey[0], CreateKey[1], "name").Hidden(Passphrase).Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, StubTable()));

        var stored = Assert.Single(StoredKeys());
        Assert.DoesNotContain('\r', stored.EncryptedPrivateKeyPem);
        Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----\n", stored.EncryptedPrivateKeyPem);

        using var opened = ECDsa.Create();
        opened.ImportFromEncryptedPem(stored.EncryptedPrivateKeyPem, Bytes(Passphrase));
        using var viaTool = KeyProtection.Decrypt(stored.EncryptedPrivateKeyPem, Bytes(Passphrase));
        Assert.Equal(stored.SigningKeyId, viaTool.SigningKeyId);
        Assert.Equal(stored.PublicKey, viaTool.PublicKey.Export());
        // The key another tool opened is the key the tool stored: same public key, not just the same ID.
        var spkiBase64Url = Convert.ToBase64String(opened.ExportSubjectPublicKeyInfo()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal($"{stored.SigningKeyId}.{spkiBase64Url}", stored.PublicKey);

        using var without = ECDsa.Create();
        Assert.ThrowsAny<Exception>(() => without.ImportFromPem(stored.EncryptedPrivateKeyPem));
        Assert.ThrowsAny<CryptographicException>(() => without.ImportFromEncryptedPem(stored.EncryptedPrivateKeyPem, Bytes("wrong passphrase!!")));
        Assert.ThrowsAny<CryptographicException>(() => KeyProtection.Decrypt(stored.EncryptedPrivateKeyPem, Bytes("wrong passphrase!!")));
    }

    [Fact]
    public void PassphraseNotTakenFromTheEnvironment_AskedAtTheHiddenPrompt()
    {
        var names = new[] { "UCL_PASSPHRASE", "PASSPHRASE", "UCL_PASSWORD" };
        var earlier = names.Select(Environment.GetEnvironmentVariable).ToArray();
        // The correct passphrase is in the environment: a tool that tried it first would not ask at all.
        foreach (var name in names)
            Environment.SetEnvironmentVariable(name, Passphrase);
        try
        {
            SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
            var console = Script(CreateKey[0], CreateKey[1], "name").Hidden(Passphrase).Line("0").Line("0");

            Assert.Equal(0, _host.Run(console, StubTable()));

            Assert.Equal(1, SessionTestHost.Count(console.Out, "Passphrase ("));
            Assert.Equal(0, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
            Assert.Single(StoredKeys());
        }
        finally
        {
            for (var i = 0; i < names.Length; i++)
                Environment.SetEnvironmentVariable(names[i], earlier[i]);
        }
    }

    [Fact]
    public void WrongPassphraseThreeTimes_EachSaidWrong_NothingStored_MenuShown_RejectedBytesZeroed()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        var created = 0;
        var heldAfter = true;
        var actions = StubTable(() => created++, s => heldAfter = s.Lock.IsHeld);
        var console = Script(CreateKey[0], CreateKey[1], "name")
            .Hidden("wrong one 1111111").Hidden("wrong one 2222222").Hidden("wrong one 3333333")
            // Back to the menu: Status runs, then Back, Quit.
            .Line("1").Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, actions));

        Assert.Equal(3, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        Assert.Equal(3, SessionTestHost.Count(console.Out, "Passphrase ("));
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Name ("));
        Assert.Contains("Cancelled.", console.Out);
        Assert.Equal(3, SessionTestHost.Lines(console.Out).Count(l => l == "Signing keys")); // opened, after the cancel, after Status
        Assert.Equal(0, created);
        Assert.Empty(StoredKeys());
        Assert.False(heldAfter);
        Assert.Equal(3, console.HiddenGiven.Count);
        Assert.All(console.HiddenGiven, h => Assert.All(h.Raw, c => Assert.Equal('\0', c)));
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void WrongPassphraseRefusedBeforeAnyKeyIsCreated_ThenRightOneAccepted_KeepingTheActionsAnswers()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        var createdWhileUnheld = 0;
        var actions = StubTable(() => createdWhileUnheld++);
        var console = Script(CreateKey[0], CreateKey[1], "name").Hidden("not the passphrase").Hidden(Passphrase).Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, actions));

        Assert.Equal(1, SessionTestHost.Count(console.Out, "Name ("));
        Assert.Equal(1, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        Assert.Equal(1, createdWhileUnheld);
        var stored = Assert.Single(StoredKeys());
        // The key was encrypted under the right passphrase, not the refused one.
        using var opened = KeyProtection.Decrypt(stored.EncryptedPrivateKeyPem, Bytes(Passphrase));
        Assert.Equal(stored.SigningKeyId, opened.SigningKeyId);
        Assert.ThrowsAny<CryptographicException>(() => KeyProtection.Decrypt(stored.EncryptedPrivateKeyPem, Bytes("not the passphrase")));
    }

    [Fact]
    public void WithPassphrase_ThatReturnsAValue_GivesItBack_AndAsksOnlyOnce()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        var actions = new ActionTable();
        var lengths = new List<int>();
        actions.Register("keys.standby", session =>
        {
            lengths.Add(KeyProtection.WithPassphrase(session, held => held.Length));
            lengths.Add(KeyProtection.WithPassphrase(session, held => held.Length));
        });
        var console = Script(CreateKey[0], CreateKey[1]).Hidden(Passphrase).Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, actions));

        Assert.Equal([Passphrase.Length, Passphrase.Length], lengths);
        Assert.Equal(1, SessionTestHost.Count(console.Out, "Passphrase ("));
    }

    [Fact]
    public void PassphraseHeld_NotAskedAgainByTheNextAction()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        var console = Script(CreateKey[0], CreateKey[1], "first").Hidden(Passphrase)
            .Line("2").Line("second").Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, StubTable()));

        Assert.Equal(1, SessionTestHost.Count(console.Out, "Passphrase ("));
        Assert.Equal(2, StoredKeys().Count);
    }

    [Fact]
    public void InstallationWithoutAPassphraseCheck_ActionFailsInOneLine_NothingAsked()
    {
        SessionTestHost.MakeInstallation(_host.Cwd);
        var console = Script(CreateKey[0], CreateKey[1], "name").Line("0").Line("0");

        Assert.Equal(0, _host.Run(console, StubTable()));

        Assert.Contains($"Signing keys - Add a standby key failed: {KeyProtection.MissingCheckMessage}", console.Out);
        Assert.Equal(0, SessionTestHost.Count(console.Out, "Passphrase ("));
        Assert.Empty(StoredKeys());
        Assert.Equal("", console.Err);
    }

    [Fact]
    public void PassphraseCheck_RoundTripsThroughSettings_AndIsWrongForAnotherPassphrase()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        using var db = Installation.OpenExisting(_host.Cwd).OpenDb();

        var check = PassphraseCheck.Read(db);

        Assert.NotNull(check);
        Assert.Equal(SessionTestHost.Iterations, check.Iterations);
        Assert.Equal(PassphraseCheck.SaltSize, check.Salt.Length);
        Assert.Equal(PassphraseCheck.HashSize, check.Hash.Length);
        Assert.True(check.Matches(Bytes(Passphrase)));
        Assert.False(check.Matches(Bytes(Passphrase + " ")));
        Assert.False(check.Matches(Bytes("")));
    }

    [Fact]
    public void PassphraseCheck_ReadsIterationsFromTheStoredRow_NotTheEnvironmentsCount()
    {
        var check = PassphraseCheck.Create(Bytes(Passphrase), 1234);

        Assert.Equal(1234, check.Iterations);
        Assert.Contains(check.ToSettings(), s => s.Name == SettingNames.PassphraseIterations && s.Value == "1234");
        Assert.True(check.Matches(Bytes(Passphrase)));
    }

    [Fact]
    public void PassphraseCheck_RowMissingOrMalformed_ReadsAsNull()
    {
        SessionTestHost.MakeInstallation(_host.Cwd, passphrase: Passphrase);
        using (var db = Installation.OpenExisting(_host.Cwd).OpenDb())
        {
            db.Settings.Single(s => s.Name == SettingNames.PassphraseSalt).Value = "not base64!";
            db.SaveChanges();
        }

        using var read = Installation.OpenExisting(_host.Cwd).OpenDb();
        Assert.Null(PassphraseCheck.Read(read));
    }

    [Fact]
    public void EncryptDecrypt_At600000Iterations_Completes()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var passphrase = Bytes(Passphrase);

        var key = KeyProtection.CreateEncrypted(passphrase, ToolEnvironment.DefaultPbkdf2Iterations);
        using var decrypted = KeyProtection.Decrypt(key.EncryptedPrivateKeyPem, passphrase);
        var check = PassphraseCheck.Create(passphrase, ToolEnvironment.DefaultPbkdf2Iterations);
        Assert.True(check.Matches(passphrase));

        output.WriteLine($"Create, decrypt and one passphrase check at {ToolEnvironment.DefaultPbkdf2Iterations} iterations: {watch.ElapsedMilliseconds} ms");
        Assert.Equal(key.SigningKeyId, decrypted.SigningKeyId);
    }
}
