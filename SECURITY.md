# Security policy

Decay encrypts private messages, so security reports come first.

## Reporting a vulnerability

Please report privately, not in a public issue:

- On GitHub, use **Report a vulnerability** on the repository's
  **Security** tab, or
- email **support@toothbrush.studio** with "Security" in the subject.

Say what you found, how to reproduce it, and which version you used (the
app's Help tab, or `decay version`). You'll get a reply within a few days.
We'll agree a fix and a disclosure date with you, and credit you unless
you'd rather not be named.

## In scope

- The payload format and its cryptography (AES-256-GCM,
  PBKDF2-HMAC-SHA256): [docs/payload-format-v1.md](docs/payload-format-v1.md)
- Anything that could reveal a message or passphrase, or send data off the
  device
- The render pipelines: [Glitch](docs/render-pipeline-v1.md) and
  [Rot](docs/render-pipeline-v2-rot.md)
- License-key verification (`Decay.Licensing`, `Decay.Cli/Licensing`)
- Crashes or hangs caused by a crafted image or file

## By design, not vulnerabilities

- A wrong passphrase and a modified image look the same: AES-GCM can't
  tell them apart.
- Images are fragile on purpose: re-compression or resizing destroys the
  message.
- The message length is visible in the image (AES-GCM is unpadded).
- License keys are checked offline, so anyone building from source can
  remove the check.
- The Rot stains are decoration. Their colors are masked with a keystream
  from a non-cryptographic generator; the message's confidentiality rests
  on AES-GCM alone.

## The format is frozen

A fix never changes how existing images decode. A flaw in the format
itself is fixed with a new format version, and every past version keeps
decoding.
