namespace Winverter2
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            using var mainForm = new MainForm();
            mainForm.InitializeRuntime();
            using var context = new TrayApplicationContext(mainForm, showWindow: !mainForm.StartInNotificationArea);
            Application.Run(context);
        }
    }
}
