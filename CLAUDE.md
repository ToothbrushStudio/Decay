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
