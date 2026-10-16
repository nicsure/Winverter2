using System.ComponentModel;

namespace Winverter2;

internal sealed class InversionOverlay : Form
{
    private readonly nint magnifier;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x080800A0; return cp; }
    }

    internal InversionOverlay()
    {
        // Bounds, clipping and magnifier source are all physical desktop pixels.
        // Only the main application UI should participate in automatic DPI scaling.
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Native.SetLayeredWindowAttributes(Handle, 0, 255, 2);
        magnifier = Native.CreateWindowEx(0, "Magnifier", "", 0x50000000, 0, 0, 1, 1, Handle, 0, 0, 0);
        var transform = new Native.Matrix { Values = [1, 0, 0, 0, 1, 0, 0, 0, 1] };
        var effect = new Native.ColorMatrix { Values = [-1, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1] };
        if (magnifier == 0 || !Native.MagSetWindowTransform(magnifier, ref transform) || !Native.MagSetColorEffect(magnifier, ref effect))
        { Dispose(); throw new Win32Exception("Windows could not create the inversion effect."); }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // WinForms' suggested rectangle scales this window around its previous DPI.
        // The target window's current DWM bounds are the sole source of geometry.
        e.Cancel = true;
        base.OnDpiChanged(e);
    }

    internal void Update(WindowInfo target, IEnumerable<WindowInfo> above, nint[] excluded)
    {
        var bounds = target.Bounds;
        // Move first so a monitor/DPI transition completes before applying the region.
        Native.SetWindowPos(Handle, new nint(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10);
        Native.SetWindowPos(magnifier, 0, 0, 0, bounds.Width, bounds.Height, 0x14);
        // Clip windows above the target so unrelated foreground content remains unchanged.
        var region = new Region(new Rectangle(Point.Empty, bounds.Size));
        foreach (var window in above)
        {
            var intersection = Rectangle.Intersect(bounds, window.Bounds);
            if (intersection.IsEmpty) continue;
            intersection.Offset(-bounds.X, -bounds.Y);
            region.Exclude(intersection);
        }
        var previous = Region;
        Region = region;
        previous?.Dispose();
        Native.MagSetWindowFilterList(magnifier, 0, excluded.Length, excluded);
        var source = new Native.Rect { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
        if (!Native.MagSetWindowSource(magnifier, source)) throw new Win32Exception("Windows could not update the inversion source.");
        if (!Visible) Show();
        Native.InvalidateRect(magnifier, 0, true);
    }
}
