using System;
using System.Threading;
using System.Threading.Tasks;

namespace Photobooth.Services.Payment
{
    /// <summary>
    /// High-level payment pulse orchestrator built on top of the VHMI serial feed.
    /// </summary>
    public interface IPaymentPulseService : IDisposable
    {
        event EventHandler<PulseDeltaEventArgs>? PulseDeltaProcessed;

        bool IsRunning { get; }

        string? CurrentPortName { get; }

        /// <summary>
        /// Returns true if we've received data from the PCB recently (within the last 60 seconds).
        /// This helps distinguish between "port is open" vs "PCB is actually connected and responding".
        /// </summary>
        bool HasReceivedDataRecently { get; }

        /// <summary>
        /// Returns true if the serial port connection has encountered an error (device disconnected, etc.).
        /// This helps detect immediate disconnections rather than waiting for the 60-second data timeout.
        /// </summary>
        bool HasConnectionError { get; }

        Task StartAsync(string portName, CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);

        void ResetCounters();

        /// <summary>
        /// Clear all processed unique IDs from memory (for testing/reset purposes).
        /// This should be called after deleting all unique IDs from the database to keep them in sync.
        /// </summary>
        void ClearProcessedUniqueIds();
    }
}

