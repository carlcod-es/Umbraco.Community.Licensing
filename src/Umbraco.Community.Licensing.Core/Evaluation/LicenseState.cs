namespace Umbraco.Community.Licensing.Core;

/// <summary>
/// What an evaluation observed, in the order the checks run: the first failing check sets the state. Names describe
/// the observation, never a presumed cause. Only <see cref="Valid"/> means licensed.
/// </summary>
public enum LicenseState
{
    /// <summary>No key string was supplied, or it was empty or whitespace only.</summary>
    Missing,

    /// <summary>The key string could not be read as a key.</summary>
    Unreadable,

    /// <summary>The key claims another product.</summary>
    WrongProduct,

    /// <summary>The key names a signing key that is not in the trusted set.</summary>
    SigningKeyNotRecognised,

    /// <summary>The signature does not verify.</summary>
    NotVerified,

    /// <summary>The signature verifies but the contents cannot be read by this product.</summary>
    NotSupported,

    /// <summary>Verified, but past its expiry.</summary>
    Expired,

    /// <summary>Verified and not past its expiry.</summary>
    Valid,
}
