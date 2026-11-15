using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace Sutki
{
    internal static class WindowsUi
    {
        private static readonly HashSet<string> InstalledFonts = new HashSet<string>(
            Fonts.SystemFontFamilies.Select(font => font.Source), StringComparer.OrdinalIgnoreCase);

        public static readonly FontFamily TextFont = SelectFont("Segoe UI Variable Text", "Segoe UI");
        public static readonly FontFamily IconFont = SelectFont("Segoe Fluent Icons", "Segoe MDL2 Assets");

        private static FontFamily SelectFont(string preferred, string fallback)
        {
            return new FontFamily(InstalledFonts.Contains(preferred) ? preferred : fallback);
        }

        public static double DpiScale(IntPtr window)
        {
            if (window == IntPtr.Zero) return 1;
            uint dpi = GetDpiForWindow(window);
            if (dpi != 0) return dpi / 96.0;

            var source = HwndSource.FromHwnd(window);
            var target = source == null ? null : source.CompositionTarget;
            return target == null ? 1 : target.TransformToDevice.M11;
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);
    }
}
