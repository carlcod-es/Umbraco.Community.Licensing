using System.Globalization;
using System.Text;
using System.Text.Json;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

using FeatureType = Umbraco.Community.Licensing.Tool.Data.FeatureType;

/// <summary>
/// The Licenses menu (vendor-tool-licenses). Every action asks all its questions first, shows the summary, asks for the
/// passphrase when the session holds none, signs, and writes the record in one transaction (design.md, Menu session).
/// </summary>
internal static class LicenseActions
{
    internal const string SignQuestion = "Sign this license?";
    internal const string NotSigned = "Not signed. Nothing is stored.";
    internal const string SaveQuestion = "Save the key to a file?";
    internal const string NotSaved = "The key was not saved. It is shown above only; Licenses > Resend gives a new key string for the same license.";

    private const string LeaveOut = ", or Enter to leave it out";

    public static void Register(ActionTable table)
    {
        table.Register("licenses.issue", Issue);
        table.Register("licenses.reissue", ReissueActions.Reissue);
        table.Register("licenses.renew", ReissueActions.Renew);
        table.Register("licenses.resend", ReissueActions.Resend);
        table.Register("licenses.find", LicenseLookupActions.Find);
        table.Register("licenses.show", LicenseLookupActions.Show);
        table.Register("licenses.customer", LicenseLookupActions.CustomerDetails);
    }

    internal static void Issue(Session session)
    {
        var product = ProductActions.AskProduct(session);
        if (product is null)
            return;

        var console = session.Console;
        var questions = session.Questions;
        var clock = session.Environment.TimeProvider;
        var definitions = ProductActions.ReadFeatures(session, product.ProductId);

        // Both are read before the first question, so a damaged installation is reported before the vendor types anything.
        var signingKey = ActiveKeys.Find(session, product.ProductId);
        var prefix = ReadPrefix(session);

        TypedValues.ExpiryAnswer? expiry = null;
        questions.AskText("Expiry", TypedValues.ExpiryAccepts, text => TypedValues.ReadExpiry(text, clock.GetUtcNow(), out expiry));
        var displayName = AskOptional(questions, "Display name", TypedValues.DisplayNameAccepts, TypedValues.CheckDisplayNameText);
        var vendorTag = AskOptional(questions, "Vendor tag", TypedValues.VendorTagAccepts, TypedValues.CheckVendorTag);
        var features = AskFeatures(questions, definitions);
        var orderRef = AskOptional(questions, "Order reference", TypedValues.OrderRefAccepts, TypedValues.CheckOrderReference);
        var customerEmail = AskOptional(questions, "Customer email", TypedValues.EmailAccepts, TypedValues.CheckCustomerEmail);

        var libraryDefinitions = ToLibrary(definitions);
        if (!CheckBeforeSummary(console, product.ProductId, displayName, vendorTag, features, libraryDefinitions))
            return;

        WriteSummary(console, product, null, expiry!.ExpiresUtc, displayName, vendorTag, features, customerEmail, orderRef);
        if (!questions.AskYesNo(SignQuestion, proposed: true))
        {
            console.WriteLine(NotSigned);
            return;
        }

        var request = new LicenseRequest
        {
            Product = product.ProductId,
            Expiry = expiry.Expiry,
            DisplayName = displayName,
            VendorTag = vendorTag,
            Features = features,
        };
        var issued = Sign(session, request, prefix, libraryDefinitions, signingKey);

        if (!KeyIdentifierFormat.TryReferenceOfDisplay(issued.Reference, out var reference))
            throw new InvalidOperationException("The issued reference is not a license reference.");

        var issuedUtc = issued.Issued.UtcDateTime;
        using (var db = session.OpenDb())
        using (var transaction = db.Database.BeginTransaction())
        {
            db.Licenses.Add(new LicenseRecord
            {
                Reference = reference,
                ProductId = product.ProductId,
                CustomerEmail = customerEmail,
                CustomerEmailKey = customerEmail is null ? null : TypedValues.MatchKey(customerEmail),
                CreatedUtc = issuedUtc,
            });
            db.IssueEntries.Add(new IssueEntry
            {
                Reference = reference,
                Kind = IssueKind.Issue,
                IssuedUtc = issuedUtc,
                ExpiresUtc = expiry.ExpiresUtc,
                DisplayName = displayName,
                VendorTag = vendorTag,
                FeaturesJson = FeaturesJson.Write(features),
                OrderRef = orderRef,
                OrderRefKey = orderRef is null ? null : TypedValues.MatchKey(orderRef),
                KeyIdentifier = issued.KeyIdentifier,
                SigningKeyId = signingKey.SigningKeyId,
            });
            db.SaveChanges();
            transaction.Commit();
        }

        ShowKey(session, issued);
    }

    /// <summary>The library's own checks, before the summary, so nothing is signed that the library would refuse. False after saying why.</summary>
    internal static bool CheckBeforeSummary(
        ToolConsole console, string productId, string? displayName, string? vendorTag, FeatureList features, FeatureDefinitions definitions)
    {
        var inputs = features.ToInputs();
        var problems = ContentRules.Check(productId, displayName, vendorTag, inputs);
        definitions.Check(inputs, problems);
        if (problems.Count == 0)
            return true;

        console.WriteLine("This license cannot be issued. Nothing is stored.");
        foreach (var problem in problems)
            console.WriteLine($"  [{problem.Field}] {problem.Message}");
        return false;
    }

    /// <summary>
    /// Decrypts the signing key, signs and disposes the key, all inside the callback, which runs under the session lock: no question
    /// and no console in it. Asks for the passphrase when the session holds none; returns only what the library issued.
    /// </summary>
    internal static IssuedLicense Sign(
        Session session, LicenseRequest request, KeyPrefix prefix, FeatureDefinitions definitions, SigningKeyRecord signingKey)
    {
        var issuer = new LicenseIssuer(prefix, session.Environment.TimeProvider);
        return KeyProtection.WithPassphrase(session, passphrase =>
        {
            using var privateKey = KeyProtection.Decrypt(signingKey.EncryptedPrivateKeyPem, passphrase);
            return issuer.Issue(request, privateKey, definitions);
        });
    }

    /// <summary>Shows the key string once, on its own line, then offers to save it.</summary>
    internal static void ShowKey(Session session, IssuedLicense issued)
    {
        session.Console.WriteLine($"Issued {issued.Reference}. The key string is shown once and is not stored:");
        session.Console.WriteLine(issued.KeyString);
        OfferSave(session, issued.KeyString);
    }

    /// <summary>
    /// Offers to save a key string to a file the vendor names, under the named-file checks. The license is already stored and the key shown, so
    /// cancelling or a file that cannot be written does not undo it: the vendor is told the key was not saved.
    /// </summary>
    internal static void OfferSave(Session session, string keyString)
    {
        var console = session.Console;
        try
        {
            if (!session.Questions.AskYesNo(SaveQuestion, proposed: false))
                return;

            var files = new NamedFiles(session.Environment, session.Questions);
            var path = files.AskAndWrite(
                "File to save the key to",
                stream =>
                {
                    var bytes = Encoding.ASCII.GetBytes(keyString); // the key string alone: ASCII, no byte order mark, no line ending
                    stream.Write(bytes, 0, bytes.Length);
                });
            console.WriteLine($"Saved the key to {path}.");
        }
        catch (ActionCancelledException)
        {
            console.WriteLine(NotSaved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            console.WriteLine($"The key could not be saved: {Failure.OneLine(ex)} {NotSaved}");
        }
    }

    /// <summary>Asks an optional input: an empty answer leaves it out (null).</summary>
    internal static string? AskOptional(Questions questions, string question, string accepts, Func<string, string?> rule)
    {
        var answer = questions.AskText(question, accepts + LeaveOut, text => text.Length == 0 ? null : rule(text)).Value;
        return answer.Length == 0 ? null : answer;
    }

    /// <summary>
    /// Asks an input at reissue, showing <paramref name="current"/>: an empty answer keeps it (leaves it out when there is none),
    /// a new value changes it, and when <paramref name="allowRemove"/> and there is a current value, <c>/remove</c> removes it (null).
    /// A kept value is returned as stored, not checked again here; the library's checks run before the summary.
    /// </summary>
    internal static string? AskCurrent(
        Questions questions, string question, string accepts, Func<string, string?> rule, string? current, bool allowRemove = false)
    {
        var answer = questions.AskText(
            question,
            current is null ? accepts + LeaveOut : accepts,
            text => text.Length == 0 ? null : rule(text),
            proposed: current,
            allowRemove: allowRemove && current is not null);
        return answer.Removed || answer.Value.Length == 0 ? null : answer.Value;
    }

    /// <summary>
    /// Each defined feature in turn, in name order, with its type. An empty answer, or <c>off</c> for a switch, leaves it out. With
    /// <paramref name="current"/> (a reissue) the current value is shown and kept by an empty answer, <c>off</c> removes a switch and
    /// <c>/remove</c> removes a number or text feature.
    /// </summary>
    internal static FeatureList AskFeatures(Questions questions, Dictionary<string, FeatureType> definitions, FeatureList? current = null)
    {
        var features = FeatureList.Empty;
        foreach (var (name, type) in definitions.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            var label = $"Feature {name} ({ProductActions.TypeWord(type)})";
            var held = current?.FirstOrDefault(f => f.Name == name)?.Value;
            switch (type)
            {
                case FeatureType.Switch:
                    var heldState = held is not null && HeldAs<SwitchFeature>(name, held).IsOn ? "on" : null;
                    var state = AskCurrent(
                        questions, label, TypedValues.SwitchAccepts, text => TypedValues.ReadSwitch(text, out _), heldState);
                    if (state is not null && TypedValues.ReadSwitch(state, out var on) is null && on)
                        features = features.With(name, true);
                    break;

                case FeatureType.Number:
                    var number = AskCurrent(
                        questions, label, TypedValues.NumberAccepts, text => TypedValues.ReadNumber(text, out _), held is null ? null : Number(HeldAs<NumberFeature>(name, held).Value), allowRemove: true);
                    if (number is not null && TypedValues.ReadNumber(number, out var value) is null)
                        features = features.With(name, value);
                    break;

                default:
                    var text = AskCurrent(
                        questions, label, "1 to 256 characters of text", t => TypedValues.CheckTextFeature(name, t), held is null ? null : HeldAs<TextFeature>(name, held).Value, allowRemove: true);
                    if (text is not null)
                        features = features.With(name, text);
                    break;
            }
        }

        return features;
    }

    private static T HeldAs<T>(string name, FeatureValue held) where T : FeatureValue =>
        held as T ?? throw new InstallationRefusedException(ActiveKeys.Damaged($"The stored feature {name} does not match its definition"));

    internal static FeatureDefinitions ToLibrary(Dictionary<string, FeatureType> definitions)
    {
        var result = FeatureDefinitions.None;
        foreach (var (name, type) in definitions.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            result = type switch
            {
                FeatureType.Switch => result.Switch(name),
                FeatureType.Number => result.Number(name),
                _ => result.Text(name),
            };
        }

        return result;
    }

    internal static KeyPrefix ReadPrefix(Session session)
    {
        var stored = SettingsActions.Read(session, SettingNames.Prefix) ?? throw new InstallationRefusedException(SettingsActions.MissingPrefix);
        if (stored.Length == 0)
            return KeyPrefix.None;
        return TypedValues.CheckPrefix(stored, out var prefix) is null ? prefix : throw new InstallationRefusedException(SettingsActions.MissingPrefix);
    }

    internal static void WriteSummary(
        ToolConsole console, Product product, string? reference, DateTime? expiresUtc, string? displayName, string? vendorTag, FeatureList features, string? customerEmail, string? orderRef)
    {
        const string None = "(none)";
        console.WriteLine();
        console.WriteLine("Summary");
        console.WriteLine($"  Product: {product.ProductId} - {product.DisplayName}");
        if (reference is not null)
            console.WriteLine($"  Reference: {reference}");
        console.WriteLine($"  Expiry: {(expiresUtc is { } e ? e.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) : TypedValues.PerpetualWord)}");
        console.WriteLine($"  Display name: {displayName ?? None}");
        console.WriteLine($"  Vendor tag: {vendorTag ?? None}");
        console.WriteLine($"  Features: {DescribeAll(features)}");
        console.WriteLine($"  Customer email: {customerEmail ?? None}");
        console.WriteLine($"  Order reference: {orderRef ?? None}");
    }

    /// <summary>The features as one line, <c>(none)</c> when there are none.</summary>
    internal static string DescribeAll(FeatureList features) => features.Count == 0 ? "(none)" : string.Join(", ", features.Select(Describe));

    private static string Describe(LicenseFeature feature) => feature.Value switch
    {
        SwitchFeature => $"{feature.Name} (on)",
        NumberFeature n => $"{feature.Name} = {Number(n.Value)}",
        TextFeature t => $"{feature.Name} = {t.Value}",
        _ => feature.Name,
    };

    internal static string Number(decimal value) =>
        NumberRule.TryNormalise(value, out var text, out _) ? text : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The features of an issue entry as JSON text: an object of name to <c>true</c>, number or string, in the order issued.</summary>
    internal static class FeaturesJson
    {
        /// <summary>Reads the text <see cref="Write"/> made. A shape it does not write (a false switch, a nested value) is a damaged record.</summary>
        public static FeatureList Read(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new FormatException("not an object");

                var features = FeatureList.Empty;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    features = property.Value.ValueKind switch
                    {
                        JsonValueKind.True => features.With(property.Name, true),
                        JsonValueKind.Number => features.With(
                            property.Name,
                            decimal.Parse(property.Value.GetRawText(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)),
                        JsonValueKind.String => features.With(property.Name, property.Value.GetString()!),
                        _ => throw new FormatException("unexpected value"),
                    };
                }

                return features;
            }
            catch (Exception ex) when (ex is JsonException or FormatException or OverflowException or InvalidOperationException)
            {
                throw new InstallationRefusedException(ActiveKeys.Damaged("The features stored for a license cannot be read"));
            }
        }

        public static string Write(FeatureList features)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var feature in features)
                {
                    writer.WritePropertyName(feature.Name);
                    switch (feature.Value)
                    {
                        case SwitchFeature:
                            writer.WriteBooleanValue(true);
                            break;
                        case NumberFeature n:
                            writer.WriteRawValue(Number(n.Value));
                            break;
                        case TextFeature t:
                            writer.WriteStringValue(t.Value);
                            break;
                    }
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
