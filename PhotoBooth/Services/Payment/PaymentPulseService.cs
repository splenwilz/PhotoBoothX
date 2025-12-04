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
        private bool _isInitialized = false;

        public event EventHandler<PulseDeltaEventArgs>? PulseDeltaProcessed;

        public bool IsRunning => _deviceClient.IsRunning;

        public string? CurrentPortName => _deviceClient.CurrentPortName;

        public PaymentPulseService()
            : this(new PulseDeviceClient(), null)
        {
        }

        internal PaymentPulseService(IPulseDeviceClient deviceClient, IDatabaseService? databaseService)
        {
            _deviceClient = deviceClient;
            _databaseService = databaseService;
            _deviceClient.PulseCountReceived += HandlePulseCountReceived;
        }

        /// <summary>
        /// Set the database service instance (called from MainWindow after database is created)
        /// </summary>
        public void SetDatabaseService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Initialize the service by loading processed unique IDs from the database.
        /// Call this on app startup after database is initialized.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

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

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to initialize PaymentPulseService", ex, ("Component", "PaymentPulseService"));
                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} INITIALIZATION ERROR: {ex.Message}");
            }
        }

        public async Task StartAsync(string portName, CancellationToken cancellationToken = default)
        {
            await _deviceClient.StartAsync(portName, cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            await _deviceClient.StopAsync().ConfigureAwait(false);
            ResetCounters();
        }

        public void ResetCounters()
        {
            lock (_syncRoot)
            {
                _lastCounts.Clear();
                _processedUniqueIds.Clear();
            }
        }

        public void Dispose()
        {
            _deviceClient.PulseCountReceived -= HandlePulseCountReceived;
            _deviceClient.Dispose();
        }

        private void HandlePulseCountReceived(object? sender, PulseCountEventArgs e)
        {
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
                                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVE FAILED: UniqueId={uniqueIdString} | Error={saveResult.ErrorMessage}");
                                        LoggingService.Application.Warning("Failed to persist unique ID to database", ("Component", "PaymentPulseService"), ("Error", saveResult.ErrorMessage ?? "Unknown error"));
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVE SKIPPED: Database service is null");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log but don't fail - in-memory tracking still works
                                Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} SAVE EXCEPTION: UniqueId={uniqueIdString} | Exception={ex.Message}");
                                LoggingService.Application.Warning("Failed to persist unique ID to database", ("Component", "PaymentPulseService"), ("Exception", ex.Message));
                            }
                        });
                        
                        // Also update lastCounts for backward compatibility
                        _lastCounts[e.Identifier] = e.PulseCount;
                    }
                }
                else
                {
                    // Old format (no unique ID) - fall back to pulseCount-based tracking
                    var lastCreditedCount = _lastCounts.TryGetValue(e.Identifier, out var value) ? value : -1;

                    if (e.PulseCount == lastCreditedCount)
                    {
                        // Duplicate packet - we've already credited this pulseCount
                        shouldCredit = false;
                        pulsesToCredit = 0;
                    }
                    else if (e.PulseCount < lastCreditedCount)
                    {
                        // Counter reset detected (PCB rebooted or reset)
                        Console.WriteLine($"[PaymentPulseService] {DateTime.Now:HH:mm:ss} COUNTER RESET DETECTED: {e.Identifier} - LastCredited={lastCreditedCount}, Received={e.PulseCount}, treating as new transaction");
                        shouldCredit = true;
                        pulsesToCredit = e.PulseCount;
                        _lastCounts[e.Identifier] = e.PulseCount;
                    }
                    else
                    {
                        // New pulseCount value - credit the full amount
                        shouldCredit = true;
                        pulsesToCredit = e.PulseCount;
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

