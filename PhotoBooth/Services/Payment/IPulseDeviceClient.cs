using System;
using System.Threading;
using System.Threading.Tasks;

namespace Photobooth.Services.Payment
{
    /// <summary>
    /// Abstraction over the VHMI USB CDC device that generates pulse events.
    /// SOURCE: docs/VHMI_Command_Guide.markdown (USB framing requirements).
    /// </summary>
    public interface IPulseDeviceClient : IDisposable
    {
        /// <summary>
        /// Raised when we parse a valid 0x02/0x02 pulse event per docs.
        /// </summary>
        event EventHandler<PulseCountEventArgs>? PulseCountReceived;

        /// <summary>
        /// Currently open COM port (null when idle) so UI can surface status.
        /// </summary>
        string? CurrentPortName { get; }

        /// <summary>
        /// True when the background serial loop is running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Opens the provided COM port and begins parsing VHMI packets.
        /// </summary>
        Task StartAsync(string portName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the background reader, closes the port, and clears buffers.
        /// </summary>
        Task StopAsync();
    }
}

