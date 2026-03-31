using System;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CMPADotNetTest.Models;

namespace CMPADotNetTest
{
    public partial class App : Application
    {
        // ----------------------------------------------------------------
        // Native crash handler — catches ExitProcess / native thread faults
        // from ingdnp.dll that are invisible to all managed exception hooks.
        // The delegate MUST be held in a static field so the GC never
        // collects it while the process is alive.
        // ----------------------------------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int UnhandledExceptionFilterDelegate(IntPtr exceptionInfo);

        private static UnhandledExceptionFilterDelegate _nativeExceptionHandler;
        private static IntPtr _previousExceptionFilter = IntPtr.Zero;

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern IntPtr SetUnhandledExceptionFilter(
            UnhandledExceptionFilterDelegate lpTopLevelExceptionFilter);

        private static int OnNativeException(IntPtr exceptionInfo)
        {
            try
            {
                MessageBox.Show(
                    "The ProtectApp service connection was lost and caused a fatal error " +
                    "in the native library (ingdnp.dll).\n\n" +
                    "The application must close. Please restart once the service is available.",
                    "Service Connection Lost",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { /* must not throw inside a native exception filter */ }

            // Return EXCEPTION_EXECUTE_HANDLER — let Windows terminate cleanly.
            return 1;
        }
        // ----------------------------------------------------------------

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Install native crash handler first — before any NAE code loads.
            _nativeExceptionHandler = OnNativeException;
            _previousExceptionFilter = SetUnhandledExceptionFilter(_nativeExceptionHandler);

            // Managed safety nets (cover .NET threads and the UI thread).
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            int presetDuration;
            if (!int.TryParse(ConfigurationManager.AppSettings["DefaultDuration"], out presetDuration) || presetDuration <= 0)
                presetDuration = 60;

            int d;
            if (e.Args.Length > 0 && int.TryParse(e.Args[0], out d) && d > 0)
                presetDuration = d;

            RunSession(presetDuration);
        }

        private void RunSession(int presetDuration)
        {
            var dialog = new StartupDialog(presetDuration);
            bool? result = dialog.ShowDialog();

            if (result != true || dialog.Config == null)
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow(dialog.Config);
            mainWindow.ShowDialog();

            if (mainWindow.RestartRequested)
                RunSession(dialog.Config.DurationSeconds);
            else
                Shutdown();
        }

        // Catches unhandled exceptions on .NET background threads (Task.Run, etc.).
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string detail = ex != null
                ? ex.GetType().Name + ": " + ex.Message
                : (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown error");

            MessageBox.Show(
                "A fatal error occurred, likely caused by the ProtectApp service becoming unavailable.\n\n"
                + detail
                + "\n\nThe application will now close.",
                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Catches unhandled exceptions on the WPF UI thread.
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageBox.Show(
                "An unexpected error occurred:\n\n"
                + e.Exception.GetType().Name + ": " + e.Exception.Message
                + "\n\nThe application will attempt to continue.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
