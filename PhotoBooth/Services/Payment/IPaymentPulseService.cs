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

        Task StartAsync(string portName, CancellationToken cancellationToken = default);

        Task StopAsync();

        void ResetCounters();
    }
}

