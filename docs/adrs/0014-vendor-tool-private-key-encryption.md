# ADR-0014: Vendor tool private-key encryption

- Status: Decided
- Date: 2026-10-28
- Source: `add-vendor-tool` design.md Q21

## Context

PDR-0032: private keys encrypted with a passphrase, in a standard format, portable by copying,
usable in scripts. The library reads only unencrypted PKCS#8 PEM (`SigningPrivateKey.FromPem`,
ADR-0001 left encryption to tooling).

## Decision

- **Format.** Encrypted PKCS#8 PEM (`ENCRYPTED PRIVATE KEY`), PBES2 with AES-256-CBC and
  PBKDF2-HMAC-SHA256, 600,000 iterations. Built with the BCL
  (`ECDsa.ExportEncryptedPkcs8PrivateKeyPem`, `ImportFromEncryptedPem`). Stored in the database.
- **Create.** `SigningKeyPair.Create()` → `ExportPem()` → import into `ECDsa` → export encrypted.
- **Use.** Decrypt into `ECDsa` → export PKCS#8 PEM → `SigningPrivateKey.FromPem`; disposed when
  the command ends.
- **Passphrase check.** PBKDF2-HMAC-SHA256 hash, 16-byte random salt, same iteration count, kept
  in settings; compared with `CryptographicOperations.FixedTimeEquals` before any key is created
  or opened.
- **Iterations** stored in each PEM and in the check, so tests can run with a low count passed
  through `ToolEnvironment`.

## Alternatives Considered

| Option | Why not |
|---|---|
| Hand-rolled AES-GCM with a custom envelope | Non-standard; keys locked into the tool |
| OS key stores (DPAPI, Keychain, libsecret) | Not portable by copying; different per OS |
| Argon2 key derivation | Not in the BCL; a dependency |
| Library change to accept `ECDsa` (`pre-1.0-considerations.md` C2) | Not needed for this change; would avoid the plain PEM string |

## Consequences

- The decrypted PEM exists briefly as a .NET string that cannot be zeroed.
- About half a second of key derivation per command that signs.
- Keys open with `openssl pkey` and the passphrase.

## Reversal Cost

Medium: re-encrypting stored keys needs the passphrase, so a format change runs as a migration
at the vendor's next signing command.
