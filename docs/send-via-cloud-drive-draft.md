# Draft: "Send it with a cloud drive" section

*Draft copy for `docs/website-how-to-send.md` and the in-app How to send page
(`HowToSendPage` / `SendGuide`). It replaces the opening of the "Safe ways to
send it" section so the cloud-drive route is the headline, and the cloud-drive
bullet under "Safe ways" can then be removed.*

---

## The easiest way: a cloud-drive link

Upload your Decay image to a cloud drive and send your friend the share link.
They download the original file, so nothing in the image changes along the way.
It works the same for everyone, on any phone or computer.

**Google Drive**
1. Upload the PNG to Drive. Use the **Files** or **Upload** option, not Google
   Photos.
2. Right-click the file, choose **Share**, and set access to **Anyone with the
   link**.
3. Copy the link and send it.

**OneDrive**
1. Upload the PNG to OneDrive (the web app, or your OneDrive folder on a
   computer).
2. Choose **Share**, and set the link to **Anyone with the link can view**.
3. Copy the link and send it.

**iCloud Drive**
1. Save the PNG to **iCloud Drive** in the Files app, not to Photos.
2. Touch and hold the file, then choose **Share**, then **Copy iCloud Link**.
   Set access to **Anyone with the link**.
3. Send the link.

**Dropbox** works the same way: upload the file, create a share link, send it.

### What your friend does

1. Open the link and choose **Download**.
2. Open the downloaded file in Decay, and enter the passphrase you gave them.

They should use the **Download** button. Right-clicking or saving the picture
from the preview in the browser can give them a copy that has been changed, and
a changed copy won't open.

### Send the passphrase separately

Don't put the passphrase in the same message as the link. Anyone who gets both
can read the letter. Tell your friend the passphrase another way: in person, by
phone, or in a different chat.

### Photo libraries are not cloud drives

**Google Photos, iCloud Photos and Apple Photos** are made for pictures, and
they may shrink, convert or re-compress what you put in them. A Decay image
saved there may stop working. Always use the **file** side of the service:
Google Drive, OneDrive, or iCloud Drive in the Files app.

### Keep your own copy

If you delete the file, or the link is turned off, the image is gone. Keep the
original on your device for as long as the message matters.

---

## Notes for the editor (not part of the copy)

- **Verify the steps against current UI before publishing.** The menu names for
  Google Drive, OneDrive and iCloud change from time to time, and I haven't
  tested them on a real account. Do one real round trip on each: upload, share,
  download on a second device, open in Decay. Fix any wording that doesn't
  match what you see.
- **The Dropbox line is optional.** You said Google Drive, OneDrive and Apple
  would do for v1. The existing guide already lists Dropbox, so I kept one
  line. Cut it if you want to support only three.
- **Suggested edit to "Want to post it online?"**: it already says to post a
  teaser with a link to the original file, which fits the cloud-drive approach.
  No change needed beyond naming Google Drive, OneDrive or iCloud Drive first.
- **In-app text** should stay short. In `SendGuide.ResendAdvice`, consider
  listing "a cloud-drive link" first, since that is now the recommended route.
