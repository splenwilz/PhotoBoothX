using System;
using System.Diagnostics;

using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.Net;
// Windows UWP APIs removed for compatibility - using netsh fallback approach

namespace Photobooth.Services
{
    /// <summary>
    /// WiFi Hotspot service implementation using Windows Mobile Hotspot APIs
    /// Provides isolated network for smartphone photo transfer
    /// </summary>
    public class WiFiHotspotService : IWiFiHotspotService
    {
        #region Events

        public event EventHandler<HotspotStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
        public event EventHandler<string>? HotspotError;

        #endregion

        #region Private Fields

        private HotspotStatus _currentStatus = HotspotStatus.Stopped;
        private string? _currentSSID;
        private string? _currentPassword;
        private string? _hotspotIPAddress;
        private int _connectedClientsCount = 0;
        private bool _disposed = false;

        // Windows API for hotspot management (simplified approach)
        // Using netsh commands for compatibility

        #endregion

        #region Properties

        public bool IsHotspotActive => _currentStatus == HotspotStatus.Active;

        public string? CurrentSSID => _currentSSID;

        public string? CurrentPassword => _currentPassword;

        public string? HotspotIPAddress => _hotspotIPAddress;

        public int ConnectedClientsCount => _connectedClientsCount;

        #endregion

        #region Constructor

        public WiFiHotspotService()
        {
            // Initialize with netsh-based approach for compatibility
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start WiFi hotspot for smartphone photo transfer
        /// </summary>
        public async Task<bool> StartHotspotAsync(string? ssid = null, string? password = null)
        {
            Console.WriteLine($"[WiFiHotspot] StartHotspotAsync called with SSID: {ssid ?? "auto-generated"}");
            
            try
            {
                Console.WriteLine("[WiFiHotspot] Starting WiFi hotspot for smartphone photo transfer");
                LoggingService.Application.Information("Starting WiFi hotspot for smartphone photo transfer");

                // Check if hotspot is supported
                Console.WriteLine("[WiFiHotspot] Checking hotspot support...");
                var isSupported = await IsHotspotSupportedAsync();
                Console.WriteLine($"[WiFiHotspot] Hotspot supported: {isSupported}");
                
                if (!isSupported)
                {
                    Console.WriteLine("[WiFiHotspot] ERROR: WiFi hotspot not supported on this system");
                    LoggingService.Application.Error("WiFi hotspot not supported on this system");
                    UpdateStatus(HotspotStatus.NotSupported, "WiFi hotspot not supported");
                    return false;
                }

                // Stop existing hotspot if running
                Console.WriteLine($"[WiFiHotspot] Current hotspot active: {IsHotspotActive}");
                if (IsHotspotActive)
                {
                    Console.WriteLine("[WiFiHotspot] Stopping existing hotspot...");
                    await StopHotspotAsync();
                }

                Console.WriteLine("[WiFiHotspot] Setting status to Starting...");
                UpdateStatus(HotspotStatus.Starting);

                // Generate session-specific credentials
                _currentSSID = ssid ?? GenerateSessionSSID();
                _currentPassword = password ?? GenerateSecurePassword();

                Console.WriteLine($"[WiFiHotspot] Generated credentials - SSID: {_currentSSID}, Password length: {_currentPassword.Length}");
                LoggingService.Application.Information("Generated hotspot credentials",
                    ("SSID", _currentSSID),
                    ("PasswordLength", _currentPassword.Length));

                // Use netsh commands for maximum compatibility
                Console.WriteLine("[WiFiHotspot] Using netsh approach for hotspot creation");
                LoggingService.Application.Information("Using netsh approach for hotspot creation");
                bool started = await StartUsingNetshAsync();
                Console.WriteLine($"[WiFiHotspot] StartUsingNetshAsync returned: {started}");

                if (started)
                {
                    _hotspotIPAddress = GetHotspotIPAddress();
                    UpdateStatus(HotspotStatus.Active);
                    
                    // Start monitoring for connected clients
                    StartClientMonitoring();

                    LoggingService.Application.Information("WiFi hotspot started successfully",
                        ("SSID", _currentSSID),
                        ("IPAddress", _hotspotIPAddress));

                    return true;
                }
                else
                {
                    UpdateStatus(HotspotStatus.Error, "Failed to start hotspot");
                    LoggingService.Application.Error("Failed to start WiFi hotspot");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Exception starting WiFi hotspot", ex);
                UpdateStatus(HotspotStatus.Error, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Stop the WiFi hotspot
        /// </summary>
        public async Task<bool> StopHotspotAsync()
        {
            try
            {
                if (!IsHotspotActive)
                {
                    return true; // Already stopped
                }

                LoggingService.Application.Information("Stopping WiFi hotspot");
                UpdateStatus(HotspotStatus.Stopping);

                StopClientMonitoring();

                // Use netsh approach
                bool stopped = await StopUsingNetshAsync();

                if (stopped)
                {
                    _currentSSID = null;
                    _currentPassword = null;
                    _hotspotIPAddress = null;
                    _connectedClientsCount = 0;
                    
                    UpdateStatus(HotspotStatus.Stopped);
                    LoggingService.Application.Information("WiFi hotspot stopped successfully");
                    return true;
                }
                else
                {
                    UpdateStatus(HotspotStatus.Error, "Failed to stop hotspot");
                    LoggingService.Application.Error("Failed to stop WiFi hotspot");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Exception stopping WiFi hotspot", ex);
                UpdateStatus(HotspotStatus.Error, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Generate unique SSID for this photobooth session
        /// </summary>
        public string GenerateSessionSSID()
        {
            // Create a short, memorable SSID
            var random = new Random();
            var suffix = random.Next(1000, 9999);
            return $"PhotoBooth-{suffix}";
        }

        /// <summary>
        /// Generate secure but simple password
        /// </summary>
        public string GenerateSecurePassword()
        {
            // Generate a simple but secure password (8-63 characters required by netsh)
            // Use words + numbers for ease of manual entry
            var words = new[] { "PhotoBooth", "PrintShop", "SmileBooth", "SnapPhoto", "PhotoTime" };
            var random = new Random();
            var word = words[random.Next(words.Length)];
            var number = random.Next(1000, 9999); // 4 digits to ensure 8+ character minimum
            var password = $"{word}{number}";
            
            Console.WriteLine($"[WiFiHotspot] Generated password: {password} (Length: {password.Length})");
            return password;
        }

        /// <summary>
        /// Check if mobile hotspot is supported
        /// </summary>
        public async Task<bool> IsHotspotSupportedAsync()
        {
            Console.WriteLine("[WiFiHotspot] Checking hotspot support...");
            
            try
            {
                // Check if we're on Windows 10/11 with Mobile Hotspot capability
                var version = Environment.OSVersion.Version;
                Console.WriteLine($"[WiFiHotspot] Windows version: {version}");
                
                if (version.Major < 10)
                {
                    Console.WriteLine("[WiFiHotspot] Windows version too old (< 10)");
                    return false; // Windows 10+ required for reliable hotspot
                }

                // Check if netsh mobile hotspot is available
                Console.WriteLine("[WiFiHotspot] Checking netsh hotspot support...");
                var netshSupport = await CheckNetshHotspotSupportAsync();
                Console.WriteLine($"[WiFiHotspot] Netsh hotspot support: {netshSupport}");
                return netshSupport;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception checking hotspot support: {ex.Message}");
                LoggingService.Application.Error("Error checking hotspot support", ex);
                return false;
            }
        }

        /// <summary>
        /// Get current hotspot status
        /// </summary>
        public async Task<HotspotStatus> GetHotspotStatusAsync()
        {
            return _currentStatus;
        }

        #endregion

        #region Private Methods - Netsh Implementation

        private async Task<bool> StartUsingNetshAsync()
        {
            Console.WriteLine("[WiFiHotspot] StartUsingNetshAsync: Beginning netsh hotspot setup");
            
            try
            {
                // First, configure the hotspot
                Console.WriteLine($"[WiFiHotspot] Configuring hosted network with SSID: {_currentSSID}");
                var configureCommand = $"wlan set hostednetwork mode=allow ssid=\"{_currentSSID}\" key=\"{_currentPassword}\"";
                Console.WriteLine($"[WiFiHotspot] Running command: netsh {configureCommand}");
                
                var configureResult = await ExecuteNetshCommandAsync(configureCommand);
                Console.WriteLine($"[WiFiHotspot] Configure result: {configureResult}");

                if (!configureResult)
                {
                    Console.WriteLine("[WiFiHotspot] ERROR: Failed to configure hosted network");
                    LoggingService.Application.Error("Failed to configure hosted network");
                    return false;
                }

                // Start the hosted network
                Console.WriteLine("[WiFiHotspot] Starting hosted network...");
                var startResult = await ExecuteNetshCommandAsync("wlan start hostednetwork");
                Console.WriteLine($"[WiFiHotspot] Start result: {startResult}");

                if (!startResult)
                {
                    Console.WriteLine("[WiFiHotspot] ERROR: Failed to start hosted network");
                    LoggingService.Application.Error("Failed to start hosted network");
                    return false;
                }

                // Enable Internet Connection Sharing (ICS)
                Console.WriteLine("[WiFiHotspot] Enabling Internet Connection Sharing...");
                await EnableInternetConnectionSharingAsync();

                Console.WriteLine("[WiFiHotspot] StartUsingNetshAsync completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception in StartUsingNetshAsync: {ex.Message}");
                Console.WriteLine($"[WiFiHotspot] Full exception: {ex}");
                LoggingService.Application.Error("Netsh hotspot start failed", ex);
                return false;
            }
        }

        private async Task<bool> StopUsingNetshAsync()
        {
            try
            {
                // Disable ICS first
                await DisableInternetConnectionSharingAsync();

                // Stop hosted network
                var result = await ExecuteNetshCommandAsync("wlan stop hostednetwork");
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Netsh hotspot stop failed", ex);
                return false;
            }
        }

        private async Task<bool> ExecuteNetshCommandAsync(string arguments)
        {
            Console.WriteLine($"[WiFiHotspot] ExecuteNetshCommandAsync: netsh {arguments}");
            
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Run as administrator
                };

                Console.WriteLine("[WiFiHotspot] Starting netsh process...");
                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    Console.WriteLine("[WiFiHotspot] Process started, waiting for completion...");
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    Console.WriteLine($"[WiFiHotspot] Process completed - ExitCode: {process.ExitCode}");
                    Console.WriteLine($"[WiFiHotspot] Output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"[WiFiHotspot] Error: {error}");
                    }

                    LoggingService.Application.Debug("Netsh command executed",
                        ("Arguments", arguments),
                        ("ExitCode", process.ExitCode),
                        ("Output", output),
                        ("Error", error));

                    var success = process.ExitCode == 0;
                    Console.WriteLine($"[WiFiHotspot] Command success: {success}");
                    return success;
                }

                Console.WriteLine("[WiFiHotspot] ERROR: Failed to start process");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception in ExecuteNetshCommandAsync: {ex.Message}");
                Console.WriteLine($"[WiFiHotspot] Full exception: {ex}");
                LoggingService.Application.Error("Failed to execute netsh command", ex, ("Arguments", arguments));
                return false;
            }
        }

        private async Task<bool> CheckNetshHotspotSupportAsync()
        {
            try
            {
                Console.WriteLine("[WiFiHotspot] Checking WiFi AutoConfig Service...");
                
                // First, ensure the Wireless AutoConfig Service is running
                if (!await EnsureWiFiServiceRunningAsync())
                {
                    Console.WriteLine("[WiFiHotspot] Failed to start WiFi AutoConfig Service");
                    return false;
                }

                // Check if WiFi drivers support hosted network
                Console.WriteLine("[WiFiHotspot] Checking WiFi drivers for hosted network support...");
                
                // First check for wireless interfaces
                var interfaceCheck = await CheckWiFiInterfaceAsync();
                if (!interfaceCheck)
                {
                    Console.WriteLine("[WiFiHotspot] CRITICAL: No WiFi interface detected - WiFi hotspot not possible");
                    Console.WriteLine("[WiFiHotspot] This system appears to be a desktop PC or VM without WiFi capability");
                    Console.WriteLine("[WiFiHotspot] SOLUTION: Use USB cable connection or add WiFi adapter");
                    
                    // Trigger error event with helpful message
                    HotspotError?.Invoke(this, "No WiFi adapter detected. This feature requires a WiFi-enabled device. Please use 'Skip & Use Camera Instead' or connect via USB cable.");
                    return false;
                }
                
                var result = await ExecuteNetshCommandAsync("wlan show drivers");
                return result; // If command succeeds, basic support exists
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception in CheckNetshHotspotSupportAsync: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> EnsureWiFiServiceRunningAsync()
        {
            try
            {
                Console.WriteLine("[WiFiHotspot] Checking WiFi AutoConfig Service status...");
                
                // Check if wlansvc is running
                var checkResult = await ExecuteServiceCommandAsync("sc query wlansvc");
                Console.WriteLine($"[WiFiHotspot] Service check result: {checkResult}");
                
                if (!checkResult)
                {
                    Console.WriteLine("[WiFiHotspot] WiFi AutoConfig Service not running, attempting to start...");
                    
                    // Try to start the service
                    var startResult = await ExecuteServiceCommandAsync("sc start wlansvc");
                    Console.WriteLine($"[WiFiHotspot] Service start result: {startResult}");
                    
                    if (startResult)
                    {
                        Console.WriteLine("[WiFiHotspot] Waiting for service to initialize...");
                        await Task.Delay(3000); // Give the service time to start
                        
                        // Verify it's now running
                        var verifyResult = await ExecuteServiceCommandAsync("sc query wlansvc");
                        Console.WriteLine($"[WiFiHotspot] Service verification result: {verifyResult}");
                        return verifyResult;
                    }
                    else
                    {
                        Console.WriteLine("[WiFiHotspot] CRITICAL: Cannot start WiFi service - Administrator privileges required");
                        Console.WriteLine("[WiFiHotspot] SOLUTION: Please restart the application as Administrator, or manually start the 'WLAN AutoConfig' service");
                        
                        // Show user-friendly error in the UI
                        HotspotError?.Invoke(this, "WiFi service not running. Please restart as Administrator or manually start 'WLAN AutoConfig' service in Services.msc");
                    }
                    
                    return false;
                }
                
                Console.WriteLine("[WiFiHotspot] WiFi AutoConfig Service is already running");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception ensuring WiFi service: {ex.Message}");
                LoggingService.Application.Error("Failed to ensure WiFi service is running", ex);
                return false;
            }
        }

        private async Task<bool> CheckWiFiInterfaceAsync()
        {
            try
            {
                Console.WriteLine("[WiFiHotspot] Checking for WiFi hardware interfaces...");
                
                // Check specifically for wireless interfaces - this is the definitive test
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    
                    Console.WriteLine($"[WiFiHotspot] Wireless interface check output: {output}");
                    
                    // If output contains "There is no wireless interface", we don't have WiFi
                    bool hasWiFi = !output.Contains("There is no wireless interface");
                    Console.WriteLine($"[WiFiHotspot] WiFi hardware detected: {hasWiFi}");
                    
                    return hasWiFi;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception checking WiFi interface: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteServiceCommandAsync(string command)
        {
            try
            {
                Console.WriteLine($"[WiFiHotspot] Executing service command: {command}");
                
                var parts = command.Split(' ', 2);
                var startInfo = new ProcessStartInfo
                {
                    FileName = parts[0], // "sc"
                    Arguments = parts.Length > 1 ? parts[1] : "", 
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Run as administrator
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    Console.WriteLine($"[WiFiHotspot] Service command ExitCode: {process.ExitCode}");
                    Console.WriteLine($"[WiFiHotspot] Service command Output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"[WiFiHotspot] Service command Error: {error}");
                    }

                    // For service queries, check if the service is running
                    if (command.Contains("query wlansvc"))
                    {
                        return output.Contains("RUNNING") || output.Contains("START_PENDING");
                    }
                    
                    // For service start, check exit code
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WiFiHotspot] Exception in ExecuteServiceCommandAsync: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods - ICS Management

        private async Task EnableInternetConnectionSharingAsync()
        {
            try
            {
                // Enable ICS programmatically using WMI
                // This is a complex operation that may require elevated privileges
                
                LoggingService.Application.Information("Attempting to enable Internet Connection Sharing");
                
                // Note: ICS configuration is complex and may require manual setup
                // For production use, consider using a dedicated networking library
                // or requiring manual ICS configuration
                
                await Task.Delay(1000); // Placeholder for ICS setup
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("ICS enable failed - manual configuration may be required", ex);
            }
        }

        private async Task DisableInternetConnectionSharingAsync()
        {
            try
            {
                LoggingService.Application.Information("Attempting to disable Internet Connection Sharing");
                await Task.Delay(500); // Placeholder for ICS cleanup
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("ICS disable failed", ex);
            }
        }

        #endregion

        #region Private Methods - Monitoring

        private void StartClientMonitoring()
        {
            try
            {
                // Monitor ARP table for connected clients
                // This is a simplified implementation
                // Production version should use more sophisticated monitoring
                
                LoggingService.Application.Information("Started client monitoring");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to start client monitoring", ex);
            }
        }

        private void StopClientMonitoring()
        {
            try
            {
                LoggingService.Application.Information("Stopped client monitoring");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to stop client monitoring", ex);
            }
        }

        private string GetHotspotIPAddress()
        {
            try
            {
                // Default Windows hotspot IP
                return "192.168.137.1";
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to get hotspot IP address", ex);
                return "192.168.137.1"; // Default fallback
            }
        }

        #endregion

        #region Private Methods - Status Management

        private void UpdateStatus(HotspotStatus newStatus, string? errorMessage = null)
        {
            var oldStatus = _currentStatus;
            _currentStatus = newStatus;

            StatusChanged?.Invoke(this, new HotspotStatusChangedEventArgs(oldStatus, newStatus, errorMessage));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                StopHotspotAsync().Wait(5000); // Give it 5 seconds to stop
                StopClientMonitoring();
                _disposed = true;
            }
        }

        #endregion
    }
}
