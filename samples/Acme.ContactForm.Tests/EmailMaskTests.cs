using Acme.ContactForm.Submissions;

namespace Acme.ContactForm.Tests;

public class EmailMaskTests
{
    [Theory]
    [InlineData("carl@sargunar.com", "c***@s*******.c**")]
    [InlineData("first.last@example.co.uk", "f*********@e******.c*.u*")]
    public void Masked_email(string email, string masked) => Assert.Equal(masked, EmailMask.Mask(email));

    [Theory]
    [InlineData("a@b.c", "a@b.c")]
    [InlineData("a@bc.d", "a@b*.d")]
    [InlineData("ab@c.de", "a*@c.d*")]
    public void One_character_parts_are_unchanged(string email, string masked) => Assert.Equal(masked, EmailMask.Mask(email));

    [Theory]
    [InlineData("carl", "c***")]
    [InlineData("c", "c")]
    [InlineData("", "")]
    public void A_value_with_no_at_keeps_its_first_character_and_stars(string value, string masked) =>
        Assert.Equal(masked, EmailMask.Mask(value));

    [Theory]
    [InlineData("a@b.io", "a@b.i*")]
    [InlineData("nobody", "n*****")]
    public void Short_parts_and_no_at_sign(string value, string masked) => Assert.Equal(masked, EmailMask.Mask(value));

    [Fact]
    public void Only_the_first_at_splits_the_address() => Assert.Equal("a@b**.c*", EmailMask.Mask("a@b@c.cd"));
}
