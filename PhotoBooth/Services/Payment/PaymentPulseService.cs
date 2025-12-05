using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Photobooth.Services;

namespace Photobooth.Services.Payment
{
    /// <summary>
    /// Converts raw VHMI pulse packets into usable deltas and publishes them to the app.
    /// </summary>
    public sealed class PaymentPulseService : IPaymentPulseService
    {
        private static PaymentPulseService? _instance;
        private static readonly object _instanceLock = new();

        public static PaymentPulseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            // Will be initialized with database service later via InitializeWithDatabaseService
                            _instance = new PaymentPulseService(new PulseDeviceClient(), null);
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly IPulseDeviceClient _deviceClient;
        private IDatabaseService? _databaseService;
        private readonly object _syncRoot = new();
        private readonly Dictionary<PulseIdentifier, int> _lastCounts = new(); // Kept for backward compatibility
        // Track unique IDs we've already processed to prevent duplicate credits
        private readonly HashSet<string> _processedUniqueIds = new();
        // Track failed database saves for retry (prevents data loss on app restart)
        private readonly Queue<(string uniqueId, string identifier, int pulseCount, decimal amountCredited, DateTime timestamp)> _failedSavesQueue = new();
        private readonly object _retryQueueLock = new();
        private bool _isInitialized = false;
        private CancellationTokenSource? _retryCancellationTokenSource;
        private DateTime? _lastDataReceivedAt; // Track when we last received data from PCB
        private readonly object _lastDataLock = new();

        public event EventHandler<PulseDeltaEventArgs>? PulseDeltaProcessed;

        public bool IsRunning => _deviceClient.IsRunning;

        public string? CurrentPortName => _deviceClient.CurrentPortName;

        public bool HasConnectionError => _deviceClient.HasConnectionError;

        /// <summary>
        /// Returns true if we've received data from the PCB recently (within the last 60 seconds).
        /// This helps distinguish between "port is open" vs "PCB is actually connected and responding".
        /// </summary>
        public bool HasReceivedDataRecently
        {
            get
            {
                lock (_lastDataLock)
                {
                    if (_lastDataReceivedAt == null)
                    {
                        return false; // Never received data
                    }
                    // Consider "recent" if data was received within the last 60 seconds
                    return (DateTime.UtcNow - _lastDataReceivedAt.Value).TotalSeconds < 60;
                }
            }
        }

        public PaymentPulseService()
            : this(new PulseDeviceClient(), null)
        {
        }

        /// <summary>
        /// Constructor for dependency injection and testing.
        /// Allows injecting mock dependencies for unit testing.
        /// </summary>
        public PaymentPulseService(IPulseDeviceClient deviceClient, IDatabaseService? databaseService)
        {
            _deviceClient = deviceClient ?? throw new ArgumentNullException(nameof(deviceClient));
            _databaseService = databaseService;
            _deviceClient.PulseCountReceived += HandlePulseCountReceived;
        }

        /// <summary>
        /// Set the database service instance (called from MainWindow after database is created)
        /// </summary>
        public void SetDatabaseService(IDatabaseService databaseService)
        {
            if (databaseService == null)
            {
                throw new ArgumentNullException(nameof(databaseService), "Database service cannot be null");
            }
            
            lock (_syncRoot)
            {
                _databaseService = databaseService;
            }
            
            LoggingService.Application.Information("Database service set on PaymentPulseService", ("Component", "PaymentPulseService"));
        }

        /// <summary>
        /// Initialize the service by loading processed unique IDs from the database.
        /// Call this on app startup after database is initialized.
        /// </summary>
        public async Task InitializeAsync()
        {
            // Double-check lock pattern to prevent race condition
            // Fast path: If already initialized, return early without acquiring lock
            if (_isInitialized)
            {
                return;
            }

            // Acquire lock and check again to prevent concurrent initialization
            // Only one thread will pass this check and proceed with initialization
            lock (_syncRoot)
            {
                if (_isInitialized)
                {
                    return;
                }
            }

            // Proceed with initialization work outside the lock
            // The double-check ensures only one thread reaches this point
            if (_databaseService == null)
            {
                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} INITIALIZATION WARNING: Database service not set, skipping initialization");
                LoggingService.Application.Warning("Database service not set, skipping initialization", ("Component", "PaymentPulseService"));
                return;
            }

            try
            {
                var result = await _databaseService.LoadProcessedPulseUniqueIdsAsync();
                if (result.Success && result.Data != null)
                {
                    lock (_syncRoot)
                    {
                        foreach (var uniqueId in result.Data)
                        {
                            _processedUniqueIds.Add(uniqueId);
                        }
                    }

                    Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} INITIALIZED: Loaded {result.Data.Count} processed unique IDs from database");
                    LoggingService.Application.Information("PaymentPulseService initialized", ("LoadedUniqueIds", result.Data.Count));
                }
                else
                {
                    Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} INITIALIZATION WARNING: {result.ErrorMessage ?? "Unknown error"}");
                    LoggingService.Application.Warning("Failed to load processed unique IDs", ("Component", "PaymentPulseService"), ("Error", result.ErrorMessage ?? "Unknown error"));
                }

                // Clean up old unique IDs (keep last 30 days)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_databaseService != null)
                        {
                            await _databaseService.CleanupOldProcessedPulseUniqueIdsAsync(keepDays: 30);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Warning("Failed to cleanup old unique IDs", ("Component", "PaymentPulseService"), ("Exception", ex.Message));
                    }
                });

                // Set initialization flag under lock to ensure thread-safety
                // This is set after successful initialization so failures can retry
                lock (_syncRoot)
                {
                    _isInitialized = true;
                }
                
                // Start retry loop for failed database saves
                StartRetryLoop();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to initialize PaymentPulseService", ex, ("Component", "PaymentPulseService"));
                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} INITIALIZATION ERROR: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Background task to retry failed database saves (prevents data loss on app restart)
        /// </summary>
        private void StartRetryLoop()
        {
            _retryCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _retryCancellationTokenSource.Token;
            
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Retry every 5 seconds
                        
                        // Process failed saves queue
                        var itemsToRetry = new List<(string uniqueId, string identifier, int pulseCount, decimal amountCredited, DateTime timestamp)>();
                        
                        lock (_retryQueueLock)
                        {
                            while (_failedSavesQueue.Count > 0)
                            {
                                itemsToRetry.Add(_failedSavesQueue.Dequeue());
                            }
                        }
                        
                        if (itemsToRetry.Count > 0 && _databaseService != null)
                        {
                            Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} RETRYING: {itemsToRetry.Count} failed database saves");
                            
                            foreach (var item in itemsToRetry)
                            {
                                try
                                {
                                    var saveResult = await _databaseService.SaveProcessedPulseUniqueIdAsync(
                                        item.uniqueId,
                                        item.identifier,
                                        item.pulseCount,
                                        item.amountCredited);
                                    
                                    if (saveResult.Success)
                                    {
                                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} RETRY SUCCESS: UniqueId={item.uniqueId}");
                                    }
                                    else
                                    {
                                        // Save failed again - re-queue for next retry (but limit queue size to prevent memory bloat)
                                        lock (_retryQueueLock)
                                        {
                                            if (_failedSavesQueue.Count < 100) // Limit queue size
                                            {
                                                _failedSavesQueue.Enqueue(item);
                                            }
                                            else
                                            {
                                                LoggingService.Application.Error(
                                                    "Failed save retry queue is full, dropping oldest entry",
                                                    null,
                                                    ("Component", "PaymentPulseService"),
                                                    ("DroppedUniqueId", item.uniqueId));
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Re-queue for retry
                                    lock (_retryQueueLock)
                                    {
                                        if (_failedSavesQueue.Count < 100)
                                        {
                                            _failedSavesQueue.Enqueue(item);
                                        }
                                    }
                                    LoggingService.Application.Warning("Retry save failed, will retry again", ("Component", "PaymentPulseService"), ("Exception", ex.Message));
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation token is triggered
                        break;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Warning("Error in retry loop", ("Component", "PaymentPulseService"), ("Exception", ex.Message));
                    }
                }
            }, cancellationToken);
        }

        public async Task StartAsync(string portName, CancellationToken cancellationToken = default)
        {
            // Warn if database service is not set (pulses will be processed but not persisted)
            if (_databaseService == null)
            {
                LoggingService.Application.Warning(
                    "Starting pulse monitoring without database service - unique IDs will not be persisted across restarts",
                    ("Component", "PaymentPulseService"),
                    ("Port", portName));
            }
            
            // Reset last data received timestamp when starting (will be updated when we receive data)
            lock (_lastDataLock)
            {
                _lastDataReceivedAt = null;
            }
            
            await _deviceClient.StartAsync(portName, cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _deviceClient.StopAsync(cancellationToken).ConfigureAwait(false);
            // NOTE: We intentionally do NOT clear _processedUniqueIds here to prevent duplicate credits
            // if the service is restarted without calling InitializeAsync again.
            // Only clear _lastCounts (used for backward compatibility with old packet format).
            ResetCounters();
        }

        public void ResetCounters()
        {
            lock (_syncRoot)
            {
                _lastCounts.Clear(); // Clear old format counters (backward compatibility)
                // DO NOT clear _processedUniqueIds - it must persist across stop/start cycles
                // to prevent duplicate credits. The set is loaded from database on InitializeAsync
                // and should only be cleared on explicit reset or app shutdown.
            }
        }

        /// <summary>
        /// Clear all processed unique IDs from memory (for testing/reset purposes).
        /// This should be called after deleting all unique IDs from the database to keep them in sync.
        /// </summary>
        public void ClearProcessedUniqueIds()
        {
            lock (_syncRoot)
            {
                _processedUniqueIds.Clear();
                LoggingService.Application.Information("Cleared all processed unique IDs from memory", ("Component", "PaymentPulseService"));
            }
        }

        public void Dispose()
        {
            // Stop retry loop
            _retryCancellationTokenSource?.Cancel();
            
            // Wait a short time for pending retries to complete (but don't block indefinitely)
            try
            {
                if (_retryCancellationTokenSource != null)
                {
                    Task.Delay(TimeSpan.FromSeconds(2)).Wait(); // Give retries 2 seconds to complete
                }
            }
            catch
            {
                // Ignore timeout during shutdown
            }
            
            _retryCancellationTokenSource?.Dispose();
            
            _deviceClient.PulseCountReceived -= HandlePulseCountReceived;
            _deviceClient.Dispose();
        }

        private void HandlePulseCountReceived(object? sender, PulseCountEventArgs e)
        {
            // Update last data received timestamp to track if PCB is actually responding
            lock (_lastDataLock)
            {
                _lastDataReceivedAt = DateTime.UtcNow;
            }

            // Use unique ID to prevent duplicate credits (new format from PCB engineer)
            // If unique ID is all zeros (old format), fall back to pulseCount-based tracking
            bool shouldCredit;
            int pulsesToCredit;
            string uniqueIdString = Convert.ToHexString(e.UniqueId).ToLowerInvariant();
            bool hasValidUniqueId = !e.UniqueId.All(b => b == 0); // Check if unique ID is not all zeros

            lock (_syncRoot)
            {
                if (hasValidUniqueId)
                {
                    // New format: Use unique ID for duplicate detection
                    if (_processedUniqueIds.Contains(uniqueIdString))
                    {
                        // We've already processed this unique ID - ignore duplicate
                        shouldCredit = false;
                        pulsesToCredit = 0;
                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} IGNORED: {e.Identifier} - UniqueId={uniqueIdString} (already processed)");
                    }
                    else
                    {
                        // New unique ID - credit the full amount
                        shouldCredit = true;
                        pulsesToCredit = e.PulseCount;
                        _processedUniqueIds.Add(uniqueIdString);
                        
                        // Persist to database asynchronously (don't block the event handler)
                        var amountCredited = (decimal)pulsesToCredit; // 1 pulse = $1
                        var dbService = _databaseService; // Capture for closure
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (dbService != null)
                                {
                                    var saveResult = await dbService.SaveProcessedPulseUniqueIdAsync(
                                        uniqueIdString,
                                        e.Identifier.ToString(),
                                        e.PulseCount,
                                        amountCredited);
                                    
                                    if (saveResult.Success)
                                    {
                                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVED TO DB: UniqueId={uniqueIdString} | PulseCount={e.PulseCount} | Amount=${amountCredited:F2}");
                                    }
                                    else
                                    {
                                        // Save failed - add to retry queue to prevent data loss
                                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVE FAILED: UniqueId={uniqueIdString} | Error={saveResult.ErrorMessage} | Adding to retry queue");
                                        LoggingService.Application.Warning("Failed to persist unique ID to database, will retry", ("Component", "PaymentPulseService"), ("Error", saveResult.ErrorMessage ?? "Unknown error"), ("UniqueId", uniqueIdString));
                                        
                                        lock (_retryQueueLock)
                                        {
                                            if (_failedSavesQueue.Count < 100) // Limit queue size
                                            {
                                                _failedSavesQueue.Enqueue((uniqueIdString, e.Identifier.ToString(), e.PulseCount, amountCredited, DateTime.UtcNow));
                                            }
                                            else
                                            {
                                                LoggingService.Application.Error(
                                                    "Failed save retry queue is full, cannot queue retry",
                                                    null,
                                                    ("Component", "PaymentPulseService"),
                                                    ("UniqueId", uniqueIdString));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Database service not set - log warning (should not happen in normal operation)
                                    Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVE SKIPPED: Database service is null");
                                    LoggingService.Application.Warning(
                                        "Cannot persist unique ID - database service is null. Pulse was credited but will not be tracked across restarts.",
                                        ("Component", "PaymentPulseService"),
                                        ("UniqueId", uniqueIdString),
                                        ("PulseCount", e.PulseCount));
                                }
                            }
                            catch (Exception ex)
                            {
                                // Save exception - add to retry queue to prevent data loss
                                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVE EXCEPTION: UniqueId={uniqueIdString} | Exception={ex.Message} | Adding to retry queue");
                                LoggingService.Application.Warning("Exception persisting unique ID to database, will retry", ("Component", "PaymentPulseService"), ("Exception", ex.Message), ("UniqueId", uniqueIdString));
                                
                                lock (_retryQueueLock)
                                {
                                    if (_failedSavesQueue.Count < 100) // Limit queue size
                                    {
                                        _failedSavesQueue.Enqueue((uniqueIdString, e.Identifier.ToString(), e.PulseCount, amountCredited, DateTime.UtcNow));
                                    }
                                    else
                                    {
                                        LoggingService.Application.Error(
                                            "Failed save retry queue is full, cannot queue retry",
                                            ex,
                                            ("Component", "PaymentPulseService"),
                                            ("UniqueId", uniqueIdString));
                                    }
                                }
                            }
                        });
                        
                        // Also update lastCounts for backward compatibility
                        _lastCounts[e.Identifier] = e.PulseCount;
                    }
                }
                else
                {
                    // Old format (no unique ID) - fall back to pulseCount-based tracking
                    // NOTE: PulseCount is CUMULATIVE, so we must calculate the delta to avoid double-crediting
                    var hasLastCreditedCount = _lastCounts.TryGetValue(e.Identifier, out var lastCreditedCount);
                    
                    if (!hasLastCreditedCount)
                    {
                        // First value we've seen for this identifier - treat as full amount
                        // This avoids the off-by-one bug where using -1 as default would cause:
                        // pulsesToCredit = e.PulseCount - (-1) = e.PulseCount + 1 (over-credit by 1)
                        shouldCredit = true;
                        pulsesToCredit = e.PulseCount;
                        _lastCounts[e.Identifier] = e.PulseCount;
                    }
                    else if (e.PulseCount == lastCreditedCount)
                    {
                        // Duplicate packet - we've already credited this pulseCount
                        shouldCredit = false;
                        pulsesToCredit = 0;
                    }
                    else if (e.PulseCount < lastCreditedCount)
                    {
                        // Counter reset detected (PCB rebooted or reset) - treat as new transaction
                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} COUNTER RESET DETECTED: {e.Identifier} - LastCredited={lastCreditedCount}, Received={e.PulseCount}, treating as new transaction");
                        shouldCredit = true;
                        pulsesToCredit = e.PulseCount; // First value after reset is the full amount
                        _lastCounts[e.Identifier] = e.PulseCount;
                    }
                    else
                    {
                        // New pulseCount value - credit only the delta (increment since last credited)
                        // Example: lastCredited=3, new=6 -> credit 3 (not 6) to avoid double-crediting
                        pulsesToCredit = e.PulseCount - lastCreditedCount;
                        
                        // Defensive check: if delta is negative (shouldn't happen after reset check), treat as new
                        if (pulsesToCredit < 0)
                        {
                            Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} WARNING: Negative delta detected: LastCredited={lastCreditedCount}, Received={e.PulseCount}, treating as new transaction");
                            pulsesToCredit = e.PulseCount;
                        }
                        
                        shouldCredit = true;
                        _lastCounts[e.Identifier] = e.PulseCount;
                    }
                }
            }

            // Log every pulse received for debugging
            if (hasValidUniqueId)
            {
                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} RECEIVED: {e.Identifier} | RawPulseCount={e.PulseCount} | UniqueId={uniqueIdString} | WillCredit={pulsesToCredit} pulses (${pulsesToCredit:F2})");
            }
            else
            {
                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} RECEIVED: {e.Identifier} | RawPulseCount={e.PulseCount} | UniqueId=0000000000 (old format) | WillCredit={pulsesToCredit} pulses (${pulsesToCredit:F2})");
            }

            if (!shouldCredit)
            {
                if (!hasValidUniqueId)
                {
                    Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} IGNORED: {e.Identifier} - PulseCount={e.PulseCount} (already credited, duplicate packet)");
                }
                return;
            }

            Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} PROCESSING: {e.Identifier} | RawPulseCount={e.PulseCount} | UniqueId={uniqueIdString} | Crediting={pulsesToCredit} pulses = ${pulsesToCredit:F2}");

            LoggingService.Hardware.Information(
                "PulseService",
                "Pulse credit processed",
                ("Identifier", e.Identifier.ToString()),
                ("RawPulseCount", e.PulseCount),
                ("UniqueId", uniqueIdString),
                ("PulsesToCredit", pulsesToCredit),
                ("AmountToCredit", pulsesToCredit));

            // Pass the full pulseCount as the "delta" since that's what we're crediting
            // The event args still use "Delta" name for backward compatibility, but it now represents the full amount
            PulseDeltaProcessed?.Invoke(
                this,
                new PulseDeltaEventArgs(e.Identifier, pulsesToCredit, e.PulseCount, e.UniqueId, e.TimestampUtc));
        }
    }
}

