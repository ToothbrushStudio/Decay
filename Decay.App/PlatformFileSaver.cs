namespace Decay.App
{
    /// <summary>
    /// A native "Save As" dialog per platform. MAUI itself has no
    /// abstraction for this, and the community toolkit's IFileSaver turned
    /// out to have no version that both registers itself in DI and matches
    /// this project's installed MAUI workload — every release with the
    /// registration wiring requires a newer Microsoft.Maui.Controls than
    /// this machine's workload provides. Rather than chase that, Windows
    /// uses WinRT's FileSavePicker directly: a first-party API already
    /// available to the Windows head, so there is no version to match.
    /// Android/iOS/Mac Catalyst throw PlatformNotSupportedException until
    /// their native pickers are added; Share remains the working path there
    /// in the meantime.
    /// </summary>
    public static class PlatformFileSaver
    {
        /// <returns>The saved file's path, or null if the user canceled.</returns>
        /// <exception cref="PlatformNotSupportedException">
        /// No native save dialog is implemented for this platform yet.
        /// </exception>
        public static async Task<string?> SaveAsync(string suggestedFileName, byte[] data)
        {
#if WINDOWS
            Windows.Storage.Pickers.FileSavePicker picker = new()
            {
                SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            };

            picker.FileTypeChoices.Add("PNG image", new List<string> { ".png" });

            Microsoft.UI.Xaml.Window nativeWindow =
                (Microsoft.UI.Xaml.Window)Application.Current!.Windows[0].Handler!.PlatformView!;
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();

            if (file is null)
            {
                return null;
            }

            await Windows.Storage.FileIO.WriteBytesAsync(file, data);

            return file.Path;
#else
            await Task.CompletedTask;

            throw new PlatformNotSupportedException(
                "Saving directly isn't available on this platform yet — use Share instead."
            );
#endif
        }
    }
}
