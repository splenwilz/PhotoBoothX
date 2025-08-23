using System;
using System.Threading.Tasks;

namespace Photobooth.Services
{
    /// <summary>
    /// Interface for managing WiFi hotspot functionality for smartphone photo transfer
    /// </summary>
    public interface IWiFiHotspotService : IDisposable
    {
        #region Events

        /// <summary>
        /// Fired when hotspot status changes
        /// </summary>
        event EventHandler<HotspotStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// Fired when a client connects to the hotspot
        /// </summary>
        event EventHandler<ClientConnectedEventArgs>? ClientConnected;

        /// <summary>
        /// Fired when a client disconnects from the hotspot
        /// </summary>
        event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        /// <summary>
        /// Fired when a hotspot error occurs that requires user attention
        /// </summary>
        event EventHandler<string>? HotspotError;

        #endregion

        #region Properties

        /// <summary>
        /// Check if hotspot is currently active
        /// </summary>
        bool IsHotspotActive { get; }

        /// <summary>
        /// Current hotspot SSID
        /// </summary>
        string? CurrentSSID { get; }

        /// <summary>
        /// Current hotspot password
        /// </summary>
        string? CurrentPassword { get; }

        /// <summary>
        /// Hotspot IP address (usually 192.168.137.1)
        /// </summary>
        string? HotspotIPAddress { get; }

        /// <summary>
        /// Number of connected clients
        /// </summary>
        int ConnectedClientsCount { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Start WiFi hotspot with specified configuration
        /// </summary>
        /// <param name="ssid">Network name (default: PhotoBooth-XXXX)</param>
        /// <param name="password">Network password (default: generated)</param>
        /// <returns>True if hotspot started successfully</returns>
        Task<bool> StartHotspotAsync(string? ssid = null, string? password = null);

        /// <summary>
        /// Stop the WiFi hotspot
        /// </summary>
        /// <returns>True if hotspot stopped successfully</returns>
        Task<bool> StopHotspotAsync();

        /// <summary>
        /// Generate a unique SSID for this session
        /// </summary>
        /// <returns>Generated SSID</returns>
        string GenerateSessionSSID();

        /// <summary>
        /// Generate a secure password for the hotspot
        /// </summary>
        /// <returns>Generated password</returns>
        string GenerateSecurePassword();

        /// <summary>
        /// Check if Mobile Hotspot feature is available on this system
        /// </summary>
        /// <returns>True if mobile hotspot is supported</returns>
        Task<bool> IsHotspotSupportedAsync();

        /// <summary>
        /// Get the current status of the hotspot
        /// </summary>
        /// <returns>Current hotspot status</returns>
        Task<HotspotStatus> GetHotspotStatusAsync();

        #endregion
    }

    #region Event Args

    public class HotspotStatusChangedEventArgs : EventArgs
    {
        public HotspotStatus OldStatus { get; set; }
        public HotspotStatus NewStatus { get; set; }
        public string? ErrorMessage { get; set; }

        public HotspotStatusChangedEventArgs(HotspotStatus oldStatus, HotspotStatus newStatus, string? errorMessage = null)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            ErrorMessage = errorMessage;
        }
    }

    public class ClientConnectedEventArgs : EventArgs
    {
        public string ClientMacAddress { get; set; }
        public string ClientIPAddress { get; set; }
        public string? ClientDeviceName { get; set; }
        public DateTime ConnectedAt { get; set; }

        public ClientConnectedEventArgs(string macAddress, string ipAddress, string? deviceName = null)
        {
            ClientMacAddress = macAddress;
            ClientIPAddress = ipAddress;
            ClientDeviceName = deviceName;
            ConnectedAt = DateTime.Now;
        }
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public string ClientMacAddress { get; set; }
        public string ClientIPAddress { get; set; }
        public DateTime DisconnectedAt { get; set; }
        public TimeSpan ConnectionDuration { get; set; }

        public ClientDisconnectedEventArgs(string macAddress, string ipAddress, DateTime connectedAt)
        {
            ClientMacAddress = macAddress;
            ClientIPAddress = ipAddress;
            DisconnectedAt = DateTime.Now;
            ConnectionDuration = DisconnectedAt - connectedAt;
        }
    }

    #endregion

    #region Enums

    /// <summary>
    /// WiFi hotspot status enumeration
    /// </summary>
    public enum HotspotStatus
    {
        /// <summary>
        /// Hotspot is stopped/inactive
        /// </summary>
        Stopped,

        /// <summary>
        /// Hotspot is starting up
        /// </summary>
        Starting,

        /// <summary>
        /// Hotspot is active and ready for connections
        /// </summary>
        Active,

        /// <summary>
        /// Hotspot is stopping
        /// </summary>
        Stopping,

        /// <summary>
        /// Hotspot encountered an error
        /// </summary>
        Error,

        /// <summary>
        /// Hotspot feature is not supported on this system
        /// </summary>
        NotSupported
    }

    #endregion
}
