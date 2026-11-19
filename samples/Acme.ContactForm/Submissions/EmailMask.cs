namespace Acme.ContactForm.Submissions;

/// <summary>
/// Masks an email address for a submission the license does not cover. Split at the first <c>@</c>: the local part is one
/// part and the domain is split at each <c>.</c>. Each part keeps its first character and every other character becomes
/// <c>*</c>; a one-character part is unchanged. Without an <c>@</c> the value is one part. Works on UTF-16 characters.
/// </summary>
public static class EmailMask
{
    /// <summary><c>carl@sargunar.com</c> becomes <c>c***@s*******.c**</c>.</summary>
    public static string Mask(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var at = email.IndexOf('@');
        if (at < 0)
        {
            return MaskPart(email);
        }

        var domain = string.Join('.', email[(at + 1)..].Split('.').Select(MaskPart));
        return $"{MaskPart(email[..at])}@{domain}";
    }

    private static string MaskPart(string part) =>
        part.Length <= 1 ? part : string.Concat(part[0], new string('*', part.Length - 1));
}
