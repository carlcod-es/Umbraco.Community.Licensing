using System.CommandLine;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Commands;

/// <summary>The <c>license</c> command group: issue, reissue, renew, resend, list, show, customer.</summary>
internal static class LicenseCommands
{
    public static Command Build(ToolHost host) =>
        new("license", "Issue licenses and work with their records.")
        {
            BuildIssue(host),
            BuildReissue(host),
            BuildRenew(host),
            BuildResend(host),
            BuildList(host),
            BuildShow(host),
            BuildCustomer(host),
        };

    private static Command BuildIssue(ToolHost host)
    {
        // Positional arguments are optional to the parser: a missing one is prompted or refused naming it, not a parse error.
        var id = new Argument<string?>("product-id")
        {
            Description = "The product ID, for example acme.commerce.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var expires = new Option<string?>("--expires") { Description = "When it expires: a date (2027-03-01), a UTC date and time (2027-03-01T23:59:59Z) or perpetual." };
        var feature = new Option<string[]>("--feature")
        {
            Description = "A feature as name=value: on|off for a switch, a number, or text. Repeat for more.",
            AllowMultipleArgumentsPerToken = false,
        };
        var displayName = new Option<string?>("--display-name") { Description = "The license's display name." };
        var vendorTag = new Option<string?>("--vendor-tag") { Description = "The vendor tag." };
        var orderRef = new Option<string?>("--order-ref") { Description = "The order reference, kept on this issue entry." };
        var email = new Option<string?>("--email") { Description = "The customer email, kept on the license." };
        var command = new Command(
            "issue",
            "Issue a license. The key string is written alone to stdout, once; it is not stored. Needs the passphrase.")
        {
            id,
            expires,
            feature,
            displayName,
            vendorTag,
            orderRef,
            email,
        };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();

            // The passphrase first: nothing is asked or written before it is known to be right.
            var passphrase = context.ReadVerifiedPassphrase(db);
            var values = context.RequireAll(
                new RequiredInput("<product-id>", context.ParseResult.GetValue(id), "Product ID"),
                new RequiredInput("--expires", context.ParseResult.GetValue(expires), "Expiry (date, UTC date and time, or perpetual)"));
            var parse = context.ParseResult;
            var issued = Issuing.Issue(
                context,
                db,
                new IssueInput(
                    values[0],
                    values[1],
                    parse.GetValue(feature) ?? [],
                    parse.GetValue(displayName),
                    parse.GetValue(vendorTag),
                    parse.GetValue(orderRef),
                    parse.GetValue(email)),
                passphrase);
            context.Console.Error.WriteLine($"Issued {issued.Reference} (key identifier {issued.KeyIdentifier}).");
            context.Console.Out.WriteLine(issued.KeyString);
            return ExitCodes.Done;
        });
        return command;
    }

    private const string Suggested = "suggested";

    private const string ExpiresReissueHelp =
        "When it expires: a date (2027-03-01), a UTC date and time (2027-03-01T23:59:59Z), perpetual, or suggested to take the suggestion without a prompt.";

    private static Argument<string?> ReferenceArgument() =>
        new("reference")
        {
            Description = "The license reference, for example LIC-8F3AK-M7RXB.",
            Arity = ArgumentArity.ZeroOrOne,
        };

    private static Command BuildReissue(ToolHost host)
    {
        var reference = ReferenceArgument();
        var expires = new Option<string?>("--expires") { Description = ExpiresReissueHelp };
        var feature = new Option<string[]>("--feature")
        {
            Description = "A feature to add or change as name=value. Repeat for more.",
            AllowMultipleArgumentsPerToken = false,
        };
        var removeFeature = new Option<string[]>("--remove-feature")
        {
            Description = "A feature to remove, by name. Repeat for more.",
            AllowMultipleArgumentsPerToken = false,
        };
        var displayName = new Option<string?>("--display-name") { Description = "The new display name." };
        var vendorTag = new Option<string?>("--vendor-tag") { Description = "The new vendor tag." };
        var orderRef = new Option<string?>("--order-ref") { Description = "The order reference, kept on the new issue entry." };
        var email = new Option<string?>("--email") { Description = "The new customer email, kept on the license." };
        var command = new Command(
            "reissue",
            "Reissue a license from its record with changes (add-on, feature change). Same product and reference. The key string is written alone to stdout, once; it is not stored. Needs the passphrase.")
        {
            reference,
            expires,
            feature,
            removeFeature,
            displayName,
            vendorTag,
            orderRef,
            email,
        };
        host.SetAction(command, context => Reissue(
            context,
            context.ParseResult.GetValue(reference),
            IssueKind.Reissue,
            context.ParseResult.GetValue(expires),
            new ReissueOptions(
                context.ParseResult.GetValue(feature) ?? [],
                context.ParseResult.GetValue(removeFeature) ?? [],
                context.ParseResult.GetValue(displayName),
                context.ParseResult.GetValue(vendorTag),
                context.ParseResult.GetValue(orderRef),
                context.ParseResult.GetValue(email))));
        return command;
    }

    private static Command BuildRenew(ToolHost host)
    {
        var reference = ReferenceArgument();
        var expires = new Option<string?>("--expires") { Description = ExpiresReissueHelp };
        var orderRef = new Option<string?>("--order-ref") { Description = "The order reference, kept on the renewal entry." };
        var email = new Option<string?>("--email") { Description = "The new customer email, kept on the license." };
        var command = new Command(
            "renew",
            "Renew a license: reissue it with a new expiry, recorded as a renewal. Suggests the current expiry plus the last term. The key string is written alone to stdout, once; it is not stored. Needs the passphrase.")
        {
            reference,
            expires,
            orderRef,
            email,
        };
        host.SetAction(command, context => Reissue(
            context,
            context.ParseResult.GetValue(reference),
            IssueKind.Renewal,
            context.ParseResult.GetValue(expires),
            new ReissueOptions([], [], null, null, context.ParseResult.GetValue(orderRef), context.ParseResult.GetValue(email))));
        return command;
    }

    private static Command BuildResend(ToolHost host)
    {
        var reference = ReferenceArgument();
        var command = new Command(
            "resend",
            "Resend a license: reissue it from its record with nothing changed, for a site owner who lost the key. The key string is written alone to stdout, once; it is not stored. Needs the passphrase.")
        {
            reference,
        };
        host.SetAction(command, context => Reissue(
            context,
            context.ParseResult.GetValue(reference),
            IssueKind.Resend,
            null,
            new ReissueOptions([], [], null, null, null, null)));
        return command;
    }

    private static Command BuildList(ToolHost host)
    {
        var product = new Option<string?>("--product") { Description = "Only licenses of this product ID." };
        var reference = new Option<string?>("--reference") { Description = "Only this license, as typed under the prefix rule (prefix optional, hyphens optional)." };
        var orderRef = new Option<string?>("--order-ref") { Description = "Only licenses with this order reference on any issue entry." };
        var email = new Option<string?>("--email") { Description = "Only licenses with this customer email (whole address, case ignored)." };
        var command = new Command(
            "list",
            "List licenses, one tab-separated row each on stdout: reference, product, current expiry, customer email, latest order reference. Filters combine. Needs no passphrase.")
        {
            product,
            reference,
            orderRef,
            email,
        };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var parse = context.ParseResult;
            var settings = InstallationSettings.Load(db);
            var rows = LicenseRecords.List(
                db,
                settings,
                new LicenseFilter(parse.GetValue(product), parse.GetValue(reference), parse.GetValue(orderRef), parse.GetValue(email)));
            if (rows.Count == 0)
            {
                context.Console.Error.WriteLine("No licenses match.");
                return ExitCodes.Done;
            }

            context.Console.Out.Write(LicenseRecords.FormatRows(settings, rows));
            return ExitCodes.Done;
        });
        return command;
    }

    private static Command BuildShow(ToolHost host)
    {
        var reference = ReferenceArgument();
        var command = new Command("show", "Show one license with every issue entry, newest first. Needs no passphrase.")
        {
            reference,
        };
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var typed = context.Require("<reference>", context.ParseResult.GetValue(reference), "License reference");
            var settings = InstallationSettings.Load(db);
            var license = Issuing.FindLicense(db, settings, typed);
            context.Console.Out.Write(LicenseRecords.Format(settings, license));
            return ExitCodes.Done;
        });
        return command;
    }

    private static Command BuildCustomer(ToolHost host)
    {
        var reference = ReferenceArgument();
        var orderRef = new Option<string?>("--order-ref") { Description = "Correct the latest issue entry's order reference." };
        var clearOrderRefs = new Option<bool>("--clear-order-refs") { Description = "Clear the order reference of every issue entry." };
        var email = new Option<string?>("--email") { Description = "Set the customer email." };
        var clearEmail = new Option<bool>("--clear-email") { Description = "Clear the customer email." };
        var command = new Command(
            "customer",
            "Edit or clear a license's customer email, correct its latest order reference, or clear every order reference. Needs no passphrase.")
        {
            reference,
            orderRef,
            clearOrderRefs,
            email,
            clearEmail,
        };
        command.Validators.Add(result =>
        {
            if (result.GetResult(orderRef) is not null && result.GetResult(clearOrderRefs) is not null)
            {
                result.AddError("--order-ref and --clear-order-refs cannot be used together.");
            }

            if (result.GetResult(email) is not null && result.GetResult(clearEmail) is not null)
            {
                result.AddError("--email and --clear-email cannot be used together.");
            }
        });
        host.SetAction(command, context =>
        {
            var parse = context.ParseResult;
            var change = new CustomerChange(
                parse.GetValue(orderRef),
                parse.GetValue(clearOrderRefs),
                parse.GetValue(email),
                parse.GetValue(clearEmail));
            using var db = context.OpenDatabase();
            var typed = context.Require("<reference>", parse.GetValue(reference), "License reference");
            var settings = InstallationSettings.Load(db);
            var license = Issuing.FindLicense(db, settings, typed);
            var done = LicenseRecords.ApplyCustomerChange(db, license, change);
            context.Console.Error.WriteLine($"Updated {LicenseRecords.DisplayReference(settings, license.Reference)}: {done}.");
            return ExitCodes.Done;
        });
        return command;
    }

    private sealed record ReissueOptions(
        IReadOnlyList<string> Features,
        IReadOnlyList<string> RemoveFeatures,
        string? DisplayName,
        string? VendorTag,
        string? OrderRef,
        string? Email);

    private static int Reissue(ToolContext context, string? typedReference, IssueKind kind, string? expiresGiven, ReissueOptions options)
    {
        using var db = context.OpenDatabase();

        // The passphrase first: nothing is asked or written before it is known to be right.
        var passphrase = context.ReadVerifiedPassphrase(db);
        var reference = context.Require("<reference>", typedReference, "License reference");
        var license = Issuing.FindLicense(db, InstallationSettings.Load(db), reference);
        var expires = kind == IssueKind.Resend
            ? Issuing.ExpiryText(license.Entries.OrderBy(e => e.Id).Last().ExpiresUtc)
            : ResolveExpiry(context, license, expiresGiven, kind == IssueKind.Renewal);
        var issued = Issuing.Reissue(
            context,
            db,
            license,
            new ReissueInput(kind, expires, options.Features, options.RemoveFeatures, options.DisplayName, options.VendorTag, options.OrderRef, options.Email),
            passphrase);
        var what = kind switch
        {
            IssueKind.Renewal => "Renewed",
            IssueKind.Resend => "Resent",
            _ => "Reissued",
        };
        context.Console.Error.WriteLine($"{what} {issued.Reference} (key identifier {issued.KeyIdentifier}).");
        context.Console.Out.WriteLine(issued.KeyString);
        return ExitCodes.Done;
    }

    /// <summary>
    /// The new expiry: as given; the suggestion for <c>suggested</c>; otherwise asked for at a terminal with the
    /// suggestion as the default, and refused when nobody can be asked (the suggestion is taken only on request).
    /// </summary>
    private static string ResolveExpiry(ToolContext context, License license, string? given, bool isRenewal)
    {
        // An empty or whitespace-only value is missing, as for every required input.
        given = given?.Trim();
        var isGiven = !string.IsNullOrEmpty(given);
        var isSuggested = isGiven && given!.Equals(Suggested, StringComparison.OrdinalIgnoreCase);
        if (isGiven && !isSuggested)
        {
            return given!;
        }

        var suggestion = Issuing.SuggestedExpiry(license, isRenewal);
        if (isSuggested)
        {
            return suggestion;
        }

        if (!context.Interactive)
        {
            throw new ToolRefusal("Missing --expires. Give a date, a UTC date and time, perpetual or suggested. Nothing changed.");
        }

        var answer = context.Console.Prompt($"Expiry [{suggestion}]: ")?.Trim();
        return string.IsNullOrEmpty(answer) || answer.Equals(Suggested, StringComparison.OrdinalIgnoreCase) ? suggestion : answer;
    }
}
