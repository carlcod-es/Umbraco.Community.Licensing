using Umbraco.Community.Licensing.Core.Tests.Support;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;

namespace Umbraco.Community.Licensing.Core.Tests.Signing;

public class RotationTests
{
    private static LicenseEvaluator EvaluatorFor(TrustedSigningKeys trusted) =>
        new(Product, trusted, new FixedTimeProvider(Later));

    [Fact]
    public void OldAndNewTrustedTogether_LicensesFromEitherKey_AreValid()
    {
        using var oldPair = SigningKeyPair.Create();
        using var newPair = SigningKeyPair.Create();
        var evaluator = EvaluatorFor(TrustedSigningKeys.Create(oldPair.PublicKey, newPair.PublicKey));

        var fromOld = evaluator.Evaluate(Issue(key: oldPair.PrivateKey));
        var fromNew = evaluator.Evaluate(Issue(key: newPair.PrivateKey));

        Assert.Equal(LicenseState.Valid, fromOld.State);
        Assert.Equal(LicenseState.Valid, fromNew.State);
    }

    [Fact]
    public void PerpetualKeyAfterRoutineRotation_SignedByOldKey_IsValid()
    {
        using var oldPair = SigningKeyPair.Create();
        using var newPair = SigningKeyPair.Create();
        var perpetual = Issue(Request(expiry: LicenseExpiry.Perpetual), key: oldPair.PrivateKey);

        // Rotation: trust the new key alongside the old and never withdraw the old.
        var trusted = TrustedSigningKeys.Create(oldPair.PublicKey).With(newPair.PublicKey);
        var evaluator = new LicenseEvaluator(Product, trusted, new FixedTimeProvider(new DateTimeOffset(2060, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(LicenseState.Valid, evaluator.Evaluate(perpetual).State);
    }

    [Fact]
    public void OldKeyWithdrawn_LicenseSignedByIt_IsSigningKeyNotRecognised()
    {
        using var oldPair = SigningKeyPair.Create();
        using var newPair = SigningKeyPair.Create();
        var fromOld = Issue(key: oldPair.PrivateKey);
        var fromNew = Issue(key: newPair.PrivateKey);
        var trusted = TrustedSigningKeys.Create(oldPair.PublicKey, newPair.PublicKey).Without(oldPair.SigningKeyId);
        var evaluator = EvaluatorFor(trusted);

        Assert.Equal(LicenseState.SigningKeyNotRecognised, evaluator.Evaluate(fromOld).State);
        Assert.Equal(LicenseState.Valid, evaluator.Evaluate(fromNew).State);
    }

    [Fact]
    public void ValidationNeedsOnlyPublicKeys_EvaluatorBuiltFromParsedExports_IsValid()
    {
        string exported;
        string key;
        using (var pair = SigningKeyPair.Create())
        {
            exported = pair.PublicKey.Export();
            key = Issue(key: pair.PrivateKey);
        }

        // Private key is disposed; only the exported public text remains.
        var evaluator = EvaluatorFor(TrustedSigningKeys.Create(SigningPublicKey.Parse(exported)));

        Assert.Equal(LicenseState.Valid, evaluator.Evaluate(key).State);
    }
}
