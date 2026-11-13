// PUBLIC, DEMO-ONLY KEY. It is published in this repository. Never trust it in a real product.

# Demo signing keys

These keys exist only so the sample site can show licensing working. The private keys are published here, so anyone can
sign a license with them. Never trust either public key in a real product, and never reuse these files as a pattern for
keeping a real private key.

| File | What it is |
|---|---|
| `demo.pem` | Private key of the demo signer. Signs every demo key except "Untrusted signer". |
| `demo.public.txt` | Its public line. The only key the sample product (`Acme.ContactForm`) trusts. |
| `untrusted.pem` | Private key of a second signer. Signs only the "Untrusted signer" demo key. |
| `untrusted.public.txt` | Its public line. The sample product does not trust it, so that key is shown as untrusted. |

Each file starts with the warning above as `//` comment lines. The library ignores text before the `-----BEGIN` line of
a PEM; a public line file needs its `//` lines dropped before the line is passed to the library.

## Regenerating

Regenerating replaces both pairs with new keys, so every demo key signed with the old ones becomes invalid: regenerate
the demo keys too, and update the public key constant in the sample product. The generator refuses to overwrite existing
files unless `DEMO_KEYS_OVERWRITE=1` is set. From the repository root:

```
dotnet run --project samples/Acme.ContactForm.Tests -- -explicit only -method "*DemoSigningKeyGenerator*" -showLiveOutput
```
