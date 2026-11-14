using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Sutki
{
    internal sealed class WindowsDesktop : IDesktop
    {
        [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDesktopWallpaper
        {
            void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] string path);
            [PreserveSig] int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, out IntPtr path);
        }

        private static IDesktopWallpaper Open()
        {
            Type type = Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"), true);
            return (IDesktopWallpaper)Activator.CreateInstance(type);
        }

        public string Current()
        {
            IDesktopWallpaper desktop = Open();
            IntPtr path = IntPtr.Zero;
            try { return desktop.GetWallpaper(null, out path) == 0 ? Marshal.PtrToStringUni(path) : null; }
            finally
            {
                Marshal.FreeCoTaskMem(path);
                Marshal.ReleaseComObject(desktop);
            }
        }

        public void Set(string path)
        {
            IDesktopWallpaper desktop = Open();
            try { desktop.SetWallpaper(null, path); }
            finally { Marshal.ReleaseComObject(desktop); }
        }
    }

    internal static class FolderPicker
    {
        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr owner);
            void SetFileTypes();
            void SetFileTypeIndex();
            void GetFileTypeIndex();
            void Advise();
            void Unadvise();
            void SetOptions(uint flags);
            void GetOptions(out uint flags);
            void SetDefaultFolder();
            void SetFolder(IShellItem folder);
            void GetFolder();
            void GetCurrentSelection();
            void SetFileName();
            void GetFileName();
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel();
            void SetFileNameLabel();
            void GetResult(out IShellItem item);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler();
            void GetParent();
            void GetDisplayName(uint format, out IntPtr name);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IShellItem item);

        public static string Pick(IntPtr owner, string existing, string title)
        {
            Type type = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"), true);
            IFileDialog dialog = (IFileDialog)Activator.CreateInstance(type);
            IShellItem folder = null, result = null;
            IntPtr path = IntPtr.Zero;
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | 0x20 | 0x40 | 0x800);
                dialog.SetTitle(title);
                Guid iid = typeof(IShellItem).GUID;
                if (System.IO.Directory.Exists(existing) && SHCreateItemFromParsingName(existing, IntPtr.Zero, ref iid, out folder) == 0)
                    dialog.SetFolder(folder);
                int status = dialog.Show(owner);
                if (status == unchecked((int)0x800704C7)) return null;
                Marshal.ThrowExceptionForHR(status);
                dialog.GetResult(out result);
                result.GetDisplayName(0x80058000, out path);
                return Marshal.PtrToStringUni(path);
            }
            finally
            {
                Marshal.FreeCoTaskMem(path);
                if (folder != null) Marshal.ReleaseComObject(folder);
                if (result != null) Marshal.ReleaseComObject(result);
                Marshal.ReleaseComObject(dialog);
            }
        }
    }

    internal enum TrayCommand { None, Glance, Settings, Exit }

    internal sealed class Tray : IDisposable
    {
        private const int Callback = 0x8041;
        private readonly int taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        private readonly HwndSource window;
        private readonly Icon icon;
        private Notify data;
        private DateTime registerAfter;
        private bool modern;
        public event Action<Rectangle, bool> Selected;
        public event Action ClockChanged;
        internal bool Registered { get; private set; }

        public Tray()
        {
            window = new HwndSource(new HwndSourceParameters("Sutki.Tray")
            {
                Width = 1, Height = 1,
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP, without WS_VISIBLE
                ExtendedWindowStyle = 0x08000080 // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            });
            window.AddHook(Message);
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Sutki.ico"))
                icon = new Icon(stream, 32, 32);
            data = new Notify { Size = Marshal.SizeOf(typeof(Notify)), Window = window.Handle, Id = 1,
                Flags = 0x87, Message = Callback, Icon = icon.Handle, Tip = "Сутки" };
            Register();
        }

        private void Register()
        {
            Registered = Shell_NotifyIcon(0, ref data);
            registerAfter = DateTime.UtcNow.AddSeconds(5);
            if (Registered)
            {
                data.Version = 4;
                modern = Shell_NotifyIcon(4, ref data);
            }
        }

        public void EnsureRegistered()
        {
            if (!Registered && DateTime.UtcNow >= registerAfter) Register();
        }

        public void Tooltip(string tip)
        {
            data.Flags = 0x84;
            data.Tip = tip;
            Shell_NotifyIcon(1, ref data);
            data.Flags = 0x87;
        }

        public Rectangle Anchor()
        {
            var identifier = new Identifier { Size = Marshal.SizeOf(typeof(Identifier)), Window = window.Handle, Id = 1 };
            Rect rect;
            if (Shell_NotifyIconGetRect(ref identifier, out rect) == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top)
                return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            Point point;
            GetCursorPos(out point);
            return new Rectangle(point.X, point.Y, 1, 1);
        }

        private IntPtr Message(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
        {
            if (taskbarCreated != 0 && message == taskbarCreated) Register();
            if ((message == 0x001E || (message == 0x0218 && (w.ToInt64() == 7 || w.ToInt64() == 18))) && ClockChanged != null)
                ClockChanged();
            if (message == Callback && Selected != null)
            {
                int action = (int)(l.ToInt64() & 0xffff);
                if (action == 0x202 || action == 0x400 || action == 0x401 || action == 0x007b || (!modern && action == 0x205))
                {
                    Rectangle anchor = Anchor();
                    bool context = action == 0x007b || action == 0x205;
                    if (context) menuAnchor = new Point { X = anchor.X + anchor.Width / 2, Y = anchor.Top };
                    if (context && modern)
                    {
                        long positions = w.ToInt64();
                        short x = (short)(positions & 0xffff), y = (short)((positions >> 16) & 0xffff);
                        if (x != -1 || y != -1) menuAnchor = new Point { X = x, Y = y };
                    }
                    else if (action == 0x205) GetCursorPos(out menuAnchor);
                    Selected(anchor, context);
                }
            }
            return IntPtr.Zero;
        }

        private Point menuAnchor;

        public System.Drawing.Point MenuAnchor { get { return new System.Drawing.Point(menuAnchor.X, menuAnchor.Y); } }

        public void Dispose()
        {
            Shell_NotifyIcon(2, ref data);
            window.Dispose();
            icon.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Identifier { public int Size; public IntPtr Window; public uint Id; public Guid Guid; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Notify
        {
            public int Size;
            public IntPtr Window;
            public uint Id, Flags, Message;
            public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
            public uint InfoFlags;
            public Guid Guid;
            public IntPtr BalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint action, ref Notify data);
        [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref Identifier data, out Rect rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    }
}
