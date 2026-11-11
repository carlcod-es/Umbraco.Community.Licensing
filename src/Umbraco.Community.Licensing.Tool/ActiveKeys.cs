using Microsoft.EntityFrameworkCore;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool;

/// <summary>Finds the signing key an action signs with: the active key of the product, or the shared active key (vendor-tool-signing-keys).</summary>
internal static class ActiveKeys
{
    internal static string Damaged(string what) =>
        $"{what}, so the installation looks damaged. Restore a backup into another folder.";

    /// <summary>
    /// The one active key for <paramref name="productId"/>: in per-product mode the key whose product is that product, in shared
    /// mode the key with no product. Zero or several active keys fail as a damaged installation; one is never picked.
    /// </summary>
    public static SigningKeyRecord Find(Session session, string productId)
    {
        var (perProduct, _) = ProductActions.ReadKeyMode(session);
        using var db = session.OpenDb();
        var active = db.SigningKeys.AsNoTracking().Where(k => k.Role == KeyRole.Active);
        var matches = (perProduct ? active.Where(k => k.ProductId == productId) : active.Where(k => k.ProductId == null)).ToList();

        if (matches.Count == 1)
            return matches[0];

        var scope = perProduct ? $"the product {productId}" : "the shared signing key";
        throw new InstallationRefusedException(Damaged(
            matches.Count == 0 ? $"There is no active signing key for {scope}" : $"There is more than one active signing key for {scope}"));
    }

    /// <summary>
    /// The active key and the standby key (if any) of one scope among <paramref name="keys"/>, which hold no replaced keys: the
    /// product's keys in per-product mode, the keys with no product in shared mode. No active key, or more than one active or
    /// standby key, fails as a damaged installation; one is never picked.
    /// </summary>
    internal static (SigningKeyRecord Active, SigningKeyRecord? Standby) Roles(IEnumerable<SigningKeyRecord> keys, string? productId)
    {
        var scope = productId is null ? "the shared signing key" : $"the product {productId}";
        var inScope = keys.Where(k => k.ProductId == productId).ToList();
        var active = inScope.Where(k => k.Role == KeyRole.Active).ToList();
        var standby = inScope.Where(k => k.Role == KeyRole.Standby).ToList();

        if (active.Count != 1)
            throw new InstallationRefusedException(Damaged(
                active.Count == 0 ? $"There is no active signing key for {scope}" : $"There is more than one active signing key for {scope}"));
        if (standby.Count > 1)
            throw new InstallationRefusedException(Damaged($"There is more than one standby signing key for {scope}"));

        return (active[0], standby.SingleOrDefault());
    }
}
