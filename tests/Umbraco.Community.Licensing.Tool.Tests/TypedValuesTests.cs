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

    // Customer email and order reference (vendor-tool-licenses: Customer email and order reference values, Matching when finding)

    [Fact]
    public void Email_Accented_IsAccepted() => Assert.Null(TypedValues.CheckCustomerEmail("j\u00fcrgen@m\u00fcller.de"));

    [Theory]
    [InlineData("owner")]
    [InlineData("a@b@example.com")]
    [InlineData("owner @example.com")]
    [InlineData("@example.com")]
    [InlineData("owner@")]
    [InlineData("@")]
    [InlineData("")]
    public void Email_OutsideTheRule_IsRefused_NamingTheCustomerEmail(string email)
    {
        var refusal = TypedValues.CheckCustomerEmail(email);
        Assert.NotNull(refusal);
        Assert.Contains("customer email", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Email_BoundaryLengths()
    {
        Assert.Null(TypedValues.CheckCustomerEmail("a@b"));
        Assert.Null(TypedValues.CheckCustomerEmail(new string('a', 250) + "@b.c")); // 254
        Assert.NotNull(TypedValues.CheckCustomerEmail(new string('a', 251) + "@b.c")); // 255
    }

    [Fact]
    public void Email_WithEmoji_IsRefused_ByTheTextRule() =>
        Assert.Contains("customer email", TypedValues.CheckCustomerEmail("a\U0001F600@b.c"), StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("#1042")]
    [InlineData("INV-2026-0042")]
    [InlineData("ORD 1042")]
    [InlineData("UMB:1042")]
    [InlineData("pi_3NqXk2LkdIwHu7ix0yJ8")]
    public void OrderReference_FromShops_IsAccepted(string orderRef) => Assert.Null(TypedValues.CheckOrderReference(orderRef));

    [Fact]
    public void OrderReference_Of64IsAccepted_AndOf65IsRefused()
    {
        Assert.Null(TypedValues.CheckOrderReference(new string('a', 64)));
        Assert.NotNull(TypedValues.CheckOrderReference(new string('a', 65)));
    }

    [Theory]
    [InlineData("ORD-1042 \u2B50")]
    [InlineData("")]
    public void OrderReference_OutsideTheRule_IsRefused_NamingTheOrderReference(string orderRef)
    {
        var refusal = TypedValues.CheckOrderReference(orderRef);
        Assert.NotNull(refusal);
        Assert.Contains("order reference", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchKey_Email_IgnoresCase() =>
        Assert.Equal(TypedValues.MatchKey("owner@example.com"), TypedValues.MatchKey("Owner@Example.com"));

    [Fact]
    public void MatchKey_AccentedLetter_IsTheSameInEitherForm()
    {
        var composed = "j\u00fcrgen@m\u00fcller.de";
        var decomposed = "ju\u0308rgen@mu\u0308ller.de";
        Assert.NotEqual(composed, decomposed);
        Assert.Equal(TypedValues.MatchKey(composed), TypedValues.MatchKey(decomposed));
    }

    [Fact]
    public void MatchKey_OrderReference_IgnoresCase() =>
        Assert.Equal(TypedValues.MatchKey("ORD-1042"), TypedValues.MatchKey("ord-1042"));

    [Fact]
    public void MatchKey_PartOfAnOrderReference_DoesNotMatch() =>
        Assert.NotEqual(TypedValues.MatchKey("ORD-1042"), TypedValues.MatchKey("1042"));

    [Fact]
    public void MatchKey_SpacesInAnOrderReference_AreSignificant()
    {
        Assert.NotEqual(TypedValues.MatchKey("ORD 1042"), TypedValues.MatchKey("ORD  1042"));
        Assert.Equal(TypedValues.MatchKey("ORD 1042"), TypedValues.MatchKey("ord 1042"));
    }
}
