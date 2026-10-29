using Umbraco.Community.Licensing.Tool.Commands;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class SetupTests
{
    private const string Pass = ScriptedTool.DefaultPassphrase;

    private static string Keys(string first, string? second = null) => $"{first}\n{second ?? first}\n";

    /// <summary>A tool at a terminal answering setup.</summary>
    private static ScriptedTool Terminal(string stdin, string hiddenKeys, IReadOnlyDictionary<string, string>? variables = null) =>
        new(stdin: stdin, stdinRedirected: false, hiddenKeys: hiddenKeys, variables: variables);

    private static Dictionary<string, string> Settings(string folder)
    {
        using var db = Installation.Open(folder);
        return db.Settings.ToDictionary(s => s.Name, s => s.Value);
    }

    private static List<SigningKey> SigningKeysIn(string folder)
    {
        using var db = Installation.Open(folder);
        return db.SigningKeys.ToList();
    }

    private static void AssertNothingCreated(ScriptedTool tool)
    {
        Assert.False(Directory.Exists(tool.HomeInstallation));
        Assert.False(File.Exists(Path.Combine(tool.Environment.CurrentDirectory, Installation.DatabaseFileName)));
    }

    [Fact]
    public void Setup_with_defaults_per_product()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));

        var exit = tool.Run("setup");

        Assert.Equal(0, exit);
        Assert.Equal("", tool.Stdout);
        var folder = Path.GetFullPath(tool.HomeInstallation);
        var settings = Settings(folder);
        Assert.Equal("LIC", settings[SettingNames.KeyPrefix]);
        Assert.Equal(SigningKeyModes.PerProduct, settings[SettingNames.SigningKeyMode]);
        Assert.Equal("true", settings[SettingNames.StandbyKeys]);
        Assert.Empty(SigningKeysIn(folder));

        using var db = Installation.Open(folder);
        Assert.True(KeyProtection.LoadCheck(db)!.Verify(Pass));

        var stderr = tool.Stderr;
        Assert.Contains($"The installation is in {folder}", stderr);
        Assert.Contains("private signing keys, encrypted", stderr);
        Assert.Contains("license records", stderr);
        Assert.Contains("customer emails and order references, which are not encrypted", stderr);
        Assert.Contains("Back this folder up", stderr);
        Assert.Contains("cloud storage", stderr);
        Assert.Contains("password manager", stderr);
        Assert.Contains("ucl product add <product-id> --name <name>", stderr);
        Assert.Contains("creates the product's signing key", stderr);
        Assert.Contains("asks for the passphrase", stderr);
        Assert.Contains("ucl key trusted <product-id>", stderr);
        Assert.DoesNotContain(Pass, stderr);
    }

    [Fact]
    public void Setup_with_defaults_shared_mode_creates_the_shared_keys()
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Shared: true));

        var folder = Path.GetFullPath(tool.HomeInstallation);
        Assert.Equal(SigningKeyModes.Shared, Settings(folder)[SettingNames.SigningKeyMode]);
        var keys = SigningKeysIn(folder);
        Assert.Equal(2, keys.Count);
        Assert.All(keys, k => Assert.Null(k.ProductId));
        var active = Assert.Single(keys, k => k.Role == SigningKeyRole.Active);
        var standby = Assert.Single(keys, k => k.Role == SigningKeyRole.Standby);
        Assert.NotEqual(active.SigningKeyId, standby.SigningKeyId);
        foreach (var key in keys)
        {
            using var opened = KeyProtection.Decrypt(key.EncryptedPrivateKeyPem, Pass);
            Assert.Equal(key.SigningKeyId, opened.SigningKeyId);
            Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
                () => KeyProtection.Decrypt(key.EncryptedPrivateKeyPem, "not the passphrase at all"));
        }
    }

    [Fact]
    public void Setup_shared_mode_next_steps_do_not_mention_a_per_product_signing_key()
    {
        using var tool = Terminal("\n\n2\n\n", Keys(Pass));

        Assert.Equal(0, tool.Run("setup"));

        Assert.Contains("ucl product add <product-id> --name <name>", tool.Stderr);
        Assert.Contains("ucl key trusted <product-id>", tool.Stderr);
        Assert.DoesNotContain("creates the product's signing key", tool.Stderr);
    }

    [Fact]
    public void Setup_shared_mode_without_standby_creates_one_key()
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Shared: true, Standby: false));

        var folder = Path.GetFullPath(tool.HomeInstallation);
        Assert.Equal("false", Settings(folder)[SettingNames.StandbyKeys]);
        Assert.Equal(SigningKeyRole.Active, Assert.Single(SigningKeysIn(folder)).Role);
    }

    [Fact]
    public void Setup_prefix_none_and_own_prefix()
    {
        using var none = ScriptedTool.Installed(new SetupAnswers(Prefix: "none"));
        Assert.Equal("", Settings(none.HomeInstallation)[SettingNames.KeyPrefix]);

        using var own = ScriptedTool.Installed(new SetupAnswers(Prefix: "ACME2"));
        Assert.Equal("ACME2", Settings(own.HomeInstallation)[SettingNames.KeyPrefix]);
    }

    [Fact]
    public void Invalid_prefix_names_the_rule_and_asks_again()
    {
        using var tool = Terminal("\nacme-shop\nlic\nWAYTOOLONGPREFIXX\nACME\n\n\n", Keys(Pass));

        Assert.Equal(0, tool.Run("setup"));

        Assert.Equal(3, Count(tool.Stderr, "1 to 16 characters of A-Z and 0-9"));
        Assert.Equal("ACME", Settings(tool.HomeInstallation)[SettingNames.KeyPrefix]);
    }

    [Fact]
    public void Invalid_prefix_creates_nothing_until_setup_completes()
    {
        using var tool = Terminal("\nacme-shop\n", Keys(Pass));

        Assert.Equal(1, tool.Run("setup"));

        Assert.Contains("1 to 16 characters of A-Z and 0-9", tool.Stderr);
        AssertNothingCreated(tool);
    }

    [Fact]
    public void Passphrases_differ_asks_for_both_again()
    {
        using var tool = Terminal("\n\n\n\n", Keys("first passphrase long", "other passphrase long") + Keys(Pass));

        Assert.Equal(0, tool.Run("setup"));

        Assert.Contains("differ", tool.Stderr);
        Assert.Equal(2, Count(tool.Stderr, "Passphrase again:"));
        using var db = Installation.Open(tool.HomeInstallation);
        var check = KeyProtection.LoadCheck(db)!;
        Assert.True(check.Verify(Pass));
        Assert.False(check.Verify("first passphrase long"));
    }

    [Fact]
    public void Passphrases_that_differ_only_before_normalisation_match()
    {
        using var tool = Terminal("\n\n\n\n", Keys("café café café café", "café café café café"));

        Assert.Equal(0, tool.Run("setup"));
        Assert.DoesNotContain("differ", tool.Stderr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("fourteen chars")]
    public void Passphrase_too_short_names_the_minimum_and_asks_again(string tooShort)
    {
        using var tool = Terminal("\n\n\n\n", $"{tooShort}\n" + Keys(Pass));

        Assert.Equal(0, tool.Run("setup"));

        Assert.Contains("at least 15 characters. Try again", tool.Stderr);
        using var db = Installation.Open(tool.HomeInstallation);
        Assert.True(KeyProtection.LoadCheck(db)!.Verify(Pass));
    }

    [Fact]
    public void Passphrase_length_counts_code_points_after_normalisation()
    {
        var fifteen = string.Concat(Enumerable.Repeat("\U0001F511", 15));
        var fourteen = string.Concat(Enumerable.Repeat("\U0001F511", 14));
        var decomposed = string.Concat(Enumerable.Repeat("é", 15)); // NFC: 15 code points, 30 UTF-16 units
        var decomposedShort = string.Concat(Enumerable.Repeat("é", 14));

        using var tool = Terminal("\n\n\n\n", $"{fourteen}\n{decomposedShort}\n" + Keys(fifteen));
        Assert.Equal(0, tool.Run("setup"));
        Assert.Equal(2, Count(tool.Stderr, "at least 15 characters. Try again"));

        using var second = Terminal("\n2\n\n\n\n", Keys(decomposed));
        Assert.Equal(0, second.Run("setup"));
        Assert.DoesNotContain("Try again", second.Stderr);
    }

    [Fact]
    public void Folder_already_holds_an_installation()
    {
        using var tool = Terminal("\n", "");
        var folder = ScriptedTool.MakeInstallation(tool.HomeInstallation);
        var database = Path.Combine(folder, Installation.DatabaseFileName);
        File.WriteAllText(database, "existing");
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(database, stamp);

        var exit = tool.Run("setup");

        Assert.Equal(1, exit);
        Assert.Contains(folder, tool.Stderr);
        Assert.Contains("already holds an installation", tool.Stderr);
        Assert.DoesNotContain("Passphrase:", tool.Stderr);
        Assert.Equal("existing", File.ReadAllText(database));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(database));
        Assert.Equal([database], Directory.GetFileSystemEntries(folder));
    }

    /// <summary>Runs <paramref name="afterLine"/> when the given 1-based line has been read, as another terminal would.</summary>
    private sealed class HookedReader(string text, int line, Action afterLine) : StringReader(text)
    {
        private int _read;

        public override string? ReadLine()
        {
            var value = base.ReadLine();
            if (++_read == line)
            {
                afterLine();
            }

            return value;
        }
    }

    [Fact]
    public void An_installation_made_while_the_questions_are_answered_is_refused_and_left_untouched()
    {
        using var probe = new ScriptedTool();
        var folder = Path.GetFullPath(probe.HomeInstallation);
        var database = Path.Combine(folder, Installation.DatabaseFileName);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        // The fourth answer is the last question: the file appears after it, before setup writes.
        var reader = new HookedReader("\n\n\n\n", 4, () =>
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(database, "made by another run");
            File.SetLastWriteTimeUtc(database, stamp);
        });
        using var tool = probe.Another(stdinRedirected: false, hiddenKeys: Keys(Pass), stdinReader: reader);

        var exit = tool.Run("setup");

        Assert.Equal(1, exit);
        Assert.Contains($"{folder} already holds an installation. Setup changed nothing there.", tool.Stderr);
        Assert.Equal("made by another run", File.ReadAllText(database));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(database));
        Assert.Equal([database], Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public void Second_installation_is_independent()
    {
        using var tool = ScriptedTool.Installed(new SetupAnswers(Prefix: "ONE"));
        var cwd = Path.GetFullPath(tool.Environment.CurrentDirectory);
        Directory.CreateDirectory(cwd);

        using var second = tool.Another(
            stdin: new SetupAnswers(CurrentFolder: true, Prefix: "TWO", Shared: true).Stdin,
            stdinRedirected: false,
            hiddenKeys: new SetupAnswers().HiddenKeys);
        Assert.Equal(0, second.Run("setup"));

        Assert.Equal("TWO", Settings(cwd)[SettingNames.KeyPrefix]);
        Assert.Equal("ONE", Settings(tool.HomeInstallation)[SettingNames.KeyPrefix]);
        Assert.Equal(2, SigningKeysIn(cwd).Count);
        Assert.Empty(SigningKeysIn(tool.HomeInstallation));

        using (var db = Installation.Open(cwd))
        {
            db.Products.Add(new Product { ProductId = "acme-shop", DisplayName = "Acme", CreatedUtc = DateTimeOffset.UnixEpoch });
            db.SaveChanges();
        }

        using (var db = Installation.Open(tool.HomeInstallation))
        {
            Assert.Empty(db.Products);
        }

        // Actions run in the current folder use it; elsewhere they still use ~/.ucl.
        using var here = tool.Another();
        Assert.Equal(0, here.Run("folder", "show"));
        Assert.Equal(cwd + System.Environment.NewLine, here.Stdout);

        var elsewhere = Path.Combine(Path.GetDirectoryName(cwd)!, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        using var away = tool.Another();
        Assert.Equal(0, ToolApp.Run(["folder", "show"], away.Environment with { CurrentDirectory = elsewhere }));
        Assert.Equal(Path.GetFullPath(tool.HomeInstallation) + System.Environment.NewLine, away.Stdout);
    }

    [Fact]
    public void Setup_with_nobody_present_redirected_stdin()
    {
        using var tool = new ScriptedTool(stdin: "\n\n\n\n", stdinRedirected: true);

        var exit = tool.Run("setup");

        Assert.Equal(1, exit);
        Assert.Contains("person at a terminal", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.Equal("", tool.Environment.Console.ReadLine());
        AssertNothingCreated(tool);
        Assert.False(Directory.Exists(tool.Environment.Home));
    }

    [Fact]
    public void Setup_with_nobody_present_no_input_at_a_terminal()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));

        var exit = tool.Run("setup", "--no-input");

        Assert.Equal(1, exit);
        Assert.Contains("person at a terminal", tool.Stderr);
        Assert.Equal("", tool.Environment.Console.ReadLine());
        Assert.False(Directory.Exists(tool.Environment.Home));
        AssertNothingCreated(tool);
    }

    [Fact]
    public void Default_folder_created()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));
        Assert.False(Directory.Exists(tool.HomeInstallation));

        Assert.Equal(0, tool.Run("setup"));

        Assert.True(Installation.Exists(tool.HomeInstallation));
        Assert.Contains(Path.GetFullPath(tool.HomeInstallation) + " (proposed)", tool.Stderr);
        Assert.Contains(Path.GetFullPath(tool.Environment.CurrentDirectory) + " (current folder)", tool.Stderr);
    }

    [Fact]
    public void Current_folder_with_other_files()
    {
        using var tool = Terminal("2\n\n\n\n", Keys(Pass));
        var cwd = Path.GetFullPath(tool.Environment.CurrentDirectory);
        Directory.CreateDirectory(Path.Combine(cwd, "sub"));
        File.WriteAllText(Path.Combine(cwd, "notes.txt"), "mine");
        File.WriteAllText(Path.Combine(cwd, "sub", "x.md"), "also mine");

        Assert.Equal(0, tool.Run("setup"));

        Assert.True(Installation.Exists(cwd));
        Assert.False(Directory.Exists(tool.HomeInstallation));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(cwd, "notes.txt")));
        Assert.Equal("also mine", File.ReadAllText(Path.Combine(cwd, "sub", "x.md")));
        Assert.Contains($"The installation is in {cwd}", tool.Stderr);
    }

    [Fact]
    public void Current_folder_that_is_home_warns_about_loose_files()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));
        Directory.CreateDirectory(tool.Environment.Home);

        var exit = ToolApp.Run(["setup"], tool.Environment with { CurrentDirectory = tool.Environment.Home + Path.DirectorySeparatorChar });

        Assert.Equal(0, exit);
        Assert.Contains("current folder is your home folder", tool.Stderr);
        Assert.Contains("sit loose in it", tool.Stderr);
    }

    [Fact]
    public void Current_folder_that_is_not_home_does_not_warn()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));

        Assert.Equal(0, tool.Run("setup"));
        Assert.DoesNotContain("your home folder", tool.Stderr);
    }

    [Fact]
    public void Folder_offered_once_when_current_folder_is_the_proposed_one()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));
        Directory.CreateDirectory(tool.HomeInstallation);

        var exit = ToolApp.Run(["setup"], tool.Environment with { CurrentDirectory = tool.HomeInstallation });

        Assert.Equal(0, exit);
        Assert.Equal(1, Count(tool.Stderr, Path.GetFullPath(tool.HomeInstallation) + " ("));
        Assert.DoesNotContain("(current folder)", tool.Stderr);
        Assert.Contains("also the current folder", tool.Stderr);
    }

    [Fact]
    public void Folder_answer_out_of_range_asks_again()
    {
        using var tool = Terminal("3\nx\n2\n\n\n\n", Keys(Pass));

        Assert.Equal(0, tool.Run("setup"));

        Assert.Equal(2, Count(tool.Stderr, "Answer with a number from 1 to 2."));
        Assert.True(Installation.Exists(tool.Environment.CurrentDirectory));
    }

    [Fact]
    public void No_installation_found_accepted_runs_setup_in_full_then_refuses_the_action()
    {
        using var tool = Terminal("y\n\n\n\n\n", Keys(Pass));

        var exit = tool.Run("folder", "show");

        Assert.Equal(1, exit);
        Assert.True(Installation.Exists(tool.HomeInstallation));
        Assert.Equal("", tool.Stdout);
        Assert.Contains("Run setup now? [y/N]", tool.Stderr);
        Assert.Contains("Setup finished. Run the command again.", tool.Stderr);
    }

    [Fact]
    public void Folder_named_on_the_command_holds_no_installation_offers_nothing()
    {
        using var tool = Terminal("y\n\n\n\n\n", Keys(Pass));
        var named = Path.GetFullPath(Path.Combine(tool.Environment.CurrentDirectory, "acme-practice"));

        var exit = tool.Run("folder", "show", "--folder", "acme-practice");

        Assert.Equal(1, exit);
        Assert.Contains(named, tool.Stderr);
        Assert.DoesNotContain("Run setup now?", tool.Stderr);
        Assert.DoesNotContain("Where should the installation live?", tool.Stderr);
        Assert.Equal("y", tool.Environment.Console.ReadLine());
        AssertNothingCreated(tool);
    }

    [Theory]
    [InlineData("--folder", "x")]
    [InlineData("--passphrase-stdin")]
    public void Setup_does_not_take_folder_or_passphrase_stdin(params string[] options)
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));

        var exit = tool.Run(["setup", .. options]);

        Assert.Equal(2, exit);
        Assert.Contains(options[0], tool.Stderr);
        Assert.Equal("\n", tool.Environment.Console.ReadLine() + "\n");
        AssertNothingCreated(tool);
        Assert.False(Directory.Exists(Path.Combine(tool.Environment.CurrentDirectory, "x")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\n\n")]
    [InlineData("\n\nLIC\n")]
    [InlineData("\n\nLIC\n2\n")]
    public void End_of_input_while_asking_refuses_and_creates_nothing(string stdin)
    {
        using var tool = Terminal(stdin, Keys(Pass));

        var exit = tool.Run("setup");

        Assert.Equal(1, exit);
        Assert.Contains("Input ended before setup was complete", tool.Stderr);
        AssertNothingCreated(tool);
        Assert.False(Directory.Exists(tool.Environment.Home));
    }

    [Fact]
    public void Passphrase_variable_is_ignored_by_setup()
    {
        var typed = "typed passphrase 123";
        using var tool = Terminal(
            "\n\n\n\n",
            Keys(typed),
            new Dictionary<string, string> { [ToolContext.PassphraseVariable] = "variable passphrase!" });

        Assert.Equal(0, tool.Run("setup"));

        using var db = Installation.Open(tool.HomeInstallation);
        var check = KeyProtection.LoadCheck(db)!;
        Assert.True(check.Verify(typed));
        Assert.False(check.Verify("variable passphrase!"));
    }

    [Fact]
    public void A_failed_write_removes_what_the_run_created()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));

        // Zero iterations fails inside the write, after ucl.db was created.
        var exit = ToolApp.Run(["setup"], tool.Environment with { Pbkdf2Iterations = 0 });

        Assert.NotEqual(0, exit);
        Assert.False(Directory.Exists(tool.HomeInstallation));
    }

    [Fact]
    public void A_failed_write_keeps_a_folder_that_already_existed_and_its_files()
    {
        using var tool = Terminal("\n\n\n\n", Keys(Pass));
        Directory.CreateDirectory(tool.HomeInstallation);
        var other = Path.Combine(tool.HomeInstallation, "keep.txt");
        File.WriteAllText(other, "keep");

        var exit = ToolApp.Run(["setup"], tool.Environment with { Pbkdf2Iterations = 0 });

        Assert.NotEqual(0, exit);
        Assert.Equal([other], Directory.GetFileSystemEntries(tool.HomeInstallation));
    }

    [Fact]
    public void Installed_helper_lets_later_commands_run_against_the_same_folders()
    {
        using var tool = ScriptedTool.Installed();

        using var again = tool.Another(variables: new Dictionary<string, string> { [ToolContext.PassphraseVariable] = Pass });
        Assert.Equal(0, again.Run("stub-key"));
        Assert.Single(SigningKeysIn(tool.HomeInstallation));
    }

    [Fact]
    public void A_passphrase_that_is_not_valid_text_is_refused()
    {
        using var tool = Terminal("\n", "\ud800 lone surrogate passphrase\n");

        var exit = tool.Run("setup");

        Assert.Equal(1, exit);
        Assert.Contains("The passphrase is not valid text. Nothing changed.", tool.Stderr);
        AssertNothingCreated(tool);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
