namespace Acme.ContactForm.Form;

/// <summary>
/// All the view gets from licensing: whether to show the file input. Nothing else about the license reaches the page.
/// </summary>
public sealed record ContactFormViewModel(bool ShowUploads);
