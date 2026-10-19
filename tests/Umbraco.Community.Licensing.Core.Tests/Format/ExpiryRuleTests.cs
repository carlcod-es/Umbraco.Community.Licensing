using Umbraco.Community.Licensing.Core.Tests.Support;

namespace Umbraco.Community.Licensing.Core.Tests.Format;

public class ExpiryRuleTests
{
    public static TheoryData<string> Cultures => ["en-US", "tr-TR", "de-DE"];

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static (bool Ok, DateTimeOffset? Expires, List<LicenseProblem> Problems) Resolve(ExpiryStatement statement, DateTimeOffset? now = null)
    {
        var problems = new List<LicenseProblem>();
        var ok = ExpiryRule.TryResolve(statement, now ?? Now, problems, out var expires);
        return (ok, expires, problems);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void DateAlone_ExpiresAtEndOfDayUtc(string culture)
    {
        using var scope = new CultureScope(culture);

        var (ok, expires, problems) = Resolve(ExpiryStatement.OnDate(new DateOnly(2027, 3, 1)));

        Assert.True(ok);
        Assert.Empty(problems);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 23, 59, 59, TimeSpan.Zero), expires);
        Assert.Equal(TimeSpan.Zero, expires!.Value.Offset);
    }

    [Fact]
    public void NoExpiryStated_NamesExpires()
    {
        var (ok, expires, problems) = Resolve(ExpiryStatement.NotStated);

        Assert.False(ok);
        Assert.Null(expires);
        Assert.Equal("expires", Assert.Single(problems).Field);
        Assert.Equal(ExpiryKind.NotStated, default(ExpiryStatement).Kind);
    }

    [Fact]
    public void Perpetual_IsAcceptedWithNoInstant()
    {
        var (ok, expires, problems) = Resolve(ExpiryStatement.Perpetual);

        Assert.True(ok);
        Assert.Null(expires);
        Assert.Empty(problems);
    }

    [Fact]
    public void ExpiryInThePast_NamesExpires()
    {
        foreach (var statement in new[]
        {
            ExpiryStatement.OnDate(new DateOnly(2026, 9, 30)),
            ExpiryStatement.AtInstant(new DateTimeOffset(2026, 10, 1, 7, 59, 59, TimeSpan.Zero)),
        })
        {
            var (ok, expires, problems) = Resolve(statement);

            Assert.False(ok);
            Assert.Null(expires);
            Assert.Equal("expires", Assert.Single(problems).Field);
        }
    }

    [Fact]
    public void ExpiryToday_IsAccepted()
    {
        var (okDate, date, _) = Resolve(ExpiryStatement.OnDate(new DateOnly(2026, 10, 1)));
        var (okTime, time, _) = Resolve(ExpiryStatement.AtInstant(Now));

        Assert.True(okDate);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.Zero), date);
        Assert.True(okTime);
        Assert.Equal(Now, time);
    }

    [Fact]
    public void Clock_IsTruncatedToTheSecond()
    {
        var now = Now.AddMilliseconds(734);

        Assert.True(Resolve(ExpiryStatement.AtInstant(Now), now).Ok);
        Assert.False(Resolve(ExpiryStatement.AtInstant(Now.AddSeconds(-1)), now).Ok);
    }

    [Fact]
    public void FractionOfASecond_NamesExpires()
    {
        foreach (var fraction in new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromTicks(1) })
        {
            var instant = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero) + fraction;

            var (ok, _, problems) = Resolve(ExpiryStatement.AtInstant(instant));

            Assert.False(ok);
            Assert.Equal("expires", Assert.Single(problems).Field);
        }
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void OffsetIsConvertedToUtc(string culture)
    {
        using var scope = new CultureScope(culture);

        var (ok, expires, _) = Resolve(ExpiryStatement.AtInstant(new DateTimeOffset(2027, 3, 1, 14, 0, 0, TimeSpan.FromHours(2))));

        Assert.True(ok);
        Assert.Equal(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero), expires);
        Assert.Equal(TimeSpan.Zero, expires!.Value.Offset);
    }

    [Fact]
    public void OffsetInstant_IsComparedAsAnInstant()
    {
        // 09:00+02:00 is 07:00Z, before the clock's 08:00Z.
        Assert.False(Resolve(ExpiryStatement.AtInstant(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(2)))).Ok);
    }
}
