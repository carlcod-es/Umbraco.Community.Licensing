namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>The SemVer 2.0.0 precedence comparison the backup version check uses (ADR-0023).</summary>
public class SemVerTests
{
    private static SemVer V(string text)
    {
        Assert.True(SemVer.TryParse(text, out var version), text);
        return version;
    }

    // The ordering example of semver.org, item 11: each is lower than the next.
    [Fact]
    public void Precedence_FollowsTheSemVerOrderingExample()
    {
        string[] ascending =
        [
            "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0",
        ];
        for (var i = 0; i < ascending.Length - 1; i++)
        {
            Assert.True(V(ascending[i]).CompareTo(V(ascending[i + 1])) < 0, $"{ascending[i]} < {ascending[i + 1]}");
            Assert.True(V(ascending[i + 1]).CompareTo(V(ascending[i])) > 0, $"{ascending[i + 1]} > {ascending[i]}");
        }
    }

    [Theory]
    [InlineData("2.0.0", "1.9.9")]
    [InlineData("1.10.0", "1.9.0")]
    [InlineData("1.0.10", "1.0.9")]
    [InlineData("0.1.0", "0.1.0-beta.7")]
    [InlineData("0.1.0-beta.10", "0.1.0-beta.7")]
    [InlineData("0.1.0-beta.7", "0.1.0-alpha.9")]
    [InlineData("0.1.0-beta.7.1", "0.1.0-beta.7")]
    [InlineData("0.1.0-beta", "0.1.0-1")]
    public void Higher_IsAboveLower(string higher, string lower)
    {
        Assert.True(V(higher).CompareTo(V(lower)) > 0);
        Assert.True(V(lower).CompareTo(V(higher)) < 0);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3+build.5")]
    [InlineData("1.2.3-rc.1+a", "1.2.3-rc.1+b")]
    public void BuildMetadata_IsIgnored(string a, string b) => Assert.Equal(0, V(a).CompareTo(V(b)));

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3")]
    [InlineData("1.2.3-a..b")]
    [InlineData("1.2.x")]
    public void NotSemVer_IsRefused(string text) => Assert.False(SemVer.TryParse(text, out _));

    [Fact]
    public void Null_IsRefused() => Assert.False(SemVer.TryParse(null, out _));

    [Fact]
    public void IsNewer_ComparesTheBackupVersionWithTheRunningOne()
    {
        Assert.True(RestoreBackup.IsNewer(V("0.1.0"), "0.1.0-beta.7"));
        Assert.True(RestoreBackup.IsNewer(V("0.1.0-beta.8"), "0.1.0-beta.7"));
        Assert.False(RestoreBackup.IsNewer(V("0.1.0-beta.7"), "0.1.0-beta.7"));
        Assert.False(RestoreBackup.IsNewer(V("0.1.0-beta.6+x"), "0.1.0-beta.7"));
        Assert.False(RestoreBackup.IsNewer(V("0.0.9"), "0.1.0-beta.7"));
    }
}
