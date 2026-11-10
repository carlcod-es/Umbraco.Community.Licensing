using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Licenses &gt; Choosing a license, Reissue, Renew and Resend (vendor-tool-licenses), as whole sessions, in both signing-key modes where the key matters.</summary>
public class ReissueTests : IDisposable
{
    private readonly List<LicenseKit> _kits = [];

    public void Dispose() => _kits.ForEach(k => k.Dispose());

    public static TheoryData<bool> Modes => new() { false, true };

    private LicenseKit Kit(bool shared, string productId = "acme.commerce", string prefix = "")
    {
        var kit = new LicenseKit(shared, productId, prefix: prefix);
        _kits.Add(kit);
        return kit;
    }

    private static IssueEntry Latest(LicenseKit kit) => kit.Entries().OrderBy(e => e.Id).Last();

    private static string StoredReference(LicenseKit kit) => Assert.Single(kit.Licenses()).Reference;

    private static void AssertOrder(string text, params string[] markers)
    {
        var at = -1;
        foreach (var marker in markers)
        {
            var next = text.IndexOf(marker, at + 1, StringComparison.Ordinal);
            Assert.True(next > at, $"'{marker}' is missing or out of order in:\n{text}");
            at = next;
        }
    }

    // --- Choosing a license

    [Theory]
    [MemberData(nameof(Modes))]
    public void KeyPastedToReissue_ChoosesItsLicense_AndTheQuestionsShowTheRecordsValues(bool shared)
    {
        var kit = Kit(shared);
        var key = kit.Issue(displayName: "Acme Shop EU", vendorTag: "reseller#7", note: "Gold plan");
        var reference = StoredReference(kit);
        var console = Sign(ReissueQuestions(new ScriptedConsole(), key));

        kit.Run(console);

        var text = console.Out;
        Assert.Contains("Enter for 2027-03-01T23:59:59Z", text);
        Assert.Contains("Enter for Acme Shop EU", text);
        Assert.Contains("Enter for reseller#7", text);
        Assert.Contains("Enter for 500", text);
        Assert.Contains("Enter for Gold plan", text);
        Assert.Contains("Enter for owner@example.com", text);
        Assert.Contains($"Reference: LIC-{reference[..5]}-{reference[5..]}", text);
        AssertOrder(text, "License (", "Expiry (", "Summary", "Sign this license? [Y/n]", "Passphrase (");
        Assert.Equal(1, SessionTestHost.Count(text, "Passphrase ("));
        Assert.Equal(1, SessionTestHost.Count(text, "License ("));
        Assert.DoesNotContain(key, text); // the pasted key string is not echoed or logged
        AssertZeroed(console);

        var entries = kit.Entries();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(reference, e.Reference));
        Assert.Equal(IssueKind.Reissue, entries[1].Kind);
        Assert.Single(kit.Licenses());
    }

    [Fact]
    public void KeyPastedToReissue_AReferenceTypedInAnyCaseWithOrWithoutHyphens_ChoosesTheSameLicense()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var reference = StoredReference(kit);
        var typed = $"lic {reference[..5].ToLowerInvariant()} {reference[5..].ToLowerInvariant()}";
        var console = Sign(ReissueQuestions(new ScriptedConsole(), typed));

        kit.Run(console);

        Assert.Equal(reference, Latest(kit).Reference);
        Assert.Equal(2, kit.Entries().Count);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void KeyFromBeforeAPrefixChange_ChoosesTheLicense_AndTheReissueTakesTheCurrentPrefix(bool shared)
    {
        var kit = Kit(shared);
        var key = kit.Issue();
        var reference = StoredReference(kit);
        Assert.StartsWith($"LIC-{reference[..5]}-{reference[5..]}-", key);
        // Settings (5) > Prefix (2): ACME; Back; Licenses (2) > Reissue (2), pasting the old key.
        var console = new ScriptedConsole().Line("5").Line("2").Line("ACME").Line("0");
        Sign(ReissueQuestions(console, key));

        kit.Run(console);

        var entries = kit.Entries();
        Assert.StartsWith("LIC-", entries[0].KeyIdentifier);
        Assert.StartsWith($"ACME-{reference[..5]}-{reference[5..]}-", entries[1].KeyIdentifier);
        Assert.Contains($"Reference: ACME-{reference[..5]}-{reference[5..]}", console.Out);
        var newKey = KeyStringOf(console);
        Assert.StartsWith(entries[1].KeyIdentifier + ".", newKey);
        Assert.Equal(reference, kit.Evaluate(newKey).License!.Reference[^11..].Replace("-", ""));
    }

    [Fact]
    public void KeyFromBeforeAPrefixChange_ATypedReferenceWithTheOldPrefix_IsNotAReference_ButTheCurrentOneAndNoneAreAccepted()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var reference = StoredReference(kit);
        var groups = $"{reference[..5]}-{reference[5..]}";
        var console = new ScriptedConsole().Line("5").Line("2").Line("ACME").Line("0");
        console.Line("2").Line("4").Line($"LIC-{groups}").Line($"ACME-{groups}");
        Sign(console); // Resend

        kit.Run(console);

        Assert.Contains("That is not a license reference or a key string.", console.Out);
        Assert.Contains("optionally preceded by the prefix ACME", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "License ("));
        Assert.Equal(IssueKind.Resend, Latest(kit).Kind);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ExpiredKeyPastedToRenew_SignedByAKeySinceReplaced_ChoosesTheLicense_AndRenewsWithItsSuggestedExpiry(bool shared)
    {
        var kit = Kit(shared);
        // Issued 2026-09-08T09:00Z for two months: expired 2026-11-08, "last month" on 2026-12-15.
        var key = kit.Issue(expiry: "2026-11-08T09:00:00Z", clock: ClockAt(Utc(2026, 9, 8, 9)));
        // Signing keys > Switch does not exist yet (section 9): the replaced state is set up through the DbContext.
        var replacedId = Latest(kit).SigningKeyId;
        var newId = kit.ReplaceActiveKey(ReplacedReason.Routine);
        Assert.NotEqual(replacedId, newId);
        var clock = ClockAt(Utc(2026, 12, 15, 9));
        Assert.Equal(LicenseState.Expired, kit.Evaluate(key, clock).State);
        var console = Sign(RenewQuestions(new ScriptedConsole(), key));

        kit.Run(console, clock);

        Assert.Contains("Enter for 2027-01-08T09:00:00Z", console.Out); // current expiry plus two calendar months
        var renewal = Latest(kit);
        Assert.Equal(IssueKind.Renewal, renewal.Kind);
        Assert.Equal(Utc(2027, 1, 8, 9), renewal.ExpiresUtc);
        Assert.Equal(newId, renewal.SigningKeyId); // signed with the current active key
        var result = kit.Evaluate(KeyStringOf(console), clock);
        Assert.True(result.IsLicensed, result.State.ToString());
        Assert.Equal(StoredReference(kit), renewal.Reference);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void KeyPastedToReissue_SignedByAKeyReplacedForALeak_ChoosesTheLicense_AfterReissuesSent(bool shared)
    {
        var kit = Kit(shared);
        var key = kit.Issue();
        var leaked = Latest(kit).SigningKeyId;
        var reference = StoredReference(kit);
        var product = shared ? null : kit.ProductId;
        // Switch (reason leak, no standby: yes to the warning, then the passphrase), then Reissues sent (the key, yes), then Quit.
        var session = new ScriptedConsole().Line("3").Line("3").Line(product ?? "1").Line("leak").Line("y").Hidden(LicenseKit.Words)
            .Line("6").Line(leaked).Line("y");
        kit.Run(Quit(session));
        using (var db = kit.Db())
        {
            var replaced = db.SigningKeys.Single(k => k.SigningKeyId == leaked);
            Assert.Equal(ReplacedReason.Leak, replaced.ReplacedReason);
            Assert.NotNull(replaced.ReissuesSentUtc);
        }

        var console = Sign(ReissueQuestions(new ScriptedConsole(), key));
        kit.Run(console);

        Assert.DoesNotContain(LicenseQuestion.NotIssuedHere, console.Out);
        Assert.Contains($"Reference: LIC-{reference[..5]}-{reference[5..]}", console.Out);
        Assert.Equal(IssueKind.Reissue, Latest(kit).Kind);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void KeyFromAnotherVendor_IsNotIssuedHere_AndAskedAgain_NoPassphraseAsked(bool shared)
    {
        var kit = Kit(shared);
        kit.Issue();
        var reference = StoredReference(kit);
        using var other = new LicenseKit(shared);
        var foreign = other.Issue(); // the same product ID, another installation's keys
        var asked = new ScriptedConsole().Line("2").Line("2").Line(foreign).Line(reference).Line("/cancel");

        kit.Run(Quit(asked));

        Assert.Contains(LicenseQuestion.NotIssuedHere, asked.Out);
        Assert.Equal(2, SessionTestHost.Count(asked.Out, "License ("));
        AssertNoPassphraseAsked(asked);
        Assert.Single(kit.Entries());
    }

    [Fact]
    public void KeyFromAnotherVendor_ForAProductTheInstallationDoesNotHold_IsNotIssuedHere()
    {
        var kit = Kit(shared: false);
        using var other = new LicenseKit(shared: false, productId: "other.product");
        var foreign = other.Issue();
        var console = Quit(new ScriptedConsole().Line("2").Line("3").Line(foreign).Line("/cancel"));

        kit.Run(console);

        Assert.Contains(LicenseQuestion.NotIssuedHere, console.Out);
        AssertNoPassphraseAsked(console);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void KeyWithNoRecord_IsSaidToHaveNoRecord_AndAskedAgain_NoPassphraseAsked(bool shared)
    {
        var kit = Kit(shared);
        var key = kit.Issue();
        var reference = StoredReference(kit);
        using (var db = kit.Db())
        {
            db.IssueEntries.RemoveRange(db.IssueEntries);
            db.SaveChanges();
            db.Licenses.RemoveRange(db.Licenses);
            db.SaveChanges();
        }

        var console = Quit(new ScriptedConsole().Line("2").Line("4").Line(key).Line(Reference).Line("/cancel"));

        kit.Run(console);

        Assert.Equal(1, SessionTestHost.Count(console.Out, $"There is no record of the license LIC-{reference[..5]}-{reference[5..]}.")); // the key's reference
        Assert.Equal(1, SessionTestHost.Count(console.Out, $"There is no record of the license {Reference}.")); // a typed one
        Assert.Equal(3, SessionTestHost.Count(console.Out, "License ("));
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void TextThatIsNeitherAReferenceNorAKey_IsRefusedAndAskedAgain_NoPassphraseAsked()
    {
        var kit = Kit(shared: false);
        var key = kit.Issue();
        var tampered = key[..^3] + (key.EndsWith("AAA", StringComparison.Ordinal) ? "BBB" : "AAA");
        var console = Quit(new ScriptedConsole().Line("2").Line("4").Line("hello there").Line("8F3AK").Line("not.a.key").Line(tampered).Line("").Line("/cancel"));

        kit.Run(console);

        Assert.Equal(5, SessionTestHost.Count(console.Out, "That is not a license reference or a key string."));
        Assert.Equal(6, SessionTestHost.Count(console.Out, "License ("));
        AssertNoPassphraseAsked(console);
        Assert.DoesNotContain(key, console.Out);
        Assert.Single(kit.Entries());
    }

    [Fact]
    public void AReferenceWithNoRecord_IsSaidToHaveNoRecord()
    {
        var kit = Kit(shared: false);
        var console = Quit(new ScriptedConsole().Line("2").Line("3").Line("LIC-8F3AK-M7RXB").Line("/cancel"));

        kit.Run(console);

        Assert.Contains("There is no record of the license LIC-8F3AK-M7RXB.", console.Out);
    }

    // --- Reissuing from the record

    [Theory]
    [MemberData(nameof(Modes))]
    public void AddOnBought_ANewKeyKeepsTheReferenceAndExpiry_AndAddsTheFeature(bool shared)
    {
        var kit = Kit(shared);
        kit.Issue();
        var reference = StoredReference(kit);
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), shipping: "on", orderRef: "ORD-1002"));

        kit.Run(console);

        var entries = kit.Entries();
        Assert.Equal(2, entries.Count);
        var added = entries[1];
        Assert.Equal(IssueKind.Reissue, added.Kind);
        Assert.Equal(entries[0].ExpiresUtc, added.ExpiresUtc);
        Assert.Equal("""{"ecommerce":true,"max-orders":500,"shipping":true}""", added.FeaturesJson);
        Assert.Equal("ORD-1002", added.OrderRef);
        Assert.Equal("ord-1002", added.OrderRefKey);
        Assert.Equal(entries[0].SigningKeyId, added.SigningKeyId);
        var result = kit.Evaluate(KeyStringOf(console));
        Assert.True(result.IsLicensed);
        Assert.EndsWith(reference[..5] + "-" + reference[5..], result.License!.Reference);
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.True(result.HasSwitch("shipping"));
        Assert.Equal(500m, result.GetNumber("max-orders"));
        Assert.Equal(entries[0].ExpiresUtc, result.License.Expires!.Value.UtcDateTime);
        Assert.Single(console.HiddenGiven);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void FeaturesKeptAtReissue_EmptyAnswersKeepEveryFeature(bool shared)
    {
        var kit = Kit(shared);
        kit.Issue(note: "Gold plan");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit)));

        kit.Run(console);

        var entries = kit.Entries();
        Assert.Equal(entries[0].FeaturesJson, entries[1].FeaturesJson);
        Assert.Equal("""{"ecommerce":true,"max-orders":500,"note":"Gold plan"}""", entries[1].FeaturesJson);
        var result = kit.Evaluate(KeyStringOf(console));
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.Equal(500m, result.GetNumber("max-orders"));
        Assert.Equal("Gold plan", result.GetText("note"));
    }

    private static string Dashed(LicenseKit kit)
    {
        var reference = StoredReference(kit);
        return $"{reference[..5]}-{reference[5..]}";
    }

    private static string Typed(LicenseKit kit) => "LIC-" + Dashed(kit);

    [Fact]
    public void NumberFeatureRemovedAtReissue_AndATextFeatureToo_TheNewKeyDoesNotCarryThem()
    {
        var kit = Kit(shared: false);
        kit.Issue(note: "Gold plan");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), maxOrders: "/remove", note: "/REMOVE"));

        kit.Run(console);

        Assert.Equal("""{"ecommerce":true}""", Latest(kit).FeaturesJson);
        var result = kit.Evaluate(KeyStringOf(console));
        Assert.Null(result.GetNumber("max-orders"));
        Assert.Null(result.GetText("note"));
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.Contains("/remove to remove it", console.Out);
    }

    [Fact]
    public void NumberFeatureChangedAtReissue_AndAZeroKept()
    {
        var kit = Kit(shared: false);
        kit.Issue(maxOrders: "0");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), maxOrders: "1200.5"));

        kit.Run(console);

        Assert.Contains("Enter for 0", console.Out);
        Assert.Equal("""{"ecommerce":true,"max-orders":1200.5}""", Latest(kit).FeaturesJson);
    }

    [Fact]
    public void DisplayNameRemovedAtReissue_TheNewKeyCarriesNone_AndKeepsTheRest()
    {
        var kit = Kit(shared: false);
        kit.Issue(displayName: "Acme Shop EU", vendorTag: "reseller#7");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), displayName: "/remove"));

        kit.Run(console);

        var latest = Latest(kit);
        Assert.Null(latest.DisplayName);
        Assert.Equal("reseller#7", latest.VendorTag);
        Assert.Equal(kit.Entries()[0].FeaturesJson, latest.FeaturesJson);
        Assert.Equal(kit.Entries()[0].ExpiresUtc, latest.ExpiresUtc);
        var result = kit.Evaluate(KeyStringOf(console));
        Assert.Null(result.License!.DisplayName);
        Assert.Equal("reseller#7", result.License.VendorTag);
        Assert.Contains("Display name: (none)", console.Out);
    }

    [Fact]
    public void VendorTagRemovedAtReissue_TheNewKeyCarriesNone_AndKeepsTheDisplayName()
    {
        var kit = Kit(shared: false);
        kit.Issue(displayName: "Acme Shop EU", vendorTag: "reseller#7");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), vendorTag: "/remove"));

        kit.Run(console);

        var latest = Latest(kit);
        Assert.Equal("Acme Shop EU", latest.DisplayName);
        Assert.Null(latest.VendorTag);
        Assert.Null(kit.Evaluate(KeyStringOf(console)).License!.VendorTag);
    }

    [Fact]
    public void DisplayNameAndVendorTagChangedAtReissue_ANewValueReplacesTheCurrentOne()
    {
        var kit = Kit(shared: false);
        kit.Issue(displayName: "Acme Shop EU");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), displayName: "Acme Shop US", vendorTag: "new-tag"));

        kit.Run(console);

        Assert.Equal("Acme Shop US", Latest(kit).DisplayName);
        Assert.Equal("new-tag", Latest(kit).VendorTag);
    }

    [Fact]
    public void NothingToRemove_RemoveIsNotOffered_AtADisplayNameThatHasNone()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        // The refused "/remove" is asked again, so the display name gets one more answer.
        var console = ReissueQuestions(new ScriptedConsole(), Typed(kit), displayName: "/remove").Line("");
        Sign(console);

        kit.Run(console);

        Assert.Contains(Questions.RemoveNotAllowed, console.Out);
        Assert.Contains("Display name (", console.Out);
        Assert.DoesNotContain("/remove to remove it", console.Out.Split("Feature ecommerce")[0]);
        Assert.Null(Latest(kit).DisplayName);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void SwitchTurnedOffAtReissue_IsRemoved_TheSameAsRemovingTheFeature(bool shared)
    {
        var kit = Kit(shared);
        kit.Issue(shipping: "on");
        Assert.Equal("""{"ecommerce":true,"max-orders":500,"shipping":true}""", Latest(kit).FeaturesJson);
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), shipping: "OFF"));

        kit.Run(console);

        Assert.Equal("""{"ecommerce":true,"max-orders":500}""", Latest(kit).FeaturesJson);
        var result = kit.Evaluate(KeyStringOf(console));
        Assert.True(result.HasSwitch("ecommerce"));
        Assert.False(result.HasSwitch("shipping"));
        Assert.Contains("Features: ecommerce (on), max-orders = 500", console.Out);
    }

    [Fact]
    public void SwitchAtReissue_RemoveIsNotAccepted_OffIsTheWayToTakeItOut()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var console = new ScriptedConsole().Line("2").Line("2").Line(Typed(kit)).Line("").Line("").Line("")
            .Line("/remove").Line("off").Line("").Line("").Line("").Line("").Line("");
        Sign(console);

        kit.Run(console);

        Assert.Contains(Questions.RemoveNotAllowed, console.Out);
        Assert.Equal("""{"max-orders":500}""", Latest(kit).FeaturesJson);
    }

    [Fact]
    public void ReissueKeepsAnAddedFeatureOffUntilAsked_AndTheOrderReferenceOfTheNewEntryIsNoneWhenEmpty()
    {
        var kit = Kit(shared: false);
        kit.Issue(orderRef: "ORD-1001");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit)));

        kit.Run(console);

        var latest = Latest(kit);
        Assert.Null(latest.OrderRef);
        Assert.Null(latest.OrderRefKey);
        Assert.Equal("ORD-1001", kit.Entries()[0].OrderRef);
        Assert.Contains("Order reference: (none)", console.Out);
    }

    [Fact]
    public void ReissueChangesTheExpiry_ByATypedAnswer()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), expiry: "perpetual"));

        kit.Run(console);

        Assert.Null(Latest(kit).ExpiresUtc);
        Assert.True(kit.Evaluate(KeyStringOf(console)).License!.IsPerpetual);
    }

    [Fact]
    public void CustomerEmailAtReissue_ShowsTheCurrentOne_AndANewOneChangesTheLicenseAndItsMatchKey()
    {
        var kit = Kit(shared: false);
        kit.Issue(email: "owner@example.com");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), email: "New.Owner@Example.com"));

        kit.Run(console);

        Assert.Contains("Enter for owner@example.com", console.Out);
        var license = Assert.Single(kit.Licenses());
        Assert.Equal("New.Owner@Example.com", license.CustomerEmail);
        Assert.Equal("new.owner@example.com", license.CustomerEmailKey);
        Assert.Contains("Customer email: New.Owner@Example.com", console.Out);
    }

    [Fact]
    public void CustomerEmailAtReissue_EmptyKeepsIt_AndOneOutsideTheRuleIsAskedAgain()
    {
        var kit = Kit(shared: false);
        kit.Issue(email: "owner@example.com");
        var asked = new ScriptedConsole();
        ReissueQuestions(asked, Typed(kit), email: "owner").Line("");
        Sign(asked);

        kit.Run(asked);

        Assert.Contains(TypedValues.EmailRule, asked.Out);
        var license = Assert.Single(kit.Licenses());
        Assert.Equal("owner@example.com", license.CustomerEmail);
        Assert.Equal("owner@example.com", license.CustomerEmailKey);
    }

    [Fact]
    public void CustomerEmailAtReissue_ALicenseWithNoEmail_CanGainOne()
    {
        var kit = Kit(shared: false);
        kit.Issue(email: "");
        var console = Sign(ReissueQuestions(new ScriptedConsole(), Typed(kit), email: "late@example.com"));

        kit.Run(console);

        Assert.Equal("late@example.com", Assert.Single(kit.Licenses()).CustomerEmail);
        Assert.Contains("Customer email (", console.Out);
    }

    [Fact]
    public void Reissue_AFeatureOutsideItsTypeIsAskedAgain_AndTheFeatureNameIsShownWithItsCurrentValue()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var console = new ScriptedConsole().Line("2").Line("2").Line(Typed(kit)).Line("").Line("").Line("").Line("")
            .Line("lots").Line("").Line("").Line("").Line("").Line("");
        Sign(console);

        kit.Run(console);

        Assert.Contains("Feature max-orders (number) (", console.Out);
        Assert.Contains(TypedValues.ReadNumber("lots", out _)!, console.Out);
        Assert.Equal(2, kit.Entries().Count);
    }

    [Fact]
    public void Reissue_NotSignedAtTheSummary_StoresNothing_AsksNoPassphrase()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var console = Quit(ReissueQuestions(new ScriptedConsole(), Typed(kit), shipping: "on").Line("n"));

        kit.Run(console);

        Assert.Contains(LicenseActions.NotSigned, console.Out);
        Assert.Single(kit.Entries());
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void Reissue_WrongPassphraseThreeTimes_CancelsIt_NothingStored_NoKeyShown()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var console = ReissueQuestions(new ScriptedConsole(), Typed(kit)).Line("").Hidden("wrong passphrase one").Hidden("wrong passphrase two").Hidden("wrong passphrase three");
        Quit(console);

        kit.Run(console);

        Assert.Single(kit.Entries());
        Assert.Equal(3, SessionTestHost.Count(console.Out, KeyProtection.WrongPassphrase));
        Assert.DoesNotContain("The key string is shown once", console.Out);
        AssertZeroed(console);
    }

    // --- Suggested expiry, as the proposed answer through the session

    /// <summary>Renew (or Reissue) a seeded license and read the proposal off the expiry question, then cancel at it.</summary>
    private string ProposedAt(bool renew, DateTime clock, params (IssueKind Kind, DateTime Issued, DateTime? Expires)[] entries)
    {
        var kit = Kit(shared: false);
        kit.Seed(entries);
        var console = Quit(new ScriptedConsole().Line("2").Line(renew ? "3" : "2").Line(Typed(kit)).Line("/cancel"));

        kit.Run(console, ClockAt(clock));

        var line = SessionTestHost.Lines(console.Out).Single(l => l.Contains("Expiry (", StringComparison.Ordinal));
        AssertNoPassphraseAsked(console);
        Assert.Equal(entries.Length, kit.Entries().Count);
        return line;
    }

    [Fact]
    public void SuggestedExpiry_RenewalOfAOneYearLicense_IsProposedAsTheAnswer()
    {
        var line = ProposedAt(true, Utc(2026, 11, 20),
            (IssueKind.Issue, Utc(2025, 11, 30, 23, 59, 59), Utc(2026, 11, 30, 23, 59, 59)));

        Assert.Contains("Enter for 2027-11-30T23:59:59Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_SecondRenewalAfterAnEarlyRenewal_IsMeasuredFromTheExpiry()
    {
        var line = ProposedAt(true, Utc(2026, 11, 21),
            (IssueKind.Issue, Utc(2025, 11, 30, 23, 59, 59), Utc(2026, 11, 30, 23, 59, 59)),
            (IssueKind.Renewal, Utc(2026, 11, 20), Utc(2027, 11, 30, 23, 59, 59)));

        Assert.Contains("Enter for 2028-11-30T23:59:59Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_AddOnBeforeRenewal_DoesNotChangeTheTerm()
    {
        var line = ProposedAt(true, Utc(2026, 12, 1),
            (IssueKind.Issue, Utc(2026, 1, 15, 10), Utc(2027, 1, 15, 10)),
            (IssueKind.Reissue, Utc(2026, 9, 1), Utc(2027, 1, 15, 10)));

        Assert.Contains("Enter for 2028-01-15T10:00:00Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_RenewalAfterALapse_ExtendsByTheTermOfTheLastRenewal()
    {
        var line = ProposedAt(true, Utc(2027, 3, 1),
            (IssueKind.Issue, Utc(2025, 11, 30, 23, 59, 59), Utc(2026, 11, 30, 23, 59, 59)),
            (IssueKind.Renewal, Utc(2027, 2, 1, 9), Utc(2028, 2, 1, 9)));

        Assert.Contains("Enter for 2029-02-01T09:00:00Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_FeatureChange_ProposesTheCurrentExpiry_AtReissue()
    {
        var line = ProposedAt(false, Utc(2026, 11, 20),
            (IssueKind.Issue, Utc(2025, 11, 30, 23, 59, 59), Utc(2026, 11, 30, 23, 59, 59)));

        Assert.Contains("Enter for 2026-11-30T23:59:59Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_PerpetualLicense_IsProposedAsPerpetual_AtRenewAndAtReissue()
    {
        var entries = new[] { (IssueKind.Issue, Utc(2026, 1, 15, 10), (DateTime?)null) };

        Assert.Contains("Enter for perpetual;", ProposedAt(true, Utc(2026, 11, 20), entries));
        Assert.Contains("Enter for perpetual;", ProposedAt(false, Utc(2026, 11, 20), entries));
    }

    [Fact]
    public void SuggestedExpiry_NeverReissued_CountsCalendarMonths_NotDays()
    {
        var line = ProposedAt(true, Utc(2026, 7, 1),
            (IssueKind.Issue, Utc(2026, 1, 15, 10), Utc(2026, 7, 15, 10)));

        Assert.Contains("Enter for 2027-01-15T10:00:00Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_TermNotAWholeNumberOfMonths_IsCountedInExactTime()
    {
        var line = ProposedAt(true, Utc(2026, 4, 20),
            (IssueKind.Issue, Utc(2026, 3, 10, 9), Utc(2026, 4, 9, 9)));

        Assert.Contains("Enter for 2026-05-09T09:00:00Z;", line);
    }

    [Fact]
    public void SuggestedExpiry_AcceptedByAnEmptyAnswer_SetsTheRenewalsExpiry_AndTheKindRenewal()
    {
        var kit = Kit(shared: false);
        kit.Seed((IssueKind.Issue, Utc(2025, 11, 30, 23, 59, 59), Utc(2026, 11, 30, 23, 59, 59)));
        var console = Sign(RenewQuestions(new ScriptedConsole(), Typed(kit)));

        kit.Run(console, ClockAt(Utc(2026, 11, 20)));

        var renewal = Latest(kit);
        Assert.Equal(IssueKind.Renewal, renewal.Kind);
        Assert.Equal(Utc(2027, 11, 30, 23, 59, 59), renewal.ExpiresUtc);
        Assert.Contains("Expiry: 2027-11-30T23:59:59Z", console.Out);
    }

    [Fact]
    public void SuggestedExpiry_ReplacedByATypedAnswer_IsUsedInstead()
    {
        var kit = Kit(shared: false);
        kit.Seed((IssueKind.Issue, Utc(2025, 11, 30, 23, 59, 59), Utc(2026, 11, 30, 23, 59, 59)));
        var console = Sign(RenewQuestions(new ScriptedConsole(), Typed(kit), expiry: "2028-06-30"));

        kit.Run(console, ClockAt(Utc(2026, 11, 20)));

        Assert.Equal(Utc(2028, 6, 30, 23, 59, 59), Latest(kit).ExpiresUtc);
    }

    [Fact]
    public void SuggestedExpiry_InThePast_IsRefusedWhenAccepted_AndTheExpiryAskedAgain()
    {
        var kit = Kit(shared: false);
        kit.Seed((IssueKind.Issue, Utc(2026, 1, 15, 10), Utc(2026, 7, 15, 10))); // proposes 2027-01-15, past on 2027-02-01
        var console = new ScriptedConsole().Line("2").Line("3").Line(Typed(kit)).Line("").Line("2027-08-15");
        Sign(console.Line("").Line("")) ;

        kit.Run(console, ClockAt(Utc(2027, 2, 1)));

        Assert.Equal(2, SessionTestHost.Count(console.Out, "Enter for 2027-01-15T10:00:00Z;"));
        Assert.Contains("earlier", console.Out, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Utc(2027, 8, 15, 23, 59, 59), Latest(kit).ExpiresUtc);
    }

    // --- Renew

    [Fact]
    public void RenewalKeepsTheEarlierOrderReference()
    {
        var kit = Kit(shared: false);
        kit.Issue(orderRef: "ORD-1001", expiry: "2027-03-01T23:59:59Z");
        var console = Sign(RenewQuestions(new ScriptedConsole(), Typed(kit), orderRef: "ORD-1874"));

        kit.Run(console);

        var entries = kit.Entries();
        Assert.Equal("ORD-1001", entries[0].OrderRef);
        Assert.Equal("ORD-1001".ToLowerInvariant(), entries[0].OrderRefKey);
        Assert.Equal(IssueKind.Renewal, entries[1].Kind);
        Assert.Equal("ORD-1874", entries[1].OrderRef);
        Assert.Equal("ord-1874", entries[1].OrderRefKey);
        Assert.Contains("Order reference: ORD-1874", console.Out);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Renew_KeepsDisplayNameVendorTagAndFeatures_AndSignsWithTheCurrentKey(bool shared)
    {
        var kit = Kit(shared);
        kit.Issue(displayName: "Acme Shop EU", vendorTag: "reseller#7", note: "Gold plan");
        var console = Sign(RenewQuestions(new ScriptedConsole(), Typed(kit)));

        kit.Run(console);

        var entries = kit.Entries();
        Assert.Equal(entries[0].DisplayName, entries[1].DisplayName);
        Assert.Equal(entries[0].VendorTag, entries[1].VendorTag);
        Assert.Equal(entries[0].FeaturesJson, entries[1].FeaturesJson);
        Assert.DoesNotContain("Display name (", console.Out);
        Assert.DoesNotContain("Feature ", console.Out.Replace("Features:", ""));
        var result = kit.Evaluate(KeyStringOf(console));
        Assert.Equal("Acme Shop EU", result.License!.DisplayName);
        Assert.Equal("Gold plan", result.GetText("note"));
        Assert.Contains("Customer email (", console.Out);
    }

    // --- Resend

    [Theory]
    [MemberData(nameof(Modes))]
    public void SiteOwnerLostTheKey_ResendGivesANewKeyWithTheSameContents_AndNoOrderReference(bool shared)
    {
        var kit = Kit(shared);
        var first = kit.Issue(displayName: "Acme Shop EU", vendorTag: "reseller#7", note: "Gold plan", shipping: "on");
        var console = Sign(ResendQuestions(new ScriptedConsole(), Typed(kit)));

        kit.Run(console);

        var text = console.Out;
        AssertOrder(text, "License (", "Summary", "Reference: LIC-", "Sign this license? [Y/n]", "Passphrase (", "The key string is shown once", "Save the key to a file?");
        Assert.DoesNotContain("Expiry (", text);
        Assert.DoesNotContain("Order reference (", text);
        Assert.DoesNotContain("Display name (", text);
        Assert.Contains("Order reference: (none)", text);
        var entries = kit.Entries();
        Assert.Equal(2, entries.Count);
        var resend = entries[1];
        Assert.Equal(IssueKind.Resend, resend.Kind);
        Assert.Null(resend.OrderRef);
        Assert.Null(resend.OrderRefKey);
        Assert.Equal("ORD-1001", entries[0].OrderRef);
        Assert.Equal(entries[0].ExpiresUtc, resend.ExpiresUtc);
        Assert.Equal(entries[0].DisplayName, resend.DisplayName);
        Assert.Equal(entries[0].VendorTag, resend.VendorTag);
        Assert.Equal(entries[0].FeaturesJson, resend.FeaturesJson);
        Assert.NotEqual(entries[0].KeyIdentifier, resend.KeyIdentifier);

        var newKey = KeyStringOf(console);
        Assert.NotEqual(first, newKey);
        var before = kit.Evaluate(first).License!;
        var after = kit.Evaluate(newKey).License!;
        Assert.Equal(before.Reference, after.Reference);
        Assert.Equal(before.Expires, after.Expires);
        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal(before.VendorTag, after.VendorTag);
        Assert.Equal(before.Features, after.Features);
        Assert.Single(kit.Licenses());
        Assert.Equal(1, SessionTestHost.Count(text, newKey));
    }

    [Fact]
    public void Resend_OfAnExpiredLicense_SaysItCannotBeResent_AndStoresNothing()
    {
        var kit = Kit(shared: false);
        kit.Issue(expiry: "2026-11-08T09:00:00Z", clock: ClockAt(Utc(2026, 9, 8, 9)));
        var console = Quit(ResendQuestions(new ScriptedConsole(), Typed(kit)));

        kit.Run(console, ClockAt(Utc(2026, 12, 15)));

        Assert.Contains("This license cannot be resent.", console.Out);
        Assert.DoesNotContain("Summary", console.Out);
        var afterRefusal = console.Out[console.Out.IndexOf("This license cannot be resent.", StringComparison.Ordinal)..];
        Assert.Contains("1 Issue", afterRefusal);
        Assert.Single(kit.Entries());
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void Resend_NotSignedAtTheSummary_StoresNothing()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        var console = Quit(ResendQuestions(new ScriptedConsole(), Typed(kit)).Line("n"));

        kit.Run(console);

        Assert.Contains(LicenseActions.NotSigned, console.Out);
        Assert.Single(kit.Entries());
        AssertNoPassphraseAsked(console);
    }

    // --- Key saved, key not stored

    [Fact]
    public void Resend_OffersToSaveTheKey_AndNoFileOfTheFolderHoldsAKeyString()
    {
        var kit = Kit(shared: false);
        var first = kit.Issue();
        var console = ResendQuestions(new ScriptedConsole(), Typed(kit)).Line("").Hidden(Words).Line("n");
        Quit(console);

        kit.Run(console);

        var second = KeyStringOf(console);
        Assert.Contains("Save the key to a file? [y/N]", console.Out);
        var identifierOfFirst = first[..first.IndexOf('.')];
        foreach (var file in Directory.EnumerateFiles(kit.Host.HomeInstallation, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllText(file, System.Text.Encoding.Latin1);
            Assert.DoesNotContain(second, bytes);
            Assert.DoesNotContain(first, bytes);
            Assert.DoesNotContain(identifierOfFirst + ".", bytes);
        }
    }

    // --- Damaged installations

    [Fact]
    public void Reissue_NoActiveKey_FailsAsADamagedInstallation_AndTheSessionContinues()
    {
        var kit = Kit(shared: false);
        kit.Issue();
        using (var db = kit.Db())
        {
            foreach (var key in db.SigningKeys.Where(k => k.Role == KeyRole.Active))
                key.Role = KeyRole.Replaced;
            db.SaveChanges();
        }

        var console = Quit(new ScriptedConsole().Line("2").Line("2").Line(Typed(kit)).Line("0"));

        kit.Run(console);

        Assert.Contains("Licenses - Reissue failed:", console.Out);
        Assert.Contains("no active signing key", console.Out);
        Assert.DoesNotContain("Expiry (", console.Out);
        Assert.Single(kit.Entries());
    }

    // --- Features as stored

    [Fact]
    public void FeaturesJson_ReadsWhatItWrote_InTheOrderIssued()
    {
        var features = FeatureList.Empty.With("max-orders", 1200.5m).With("ecommerce", true).With("note", "Gold \"plan\" ü").With("zero", 0m).With("big", 123456789012345m);

        var json = LicenseActions.FeaturesJson.Write(features);
        var read = LicenseActions.FeaturesJson.Read(json);

        Assert.Equal(features, read);
        Assert.Equal(json, LicenseActions.FeaturesJson.Write(read));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Number, document.RootElement.GetProperty("max-orders").ValueKind);
        Assert.Equal(JsonValueKind.True, document.RootElement.GetProperty("ecommerce").ValueKind);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("note").ValueKind);
        Assert.Equal(FeatureList.Empty, LicenseActions.FeaturesJson.Read("{}"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"a":false}""")]
    [InlineData("""{"a":null}""")]
    [InlineData("""{"a":{"b":1}}""")]
    [InlineData("not json")]
    [InlineData("""{"a":1e99999}""")]
    public void FeaturesJson_ShapeItDoesNotWrite_IsADamagedInstallation(string json)
    {
        var ex = Assert.Throws<InstallationRefusedException>(() => LicenseActions.FeaturesJson.Read(json));

        Assert.Contains("looks damaged", ex.Message);
    }
}
