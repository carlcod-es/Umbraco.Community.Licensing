using System.Text;
using Umbraco.Community.Licensing.Tool.Data;
using static Umbraco.Community.Licensing.Tool.Tests.LicenseKit;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>Licenses &gt; Find and list, Show a license and Customer details (vendor-tool-licenses), as whole sessions. The Licenses menu is 2: Find 5, Show 6, Customer details 7.</summary>
public class LicenseLookupTests : IDisposable
{
    private readonly List<LicenseKit> _kits = [];

    public void Dispose() => _kits.ForEach(k => k.Dispose());

    private LicenseKit Kit(bool shared = false, string productId = "acme.commerce")
    {
        var kit = new LicenseKit(shared, productId);
        _kits.Add(kit);
        return kit;
    }

    private static string Display(string reference) => $"LIC-{reference[..5]}-{reference[5..]}";

    /// <summary>Find and list with the four answers in order (product, license, order reference, customer email); empty skips a filter.</summary>
    private static ScriptedConsole Find(string product = "", string license = "", string orderRef = "", string email = "") =>
        Quit(new ScriptedConsole().Line("2").Line("5").Line(product).Line(license).Line(orderRef).Line(email));

    private static ScriptedConsole Show(string license) => Quit(new ScriptedConsole().Line("2").Line("6").Line(license));

    /// <summary>Customer details: the license, then the entry of the submenu (1 to 4) and its answers.</summary>
    private static ScriptedConsole Customer(string license, string entry, params string[] answers)
    {
        var console = new ScriptedConsole().Line("2").Line("7").Line(license).Line(entry);
        foreach (var answer in answers)
            console.Line(answer);
        return Quit(console);
    }

    /// <summary>The rows listed; a row can share its line with the last prompt, since a scripted answer echoes no line ending.</summary>
    private static string[] Rows(ScriptedConsole console) =>
        [.. System.Text.RegularExpressions.Regex.Matches(console.Out, @"(?:LIC|ACME)-[A-Z0-9]{5}-[A-Z0-9]{5}  \S+  expires [^\r\n]*").Select(m => m.Value)];

    private static void Renew(LicenseKit kit, string license, string orderRef, DateTime now) =>
        kit.Run(Sign(RenewQuestions(new ScriptedConsole(), license, orderRef: orderRef)), ClockAt(now));

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

    /// <summary>Every file of the installation folder, read whole: the database, and any journal or other file beside it.</summary>
    private static List<(string Path, byte[] Bytes)> FolderBytes(LicenseKit kit)
    {
        var files = Directory.GetFiles(kit.Host.HomeInstallation, "*", SearchOption.AllDirectories).Select(f => (f, ReadShared(f))).ToList();
        Assert.Contains(files, f => Path.GetFileName(f.f) == "ucl.db");
        return files;
    }

    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool Holds(LicenseKit kit, string text) =>
        FolderBytes(kit).Any(f => f.Bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0);

    // --- Find and list

    [Fact]
    public void FindByCustomerEmail_ShowsEveryLicenseWithThatEmail()
    {
        var kit = Kit();
        kit.Issue(email: "owner@example.com", orderRef: "ORD-1");
        kit.Issue(email: "other@example.com", orderRef: "ORD-2");
        kit.Issue(email: "owner@example.com", orderRef: "ORD-3");
        var licenses = kit.Licenses();
        var console = Find(email: "owner@example.com");

        kit.Run(console);

        var rows = Rows(console);
        Assert.Equal(2, rows.Length);
        var wanted = licenses.Where(l => l.CustomerEmail == "owner@example.com").Select(l => Display(l.Reference)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(wanted, rows.Select(r => r.Split(' ')[0]).ToArray()); // reference order
        Assert.All(rows, r =>
        {
            Assert.Contains("acme.commerce", r);
            Assert.Contains("expires 2027-03-01T23:59:59Z", r);
            Assert.Contains("email owner@example.com", r);
        });
        Assert.Contains("2 licenses.", console.Out);
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void FindByAnEarlierOrderReference_ShowsTheLicenseWithItsLatestOrderReference()
    {
        var kit = Kit();
        var key = kit.Issue(orderRef: "ORD-1001");
        Renew(kit, key, "ORD-1874", new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc));
        kit.Issue(orderRef: "ORD-5000");
        var console = Find(orderRef: "ORD-1001");

        kit.Run(console);

        var row = Assert.Single(Rows(console));
        Assert.StartsWith(Display(kit.Entries()[0].Reference), row);
        Assert.EndsWith("order reference ORD-1874", row);
    }

    [Fact]
    public void Find_WithEveryFilterEmpty_ListsEveryLicense_AndWithProductAndLicense_NarrowsThem()
    {
        var kit = Kit();
        kit.Issue();
        kit.Issue(email: "");
        var licenses = kit.Licenses();

        var all = Find();
        kit.Run(all);
        Assert.Equal(2, Rows(all).Length);
        Assert.Contains("email (none)", all.Out);
        Assert.Equal(1, SessionTestHost.Count(all.Out, "Product ("));
        Assert.Contains("Enter to leave it out", all.Out);

        var byProduct = Find(product: "1");
        kit.Run(byProduct);
        Assert.Equal(2, Rows(byProduct).Length);

        // An ID that is not in the list is refused and asked again; the empty answer then skips the filter.
        var refused = Quit(new ScriptedConsole().Line("2").Line("5").Line("no-such-product").Line("acme.commerce").Line("").Line("").Line(""));
        kit.Run(refused);
        Assert.Contains("Answer a number from 1 to 1, or an ID.", refused.Out);
        Assert.Equal(2, Rows(refused).Length);
        Assert.Equal(2, licenses.Count);
    }

    [Fact]
    public void Find_ByLicense_ShowsOnlyThatLicense_AndARefusedLicenseIsAskedAgain()
    {
        var kit = Kit();
        kit.Issue();
        kit.Issue();
        var wanted = kit.Licenses()[0].Reference;
        var console = new ScriptedConsole().Line("2").Line("5").Line("").Line($"LIC-{wanted[..5]}-ZZZZZ").Line(wanted.ToLowerInvariant()).Line("").Line("");

        kit.Run(Quit(console));

        Assert.Contains("There is no record of the license", console.Out);
        var row = Assert.Single(Rows(console));
        Assert.StartsWith(Display(wanted), row);
    }

    [Fact]
    public void Find_WithNothingMatching_SaysSo()
    {
        var kit = Kit();
        kit.Issue();
        var console = Find(email: "nobody@example.com");

        kit.Run(console);

        Assert.Contains(LicenseLookupActions.NoMatch, console.Out);
        Assert.Empty(Rows(console));
    }

    [Fact]
    public void Find_TypedFilterOutsideItsRule_IsRefusedNamingTheRule_AndAskedAgain()
    {
        var kit = Kit();
        kit.Issue();
        // A lone surrogate cannot be turned into a match key; the rule refuses it first. Then an email without @.
        var console = new ScriptedConsole().Line("2").Line("5").Line("").Line("").Line("ORD-\uD800").Line("ORD-1001").Line("no-at-sign").Line("owner@example.com");

        kit.Run(Quit(console));

        Assert.Equal(2, SessionTestHost.Count(console.Out, "Order reference ("));
        Assert.Equal(2, SessionTestHost.Count(console.Out, "Customer email ("));
        Assert.Contains("An order reference", console.Out);
        Assert.Contains(TypedValues.EmailRule, console.Out);
        Assert.Single(Rows(console));
    }

    // --- Matching when finding

    [Fact]
    public void EmailFoundIgnoringCase()
    {
        var kit = Kit();
        kit.Issue(email: "Owner@Example.com");
        var console = Find(email: "owner@example.com");

        kit.Run(console);

        var row = Assert.Single(Rows(console));
        Assert.Contains("email Owner@Example.com", row); // shown as typed when issued
    }

    [Fact]
    public void AccentedLetterTypedInTheOtherForm_IsFound()
    {
        var kit = Kit();
        kit.Issue(email: "jürgen@müller.de");
        var console = Find(email: "jürgen@müller.de");

        kit.Run(console);

        Assert.Single(Rows(console));
    }

    [Fact]
    public void OrderReferenceFoundIgnoringCase()
    {
        var kit = Kit();
        kit.Issue(orderRef: "ORD-1042");
        var console = Find(orderRef: "ord-1042");

        kit.Run(console);

        Assert.Single(Rows(console));
    }

    [Fact]
    public void PartOfAnOrderReference_IsNotFound()
    {
        var kit = Kit();
        kit.Issue(orderRef: "ORD-1042");
        var console = Find(orderRef: "1042");

        kit.Run(console);

        Assert.Empty(Rows(console));
        Assert.Contains(LicenseLookupActions.NoMatch, console.Out);
    }

    [Fact]
    public void SpacesInAnOrderReference_MatchExactly()
    {
        var kit = Kit();
        kit.Issue(orderRef: "ORD 1042");
        var twoSpaces = Find(orderRef: "ORD  1042");
        var oneSpace = Find(orderRef: "ORD 1042");

        kit.Run(twoSpaces);
        kit.Run(oneSpace);

        Assert.Empty(Rows(twoSpaces));
        Assert.Single(Rows(oneSpace));
    }

    // --- Show a license

    [Fact]
    public void LicenseHistory_ShowsEveryIssueEntryNewestFirst_WithItsDetails()
    {
        var kit = Kit();
        var key = kit.Issue(orderRef: "ORD-1001", clock: ClockAt(Utc(2026, 11, 8, 9)));
        kit.Run(Sign(ReissueQuestions(new ScriptedConsole(), key, expiry: "2027-06-01T00:00:00Z", orderRef: "ORD-1002", maxOrders: "900")), ClockAt(Utc(2026, 11, 9, 9)));
        kit.Run(Sign(ReissueQuestions(new ScriptedConsole(), key, expiry: "2027-09-01T00:00:00Z", orderRef: "ORD-1003")), ClockAt(Utc(2026, 11, 10, 9)));
        var entries = kit.Entries();
        var console = Show(Display(entries[0].Reference));

        kit.Run(console);

        var text = console.Out;
        Assert.Equal(3, entries.Count);
        AssertOrder(text, Display(entries[0].Reference), "Product: acme.commerce", "Customer email: owner@example.com",
            "2026-11-10T09:00:00Z  reissue", "Expiry: 2027-09-01T00:00:00Z", "Order reference: ORD-1003",
            "2026-11-09T09:00:00Z  reissue", "Expiry: 2027-06-01T00:00:00Z", "Features: ecommerce (on), max-orders = 900", "Order reference: ORD-1002",
            "2026-11-08T09:00:00Z  issue", "Expiry: 2027-03-01T23:59:59Z", "Features: ecommerce (on), max-orders = 500", "Order reference: ORD-1001");
        foreach (var e in entries)
        {
            Assert.Contains($"Key identifier: {e.KeyIdentifier}", text);
            Assert.Contains($"Signing key: {e.SigningKeyId}", text);
        }

        Assert.DoesNotContain(key, text);
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void Show_ByAKeyString_ChoosesTheLicense_AndAReferenceWithNoRecordIsAskedAgain()
    {
        var kit = Kit();
        var key = kit.Issue();
        var reference = kit.Licenses()[0].Reference;
        // The refusal asks again; a second answer chooses the license.
        var asked = Quit(new ScriptedConsole().Line("2").Line("6").Line("LIC-ZZZZZ-ZZZZZ").Line(key));

        kit.Run(asked);

        Assert.Contains("There is no record of the license", asked.Out);
        Assert.Contains(Display(reference), asked.Out);
        Assert.Equal(2, SessionTestHost.Count(asked.Out, "License ("));
        Assert.DoesNotContain(key, asked.Out);
    }

    [Fact]
    public void EarlierPrefixTyped_IsRefusedNamingTheConfiguredPrefix_AndTheReferenceWithoutPrefixFindsTheLicense()
    {
        var kit = Kit();
        kit.Issue();
        var reference = kit.Licenses()[0].Reference;
        var groups = $"{reference[..5]}-{reference[5..]}";
        // Settings (5) > Prefix (2): ACME; Back; Licenses (2) > Show (6): the old form, then the form without a prefix.
        var console = new ScriptedConsole().Line("5").Line("2").Line("ACME").Line("0").Line("2").Line("6").Line($"LIC-{groups}").Line(groups);

        kit.Run(Quit(console));

        Assert.Contains("That is not a license reference or a key string.", console.Out);
        Assert.Contains("optionally preceded by the prefix ACME", console.Out);
        Assert.Equal(2, SessionTestHost.Count(console.Out, "License ("));
        Assert.Contains($"ACME-{groups}", console.Out);
        Assert.Contains("Product: acme.commerce", console.Out);
    }

    // --- Customer details

    [Fact]
    public void CustomerEmail_Changed_IsHeldWithItsMatchKey_AndFoundByTheNewValue()
    {
        var kit = Kit();
        kit.Issue(email: "owner@example.com");
        var reference = kit.Licenses()[0].Reference;
        var console = Customer(Display(reference), "1", "New.Owner@Example.com");

        kit.Run(console);

        Assert.Contains("Customer email: owner@example.com", console.Out);
        var stored = Assert.Single(kit.Licenses());
        Assert.Equal("New.Owner@Example.com", stored.CustomerEmail);
        Assert.Equal("new.owner@example.com", stored.CustomerEmailKey);
        var found = Find(email: "NEW.owner@example.com");
        kit.Run(found);
        Assert.Single(Rows(found));
        var old = Find(email: "owner@example.com");
        kit.Run(old);
        Assert.Empty(Rows(old));
        AssertNoPassphraseAsked(console);
    }

    [Fact]
    public void CustomerEmail_ChangedToAnInvalidValue_IsRefusedAndAskedAgain_AndEnterKeepsTheCurrentOne()
    {
        var kit = Kit();
        kit.Issue(email: "owner@example.com");
        var reference = kit.Licenses()[0].Reference;
        var console = Customer(Display(reference), "1", "not an email", "");

        kit.Run(console);

        Assert.Contains(TypedValues.EmailRule, console.Out);
        Assert.Contains(SettingsActions.Unchanged, console.Out);
        Assert.Equal("owner@example.com", Assert.Single(kit.Licenses()).CustomerEmail);
    }

    [Fact]
    public void ErasureRequest_ClearedEmailAndItsMatchKey_AreInNoByteOfTheInstallation_AndTheLicenseRemains()
    {
        var kit = Kit();
        var key = kit.Issue(email: "Owner@Example.com", orderRef: "ORD-1001");
        var reference = kit.Licenses()[0].Reference;
        Assert.True(Holds(kit, "Owner@Example.com"));
        Assert.True(Holds(kit, "owner@example.com")); // the match key, so the scan is looking at the right thing
        var console = Customer(key, "2", "y");

        kit.Run(console);

        Assert.False(Holds(kit, "Owner@Example.com"));
        Assert.False(Holds(kit, "owner@example.com"));
        var stored = Assert.Single(kit.Licenses());
        Assert.Equal(reference, stored.Reference);
        Assert.Null(stored.CustomerEmail);
        Assert.Null(stored.CustomerEmailKey);
        Assert.Single(kit.Entries());
        var again = Customer(key, "clear-email");
        kit.Run(again);
        Assert.Contains(LicenseLookupActions.NoEmail, again.Out);
        Assert.DoesNotContain("[y/N]", again.Out); // nothing to clear, nothing asked
        Assert.Contains("Clear this customer email? It cannot be undone. [y/N]", console.Out);
        Assert.Contains($"License: {Display(reference)}", console.Out);
        Assert.Contains("Owner@Example.com", console.Out);
    }

    [Fact]
    public void ClearingDeclined_TheLicenseStillHoldsTheEmail_NothingIsStored_AndTheMenuIsShown()
    {
        var kit = Kit();
        var key = kit.Issue(email: "jürgen@müller.de");
        var reference = kit.Licenses()[0].Reference;
        var console = Customer(key, "2", "n");

        kit.Run(console);

        AssertOrder(console.Out, Display(reference), "jürgen@müller.de", "[y/N]", LicenseLookupActions.Declined);
        var stored = Assert.Single(kit.Licenses());
        Assert.Equal("jürgen@müller.de", stored.CustomerEmail);
        Assert.NotNull(stored.CustomerEmailKey);
        Assert.True(Holds(kit, "jürgen@müller.de"));
        Assert.DoesNotContain("is cleared", console.Out);
        Assert.True(SessionTestHost.Count(console.Out, "Customer details") >= 1);
        Assert.Contains("Licenses", console.Out[console.Out.LastIndexOf(LicenseLookupActions.Declined, StringComparison.Ordinal)..]);
    }

    [Fact]
    public void ClearingOrderReferencesDeclined_NoEntryChanges()
    {
        var kit = Kit();
        var key = kit.Issue(orderRef: "Ord-1001");
        Renew(kit, key, "Ord-1874", Utc(2026, 11, 9, 9));
        var reference = kit.Licenses()[0].Reference;
        var console = Customer(Display(reference), "4", "");

        kit.Run(console);

        AssertOrder(console.Out, Display(reference), "Ord-1874", "Ord-1001", "[y/N]", LicenseLookupActions.Declined);
        Assert.Equal(["Ord-1001", "Ord-1874"], kit.Entries().Select(e => e.OrderRef));
        Assert.Equal(["ord-1001", "ord-1874"], kit.Entries().Select(e => e.OrderRefKey));
        Assert.DoesNotContain("Cleared the order", console.Out);
    }

    [Fact]
    public void ClearingCancelledAtTheQuestion_ChangesNothing_ForTheEmailAndTheOrderReferences()
    {
        var kit = Kit();
        var key = kit.Issue(email: "owner@example.com", orderRef: "ORD-1001");
        var email = Customer(key, "2", "/cancel");
        var orders = Customer(key, "4", "/cancel");

        kit.Run(email);
        kit.Run(orders);

        var stored = Assert.Single(kit.Licenses());
        Assert.Equal("owner@example.com", stored.CustomerEmail);
        Assert.Equal("ORD-1001", Assert.Single(kit.Entries()).OrderRef);
        Assert.DoesNotContain("is cleared", email.Out);
        Assert.DoesNotContain("Cleared the order", orders.Out);
    }

    [Fact]
    public void ChangingTheEmailAndCorrectingTheLatestOrderReference_AskNoConfirmation()
    {
        var kit = Kit();
        var key = kit.Issue(email: "owner@example.com", orderRef: "ORD-1001");
        var change = Customer(key, "1", "new@example.com");
        var correct = Customer(key, "3", "ORD-1002");

        kit.Run(change);
        kit.Run(correct);

        Assert.DoesNotContain("[y/N]", change.Out);
        Assert.DoesNotContain("[y/N]", correct.Out);
        Assert.Equal("new@example.com", Assert.Single(kit.Licenses()).CustomerEmail);
        Assert.Equal("ORD-1002", Assert.Single(kit.Entries()).OrderRef);
    }

    [Fact]
    public void CorrectingTheLatestOrderReference_ChangesOnlyTheLatestEntry_AndNoRecordHoldsTheEarlierValue()
    {
        var kit = Kit();
        var key = kit.Issue(orderRef: "ORD-1001");
        Renew(kit, key, "Ord-1874", Utc(2026, 11, 9, 9));
        var reference = kit.Licenses()[0].Reference;
        Assert.True(Holds(kit, "Ord-1874"));
        Assert.True(Holds(kit, "ord-1874")); // the match key, so the scan is looking at the right thing
        var console = Customer(Display(reference), "3", "ORD-1875");

        kit.Run(console);

        Assert.Contains("Order reference of the latest issue entry: Ord-1874", console.Out);
        var entries = kit.Entries();
        Assert.Equal("ORD-1001", entries[0].OrderRef);
        Assert.Equal("ord-1001", entries[0].OrderRefKey);
        Assert.Equal("ORD-1875", entries[1].OrderRef);
        Assert.Equal("ord-1875", entries[1].OrderRefKey);
        Assert.False(Holds(kit, "Ord-1874"));
        Assert.False(Holds(kit, "ord-1874"));
        var oldValue = Find(orderRef: "ORD-1874");
        kit.Run(oldValue);
        Assert.Empty(Rows(oldValue));
        var newValue = Find(orderRef: "ord-1875");
        kit.Run(newValue);
        Assert.Single(Rows(newValue));
    }

    [Fact]
    public void ClearingOrderReferences_LeavesNoOrderReferenceOrKey_AndTheLicenseAndEntriesRemain()
    {
        var kit = Kit();
        var key = kit.Issue(orderRef: "Ord-1001");
        Renew(kit, key, "Ord-1874", Utc(2026, 11, 9, 9));
        var reference = kit.Licenses()[0].Reference;
        Assert.True(Holds(kit, "Ord-1001"));
        var console = Customer(Display(reference), "4", "y");

        kit.Run(console);

        Assert.All(kit.Entries(), e =>
        {
            Assert.Null(e.OrderRef);
            Assert.Null(e.OrderRefKey);
        });
        Assert.Equal(2, kit.Entries().Count);
        Assert.Single(kit.Licenses());
        foreach (var value in new[] { "Ord-1001", "ord-1001", "Ord-1874", "ord-1874" })
            Assert.False(Holds(kit, value), value);
        Assert.Contains("Cleared the order references of 2 issue entries", console.Out);
        Assert.Contains("Clear these order references? They cannot be undone. [y/N]", console.Out);
        Assert.Contains("Ord-1001", console.Out);
        Assert.Contains("Ord-1874", console.Out);
        var again = Customer(Display(reference), "clear-order-references");
        kit.Run(again);
        Assert.Contains(LicenseLookupActions.NoOrderReferences, again.Out);
        var find = Find(orderRef: "ord-1001");
        kit.Run(find);
        Assert.Empty(Rows(find));
    }

    [Fact]
    public void NoDelete_NoEntryOfTheLicensesMenuOrCustomerDetailsDeletesALicense()
    {
        var kit = Kit();
        var key = kit.Issue();
        // An out-of-range answer at Customer details is refused; /cancel then ends the action.
        var console = Quit(new ScriptedConsole().Line("2").Line("7").Line(key).Line("0").Line("/cancel"));

        kit.Run(console);

        Assert.DoesNotContain("delete", console.Out, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remove a license", console.Out, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Clear the customer email", console.Out);
        foreach (var label in AllLabels(Menus.Main))
            Assert.DoesNotContain("delete", label, StringComparison.OrdinalIgnoreCase);
        Assert.Single(kit.Licenses());
        Assert.Single(kit.Entries());
    }

    private static IEnumerable<string> AllLabels(IReadOnlyList<MenuNode> nodes) =>
        nodes.SelectMany(n => new[] { n.Label }.Concat(n.Children is null ? [] : AllLabels(n.Children)));

    [Fact]
    public void NoPassphraseForReading_FindAndShowAFreshSessionNeverAskForIt()
    {
        var kit = Kit(shared: false);
        var key = kit.Issue();

        var find = Find();
        kit.Run(find);
        var show = Show(key);
        kit.Run(show);
        var customer = Customer(key, "2", "n");
        kit.Run(customer);

        AssertNoPassphraseAsked(find);
        AssertNoPassphraseAsked(show);
        AssertNoPassphraseAsked(customer);
    }
}
