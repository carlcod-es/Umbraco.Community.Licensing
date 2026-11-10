# Umbraco Licensing

Offline license keys for paid Umbraco packages: one library to issue keys, check them inside a
product, and manage the signing keys behind them.

> **Beta: no stability before 1.0.** Any 0.x release may change the key format, the public API,
> or both. A key issued under a beta may read as `NotSupported` or `Unreadable` under a later
> version. **Do not issue beta keys to paying customers. Reissue every key when moving to 1.0.**
> From 1.0 the key format is permanent.
> ([PDR-0024](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0024-beta-without-stability-promise.md))

Runs on Windows, Linux and macOS. Each release is tested on one of them; all three will be checked before 1.0.
([PDR-0028](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/decisions/0028-test-coverage-per-release.md))
Every release and what changed:
[`RELEASES.md`](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/RELEASES.md).

## What it does

- **Offline.** A key is checked entirely on the site: no licensing server, no network call.
- **Signed.** A key cannot be edited or forged without the vendor's private signing key.
- **One key per product.** Each key names the product it unlocks; add-ons, upgrades and renewals
  are reissues of that key.
- **Features.** A key carries switches (`ecommerce`), numbers (`max-orders: 2500`) and text
  (`licensed-domains`); the product decides what they mean.
- **Same answers everywhere.** Every product reports the same states (`Valid`, `Expired`,
  `Missing`, ...) with the same first action, and checking a key never throws.
- **Pasteable keys.** One line of plain text, safe in an email or an environment variable, with
  a readable identifier such as `LIC-8F3AK-M7RXB-7Q2D` for support.
- **Signing key rotation.** Trust a new public key alongside the old one; existing keys keep
  working.
- **Vendor tool.** `ucl` issues and keeps records of keys from a terminal menu
  ([guide](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/vendor-tool.md)).

No Umbraco or host dependency: the package is named for its audience, Umbraco package vendors,
and uses only the .NET base class library.

## Documentation

| Page | Contents |
|---|---|
| [How it works](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/how-it-works.md) | The flow, the key string, features, states, limits of offline licensing |
| [Vendor guide](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/vendor-guide.md) | Code to create signing keys, issue, check, reissue and rotate; field rules; vendor notes |
| [Vendor tool](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/vendor-tool.md) | Installing and using `ucl`, the vendor tool |
| [Implementor guide](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/implementor-guide.md) | Installing keys and acting on each state |
| [Key format](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/key-format.md) | The key string byte for byte, with a decoded example |
| [License examples](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/license-examples.md) | Key schema and worked examples with real key strings |
| [Personas](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/personas.md) | Who the library serves, in priority order |
| [Decisions (PDR)](https://github.com/carlcod-es/Umbraco.Community.Licensing/tree/main/docs/decisions) | Product decision records |
| [Architecture (ADR)](https://github.com/carlcod-es/Umbraco.Community.Licensing/tree/main/docs/adrs) | Architecture decision records |
| [Releasing](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/docs/releasing.md) | How a release is set up, rehearsed, started and recovered |
| [`openspec/`](https://github.com/carlcod-es/Umbraco.Community.Licensing/tree/main/openspec) | Specifications and changes (source of truth for behaviour) |

## License

MIT. See [`LICENSE`](https://github.com/carlcod-es/Umbraco.Community.Licensing/blob/main/LICENSE).

Icon based on Document Lock by NurJannah from [Noun Project](https://thenounproject.com/browse/icons/term/document-lock/), licensed under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). The icon is not covered by the MIT licence.
