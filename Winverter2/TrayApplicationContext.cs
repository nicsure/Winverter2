namespace Winverter2;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Form window;

    internal TrayApplicationContext(Form window, bool showWindow)
    {
        this.window = window;
        // Leave MainForm null: Application.Run automatically shows it otherwise.
        window.FormClosed += WindowClosed;
        if (showWindow) window.Show();
    }

    private void WindowClosed(object? sender, FormClosedEventArgs e) => ExitThread();

    protected override void Dispose(bool disposing)
    {
        if (disposing) window.FormClosed -= WindowClosed;
        base.Dispose(disposing);
    }
}
