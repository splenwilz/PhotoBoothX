using System.Configuration;
using System.Data;
using System.Windows;
using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;
using Photobooth.Services;

namespace PhotoBooth
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register global handler for unobserved task exceptions
            // This catches exceptions from fire-and-forget tasks that aren't properly awaited
            // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                // Log the exception
                try
                {
                    Console.WriteLine($"!!! UNOBSERVED TASK EXCEPTION: {args.Exception?.GetBaseException()?.Message ?? "Unknown"} !!!");
                    Console.WriteLine($"!!! StackTrace: {args.Exception?.GetBaseException()?.StackTrace ?? "N/A"} !!!");
                    
                    // Use logging service if available (may not be initialized yet during startup)
                    try
                    {
                        LoggingService.Application.Error("Unobserved task exception caught by global handler", args.Exception);
                    }
                    catch
                    {
                        // Logging service not available yet, console output is sufficient
                    }
                }
                catch
                {
                    // Even logging failed, but at least we tried
                }
                
                // Mark exception as observed to prevent application crash
                // In production, you might want to set args.SetObserved() to prevent crashes
                args.SetObserved();
            };

            // Debug console for troubleshooting (enabled for production debugging)
            AllocConsole();
            Console.WriteLine("===========================================");
            Console.WriteLine("PhotoBoothX Debug Console");
            Console.WriteLine("Master Password Debugging Enabled");
            Console.WriteLine("===========================================");
            Console.WriteLine();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up console on exit
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
            base.OnExit(e);
        }
    }
}
