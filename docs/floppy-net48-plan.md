# Floppy edition: a .NET Framework 4.8 console mirror

Status: **plan only, nothing built yet.** Written on branch
`claude/floppy-net48-plan`.

## Goal

A second, separate deliverable: a bare-bones command-line Decay that fits on
a 1.44 MB floppy and runs on a stock Windows 10/11 machine with nothing
installed. The GUI (`Decay.App`, MAUI) and its release process are untouched.

Why .NET Framework 4.8: it ships in-box with Windows 10/11, so the floppy
needs no runtime. .NET 10 would need either a ~70 MB self-contained bundle or
an installed runtime. (NativeAOT on .NET 10 is the alternative; see "Fallback".)

## Size budget

A 1.44 MB FAT12 disk has 2847 data sectors x 512 B = **1,457,664 bytes**
usable, and every file rounds up to a 512 B cluster. Target: **under 1 MB**
total so there is room for a README and a second copy of the manifest.

| Piece | Expected |
|---|---|
| Engine (Core + Renderer), compiled | about 50 KB (docs/release.md: ~54 KB incl. Licensing) |
| CLI + compat shims | 20-40 KB |
| `System.Memory` + deps (`System.Buffers`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`) | about 250 KB, less if merged/trimmed |
| **Total** | **roughly 0.3-0.4 MB, unmeasured** |

These are estimates. Measure after Phase 3 and record the real numbers here.

## Approach: a mirror, not a retarget

New project `Decay.Console` (`net48`, `OutputType=Exe`) in `Decay.slnx`.
It **compiles the engine's existing source files in place** with
`<Compile Include="..\Decay.Core\**\*.cs" Exclude="..\Decay.Core\obj\**;..\Decay.Core\bin\**" />`
(and the same for `Decay.Renderer`). No copies, no fork: the net10 engine
stays the source of truth and keeps its tests. `Decay.Core`, `Decay.Renderer`
and `Decay.App` keep their current target frameworks.

Add `Microsoft.NETFramework.ReferenceAssemblies` so the project at least
restores and compiles on non-Windows machines. `LangVersion` `latest`, with
`Nullable` and `ImplicitUsings` on, matching the engine.

## What breaks on net48 (from reading the engine)

The engine has no UI or package dependencies, but it uses modern BCL APIs.
Findings from a grep of `Decay.Core`, `Decay.Renderer` and `Decay.Licensing`.
The first real compile will probably find a few more.

| Used by engine | Where | net48 answer |
|---|---|---|
| `AesGcm` | `PayloadContainer.cs` | No managed type exists. Write a small `bcrypt.dll` P/Invoke wrapper (CNG, GCM mode, 12 B nonce, 16 B tag). About 100 lines and no extra dependency. |
| `ZLibStream` | `DecayPng.cs` (read and write) | Wrapper class: 2-byte zlib header + `DeflateStream` + Adler-32 trailer on write, skip header on read. About 40 lines. |
| `Rfc2898DeriveBytes.Pbkdf2(...)` static | `PayloadContainer.DeriveKey` | Use the instance constructor with `HashAlgorithmName.SHA256` (net47+) and the same UTF-8 bytes. 600,000 iterations may be slower there; measure. |
| `SHA256.HashData`, `HMACSHA256.HashData` | `DecaySequence`, `RotDecay`, `PayloadContainer` (3 sites) | Instance `ComputeHash`. Edit needed. |
| `RandomNumberGenerator.GetBytes` static | `PayloadContainer.Seal` (2 sites) | `RandomNumberGenerator.Create().GetBytes`. Edit needed. |
| `CryptographicOperations.ZeroMemory` | `PayloadContainer` | `Array.Clear`. |
| `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrEmpty`, `ArgumentOutOfRangeException.ThrowIf*` | about 50 call sites in 8 files (31 `ThrowIfNull`, 5 `ThrowIfNullOrEmpty`, 14 range checks) | New static methods on existing types cannot be added from outside. See decision 1. |
| `Math.Clamp` | 7 sites (`CarrierPresets` 3, `RotDecay` 4) | Same problem: `Math` is a static class. Edit needed. |
| `Array.Fill` | 1 site (`RotDecay`) | Edit needed. |
| `Stream.Write(ReadOnlySpan<byte>)` | `DecayPng` (about 5 sites) | An extension method in the compat layer works; no engine edit. |
| `[.. "..."u8, .. container]` collection spread | 1 site (`RotDecay`) | May compile with `System.Memory`; otherwise one edit. Verify. |
| `required` and `init` members | `DecayOp`, `RotDecay` | Polyfill the compiler attributes (`IsExternalInit`, `RequiredMemberAttribute`, `CompilerFeatureRequiredAttribute`); no engine edit. |
| `"DCAY"u8` literals, `ReadOnlySpan<byte>` | `PayloadContainer`, `DecayPng` | Compiles with `System.Memory`; verify. |
| `readonly record struct` / `record` | `PayloadContainer`, Renderer | Add an `IsExternalInit` polyfill. |
| `Span<T>`, `BinaryPrimitives`, `stackalloc Span` | many (about 23 `Span<` uses) | Provided by the `System.Memory` NuGet package. |
| `System.Text.Json`, `ImportSubjectPublicKeyInfo`, `DateOnly` | `Decay.Licensing` only | Not linked. Replaced by a small verify-only mirror, `Decay.Console/Licensing/`. See "Licensing" below. |

## Decisions needed before Phase 2

1. **Engine edits.** Missing *types* (`AesGcm`, `ZLibStream`,
   `CryptographicOperations`, the compiler attributes) are defined in the
   compat layer under their real namespaces, with no engine edit. Missing
   *static methods on existing types* cannot be, so these engine lines
   change, all one-line and behavior-neutral on net10:
   - about 50 argument guards, to plain `if (x is null) throw new ArgumentNullException(nameof(x));`
     (and the equivalent for range and empty-string checks);
   - 7 `Math.Clamp`, 1 `Array.Fill`, 3 `HashData`, 2 `RandomNumberGenerator.GetBytes`,
     1 `Rfc2898DeriveBytes.Pbkdf2`, and possibly 1 collection-spread;
   - roughly 65 lines in about 12 files. They should land in their own commit,
     guarded by the existing net10 test projects. Check that no test asserts
     on exact exception messages.
   *Recommendation: make the edits.* A trick with `global using` aliases that
   shadow `SHA256`, `Math` etc. could avoid some of them, but it is invisible
   magic and unverified, and `Math` and `Array` cannot be shadowed that way.
2. **Licensing.** Proposed: support offline keys in v1 through a verify-only
   wrapper (see "Licensing" below). If you'd rather ship Free-only first,
   skip that section; the rest of the plan doesn't depend on it.
3. **Name.** Decided: the project is `Decay.Console`. Set `RootNamespace` to
   `Decay.Cli` and `AssemblyName` to `decay`. A namespace called `Decay.Console`
   would shadow `System.Console` for every file under `namespace Decay.*`,
   including the linked engine files, and break `Console.WriteLine`.

## Phases

**Phase 0 - branch and baseline.** Done: branch exists. Run the three
existing test projects on Windows first so there is a known-good baseline.

**Phase 1 - scaffold.** `Decay.Console.csproj` (`AssemblyName` `decay`, `RootNamespace` `Decay.Cli`), linked engine sources, polyfills,
add to `Decay.slnx`. Goal: it compiles with a stub `Main`. Expect the error
list to match the table above.

**Phase 2 - compat layer (`Decay.Console/Compat/`).** `CngAesGcm`, `ZLib`
wrapper, `IsExternalInit`, and the helpers behind the engine edits from
decision 1.

**Phase 3 - CLI.** Same pipeline the GUI pages call, with no UI:

```
decay encrypt --text "..."|--in msg.txt --out letter.png [--preset N] [--style glitch|rot]
decay decrypt letter.png [--carrier-out recovered.png]
decay presets
decay selftest
```

- Passphrase is read from a no-echo console prompt, not from the command
  line. A `--pass` flag could be added for scripting, with a warning.
- Calls go to `DecayProduct.EncryptWithPreset` (tier from the license, Free if none) and
  `DecayArtist.Decrypt`.
- Custom carriers are PNG only (no ImageSharp). Anything else is rejected
  with a clear message.
- No animated reveal. Decrypt prints the message and can write the recovered carrier.

**Phase 4 - cross-compatibility.** This is the part that matters most. A
letter made by the GUI/net10 engine must open in the floppy build, and
the reverse.

- Add a net10 test (or small tool) that emits golden vectors: a sealed
  container, a PNG, and the expected plaintext.
- `decay selftest` embeds those vectors and verifies them on net48.
  Run it on a Windows machine.
- Compare *decoded pixels*, not PNG bytes. net48's `DeflateStream`
  compresses differently from net10's zlib, so the IDAT bytes will differ
  while the image stays identical. Check nothing in the engine depends on
  compressed size.

**Phase 5 - packaging and measurement.** Build with
`dotnet build Decay.Console -c Release`. Do not use the GUI publish command or
global `RuntimeIdentifier` properties (see CLAUDE.md). Stage `decay.exe`, the
needed `System.*.dll` files, `decay.exe.config` and a `README.TXT` into a
folder, then record the real byte total and the FAT12 cluster-rounded total
in this document. If it is tight, try ILRepack to merge the DLLs into the
exe. Only after this works on a real machine should the command go into
CLAUDE.md, which is for verified commands.

## Licensing (offline keys)

Yes, this can be wrapped. The key format is already self-contained:
`DECAY1.<base64url JSON {n,t,d}>.<base64url ECDSA P-256 signature>`, checked
against the publisher public key. No server is involved, and the console
build needs only the verify half. `Sign` stays in `tools/Decay.KeyGen` on
net10.

`Decay.Licensing/LicenseKey.cs` is not linked, because it uses three things
net48 lacks. The mirror (`Decay.Console/Licensing/LicenseValidator.cs`,
about 60 lines) replaces them:

| net10 code | net48 replacement |
|---|---|
| `ECDsa.ImportSubjectPublicKeyInfo` | The P-256 public key is a fixed 91-byte SPKI: a 26-byte header, then `04 \|\| X \|\| Y`. Check the header, slice X and Y, and call `ECDsa.Create(new ECParameters { Curve = nistP256, Q = ... })` (net47+). Signatures are IEEE P1363 `r\|\|s` on both sides. |
| `System.Text.Json` for `{n,t,d}` | `JavaScriptSerializer` from `System.Web.Extensions`, which is in-box on net48 and adds no file to the floppy. |
| `DateOnly` | `DateTime.ParseExact(d, "yyyy-MM-dd")`, wrapped in a small local license type. |

- `DecayTier` and `TierPolicy` come from the linked Renderer, so **the Pro
  gates (custom carrier, rot style, intensity, watermark) come for free.**
  `DecayProduct` already takes a tier.
- `PublisherKey.SubjectPublicKeyInfoBase64` is a plain constant in
  `Decay.App/PublisherKey.cs`. Link that one file; no copy to keep in sync.
  An empty key still means Free-only, as in the GUI.
- Same safety property as the GUI: decryption never needs a license.

Where the key lives (checked in this order):
1. `--license <key>` on the command line
2. `DECAY_LICENSE` environment variable
3. `license.key` next to `decay.exe`, so a key can travel on the floppy
4. `%APPDATA%\Decay\license.key`

`decay activate <key>` validates, then saves to #4, or to #3 with `--portable`.
`decay license` prints the licensee and tier. `decay deactivate` deletes the file.

Verification: add known-good keys signed by the real net10 `KeyGen` to
`selftest` (and a tampered, a truncated and a wrong-key case that must be
rejected). They must validate identically on net48 and net10.

## Risks

- **Format drift.** Any difference in the AES-GCM, PBKDF2 or bit-embedding
  path makes letters unreadable across editions. Phase 4 is the safeguard
  and should gate any release.
- **Windows-only to run.** Building may work elsewhere, but running and
  `selftest` need Windows (Mono is a possible stand-in and has not been
  checked). This planning session's container has no `dotnet` or `mono`, so
  none of this has been compiled yet.
- **Slower KDF.** 600,000 PBKDF2 iterations on the net48 path may take
  noticeably longer than on net10.
- **Licensing mirror drift.** The mirror duplicates the verify logic. The
  shared test keys catch any divergence.
- **Old Windows.** CNG's GCM support makes Windows 10/11 the practical target.

## Fallback

If net48 turns out to be more trouble than it's worth, the other route is a
NativeAOT console app on .NET 10. It reuses the engine unmodified, but
the size is borderline (about 1.5-4 MB estimated) and it needs a Windows
build machine with the C++ tools. The same `Decay.Console` CLI code would be
reusable there.
