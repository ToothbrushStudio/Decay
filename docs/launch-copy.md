# Decay — Launch Copy

Working drafts for store listings and launch posts. Voice: quiet confidence,
a little gothic, zero crypto-bro. The product is a *feeling* — receiving
something delicate — before it is a cipher.

## Taglines (pick one, keep the rest for A/B)

- **Encrypt your message into the rot.**
- The glitch is the letter.
- Ruined on purpose. Readable by one person.
- Send something delicate.

*Note: v1 ships on Windows through itch.io, with the page copy below. The
App Store / Play sections after it are for when mobile ships.*

## itch.io page

One page, **Paid** at the Pro price, with the Windows zip as the free "demo"
file (docs/itch-release.md, step 5). The copy has to say that the free
download is the full Free app, since itch labels it a demo.

**Short description:** Encrypt messages into glitch art. Only the passphrase
un-rots them.

**Tags:** encryption, glitch-art, steganography, privacy, cipher,
secret-message, datamosh, vaporwave, aesthetic, art

**Description** (make the one-line headings bold in itch's editor):

> Decay turns private messages into glitch art.
>
> Write a letter, choose a passphrase, and watch the image rot: every tear,
> block-swap and burst of static is generated from the encrypted message
> itself. The corruption isn't decoration. It is your words, sealed with
> AES-256.
>
> Send the image to one person. When they enter the passphrase, the decay
> runs backwards: the rot drains away, the original artwork resurfaces, and
> your message appears. Anyone else just sees a beautiful ruin.
>
> **Free download**
> The free download (listed as the demo) opens every Decay image, Glitch or
> Rot, and makes Glitch images on the TV Static carrier, with a watermark.
> Decrypting is free, forever, for everyone.
>
> **Decay Pro**
> Your Pro key appears on the download page after you buy. Paste it into the
> License tab to unlock all five carrier artworks, Rot mode (keeps the image
> recognizable and stains it instead), your own images as carriers, the
> intensity dial, and watermark-free exports. The key works offline, with no
> account or activation.
>
> **Send the file, never a screenshot**
> A Decay image only survives as the original PNG. Screenshots, reposts and
> chat-app compression destroy the message. Share it through Google Drive,
> OneDrive, iCloud Drive or as an email attachment. The send guide in the app
> shows how.
>
> No accounts. No servers. Nothing leaves your device. The format is frozen
> and versioned, so every image you make will still open years from now.
>
> Windows 10 or 11, 64-bit. Extract the zip first (right-click, Extract All),
> then run Decay.App.exe. Nothing to install.
>
> Questions, problems or refunds: support@toothbrush.studio

## App Store / Play — short description (≤80 chars)

> Encrypt messages into glitch art. Only the passphrase un-rots them.

## App Store / Play — long description

> **Decay turns private messages into glitch art.**
>
> Write a letter, choose a passphrase, and watch the image rot: every tear,
> block-swap, and burst of static is generated from the encrypted message
> itself. The corruption isn't decoration — it *is* your words, sealed with
> AES-256.
>
> Send the image to one person. When they enter the passphrase, the decay
> runs backwards: the rot drains away, the original artwork resurfaces, and
> your message appears. Anyone else just sees a beautiful ruin.
>
> **Delicate on purpose.** A Decay image lives only as the original file.
> Screenshots, reposts, and platform compression destroy the message
> forever — so receiving one intact means someone took care.
>
> **Send it as a file.** Share the image through Google Drive, OneDrive,
> iCloud Drive, or an email attachment. Don't post it to social sites or
> send it as a chat photo, since those re-compress it.
>
> — No accounts. No servers. Nothing leaves your device.
> — Decrypting is free, forever, for everyone.
> — Every image you make will still open years from now: the format is
>   frozen and versioned.
>
> **Decay Pro** unlocks all five carrier artworks, Rot mode, your own
> images as carriers, the decay intensity dial, and watermark-free exports.

## Store keywords

encryption, glitch art, secret message, private, steganography, cipher,
letters, aesthetic, vaporwave, datamosh, secure notes, art

## Launch post (X / Bluesky / Mastodon, with hero + reveal images)

> This image is an encrypted letter.
>
> The glitch isn't hiding the message — the glitch IS the message. Enter the
> passphrase and the rot runs backwards until the words surface.
>
> Screenshot it and it dies forever. Send the file. Take care of it.
>
> (The image in this post is a preview — posting re-compresses it. The real
> file travels by cloud-drive link, email, or AirDrop.)
>
> Decay — encrypt your message into the rot. [link]

## Launch post (longer, for Reddit r/SideProject etc.)

> I built an app that turns encrypted messages into glitch art.
>
> Every visual artifact in the image is driven by the ciphertext of the
> message inside — 120 invertible pixel operations seeded from the encrypted
> payload. Because every operation is reversible, the receiver's app can run
> the whole decay *backwards*: enter the passphrase and you watch the
> corruption drain out until the original artwork and the message surface.
>
> Design decisions I'm proud of:
> - The image + passphrase is everything. No accounts, no servers. The
>   engine is ~66 KB with zero dependencies — it would fit on a floppy.
> - Decryption is free forever, for everyone. Messages can't be held hostage.
> - It's fragile on purpose: screenshots and re-compression kill the message.
>   You have to send the actual file, like handing someone a real letter.
> - The format is frozen and versioned — anything encrypted today opens in
>   any future version.
>
> Windows first (itch.io); built on .NET MAUI and C#, so other platforms are
> possible later. To send one, upload the file to Google Drive, OneDrive, or
> iCloud Drive and share the link. The image posted here is only a preview.

## Press boilerplate

> Decay is a Toothbrush Studio app that encrypts private messages into
> glitch art. Each image's corruption is generated from the encrypted
> message itself and reverses only for the passphrase holder, revealing the
> original artwork as the message surfaces. Decay stores nothing, phones
> home to nothing, and its decoder is free forever.

## Open launch items (not copy)

- [x] First device build of Decay.App (Windows for v1; other platforms later)
- [x] Run KeyGen; fill PublisherKey.cs; store private key offline
- [x] Choose a source license for the repo: all rights reserved (`LICENSE`)
- [ ] App Store export-compliance answers (see release.md)
- [ ] Screenshots/screen-recordings of the reveal animation from a real
      device for the store listings (the un-rot is the whole pitch — video
      will sell it better than stills)
