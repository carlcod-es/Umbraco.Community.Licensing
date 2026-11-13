using Acme.ContactForm.Form;
using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

// The view model builder and the handler the surface controller delegates to. A SurfaceController needs Umbraco's
// services to construct, so the controller stays thin and is not built here.
public class ContactFormFormTests
{
    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();

    public static TheoryData<string> EntryNames => [.. Entries.Select(e => e.Name)];

    private sealed class Rig
    {
        public Rig(DemoKeyEntry entry)
        {
            var clock = new TestClock(entry.DemoDate is { } d ? DemoKeyFile.ParseInstant(d) : DemoKeyFile.FixedClock);
            Store = new InMemorySubmissionStore(TimeProvider.System);
            var licensing = new ContactFormLicensing(
                new ContactFormProduct(clock),
                new FixedSource(entry.Key),
                Store,
                new LicenseStateLog(NullLogger<LicenseStateLog>.Instance));
            Builder = new ContactFormViewModelBuilder(licensing);
            Handler = new ContactFormHandler(Builder, Store);
        }

        public InMemorySubmissionStore Store { get; }

        public ContactFormViewModelBuilder Builder { get; }

        public ContactFormHandler Handler { get; }
    }

    private sealed class FixedSource(string? key) : ILicenseKeySource
    {
        public LicenseKeyReading Read() => new(key, "Test source");
    }

    private static DemoKeyEntry Entry(string name) => Entries.Single(e => e.Name == name);

    private static bool UploadsExpected(DemoKeyEntry e) =>
        e.State == nameof(LicenseState.Valid) && e.Features.TryGetValue(ContactFormProduct.FileUploadsFeature, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True;

    private static ContactFormPost Post() => new("Ada", "ada@example.com", "Hello");

    private static FormFile File(string name, long length) =>
        new(new MemoryStream(new byte[length]), 0, length, "upload", name);

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Uploads_shown_only_for_a_valid_key_with_file_uploads(string name)
    {
        var entry = Entry(name);
        var model = new Rig(entry).Builder.Build("localhost:5000");

        Assert.Equal(UploadsExpected(entry), model.ShowUploads);
    }

    [Fact]
    public void Uploads_granted()
    {
        Assert.True(new Rig(Entry("Valid: full plan")).Builder.Build("localhost").ShowUploads);
    }

    [Theory]
    [InlineData("Valid: small plan")]
    [InlineData("Expired")]
    [InlineData("No key")]
    public void Uploads_not_granted(string name)
    {
        Assert.False(new Rig(Entry(name)).Builder.Build("localhost").ShowUploads);
    }

    [Fact]
    public void The_view_model_carries_nothing_but_ShowUploads()
    {
        Assert.Equal(["ShowUploads"], typeof(ContactFormViewModel).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name));
    }

    [Fact]
    public void Submission_with_a_missing_key()
    {
        var rig = new Rig(Entry("No key"));
        var result = rig.Handler.Handle(Post(), null, "localhost");

        Assert.True(result.Accepted);
        var stored = Assert.Single(rig.Store.ListThisMonth());
        Assert.Equal(("Ada", "ada@example.com", "Hello"), (stored.Name, stored.Email, stored.Message));
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Every_demo_key_accepts_a_valid_submission(string name)
    {
        var rig = new Rig(Entry(name));
        var result = rig.Handler.Handle(Post(), null, "localhost");

        Assert.True(result.Accepted);
        Assert.Empty(result.FieldErrors);
        Assert.Single(rig.Store.ListThisMonth());
    }

    [Theory]
    [MemberData(nameof(EntryNames))]
    public void Upload_kept_as_name_and_size_only_when_shown(string name)
    {
        var entry = Entry(name);
        var rig = new Rig(entry);
        var result = rig.Handler.Handle(Post(), File("brief.pdf", 20 * 1024), "localhost");

        Assert.True(result.Accepted);
        var stored = Assert.Single(rig.Store.ListThisMonth());
        if (UploadsExpected(entry))
        {
            Assert.Equal("brief.pdf", stored.UploadFileName);
            Assert.Equal(20 * 1024, stored.UploadSizeBytes);
        }
        else
        {
            // Upload sent anyway: the rest is accepted, the file is ignored.
            Assert.Null(stored.UploadFileName);
            Assert.Null(stored.UploadSizeBytes);
        }
    }

    [Fact]
    public void Upload_sent_anyway()
    {
        var rig = new Rig(Entry("Valid: small plan"));
        var result = rig.Handler.Handle(Post(), File("brief.pdf", 20 * 1024), "localhost");

        Assert.True(result.Accepted);
        var stored = Assert.Single(rig.Store.ListThisMonth());
        Assert.Null(stored.UploadFileName);
        Assert.Null(stored.UploadSizeBytes);
    }

    [Fact]
    public void Upload_kept_as_name_and_size()
    {
        var rig = new Rig(Entry("Valid: full plan"));
        rig.Handler.Handle(Post(), File("brief.pdf", 20 * 1024), "localhost");

        var stored = Assert.Single(rig.Store.ListThisMonth());
        Assert.Equal("brief.pdf", stored.UploadFileName);
        Assert.Equal(20480, stored.UploadSizeBytes);
        // The submission type has no member that could hold content.
        Assert.DoesNotContain(typeof(Submission).GetProperties(), p => p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream));
    }

    [Fact]
    public void Thank_you_response_over_the_limit()
    {
        var entry = Entry("Valid: small plan");
        var rig = new Rig(entry);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(rig.Handler.Handle(Post(), null, "localhost").Accepted);
        }

        var fourth = rig.Handler.Handle(Post(), null, "localhost");

        Assert.True(fourth.Accepted);
        Assert.Empty(fourth.FieldErrors);
        Assert.Equal(4, rig.Store.CountThisMonth());

        // The one thank-you is a constant, so it is the same in every state and holds nothing about the license.
        var text = ContactFormHandler.ThankYou;
        var forbidden = new List<string> { entry.Key!, "Valid", "Expired", "Missing", "limit", "expire", "license", "licence", "3", "4",
            ContactFormProduct.FileUploadsFeature, ContactFormProduct.MaxSubmissionsPerMonthFeature, ContactFormProduct.LicensedDomainsFeature, entry.Name };
        forbidden.AddRange(Entries.Select(e => e.State));
        foreach (var word in forbidden)
        {
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null, "a@b.c", "m", "Name")]
    [InlineData("n", "  ", "m", "Email")]
    [InlineData("n", "a@b.c", null, "Message")]
    public void Missing_required_field_is_not_recorded(string? name, string? email, string? message, string field)
    {
        var rig = new Rig(Entry("Valid: full plan"));
        var result = rig.Handler.Handle(new ContactFormPost(name, email, message), null, "localhost");

        Assert.False(result.Accepted);
        Assert.Equal([field], result.FieldErrors.Keys);
        Assert.Empty(rig.Store.ListThisMonth());
    }

    [Fact]
    public void Upload_over_the_cap_is_not_read_but_the_submission_is_kept()
    {
        var rig = new Rig(Entry("Valid: full plan"));
        var result = rig.Handler.Handle(Post(), File("big.bin", ContactFormHandler.MaxRequestBytes + 1), "localhost");

        Assert.True(result.Accepted);
        Assert.Empty(result.FieldErrors);
        var stored = Assert.Single(rig.Store.ListThisMonth());
        Assert.Null(stored.UploadFileName);
        Assert.Null(stored.UploadSizeBytes);
    }

    [Fact]
    public void Upload_at_the_cap_is_read()
    {
        var rig = new Rig(Entry("Valid: full plan"));
        rig.Handler.Handle(Post(), File("edge.bin", ContactFormHandler.MaxRequestBytes), "localhost");

        Assert.Equal(ContactFormHandler.MaxRequestBytes, Assert.Single(rig.Store.ListThisMonth()).UploadSizeBytes);
    }

    [Fact]
    public void Request_cap_is_one_megabyte()
    {
        Assert.Equal(1_048_576, ContactFormHandler.MaxRequestBytes);
    }
}
