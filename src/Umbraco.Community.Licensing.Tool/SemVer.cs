using System.Numerics;
using System.Text.RegularExpressions;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>
/// A SemVer 2.0.0 version, enough to tell which of two has the higher precedence (ADR-0023). Build metadata is read and ignored.
/// Written here because no package is added for one comparison.
/// </summary>
internal sealed partial class SemVer : IComparable<SemVer>
{
    private readonly BigInteger _major;
    private readonly BigInteger _minor;
    private readonly BigInteger _patch;
    private readonly string[] _prerelease;

    private SemVer(BigInteger major, BigInteger minor, BigInteger patch, string[] prerelease)
    {
        _major = major;
        _minor = minor;
        _patch = patch;
        _prerelease = prerelease;
    }

    // The grammar of semver.org, without the leading <c>v</c>: no leading zeros in numbers or in numeric prerelease identifiers.
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Grammar();

    /// <summary>Parses a valid SemVer 2.0.0 version; false for anything else.</summary>
    public static bool TryParse(string? text, out SemVer version)
    {
        version = null!;
        if (text is null)
            return false;

        var match = Grammar().Match(text);
        if (!match.Success)
            return false;

        var prerelease = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        version = new SemVer(
            BigInteger.Parse(match.Groups[1].Value),
            BigInteger.Parse(match.Groups[2].Value),
            BigInteger.Parse(match.Groups[3].Value),
            prerelease);
        return true;
    }

    /// <summary>
    /// SemVer precedence: major, minor and patch as numbers; a release above any of its prereleases; prerelease identifiers
    /// dot by dot, numeric ones as numbers and below alphanumeric ones, alphanumeric ones ordinally, fewer identifiers lower when all before are equal.
    /// </summary>
    public int CompareTo(SemVer? other)
    {
        if (other is null)
            return 1;

        var core = _major.CompareTo(other._major);
        if (core != 0) return core;
        core = _minor.CompareTo(other._minor);
        if (core != 0) return core;
        core = _patch.CompareTo(other._patch);
        if (core != 0) return core;

        if (_prerelease.Length == 0 || other._prerelease.Length == 0)
            return other._prerelease.Length.CompareTo(_prerelease.Length); // no prerelease is the higher one

        for (var i = 0; i < Math.Min(_prerelease.Length, other._prerelease.Length); i++)
        {
            var result = CompareIdentifier(_prerelease[i], other._prerelease[i]);
            if (result != 0)
                return result;
        }

        return _prerelease.Length.CompareTo(other._prerelease.Length);
    }

    private static int CompareIdentifier(string a, string b)
    {
        var aNumeric = a.All(char.IsAsciiDigit);
        var bNumeric = b.All(char.IsAsciiDigit);
        if (aNumeric && bNumeric)
            return BigInteger.Parse(a).CompareTo(BigInteger.Parse(b));
        if (aNumeric)
            return -1;
        if (bNumeric)
            return 1;
        return Math.Sign(string.CompareOrdinal(a, b));
    }
}
