using System.Runtime.InteropServices;
using System.Text;

namespace Winverter2;

internal static class Native
{
    internal delegate bool EnumWindowCallback(nint hwnd, nint parameter);
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle Bounds => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Matrix { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)] public float[] Values; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ColorMatrix { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)] public float[] Values; }
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] internal static extern int DwmGetWindowBounds(nint hwnd, int attribute, out Rect value, int size);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("advapi32.dll", SetLastError = true)] internal static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref int size);

    internal static string? ExecutablePath(int pid)
    {
        var process = OpenProcess(0x1000, false, (uint)pid);
        if (process == 0) return null;
        try
        {
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            return QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString() : null;
        }
        finally { CloseHandle(process); }
    }
    [DllImport("Magnification.dll")] internal static extern bool MagInitialize();
    [DllImport("Magnification.dll")] internal static extern bool MagUninitialize();
    [DllImport("Magnification.dll")] internal static extern bool MagSetWindowTransform(nint hwnd, ref Matrix matrix);
    [DllImport("Magnification.dll")] internal static extern bool MagSetColorEffect(nint hwnd, ref ColorMatrix matrix);
    [DllImport("Magnification.dll")] internal static extern bool MagSetWindowSource(nint hwnd, Rect rect);
    [DllImport("Magnification.dll")] internal static extern bool MagSetWindowFilterList(nint hwnd, uint mode, int count, nint[] windows);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool InvalidateRect(nint hwnd, nint rect, bool erase);

    internal static Rectangle WindowBounds(nint hwnd)
    {
        if (DwmGetWindowBounds(hwnd, 9, out var rect, 16) != 0) GetWindowRect(hwnd, out rect);
        return rect.Bounds;
    }

    // EnumWindows returns top-level windows in front-to-back order, including dialogs.
    internal static List<WindowInfo> Windows(bool includeMinimized = false)
    {
        var result = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || (!includeMinimized && IsIconic(hwnd))) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            var title = new StringBuilder(1024);
            GetWindowText(hwnd, title, title.Capacity);
            var bounds = WindowBounds(hwnd);
            if (bounds.Width > 0 && bounds.Height > 0) result.Add(new(hwnd, (int)pid, title.ToString(), bounds));
            return true;
        }, 0);
        return result;
    }
}

internal sealed record WindowInfo(nint Handle, int ProcessId, string Title, Rectangle Bounds);
