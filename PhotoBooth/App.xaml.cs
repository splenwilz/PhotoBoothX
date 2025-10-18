using System.Configuration;
using System.Data;
using System.Windows;
using System.Runtime.InteropServices;
using System;

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

            // TEMPORARY: Always allocate console for debugging master password feature
            // TODO: Remove or make conditional after debugging is complete
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
