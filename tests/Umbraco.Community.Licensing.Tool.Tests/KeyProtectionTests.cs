using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;
using Xunit;

namespace Umbraco.Community.Licensing.Tool.Tests;

public class KeyProtectionTests(ITestOutputHelper output)
{
    private const string Passphrase = "correct horse battery";

    [Fact]
    public void Opened_by_another_tool()
    {
        using var pair = SigningKeyPair.Create();

        var pem = KeyProtection.Encrypt(pair.PrivateKey, Passphrase, 1_000);

        Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----", pem);
        using (var other = ECDsa.Create())
        {
            other.ImportFromEncryptedPem(pem, Passphrase);
            Assert.Equal(256, other.KeySize);
        }

        using var wrong = ECDsa.Create();
        Assert.ThrowsAny<CryptographicException>(() => wrong.ImportFromEncryptedPem(pem, "wrong passphrase 123"));
        Assert.ThrowsAny<CryptographicException>(() => wrong.ImportFromEncryptedPem(pem, ""));

        using var decrypted = KeyProtection.Decrypt(pem, Passphrase);
        Assert.Equal(pair.PublicKey.Export(), decrypted.PublicKey.Export());
        Assert.Equal(pair.SigningKeyId, decrypted.SigningKeyId);
    }

    [Fact]
    public void Decrypt_with_a_wrong_passphrase_throws()
    {
        using var pair = SigningKeyPair.Create();
        var pem = KeyProtection.Encrypt(pair.PrivateKey, Passphrase, 1_000);

        Assert.ThrowsAny<CryptographicException>(() => KeyProtection.Decrypt(pem, "wrong passphrase 123"));
    }

    [Fact]
    public void Encrypt_and_decrypt_at_the_production_iteration_count()
    {
        using var pair = SigningKeyPair.Create();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var pem = KeyProtection.Encrypt(pair.PrivateKey, Passphrase, ToolEnvironment.ProductionPbkdf2Iterations);
        using var decrypted = KeyProtection.Decrypt(pem, Passphrase);

        output.WriteLine($"Encrypt + decrypt at {ToolEnvironment.ProductionPbkdf2Iterations} iterations: {watch.ElapsedMilliseconds} ms");
        Assert.Equal(pair.PublicKey.Export(), decrypted.PublicKey.Export());
    }

    [Fact]
    public void The_check_accepts_the_right_passphrase_and_refuses_a_wrong_one()
    {
        var check = PassphraseCheck.Create(Passphrase, 1_000);

        Assert.True(check.Verify(Passphrase));
        Assert.False(check.Verify("wrong passphrase 123"));
        Assert.False(check.Verify(Passphrase + " "));
    }

    // "créme brûlée façade" with every accent composed (NFC) or as base letter plus combining mark (NFD).
    private const string Nfc = "cr\u00e9me br\u00fbl\u00e9e fa\u00e7ade";
    private const string Nfd = "cre\u0301me bru\u0302le\u0301e fac\u0327ade";

    [Fact]
    public void The_two_Unicode_forms_differ_as_strings()
    {
        Assert.NotEqual(Nfc, Nfd, StringComparer.Ordinal);
        Assert.True(Nfc.Length >= 15);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_check_verifies_the_other_Unicode_form(bool createdFromNfd)
    {
        var check = PassphraseCheck.Create(createdFromNfd ? Nfd : Nfc, 1_000);

        Assert.True(check.Verify(createdFromNfd ? Nfc : Nfd));
        Assert.True(check.Verify(createdFromNfd ? Nfd : Nfc));
        Assert.False(check.Verify("cr\u00e9me br\u00fbl\u00e9e fa\u00e7ade!"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_key_decrypts_with_the_other_Unicode_form(bool encryptedWithNfd)
    {
        using var pair = SigningKeyPair.Create();
        var pem = KeyProtection.Encrypt(pair.PrivateKey, encryptedWithNfd ? Nfd : Nfc, 1_000);

        using var key = KeyProtection.Decrypt(pem, encryptedWithNfd ? Nfc : Nfd);

        Assert.Equal(pair.PublicKey.Export(), key.PublicKey.Export());
    }

    [Fact]
    public void The_check_uses_its_stored_iterations()
    {
        var check = PassphraseCheck.Create(Passphrase, 1_000);
        var other = check with { Iterations = 2_000 };

        // Verification reads the count from the check, not from the environment.
        Assert.True(check.Verify(Passphrase));
        Assert.False(other.Verify(Passphrase));
    }

    [Fact]
    public void A_stored_check_is_verified_with_the_stored_iterations_whatever_the_environment_says()
    {
        using var folder = new TempFolder();
        using (var db = Installation.Open(folder.Path))
        {
            KeyProtection.StoreCheck(db, Passphrase, iterations: 1_500);
            db.SaveChanges();
        }

        using var reopened = Installation.Open(folder.Path);
        var check = KeyProtection.LoadCheck(reopened);

        Assert.NotNull(check);
        Assert.Equal(1_500, check.Iterations);
        Assert.Equal(16, check.Salt.Length);
        Assert.True(check.Verify(Passphrase));
        Assert.False(check.Verify("wrong passphrase 123"));
    }

    [Fact]
    public void Two_checks_for_the_same_passphrase_have_different_salts()
    {
        var first = PassphraseCheck.Create(Passphrase, 1_000);
        var second = PassphraseCheck.Create(Passphrase, 1_000);

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void An_installation_without_a_check_has_none_to_load()
    {
        using var folder = new TempFolder();
        using var db = Installation.Open(folder.Path);

        Assert.Null(KeyProtection.LoadCheck(db));
    }

    [Fact]
    public void Deleting_a_product_that_has_a_license_is_refused()
    {
        using var folder = new TempFolder();
        using var db = Installation.Open(folder.Path);
        db.Products.Add(new Product { ProductId = "acme", DisplayName = "Acme" });
        db.Licenses.Add(new License { Reference = "ABCDEFGHJK", ProductId = "acme" });
        db.SaveChanges();

        db.Products.Remove(db.Products.Single());

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        Assert.Equal(1, db.Products.Count());
        Assert.Equal(1, db.Licenses.Count());
    }
}
