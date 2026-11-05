# CLAUDE.md

## Building the Windows release (verified)

```bash
dotnet publish Decay.App -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o publish
```

This produces the unpackaged, self-contained build that is zipped for itch.io
(zip the contents of `publish`, name it `Decay-<version>-win-x64.zip`). It is
the command that was run and confirmed working.

Do not add `-r win-x64`, `-p:RuntimeIdentifier=...` or `-p:SelfContained=true`
on the command line. Global properties reach every target framework in
`Decay.App.csproj`, including Android, iOS and Mac Catalyst, and restore fails
with `NU1102: Unable to find package Microsoft.NETCore.App.Runtime.Mono.win-x64`.

Full release steps (version bump, signing, hash, itch.io): `docs/itch-release.md`
and `docs/release.md`. Never commit the publisher private key (`*.pem`) or key
files such as `itch-keys.txt`.

## Console edition (.NET Framework 4.8, floppy-sized)

```bash
dotnet build Decay.Cli -c Release -t:PackCli
```

Builds `decay.exe` and copies the floppy files (about 400 KB) to
`Decay.Cli/bin/cli`. It compiles the `Decay.Core` and `Decay.Renderer`
sources in place for `net48`, so **keep .NET-Core-only APIs out of those two
projects** (static `HashData`/`GetBytes`/`Pbkdf2`, `Math.Clamp`,
`ThrowIfNull`, `Array.Fill`, `ulong.RotateLeft`, and so on); building
`Decay.Cli` is the check. It is built and tested on Linux with Mono; it has
not yet been run on Windows. On Windows, run `decay selftest` first and make
sure nothing prints SKIP. Plan, results and what is unverified:
`docs/cli-net48-plan.md`.

Cross-checks against the net10 engine (`tools/Decay.Cli.Compat`):
`licensetest`, `storetest`, `make`/`check`, and `vectors <file>` (regenerates
`Decay.Cli/Selftest/Vectors.g.cs`). Build by project path as above; do not
publish the solution with global properties.

