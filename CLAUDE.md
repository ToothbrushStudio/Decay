# CLAUDE.md

## Building the Windows release (verified)

```bash
dotnet publish Decay.App -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o publish
```

This produces the unpackaged build that is zipped for itch.io (zip the
contents of `publish`, name it `Decay-<version>-win-x64.zip`). It is the
command that was run and confirmed working. Delete `publish` first: publish
adds to an existing folder without removing old files.

The command bundles the Windows App SDK; the .NET runtime is bundled by
`SelfContained` and `RuntimeIdentifier` (win-x64) in the Windows-only
`PropertyGroup` of `Decay.App.csproj`, added after a publish without them
turned out to need .NET installed. Before zipping, `publish` must contain
`coreclr.dll` and `hostfxr.dll`.

Do not add `-r win-x64`, `-p:RuntimeIdentifier=...` or `-p:SelfContained=true`
on the command line. Global properties reach every target framework in
`Decay.App.csproj`, including Android, iOS and Mac Catalyst, and restore fails
with `NU1102: Unable to find package Microsoft.NETCore.App.Runtime.Mono.win-x64`.
That is why those two settings live in the project file instead.

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
`Decay.Cli` is the check. It is built and tested on Linux with Mono, and the
CI workflow's Windows job runs `decay selftest` on .NET Framework 4.8 on
every push. The self-test must end with "0 failed"; license checks can SKIP
only under Mono (on Windows a missing ECDSA is a failure). Plan, results and
what is unverified: `docs/cli-net48-plan.md`.

## Frozen formats

The payload format (`docs/payload-format-v1.md`) and both render pipelines,
Glitch (`docs/render-pipeline-v1.md`) and Rot
(`docs/render-pipeline-v2-rot.md`), are frozen. `FrozenFormatTests` and
`FrozenPipelineTests` hold their golden vectors. If one fails, revert the
change that broke it; never update an expected value. New behavior gets a
new format version or pipeline id.

Cross-checks against the net10 engine (`tools/Decay.Cli.Compat`):
`licensetest`, `storetest`, `make`/`check`, and `vectors <file>` (regenerates
`Decay.Cli/Selftest/Vectors.g.cs`). Build by project path as above; do not
publish the solution with global properties.

