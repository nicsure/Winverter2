using System.Runtime.InteropServices;

namespace Winverter2;

internal static class DarkTheme
{
    private static readonly Color Background = Color.FromArgb(28, 28, 32);
    private static readonly Color Surface = Color.FromArgb(36, 36, 41);
    private static readonly Color Header = Color.FromArgb(48, 48, 55);
    private static readonly Color Text = Color.FromArgb(235, 235, 240);
    private static readonly Color Selection = Color.FromArgb(43, 85, 130);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    internal static void Apply(Form form)
    {
        ApplyControl(form);
        form.HandleCreated += (_, _) => ApplyTitleBar(form);
        if (form.IsHandleCreated) ApplyTitleBar(form);
    }

    private static void ApplyTitleBar(Form form)
    {
        int dark = 1;
        DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
        // Explicit caption colours are supported on Windows 11; older Windows ignores them.
        int caption = ColorTranslator.ToWin32(Background);
        int text = ColorTranslator.ToWin32(Text);
        DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 36, ref text, sizeof(int));
    }

    private static void ApplyControl(Control control)
    {
        control.BackColor = Background;
        control.ForeColor = Text;
        if (control is Button button)
        {
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Header;
            button.FlatAppearance.BorderColor = Color.FromArgb(85, 85, 96);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 74);
            button.FlatAppearance.MouseDownBackColor = Selection;
        }
        if (control is CheckBox checkBox) checkBox.FlatStyle = FlatStyle.Flat;
        if (control is ListView list)
        {
            list.BackColor = Surface;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.OwnerDraw = true;
            // DrawColumnHeader only covers actual columns, not the native header's
            // remaining width. Keep that area dark as well.
            _ = new HeaderBackground(list);
            list.DrawColumnHeader += (_, e) =>
            {
                using var brush = new SolidBrush(Header);
                e.Graphics.FillRectangle(brush, e.Bounds);
                DrawText(e.Graphics, e.Header?.Text ?? "", e.Font ?? list.Font, e.Bounds);
            };
            list.DrawItem += (_, e) => { e.DrawDefault = false; };
            list.DrawSubItem += (_, e) =>
            {
                if (e.Item == null || e.SubItem == null) return;
                using var brush = new SolidBrush(e.Item.Selected ? Selection : Surface);
                e.Graphics.FillRectangle(brush, e.Bounds);
                DrawText(e.Graphics, e.SubItem.Text, list.Font, e.Bounds);
                if (e.Item.Focused && list.Focused && e.ColumnIndex == 0)
                    ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -1, -1), Text, e.Item.Selected ? Selection : Surface);
            };
            // Owner-drawn selections need repainting when keyboard focus moves elsewhere.
            list.GotFocus += (_, _) => list.Invalidate();
            list.LostFocus += (_, _) => list.Invalidate();
        }
        foreach (Control child in control.Controls) ApplyControl(child);
    }

    private static void DrawText(Graphics graphics, string text, Font font, Rectangle bounds)
    {
        bounds.Inflate(-6, 0);
        TextRenderer.DrawText(graphics, text, font, bounds, Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    private sealed class HeaderBackground : NativeWindow
    {
        private const int LvmGetHeader = 0x101F;
        private const int HdmGetItemCount = 0x1200;
        private const int HdmGetItemRect = 0x1207;
        private const int WmPaint = 0x000F;
        private const int WmPrintClient = 0x0318;

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern nint SendMessage(nint hwnd, int message, nint wParam, nint lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern nint SendMessage(nint hwnd, int message, nint wParam, ref Native.Rect rect);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(nint hwnd, out Native.Rect rect);

        internal HeaderBackground(ListView list)
        {
            // Event subscriptions retain this subclass for the list's lifetime.
            list.HandleCreated += (_, _) => Attach(list);
            list.HandleDestroyed += (_, _) => ReleaseHandle();
            if (list.IsHandleCreated) Attach(list);
        }

        private void Attach(ListView list)
        {
            if (Handle != 0) ReleaseHandle();
            var header = SendMessage(list.Handle, LvmGetHeader, 0, 0);
            if (header != 0) AssignHandle(header);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WmPaint && m.Msg != WmPrintClient) return;
            if (Handle == 0 || !GetClientRect(Handle, out var client)) return;
            int right = 0;
            int count = (int)SendMessage(Handle, HdmGetItemCount, 0, 0);
            for (int index = 0; index < count; index++)
            {
                var rect = new Native.Rect();
                if (SendMessage(Handle, HdmGetItemRect, index, ref rect) != 0)
                    right = Math.Max(right, rect.Right);
            }
            if (right >= client.Right) return;
            // Native header rectangles already account for horizontal scrolling and DPI.
            using var graphics = m.Msg == WmPrintClient && m.WParam != 0
                ? Graphics.FromHdc(m.WParam) : Graphics.FromHwnd(Handle);
            using var brush = new SolidBrush(Header);
            graphics.FillRectangle(brush, Rectangle.FromLTRB(right, client.Top, client.Right, client.Bottom));
        }
    }
}
