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
}
