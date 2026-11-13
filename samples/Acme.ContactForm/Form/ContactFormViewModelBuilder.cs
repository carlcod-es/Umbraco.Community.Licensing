using Acme.ContactForm.Licensing;

namespace Acme.ContactForm.Form;

/// <summary>Turns a license check into the form's view model. Register as a singleton.</summary>
public sealed class ContactFormViewModelBuilder(ContactFormLicensing licensing)
{
    /// <param name="host">The request host, possibly with a port.</param>
    public ContactFormViewModel Build(string host) => new(licensing.Check(host).UploadsGranted);
}
