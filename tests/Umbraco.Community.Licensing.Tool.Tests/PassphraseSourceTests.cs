using System.Text;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class PassphraseSourceTests
{
    private const string Passphrase = "correct horse battery";

    private static IReadOnlyDictionary<string, string> Env(string value) =>
        new Dictionary<string, string> { [ToolContext.PassphraseVariable] = value };

    private static ScriptedTool Prepared(
        string passphrase = Passphrase,
        string stdin = "",
        bool stdinRedirected = true,
        string hiddenKeys = "",
        IReadOnlyDictionary<string, string>? variables = null,
        byte[]? stdinBytes = null)
    {
        var tool = new ScriptedTool(stdin, stdinRedirected, hiddenKeys, variables: variables, stdinBytes: stdinBytes);
        using var db = Installation.Open(tool.HomeInstallation);
        KeyProtection.StoreCheck(db, passphrase, tool.Environment.Pbkdf2Iterations);
        db.SaveChanges();
        return tool;
    }

    private static int KeyCount(ScriptedTool tool)
    {
        using var db = Installation.Open(tool.HomeInstallation);
        return db.SigningKeys.Count();
    }

    [Fact]
    public void A_wrong_passphrase_is_refused_before_any_key_is_created()
    {
        using var tool = Prepared(variables: Env("not the passphrase"));

        Assert.Equal(1, tool.Run("stub-key"));

        Assert.Contains("Wrong passphrase", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void The_right_passphrase_creates_the_key_encrypted()
    {
        using var tool = Prepared(variables: Env(Passphrase));

        Assert.Equal(0, tool.Run("stub-key"));

        using var db = Installation.Open(tool.HomeInstallation);
        var row = db.SigningKeys.Single();
        Assert.Equal(row.SigningKeyId, tool.Stdout.Trim());
        using var key = KeyProtection.Decrypt(row.EncryptedPrivateKeyPem, Passphrase);
        Assert.Equal(row.PublicKey, key.PublicKey.Export());
    }

    [Fact]
    public void An_installation_without_a_check_is_refused()
    {
        using var tool = new ScriptedTool(variables: Env(Passphrase));
        Installation.Open(tool.HomeInstallation).Dispose();

        Assert.Equal(1, tool.Run("stub-key"));

        Assert.Contains("no usable passphrase check", tool.Stderr);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void Stdin_wins_over_the_environment()
    {
        using var tool = Prepared(stdin: Passphrase + "\nsecond line\n", variables: Env("not the passphrase"));

        Assert.Equal(0, tool.Run("stub-key", "--passphrase-stdin"));
        Assert.Equal(1, KeyCount(tool));
    }

    [Fact]
    public void The_environment_is_used_without_a_prompt()
    {
        using var tool = Prepared(variables: Env(Passphrase));

        Assert.Equal(0, tool.Run("stub-key"));
        Assert.DoesNotContain("Passphrase:", tool.Stderr);
    }

    [Fact]
    public void The_hidden_prompt_is_used_at_a_terminal_when_nothing_else_is_given()
    {
        using var tool = Prepared(stdinRedirected: false, hiddenKeys: Passphrase + "\n");

        Assert.Equal(0, tool.Run("stub-key"));
        Assert.Contains("Passphrase: ", tool.Stderr);
        Assert.DoesNotContain(Passphrase, tool.Stderr);
        Assert.Equal(1, KeyCount(tool));
    }

    [Fact]
    public void Missing_under_no_input_at_a_terminal_names_both_sources()
    {
        using var tool = Prepared(stdinRedirected: false);

        Assert.Equal(1, tool.Run("stub-key", "--no-input"));
        Assert.Contains("--passphrase-stdin", tool.Stderr);
        Assert.Contains("UCL_PASSPHRASE", tool.Stderr);
        Assert.DoesNotContain("Passphrase: ", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void Missing_with_redirected_stdin_is_refused()
    {
        using var tool = Prepared();

        Assert.Equal(1, tool.Run("stub-key"));
        Assert.Contains("UCL_PASSPHRASE", tool.Stderr);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void An_empty_prompt_answer_counts_as_missing()
    {
        using var tool = Prepared(stdinRedirected: false, hiddenKeys: "\n");

        Assert.Equal(1, tool.Run("stub-key"));
        Assert.Contains("--passphrase-stdin", tool.Stderr);
    }

    [Fact]
    public void An_empty_environment_variable_counts_as_missing()
    {
        using var tool = Prepared(variables: Env(""));

        Assert.Equal(1, tool.Run("stub-key"));
        Assert.Contains("UCL_PASSPHRASE", tool.Stderr);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void An_empty_stdin_line_is_refused_without_falling_through_to_the_environment()
    {
        using var tool = Prepared(stdin: "\n", variables: Env(Passphrase));

        Assert.Equal(1, tool.Run("stub-key", "--passphrase-stdin"));
        Assert.Contains("--passphrase-stdin was given but no passphrase was on stdin", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void End_of_stdin_is_refused_without_falling_through_to_the_environment()
    {
        using var tool = Prepared(variables: Env(Passphrase));

        Assert.Equal(1, tool.Run("stub-key", "--passphrase-stdin"));
        Assert.Contains("--passphrase-stdin was given but no passphrase was on stdin", tool.Stderr);
        Assert.Equal("", tool.Stdout);
        Assert.Equal(0, KeyCount(tool));
    }

    // "créme brûlée façade" with every accent composed (NFC) or as base letter plus combining mark (NFD).
    private const string Nfc = "cr\u00e9me br\u00fbl\u00e9e fa\u00e7ade";
    private const string Nfd = "cre\u0301me bru\u0302le\u0301e fac\u0327ade";

    [Fact]
    public void A_non_ASCII_stdin_line_matches_the_same_passphrase_typed_at_the_prompt()
    {
        // The check is made from the prompt run; each stdin form must verify against it.
        using var typed = Prepared(passphrase: Nfc, stdinRedirected: false, hiddenKeys: Nfc + "\n");
        Assert.Equal(0, typed.Run("stub-key"));

        var utf8 = new UTF8Encoding(false);
        byte[] Line(string text, bool bom, string ending) =>
            [.. bom ? Encoding.UTF8.GetPreamble() : [], .. utf8.GetBytes(text + ending)];

        foreach (var text in new[] { Nfc, Nfd })
        {
            foreach (var bom in new[] { false, true })
            {
                foreach (var ending in new[] { "", "\n", "\r\n" })
                {
                    using var piped = Prepared(
                        passphrase: Nfc,
                        stdinBytes: Line(text, bom, ending));
                    Assert.True(
                        piped.Run("stub-key", "--passphrase-stdin") == 0,
                        $"nfd={text == Nfd} bom={bom} ending={ending.Length}: {piped.Stderr}");
                    Assert.Equal(1, KeyCount(piped));
                }
            }
        }
    }

    [Fact]
    public void Passphrase_stdin_at_a_terminal_is_refused_without_reading()
    {
        using var tool = Prepared(stdin: Passphrase + "\n", stdinRedirected: false, hiddenKeys: Passphrase + "\n");

        Assert.Equal(1, tool.Run("stub-key", "--passphrase-stdin"));
        Assert.Contains("piped on stdin", tool.Stderr);
        Assert.Contains("leave it out", tool.Stderr);
        Assert.DoesNotContain("Passphrase: ", tool.Stderr);
        Assert.Equal(0, KeyCount(tool));
    }

    [Fact]
    public void Spaces_in_the_passphrase_are_kept()
    {
        const string spaced = "  spaced passphrase here  ";
        using var tool = Prepared(passphrase: spaced, stdin: spaced + "\r\n");

        Assert.Equal(0, tool.Run("stub-key", "--passphrase-stdin"));
        Assert.Equal(1, KeyCount(tool));
    }

    [Fact]
    public void A_trimmed_passphrase_does_not_match_a_spaced_one()
    {
        using var tool = Prepared(passphrase: "  spaced passphrase here  ", variables: Env("spaced passphrase here"));

        Assert.Equal(1, tool.Run("stub-key"));
    }
}
