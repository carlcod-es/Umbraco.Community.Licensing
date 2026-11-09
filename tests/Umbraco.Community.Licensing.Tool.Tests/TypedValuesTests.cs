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

    // Rows 95 and 96 of follow-ups, and the Issue session's typed values

    [Fact]
    public void Email_TextRuleRefusal_AlsoStatesTheAtAndSpaceRules()
    {
        var refusal = TypedValues.CheckCustomerEmail("a\U0001F600@b.c");
        Assert.Contains(TypedValues.EmailRule, refusal);
    }

    [Theory]
    [InlineData(" owner@example.com")]
    [InlineData("owner@example.com ")]
    public void Email_WithLeadingOrTrailingSpace_IsRefused(string email) => Assert.NotNull(TypedValues.CheckCustomerEmail(email));

    [Theory]
    [InlineData(" ORD-1042")]
    [InlineData("ORD-1042 ")]
    public void OrderReference_WithLeadingOrTrailingSpace_IsRefused(string orderRef) => Assert.NotNull(TypedValues.CheckOrderReference(orderRef));

    [Fact]
    public void Email_DecomposedAccentedForm_IsAccepted() =>
        Assert.Null(TypedValues.CheckCustomerEmail("ju\u0308rgen@mu\u0308ller.de"));

    private static readonly DateTimeOffset Now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadExpiry_Date_IsTheEndOfThatDayUtc()
    {
        Assert.Null(TypedValues.ReadExpiry("2027-03-01", Now, out var answer));
        Assert.Equal(new DateTime(2027, 3, 1, 23, 59, 59, DateTimeKind.Utc), answer!.ExpiresUtc);
    }

    [Fact]
    public void ReadExpiry_UtcInstant_IsKept()
    {
        Assert.Null(TypedValues.ReadExpiry("2027-03-01T12:30:15Z", Now, out var answer));
        Assert.Equal(new DateTime(2027, 3, 1, 12, 30, 15, DateTimeKind.Utc), answer!.ExpiresUtc);
    }

    [Theory]
    [InlineData("perpetual")]
    [InlineData("Perpetual")]
    [InlineData("PERPETUAL")]
    public void ReadExpiry_Perpetual_InAnyCase_HasNoInstant(string text)
    {
        Assert.Null(TypedValues.ReadExpiry(text, Now, out var answer));
        Assert.Null(answer!.ExpiresUtc);
    }

    [Theory]
    [InlineData("2027-02-30")]
    [InlineData("2027-13-01")]
    [InlineData("1/3/2027")]
    [InlineData("2027-03-01T10:00:00")] // no Z
    [InlineData("2027-03-01T10:00:00+01:00")]
    [InlineData("2027-03-01T10:00:00.5Z")]
    [InlineData("never")]
    [InlineData("")]
    public void ReadExpiry_OutsideTheRule_NamesTheExpiryRule(string text) =>
        Assert.Equal(TypedValues.ExpiryRuleMessage, TypedValues.ReadExpiry(text, Now, out _));

    [Fact]
    public void ReadExpiry_EarlierThanNow_IsRefusedByTheLibraryRule_AndTheCurrentSecondIsAccepted()
    {
        Assert.Contains("earlier than the current time", TypedValues.ReadExpiry("2026-11-08T08:59:59Z", Now, out _));
        Assert.Null(TypedValues.ReadExpiry("2026-11-08T09:00:00Z", Now, out _));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("500", 500)]
    [InlineData("2.5", 2.5)]
    [InlineData("0.0001", 0.0001)]
    [InlineData("999999999999999", 999999999999999)]
    public void ReadNumber_Accepts(string text, double expected)
    {
        Assert.Null(TypedValues.ReadNumber(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("lots")]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.23456")]
    [InlineData("1,000")]
    [InlineData("1e3")]
    [InlineData("\u0665")] // an Arabic-Indic digit
    [InlineData("1234567890123456")] // 16 digits
    public void ReadNumber_Refuses_NamingTheNumberRule(string text) =>
        Assert.Contains("A number is a number", TypedValues.ReadNumber(text, out _));

    [Theory]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("off", false)]
    [InlineData("Off", false)]
    public void ReadSwitch_AcceptsOnAndOff_InAnyCase(string text, bool expected)
    {
        Assert.Null(TypedValues.ReadSwitch(text, out var on));
        Assert.Equal(expected, on);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("true")]
    [InlineData("")]
    public void ReadSwitch_RefusesAnythingElse(string text) => Assert.Equal(TypedValues.SwitchRule, TypedValues.ReadSwitch(text, out _));

    [Fact]
    public void CheckVendorTag_FollowsTheLibraryRule()
    {
        Assert.Null(TypedValues.CheckVendorTag("reseller#7"));
        Assert.NotNull(TypedValues.CheckVendorTag("bad tag"));
        Assert.NotNull(TypedValues.CheckVendorTag(new string('a', 65)));
    }

    [Fact]
    public void CheckTextFeature_FollowsTheLibraryRule()
    {
        Assert.Null(TypedValues.CheckTextFeature("note", "Gold plan"));
        Assert.NotNull(TypedValues.CheckTextFeature("note", "star \u2B50"));
        Assert.NotNull(TypedValues.CheckTextFeature("note", new string('a', 257)));
    }

    [Fact]
    public void FeaturesJson_WritesSwitchNumberAndText_InOrder_WithCanonicalNumbers()
    {
        var features = FeatureList.Empty.With("a", true).With("b", 2.50m).With("c", "t\"x\u00fc");
        Assert.Equal("""{"a":true,"b":2.5,"c":"t\u0022x\u00FC"}""", LicenseActions.FeaturesJson.Write(features).Replace("\\u0022", "\\u0022"));
        Assert.Equal("{}", LicenseActions.FeaturesJson.Write(FeatureList.Empty));
    }
}
