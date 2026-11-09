namespace Umbraco.Community.Licensing.Tool.Tests;

public class TypedValuesTests
{
    [Fact]
    public void CheckDisplayName_ValidName_GivesNoProblems() =>
        Assert.Empty(TypedValues.CheckDisplayName("Acme Ltd"));

    [Fact]
    public void CheckDisplayName_NameWithEmoji_GivesProblem() =>
        Assert.NotEmpty(TypedValues.CheckDisplayName("Acme \U0001F600"));

    private static string? Passphrase(string typed) => TypedValues.CheckPassphrase(typed, typed.Length);

    [Theory]
    [InlineData("correct horse battery staple")] // words with spaces inside
    [InlineData("123456789012345")] // exactly 15
    [InlineData("a b c d e f g h i j")]
    [InlineData("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~ab")] // every symbol of a standard keyboard
    public void Passphrase_Accepted(string typed) => Assert.Null(Passphrase(typed));

    [Fact]
    public void Passphrase_Of256Characters_IsAccepted_AndOf257IsTooLong()
    {
        Assert.Null(Passphrase(new string('a', 256)));
        Assert.Equal(TypedValues.PassphraseTooLong, Passphrase(new string('a', 257)));
    }

    [Fact]
    public void Passphrase_LongerThanTheBuffer_IsTooLong_ByTheTrueLengthTyped() =>
        Assert.Equal(TypedValues.PassphraseTooLong, TypedValues.CheckPassphrase(new string('a', HiddenAnswer.Capacity), 400));

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345678901234")] // 14
    public void Passphrase_TooShort_NamesTheMinimum(string typed) =>
        Assert.Equal(TypedValues.PassphraseTooShort, Passphrase(typed));

    [Theory]
    [InlineData("Grüße aus München 2026")]
    [InlineData("a passphrase with é")]
    [InlineData("tab\tseparated passphrase")]
    [InlineData("an emoji \U0001F600 passphrase")]
    [InlineData("a passphrase with nbsp")]
    public void Passphrase_NotPrintableAscii_NamesTheCharacterRule(string typed) =>
        Assert.Equal(TypedValues.PassphraseCharacters, Passphrase(typed));

    [Theory]
    [InlineData(" leading space passphrase")]
    [InlineData("trailing space passphrase ")]
    [InlineData(" both ends of a passphrase ")]
    public void Passphrase_LeadingOrTrailingSpace_Refused(string typed) =>
        Assert.Equal(TypedValues.PassphraseEdgeSpace, Passphrase(typed));

    [Theory]
    [InlineData("LIC", "LIC")]
    [InlineData("ACME", "ACME")]
    [InlineData("A1B2C3D4E5F6G7H8", "A1B2C3D4E5F6G7H8")]
    [InlineData("none", "")]
    [InlineData("NONE", "")]
    [InlineData("None", "")]
    public void Prefix_Accepted(string typed, string expected)
    {
        Assert.Null(TypedValues.CheckPrefix(typed, out var prefix));
        Assert.Equal(expected, prefix.Value);
    }

    [Theory]
    [InlineData("acme-shop")]
    [InlineData("acme")]
    [InlineData("A1B2C3D4E5F6G7H8I")] // 17
    [InlineData("AC ME")]
    [InlineData("none please")]
    [InlineData("")]
    public void Prefix_Refused_NamesTheRule(string typed) =>
        Assert.Equal(TypedValues.PrefixRule, TypedValues.CheckPrefix(typed));

    [Theory]
    [InlineData("1", 1)]
    [InlineData("15", 15)]
    [InlineData("1440", 1440)]
    [InlineData("007", 7)]
    public void IdleLock_Minutes_Accepted(string typed, int minutes)
    {
        Assert.True(TypedValues.TryParseIdleLock(typed, out var idle));
        Assert.Equal(TimeSpan.FromMinutes(minutes), idle);
    }

    [Theory]
    [InlineData("never")]
    [InlineData("NEVER")]
    [InlineData("Never")]
    public void IdleLock_Never_AnyCase_IsNull(string typed)
    {
        Assert.True(TypedValues.TryParseIdleLock(typed, out var idle));
        Assert.Null(idle);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("ten")]
    [InlineData("")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("٣")] // an Arabic-Indic digit is not an ASCII digit
    [InlineData("99999999999999999999")]
    public void IdleLock_Refused_NamesTheRule(string typed)
    {
        Assert.False(TypedValues.TryParseIdleLock(typed, out _));
        Assert.Equal(TypedValues.IdleLockRule, TypedValues.CheckIdleLock(typed));
    }

    [Theory]
    [InlineData(15, "15")]
    [InlineData(1440, "1440")]
    public void IdleLockSetting_IsWholeMinutes(int minutes, string expected) =>
        Assert.Equal(expected, TypedValues.IdleLockSetting(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void IdleLockSetting_Never_IsLowerCaseNever() =>
        Assert.Equal("never", TypedValues.IdleLockSetting(null));
}
