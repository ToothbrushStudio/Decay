DECAY - encrypt your message into the rot
Console edition, for Windows 10 / 11. Nothing to install.

Open Command Prompt or PowerShell, go to this disk (for example A:) and run:

  decay encrypt --text "meet at dawn" --out letter.png
  decay decrypt letter.png

You are asked for a passphrase; typing is hidden. The image IS the message:
send the PNG file itself. A screenshot, resize or re-compression destroys it.

  decay help          all commands and options
  decay presets       the carrier images you can use
  decay selftest      check this copy works on your PC
  decay activate KEY  unlock Decay Pro with your license key (offline)
  decay license       show the license in use

"decay activate KEY" saves your license key in your user profile. To keep
it beside the program instead, so it travels with these files, copy them to
a writable drive and run "decay activate KEY --portable".
Decrypting is always free. No accounts, no servers, nothing leaves your PC.

Keep all the files in this folder together: decay.exe needs the DLLs next
to it.

toothbrush.studio
