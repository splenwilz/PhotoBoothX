using Photobooth.Services;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Photobooth.Services.Payment
{
    /// <summary>
    /// VHMI serial client that reads pulse packets (docs/VHMI_Command_Guide.markdown).
    /// </summary>
    public sealed class PulseDeviceClient : IPulseDeviceClient
    {
        private const byte PulsePacketType = 0x02; // type=0x02 → response/event per doc
        private const byte PulsePacketId = 0x02;   // cmd=0x02 → pulse event per doc
        private const int DefaultBaudRate = 115200; // firmware sample uses 115200 baud

        private readonly object _stateLock = new();          // protects start/stop state
        private readonly List<byte> _packetBuffer = new();   // accumulates bytes between reads

        private SerialPort? _serialPort;
        private Task? _readLoopTask;
        private CancellationTokenSource? _cts;
        private bool _hasConnectionError = false; // Track if serial port has encountered an error

        public event EventHandler<PulseCountEventArgs>? PulseCountReceived;

        public string? CurrentPortName { get; private set; }

        public bool IsRunning => _readLoopTask != null && !_readLoopTask.IsCompleted;

        public bool HasConnectionError
        {
            get
            {
                lock (_stateLock)
                {
                    return _hasConnectionError;
                }
            }
        }

        public async Task StartAsync(string portName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                throw new ArgumentException("Port name is required.", nameof(portName)); // avoid guessing ports
            }

            // Check if we need to stop and restart due to connection error
            bool needsRestart = false;
            lock (_stateLock)
            {
                if (_serialPort != null)
                {
                    if (string.Equals(CurrentPortName, portName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Already connected to this port
                        if (_hasConnectionError)
                        {
                            // Connection error detected - need to stop and restart to recover
                            needsRestart = true;
                        }
                        else
                        {
                            return; // already connected to this port and working; nothing to do
                        }
                    }
                    else
                    {
                        // Different port requested - need to stop current one first
                        throw new InvalidOperationException("Stop the active client before starting another port.");
                    }
                }
            }

            // If we detected a connection error, stop first before restarting
            if (needsRestart)
            {
                await StopAsync(cancellationToken).ConfigureAwait(false);
                // Small delay to ensure port is fully released
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            // Check for pre-canceled token before opening port to prevent "zombie" connections
            // If token is already canceled, ReadLoopAsync will exit immediately, leaving port open
            cancellationToken.ThrowIfCancellationRequested();

            // Use local variables first to avoid half-initialized state if Open() throws
            // Wrap both creation and opening in try/catch to ensure linkedCts is disposed if CreateSerialPort throws
            SerialPort? port = null;
            CancellationTokenSource? linkedCts = null;
            
            try
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); // propagate host cancellation
                port = CreateSerialPort(portName);
                
                lock (_stateLock)
                {
                    // Another StartAsync may have completed while we were creating the port/CTS
                    // Re-check _serialPort inside the lock to prevent double-opening and orphaned readers
                    if (_serialPort != null)
                    {
                        // Another concurrent call already started - dispose our locals and handle appropriately
                        port.Dispose();
                        linkedCts.Dispose();
                        
                        if (string.Equals(CurrentPortName, portName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Same port - the other call succeeded, we can just return
                            return;
                        }
                        
                        // Different port - this is an error condition
                        throw new InvalidOperationException("Stop the active client before starting another port.");
                    }
                    
                    port.Open(); // throws immediately if device missing

                    // Only assign to class fields after successful port open to maintain consistent state
                    _cts = linkedCts;
                    _serialPort = port;
                    CurrentPortName = portName;
                    _hasConnectionError = false; // Reset error flag on successful start
                    _readLoopTask = Task.Run(() => ReadLoopAsync(linkedCts.Token), CancellationToken.None); // background worker
                }
            }
            catch
            {
                // Dispose local resources if creation or Open() fails to prevent resource leaks
                // This is especially important with retry logic that may call StartAsync multiple times
                port?.Dispose();
                linkedCts?.Dispose();
                throw; // Re-throw to propagate the original exception
            }

            await Task.CompletedTask.ConfigureAwait(false); // maintain async signature without extra allocations
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            CancellationTokenSource? ctsSnapshot;
            Task? readTaskSnapshot;
            SerialPort? serialPortSnapshot;
            string? portNameSnapshot; // Capture port name before nulling for logging

            lock (_stateLock)
            {
                ctsSnapshot = _cts;
                readTaskSnapshot = _readLoopTask;
                serialPortSnapshot = _serialPort;
                portNameSnapshot = CurrentPortName; // Capture before nulling
                _cts = null;
                _readLoopTask = null;
                _serialPort = null;
                CurrentPortName = null;
                _hasConnectionError = false; // Reset error flag when stopping - error state is cleared
            }

            ctsSnapshot?.Cancel(); // signal background loop to exit

            if (readTaskSnapshot != null)
            {
                try
                {
                    // Wait for clean shutdown, but respect cancellation token for timeout enforcement
                    await readTaskSnapshot.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token stops the task or timeout expires
                    // Log warning if it's from the timeout (not the internal cancellation)
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LoggingService.Hardware.Warning(
                            "PulseDeviceClient",
                            "StopAsync cancelled due to timeout",
                            ("Port", portNameSnapshot ?? "Unknown"));
                    }
                }
            }

            serialPortSnapshot?.Dispose(); // release COM handle
            ctsSnapshot?.Dispose();        // release CTS resources
            _packetBuffer.Clear();         // drop partial packets before next start
        }

        public void Dispose()
        {
            // Capture port name before calling StopAsync (which will null it)
            string? portNameSnapshot;
            lock (_stateLock)
            {
                portNameSnapshot = CurrentPortName;
            }

            // Use a timeout during shutdown to prevent hanging (5 seconds should be sufficient for cleanup)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                // NOTE: Blocking on async in Dispose() is generally not ideal, but IDisposable.Dispose() must be synchronous.
                // This is safe from deadlock because:
                // 1. ReadLoopAsync uses ConfigureAwait(false) - no synchronization context capture
                // 2. StopAsync uses ConfigureAwait(false) when waiting - no marshaling back to original context
                // 3. Timeout (5 seconds) prevents indefinite blocking
                // 4. ReadLoopAsync is a background task that doesn't interact with UI thread
                // If deadlock concerns arise in practice, consider implementing IAsyncDisposable for async disposal scenarios.
                StopAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Timeout expired - log warning but continue with cleanup
                LoggingService.Hardware.Warning(
                    "PulseDeviceClient",
                    "StopAsync timed out during Dispose, forcing cleanup",
                    ("Port", portNameSnapshot ?? "Unknown"));
                // Force cleanup even if timeout occurred
                _packetBuffer.Clear();
            }
        }

        private static SerialPort CreateSerialPort(string portName)
        {
            return new SerialPort(portName, DefaultBaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 100,               // short timeout so cancellation can break the loop quickly
                WriteTimeout = 100,
                DtrEnable = true,                // ESP32 CDC stacks often require DTR/RTS asserted
                RtsEnable = true,
                Handshake = Handshake.None
            };
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            // Initial null check - if port doesn't exist at start, exit immediately
            SerialPort? port;
            lock (_stateLock)
            {
                port = _serialPort;
            }
            
            if (port == null)
            {
                return; // guard: StartAsync failed before serial port creation
            }

            var scratch = new byte[64]; // matches python sample chunk size for parity

            while (!cancellationToken.IsCancellationRequested)
            {
                // Capture serial port reference inside loop to prevent race condition with StopAsync.
                // StopAsync can set _serialPort to null while we're reading, so we need a stable reference for this iteration.
                lock (_stateLock)
                {
                    port = _serialPort;
                }
                
                if (port == null)
                {
                    // Serial port was disposed by StopAsync - exit gracefully
                    return;
                }
                
                try
                {
                    var bytesRead = port.Read(scratch, 0, scratch.Length); // blocking read with timeout

                    if (bytesRead > 0)
                    {
                        AppendAndProcess(scratch, bytesRead); // parse whatever bytes arrived
                    }
                }
                catch (TimeoutException)
                {
                    // harmless: ReadTimeout expired and we loop again to honor cancellation
                }
                catch (ObjectDisposedException)
                {
                    // Serial port was disposed by StopAsync - exit gracefully
                    return;
                }
                catch (Exception ex)
                {
                    // Mark connection error for immediate reconnection detection
                    lock (_stateLock)
                    {
                        _hasConnectionError = true;
                    }

                    LoggingService.Hardware.Error(
                        "PulseDeviceClient",
                        "Serial read failed",
                        ex,
                        ("Port", CurrentPortName ?? "Unknown"));

                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false); // brief backoff
                }
            }
        }

        private void AppendAndProcess(ReadOnlySpan<byte> chunk, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _packetBuffer.Add(chunk[i]); // accumulate so we can parse across read boundaries
            }

            ParsePackets(); // try to decode as many packets as currently buffered
        }

        private void ParsePackets()
        {
            while (_packetBuffer.Count >= 4) // need at least header (type, cmd, length LSB/MSB)
            {
                if (_packetBuffer[0] != PulsePacketType || _packetBuffer[1] != PulsePacketId)
                {
                    _packetBuffer.RemoveAt(0); // shift until we align on 0x02/0x02 header from doc
                    continue;
                }

                var payloadLength = _packetBuffer[2] | (_packetBuffer[3] << 8); // 16-bit little endian length
                var totalLength = 4 + payloadLength;

                if (_packetBuffer.Count < totalLength)
                {
                    return; // wait for more data
                }

                // Protocol specifies only two valid payload lengths:
                // - Old format: exactly 6 bytes
                // - New format: exactly 16 bytes
                // Restrict parsing to documented lengths to prevent mis-crediting on malformed packets
                if (payloadLength == 6)
                {
                    // Old format (6 bytes) - parse for backward compatibility
                    var identifierByte = _packetBuffer[4];
                    var pulseLowIndex = 4 + payloadLength - 2; // 4 + 6 - 2 = 8 (correct for 6-byte format)
                    var pulseCount = _packetBuffer[pulseLowIndex] | (_packetBuffer[pulseLowIndex + 1] << 8);
                    var identifier = Enum.IsDefined(typeof(PulseIdentifier), identifierByte)
                        ? (PulseIdentifier)identifierByte
                        : PulseIdentifier.CardAccepter;
                    
                    // Old format - no unique ID, create empty array
                    var emptyUniqueId = new byte[10];
                    var packetBytes = _packetBuffer.Take(totalLength).ToArray();
                    var hexString = string.Join(" ", packetBytes.Select(b => b.ToString("X2")));
                    
                    Console.WriteLine($"[PulseDeviceClient] {DateTime.Now:HH:mm:ss} RAW PACKET (OLD FORMAT): {hexString}");
                    Console.WriteLine($"[PulseDeviceClient] {DateTime.Now:HH:mm:ss} PARSED: identifier={identifier} | pulseCount={pulseCount} | WARNING: No unique ID (old format)");
                    
                    PulseCountReceived?.Invoke(
                        this,
                        new PulseCountEventArgs(identifier, pulseCount, emptyUniqueId, DateTime.UtcNow));
                    
                    _packetBuffer.RemoveRange(0, totalLength);
                    continue;
                }
                else if (payloadLength != 16)
                {
                    // Unknown payload length - drop and log to avoid mis-crediting
                    var badBytes = _packetBuffer.Take(totalLength).ToArray();
                    var badHex = string.Join(" ", badBytes.Select(b => b.ToString("X2")));
                    LoggingService.Hardware.Warning(
                        "PulseDeviceClient",
                        $"Dropping packet with unexpected payload length {payloadLength} (expected 6 or 16 bytes)",
                        ("PayloadHex", badHex),
                        ("TotalLength", totalLength));
                    
                    _packetBuffer.RemoveRange(0, totalLength);
                    continue;
                }

                // New format (16 bytes payload)
                // Structure: [Identifier(1)][Padding(3)][PulseCount(2)][UniqueId(10)]
                var identifierByteNew = _packetBuffer[4]; // first payload byte = enum identifier
                var pulseCountIndex = 4; // Pulse count is at payload offset 4-5 (after identifier + 3 padding bytes)
                var pulseCountNew = _packetBuffer[4 + pulseCountIndex] | (_packetBuffer[4 + pulseCountIndex + 1] << 8);
                
                // Unique ID is the last 10 bytes of the payload (payload offset 6-15, packet positions 10-19)
                var uniqueIdStartIndex = 4 + 6; // Start of unique ID in packet buffer (after identifier + padding + pulseCount)
                var uniqueId = new byte[10];
                for (int i = 0; i < 10; i++)
                {
                    uniqueId[i] = _packetBuffer[uniqueIdStartIndex + i];
                }

                var identifierNew = Enum.IsDefined(typeof(PulseIdentifier), identifierByteNew)
                    ? (PulseIdentifier)identifierByteNew
                    : PulseIdentifier.CardAccepter;

                // Extract raw packet bytes for debugging
                var packetBytesNew = _packetBuffer.Take(totalLength).ToArray();
                var hexStringNew = string.Join(" ", packetBytesNew.Select(b => b.ToString("X2")));
                var uniqueIdHex = string.Join(" ", uniqueId.Select(b => b.ToString("X2")));

                // Enhanced logging showing raw packet and parsed values
                Console.WriteLine($"[PulseDeviceClient] {DateTime.Now:HH:mm:ss} RAW PACKET: {hexStringNew}");
                Console.WriteLine($"[PulseDeviceClient] {DateTime.Now:HH:mm:ss} PARSED: identifier={identifierNew} | pulseCount={pulseCountNew} | uniqueId={uniqueIdHex}");

                PulseCountReceived?.Invoke(
                    this,
                    new PulseCountEventArgs(identifierNew, pulseCountNew, uniqueId, DateTime.UtcNow));

                _packetBuffer.RemoveRange(0, totalLength); // drop processed bytes and continue parsing
            }
        }
    }
}

