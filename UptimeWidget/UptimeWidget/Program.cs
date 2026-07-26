namespace UptimeWidget
{
    internal static class Program
    {
        private const string MutexName = "UptimeWidget.SingleInstance.9F2C1E4A";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            using Mutex mutex = new(initiallyOwned: true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance already owns the mutex; exit quietly.
                return;
            }

            // Applies the PerMonitorV2 high-DPI mode configured in the project file.
            ApplicationConfiguration.Initialize();
            Application.Run(new WidgetContext());

            GC.KeepAlive(mutex);
        }
    }
}