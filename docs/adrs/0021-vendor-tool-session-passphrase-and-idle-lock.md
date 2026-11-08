# ADR-0021: Vendor tool session passphrase and idle lock

- Status: Amended by ADR-0022
- Date: 2026-11-08
- Source: `add-vendor-tool` design.md Q60, Q64. Amends ADR-0014.

## Context

PDR-0049 keeps the passphrase for the session after the first action that signs or creates a
key, forgets it and every decrypted private key at Quit or when the idle lock fires, and allows
three tries per action. The idle time is set at setup (1 to 1440 minutes, or `never`) and a
change takes effect from the next session. ADR-0014 decrypts a key per command and disposes it
when the command ends; it never held a secret across actions.

## Decision

- **What is held.** The passphrase only, as its ASCII bytes in a pinned array
  (`GC.AllocateArray<byte>(n, pinned: true)`), owned by one `SessionLock` object. No decrypted
  private key outlives the action that uses it: each signing decrypts the stored PEM with the
  held bytes, signs, and disposes the `ECDsa` and the library key before the action returns.
- **Byte-only passphrase path.** The hidden prompt reads key presses into a pinned `char`
  buffer of 257, counting further presses without storing them (so "too long" is still
  detected), checks the passphrase rule at setup (ADR-0019), then copies to the pinned byte
  array and zeroes the `char` buffer. The passphrase check uses `Rfc2898DeriveBytes.Pbkdf2`
  over the bytes, encryption uses `ExportEncryptedPkcs8PrivateKeyPem(ReadOnlySpan<byte>, ...)`,
  decryption `ImportFromEncryptedPem(ReadOnlySpan<char>, ReadOnlySpan<byte>)`. The passphrase
  never becomes a `string`. For PBES2 the BCL encodes a `char` password as UTF-8, so for ASCII
  the bytes are identical and keys keep opening with `openssl pkey` (ADR-0014).
- **Setup** holds the passphrase it set, in both signing-key modes, from the end of setup
  (PDR-0050); the idle lock applies from then.
- **Unlocking.** An action that signs or creates a key asks the lock for the passphrase. When
  none is held, the hidden prompt asks; the answer is checked against the stored passphrase
  check (ADR-0014) before it is held. A wrong answer is said to be wrong and asked again, up to
  three tries per action; after the third the action is cancelled (ADR-0020). Earlier answers of
  the action are kept, because the passphrase is asked after the action's other questions
  (after the summary, where there is one), just before signing or creating a key.
- **Idle lock.** The idle time is read from `Settings` when the session opens, so a change
  applies from the next session. Every completed answer (a line or the hidden prompt) records
  its time from `TimeProvider` and restarts a timer made with `TimeProvider.CreateTimer`. When the
  timer fires, the lock zeroes the bytes (`CryptographicOperations.ZeroMemory`) and drops them.
  Each request for the passphrase also compares the time since the last answer with the idle
  time and locks first when it has passed, so a late timer never hands out a passphrase. A lock
  object guards the bytes, since the timer runs on another thread while the main thread waits at
  a question. `never`: no timer and no comparison.
- **In progress.** A lock that fires during an action costs nothing until the action needs the
  passphrase; it is then asked again at that point, the action's answers kept.
- **End.** Quit, end of input and the end of `ToolApp.Run` zero and drop the bytes (a `finally`
  in the session). A process killed by the OS or by Ctrl+C is not zeroed; its memory goes with
  the process.
- **Cost.** One passphrase check (600,000 PBKDF2 iterations) per unlock, plus one decryption of
  the same cost per action that signs. Creating a key costs one encryption.
- **Tests** inject a fake `TimeProvider` written in the test project (overriding `GetUtcNow` and
  `CreateTimer`; no package), and the scripted console advances it between answers.

## Alternatives Considered

| Option | Why not |
|---|---|
| Hold the decrypted private keys as well, keyed by signing key | Saves one decryption (about half a second) per signing action. The passphrase must still be held to encrypt new keys (product add, standby, switch), so holding keys adds exposure and gains only time |
| Hold decrypted keys instead of the passphrase | Creating a key in the same session would ask again, which PDR-0049 rules out |
| Check the idle time only when the passphrase is next needed, no timer | The passphrase stays in memory while the terminal is unattended, which is what the idle lock is for |
| Passphrase as a `string` | Cannot be zeroed; may be copied by the runtime |
| `ProtectedMemory`, DPAPI or the OS keychain for the held passphrase | Windows only, or differs per OS and adds a dependency (PDR-0038) |
| `Microsoft.Extensions.TimeProvider.Testing` for the fake clock | A test dependency for a class of about 40 lines |
| A background thread polling the clock | A timer from `TimeProvider` does the same, and is the one a fake clock can drive |

## Consequences

- For as long as the session is unlocked, the passphrase is in process memory; a person at the
  unattended terminal, or a memory dump, can use it. The idle lock bounds this; `never` does not
  (PDR-0049).
- The decrypted PEM still exists briefly as a .NET string during each signing (ADR-0014,
  `pre-1.0-considerations.md` C2).
- A signing action takes about half a second more than holding decrypted keys would.
- The timer is the tool's only second thread; everything it touches is behind the lock.

## Reversal Cost

Low: holding, timing and clearing sit in `SessionLock`; actions only ask it for the passphrase.
Caching decrypted keys later is a change inside it.
