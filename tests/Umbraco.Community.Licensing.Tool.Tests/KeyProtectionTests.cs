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
