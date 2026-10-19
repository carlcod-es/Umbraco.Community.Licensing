namespace Umbraco.Community.Licensing.Core.Tests.Issuing;

public class LicenseExpiryTests
{
    [Fact]
    public void NotStated_MapsToNotStated() => Assert.Equal(ExpiryStatement.NotStated, LicenseExpiry.ToStatement(null));

    [Fact]
    public void On_MapsToOnDate()
    {
        var date = new DateOnly(2027, 3, 4);

        Assert.Equal(ExpiryStatement.OnDate(date), LicenseExpiry.ToStatement(LicenseExpiry.On(date)));
    }

    [Fact]
    public void At_MapsToAtInstantKeepingInstantAndFraction()
    {
        var instant = new DateTimeOffset(2027, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)).AddTicks(5);

        var statement = LicenseExpiry.ToStatement(LicenseExpiry.At(instant));

        Assert.Equal(ExpiryKind.Instant, statement.Kind);
        Assert.Equal(instant, statement.Instant);
        Assert.Equal(instant.Ticks, statement.Instant.Ticks);
    }

    [Fact]
    public void Perpetual_MapsToPerpetual() => Assert.Equal(ExpiryStatement.Perpetual, LicenseExpiry.ToStatement(LicenseExpiry.Perpetual));

    [Fact]
    public void SameExpiries_AreEqual()
    {
        Assert.Equal(LicenseExpiry.On(new DateOnly(2027, 1, 1)), LicenseExpiry.On(new DateOnly(2027, 1, 1)));
        Assert.NotEqual(LicenseExpiry.Perpetual, LicenseExpiry.On(new DateOnly(2027, 1, 1)));
    }
}
