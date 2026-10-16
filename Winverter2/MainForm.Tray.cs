namespace Winverter2;

public partial class MainForm
{
    private bool exitRequested;

    private void InitializeNotificationArea()
    {
        components ??= new System.ComponentModel.Container();
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Winverter2.AppIcon")
            ?? throw new InvalidOperationException("The application icon is missing.");
        using var sourceIcon = new Icon(stream);
        var appIcon = (Icon)sourceIcon.Clone();
        Icon = appIcon;

        var menu = new ContextMenuStrip(components)
        {
            BackColor = Color.FromArgb(36, 36, 41),
            ForeColor = Color.FromArgb(235, 235, 240),
            Renderer = new ToolStripProfessionalRenderer(new TrayMenuColors())
        };
        menu.Items.Add("Open Winverter", null, (_, _) => RestoreMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit Winverter", null, (_, _) =>
        {
            exitRequested = true;
            Close();
        });

        var notificationIcon = new NotifyIcon(components)
        {
            Icon = appIcon,
            Text = "Winverter — colour inversion",
            ContextMenuStrip = menu,
            Visible = true
        };
        notificationIcon.DoubleClick += (_, _) => RestoreMainWindow();
        // The component container disposes the notification icon and menu first.
        Disposed += (_, _) => appIcon.Dispose();
    }

    private void RestoreMainWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        // Windows shutdown and explicit tray Exit proceed through normal cleanup.
    }

    private sealed class TrayMenuColors : ProfessionalColorTable
    {
        public TrayMenuColors() { UseSystemColors = false; }
        public override Color ToolStripDropDownBackground => Color.FromArgb(36, 36, 41);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => Color.FromArgb(43, 85, 130);
        public override Color MenuItemBorder => Color.FromArgb(85, 85, 96);
        public override Color MenuBorder => MenuItemBorder;
        public override Color SeparatorDark => MenuItemBorder;
        public override Color SeparatorLight => ToolStripDropDownBackground;
    }
}
