using Winverter2;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        VerifyStartup(showWindow: false);
        VerifyStartup(showWindow: true);
        Console.WriteLine("Startup checks passed: no automatic show in tray mode, normal startup requests a window, explicit reopen works, and closing exits the message loop.");
    }

    private static void VerifyStartup(bool showWindow)
    {
        using var form = new VisibilityProbe();
        _ = form.Handle;
        using var context = new TrayApplicationContext(form, showWindow);
        if (context.MainForm != null) throw new Exception("MainForm would be automatically shown by Application.Run.");
        string? failure = null;
        bool ticked = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ticked = true;
            if (form.ShowRequests != (showWindow ? 1 : 0)) failure = "Incorrect visibility after entering the real WinForms message loop.";
            var previous = form.ShowRequests;
            form.Show();
            if (form.ShowRequests != previous + 1) failure = "Explicit reopen was blocked.";
            form.Close(); // Must exit the context even though MainForm is null.
        };
        timer.Start();
        Application.Run(context);
        if (!ticked) throw new Exception("Message loop exited before startup could be checked.");
        if (failure != null) throw new Exception(failure);
    }

    // Record show requests while keeping this regression test visually hidden.
    private sealed class VisibilityProbe : Form
    {
        internal int ShowRequests { get; private set; }
        protected override void SetVisibleCore(bool value)
        {
            if (value) ShowRequests++;
            base.SetVisibleCore(false);
        }
    }
}
