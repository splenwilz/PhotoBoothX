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

            // SECURITY: Initialize master password config on startup
            // This ensures config file is loaded and deleted immediately, not waiting for login
            InitializeMasterPasswordConfigAsync();
        }

        /// <summary>
        /// Initialize master password config on app startup to ensure security
        /// Config file is loaded into encrypted database and deleted immediately
        /// </summary>
        private async void InitializeMasterPasswordConfigAsync()
        {
            try
            {
                Console.WriteLine("[SECURITY] Initializing master password config on startup...");
                
                // Create services
                var dbService = new Photobooth.Services.DatabaseService();
                await dbService.InitializeAsync();
                
                var masterPasswordConfigService = new Photobooth.Services.MasterPasswordConfigService(dbService);
                
                // Try to get base secret - this will load from config file if needed and delete it
                try
                {
                    await masterPasswordConfigService.GetBaseSecretAsync();
                    Console.WriteLine("[SECURITY] Master password config initialized and secured.");
                }
                catch (InvalidOperationException)
                {
                    // Expected if master password feature is not configured
                    Console.WriteLine("[INFO] Master password feature not configured (this is normal for self-hosted builds)");
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the app
                Console.WriteLine($"[WARNING] Failed to initialize master password config: {ex.Message}");
            }
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
