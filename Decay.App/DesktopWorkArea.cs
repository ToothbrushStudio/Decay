using System.Runtime.InteropServices;

namespace Decay.App
{
    /// <summary>
    /// The usable area of the main screen (without the taskbar), in
    /// device-independent units, so the window can open as large as fits.
    /// Windows only; elsewhere, or if the call fails, it reports nothing and
    /// the caller falls back to a size that fits common screens.
    /// </summary>
    internal static class DesktopWorkArea
    {
        private const uint SpiGetWorkArea = 0x0030;

        internal readonly struct Area
        {
            internal Area(double left, double top, double width, double height)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }

            internal double Left { get; }
            internal double Top { get; }
            internal double Width { get; }
            internal double Height { get; }
        }

        internal static Area? Get()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                Rect area = default;

                if (!SystemParametersInfo(SpiGetWorkArea, 0, ref area, 0))
                {
                    return null;
                }

                // The app is per-monitor DPI aware, so the work area is in
                // physical pixels; the system DPI converts it to units.
                double scale = GetDpiForSystem() / 96.0;

                if (scale <= 0 || area.Right <= area.Left || area.Bottom <= area.Top)
                {
                    return null;
                }

                return new Area(
                    area.Left / scale,
                    area.Top / scale,
                    (area.Right - area.Left) / scale,
                    (area.Bottom - area.Top) / scale
                );
            }
            catch (Exception exception) when (
                exception is DllNotFoundException or EntryPointNotFoundException)
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint action, uint param, ref Rect rect, uint winIni);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();
    }
}
