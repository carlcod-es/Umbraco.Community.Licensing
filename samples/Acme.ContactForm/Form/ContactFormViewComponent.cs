using Microsoft.AspNetCore.Mvc;

namespace Acme.ContactForm.Form;

/// <summary>
/// Renders the contact form. In a template: <c>@await Component.InvokeAsync("ContactForm")</c>.
/// The view is Views/Shared/Components/ContactForm/Default.cshtml, compiled into the package.
/// </summary>
[ViewComponent(Name = "ContactForm")]
public sealed class ContactFormViewComponent(ContactFormViewModelBuilder builder) : ViewComponent
{
    public IViewComponentResult Invoke() => View(builder.Build(Request.Host.Value ?? ""));
}
