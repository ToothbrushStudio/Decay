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

New project `Decay.Floppy` (`net48`, `OutputType=Exe`) in `Decay.slnx`.
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
| `SHA256.HashData`, `HMACSHA256.HashData` | `DecaySequence`, `PayloadContainer` | Instance `ComputeHash`. |
| `RandomNumberGenerator.GetBytes` static | `PayloadContainer.Seal` | `RandomNumberGenerator.Create().GetBytes`. |
| `CryptographicOperations.ZeroMemory` | `PayloadContainer` | `Array.Clear`. |
| `ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIf*` | about 20 call sites | Cannot be shimmed from outside, because the real type shadows it. See decision 1. |
| `"DCAY"u8` literals, `ReadOnlySpan<byte>` | `PayloadContainer`, `DecayPng` | Compiles with `System.Memory`; verify. |
| `readonly record struct` / `record` | `PayloadContainer`, Renderer | Add an `IsExternalInit` polyfill. |
| `Span<T>`, `BinaryPrimitives`, `stackalloc Span` | many (about 23 `Span<` uses) | Provided by the `System.Memory` NuGet package. |
| `System.Text.Json`, `ECDsa` key import, `DateOnly` | `Decay.Licensing` only | **Left out of v1.** See decision 2. |

## Decisions needed before Phase 2

1. **Engine edits.** The `ThrowIf*` calls and the static hash/RNG helpers
   can only be made portable by a small mechanical edit to the shared engine
   files (for example `if (x is null) throw new ArgumentNullException(...)`,
   or an internal `Guard` helper). These edits are behavior-neutral on net10,
   and the existing tests guard them. The alternative is a build-time text
   transform, which is fragile. *Recommendation: make the edits, in their own
   commit.*
2. **Licensing.** The CLI should ship Free tier only in v1. That means a
   watermark, preset carriers only and locked intensity (see `TierPolicy`).
   Key validation needs JSON parsing and SPKI public-key import, neither of
   which exists on net48. If the floppy edition should unlock Pro, that is a
   Phase 5 add-on.
3. **Name.** `Decay.Floppy` (proposed) vs. `Decay.Cli48`.

## Phases

**Phase 0 - branch and baseline.** Done: branch exists. Run the three
existing test projects on Windows first so there is a known-good baseline.

**Phase 1 - scaffold.** `Decay.Floppy.csproj`, linked engine sources, polyfills,
add to `Decay.slnx`. Goal: it compiles with a stub `Main`. Expect the error
list to match the table above.

**Phase 2 - compat layer (`Decay.Floppy/Compat/`).** `CngAesGcm`, `ZLib`
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
- Calls go to `DecayProduct.EncryptWithPreset` (Free tier) and
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
`dotnet build Decay.Floppy -c Release`. Do not use the GUI publish command or
global `RuntimeIdentifier` properties (see CLAUDE.md). Stage `decay.exe`, the
needed `System.*.dll` files, `decay.exe.config` and a `README.TXT` into a
folder, then record the real byte total and the FAT12 cluster-rounded total
in this document. If it is tight, try ILRepack to merge the DLLs into the
exe. Only after this works on a real machine should the command go into
CLAUDE.md, which is for verified commands.

**Optional - Pro licensing,** if wanted: hand-parse the small license payload
and import the public key through `ECDsa`/`ECDsaCng` with a custom SPKI reader.

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
- **Old Windows.** CNG's GCM support makes Windows 10/11 the practical target.

## Fallback

If net48 turns out to be more trouble than it's worth, the other route is a
NativeAOT console app on .NET 10. It reuses the engine unmodified, but
the size is borderline (about 1.5-4 MB estimated) and it needs a Windows
build machine with the C++ tools. The same `Decay.Floppy` CLI code would be
reusable there.
