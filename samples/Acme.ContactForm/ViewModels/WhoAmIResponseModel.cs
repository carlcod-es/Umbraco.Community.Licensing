namespace Acme.ContactForm.ViewModels;

public class WhoAmIResponseModel
{
    public required string? Name { get; init; }

    public required string Email { get; init; }

    public required IEnumerable<string> Groups { get; init; }
}
