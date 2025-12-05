using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Photobooth.Services;
using Photobooth.Models;

namespace Photobooth.Tests.Services.Payment
{
    /// <summary>
    /// Comprehensive unit tests for PaymentPulseService
    /// Tests duplicate detection, unique ID tracking, and credit processing
    /// </summary>
    [TestClass]
    public class PaymentPulseServiceTests : IDisposable
    {
        private Mock<IPulseDeviceClient>? _mockDeviceClient;
        private Mock<IDatabaseService>? _mockDatabaseService;
        private PaymentPulseService? _service;
        private List<PulseDeltaEventArgs>? _processedEvents;
        private bool _disposed = false;

        [TestInitialize]
        public void Setup()
        {
            _mockDeviceClient = new Mock<IPulseDeviceClient>();
            _mockDatabaseService = new Mock<IDatabaseService>();
            _processedEvents = new List<PulseDeltaEventArgs>();

            // Setup mock device client
            _mockDeviceClient.Setup(x => x.IsRunning).Returns(false);
            _mockDeviceClient.Setup(x => x.CurrentPortName).Returns((string?)null);

            // Setup default mock database service responses
            _mockDatabaseService
                .Setup(x => x.SaveProcessedPulseUniqueIdAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<decimal>()))
                .ReturnsAsync(Photobooth.Models.DatabaseResult.SuccessResult());

            // Create service with mocks using public constructor (allows dependency injection for testing)
            _service = new PaymentPulseService(_mockDeviceClient.Object, _mockDatabaseService.Object);
            
            // Ensure database service is set (constructor accepts nullable, but we want it set for tests)
            _service.SetDatabaseService(_mockDatabaseService.Object);
            
            _service.PulseDeltaProcessed += (sender, args) => _processedEvents!.Add(args);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_disposed && _service != null)
            {
                _service.Dispose();
                _disposed = true;
            }
        }

        #region Duplicate Detection Tests

        [TestMethod]
        public void HandlePulseCountReceived_NewUniqueId_ProcessesAndCredits()
        {
            // Arrange
            var uniqueId = new byte[] { 0x9E, 0xD0, 0x5B, 0x1C, 0xD1, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Assert
            _processedEvents.Should().NotBeNull();
            _processedEvents.Should().HaveCount(1);
            _processedEvents![0].Delta.Should().Be(5); // Full pulse count credited
            _processedEvents[0].Identifier.Should().Be(PulseIdentifier.BillAccepter);
            _processedEvents[0].RawCount.Should().Be(5);
        }

        [TestMethod]
        public void HandlePulseCountReceived_DuplicateUniqueId_IgnoresDuplicate()
        {
            // Arrange
            var uniqueId = new byte[] { 0x9E, 0xD0, 0x5B, 0x1C, 0xD1, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args1 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);
            var args2 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId, // Same unique ID
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);
            _processedEvents!.Clear(); // Clear first event
            _mockDeviceClient.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args2);

            // Assert: Second event should be ignored (duplicate)
            _processedEvents.Should().BeEmpty(); // No new events processed
        }

        [TestMethod]
        public void HandlePulseCountReceived_DifferentUniqueIds_BothProcessed()
        {
            // Arrange
            var uniqueId1 = new byte[] { 0x9E, 0xD0, 0x5B, 0x1C, 0xD1, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var uniqueId2 = new byte[] { 0x4A, 0x78, 0xEB, 0x6E, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00 };
            
            var args1 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId1,
                timestampUtc: DateTime.UtcNow);
            var args2 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5, // Same amount, different ID
                uniqueId: uniqueId2,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);
            _mockDeviceClient.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args2);

            // Assert: Both should be processed
            _processedEvents.Should().NotBeNull();
            _processedEvents.Should().HaveCount(2);
            _processedEvents![0].Delta.Should().Be(5);
            _processedEvents[1].Delta.Should().Be(5);
        }

        [TestMethod]
        public void HandlePulseCountReceived_SameAmountDifferentIds_MultipleCredits()
        {
            // Arrange: Multiple $5 bills with different unique IDs
            var uniqueIds = new[]
            {
                new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            };

            // Act: Process 3 separate $5 transactions
            for (int i = 0; i < 3; i++)
            {
                var args = new PulseCountEventArgs(
                    PulseIdentifier.BillAccepter,
                    pulseCount: 5,
                    uniqueId: uniqueIds[i],
                    timestampUtc: DateTime.UtcNow);
                _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);
            }

            // Assert: All 3 should be credited
            _processedEvents.Should().NotBeNull();
            _processedEvents.Should().HaveCount(3);
            _processedEvents!.Sum(e => e.Delta).Should().Be(15); // Total $15
        }

        #endregion

        #region Old Format (No Unique ID) Tests

        [TestMethod]
        public void HandlePulseCountReceived_OldFormat_AllZerosUniqueId_UsesPulseCountTracking()
        {
            // Arrange: Old format (all zeros unique ID)
            var emptyUniqueId = new byte[10]; // All zeros
            var args1 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);
            var args2 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5, // Same pulse count
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);
            _processedEvents!.Clear();
            _mockDeviceClient.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args2);

            // Assert: Second should be ignored (same pulse count)
            _processedEvents.Should().BeEmpty();
        }

        [TestMethod]
        public void HandlePulseCountReceived_OldFormat_NewPulseCount_Processes()
        {
            // Arrange: Old format with increasing pulse count
            var emptyUniqueId = new byte[10];
            var args1 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);
            var args2 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 8, // New pulse count
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);
            _mockDeviceClient.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args2);

            // Assert: Both should be processed (different pulse counts)
            // First packet: credits full amount (5)
            // Second packet: credits delta (8 - 5 = 3)
            _processedEvents.Should().NotBeNull();
            _processedEvents.Should().HaveCount(2);
            _processedEvents![0].Delta.Should().Be(5, "First packet should credit full pulse count");
            _processedEvents[1].Delta.Should().Be(3, "Second packet should credit delta (8 - 5 = 3), not full amount");
        }

        [TestMethod]
        public void HandlePulseCountReceived_OldFormat_FirstPacket_CreditsFullAmount()
        {
            // Arrange: First packet for an identifier (no previous entry in _lastCounts)
            // This tests the fix for the off-by-one bug where using -1 as default would cause:
            // pulsesToCredit = e.PulseCount - (-1) = e.PulseCount + 1 (over-credit by 1)
            var emptyUniqueId = new byte[10]; // All zeros (old format)
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 3, // First packet with 3 pulses
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act: Process first packet (no previous entry)
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Assert: Should credit exactly 3 pulses (not 4, which would be the bug)
            _processedEvents.Should().NotBeNull();
            _processedEvents.Should().HaveCount(1);
            _processedEvents![0].Delta.Should().Be(3, "First packet should credit the full pulse count, not pulseCount + 1");
            _processedEvents[0].RawCount.Should().Be(3);
            _processedEvents[0].Identifier.Should().Be(PulseIdentifier.BillAccepter);
        }

        [TestMethod]
        public void HandlePulseCountReceived_OldFormat_CounterReset_ProcessesAsNew()
        {
            // Arrange: Counter reset scenario (pulse count decreases)
            var emptyUniqueId = new byte[10];
            var args1 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 10,
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);
            var args2 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 3, // Counter reset (lower than previous)
                uniqueId: emptyUniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);
            _mockDeviceClient.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args2);

            // Assert: Both should be processed (counter reset detected)
            _processedEvents.Should().NotBeNull();
            _processedEvents.Should().HaveCount(2);
            _processedEvents![1].Delta.Should().Be(3);
        }

        #endregion

        #region Identifier Tests

        [TestMethod]
        public void HandlePulseCountReceived_BillAccepter_SetsCorrectIdentifier()
        {
            // Arrange
            var uniqueId = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Assert
            _processedEvents.Should().NotBeNull();
            _processedEvents![0].Identifier.Should().Be(PulseIdentifier.BillAccepter);
        }

        [TestMethod]
        public void HandlePulseCountReceived_CardAccepter_SetsCorrectIdentifier()
        {
            // Arrange
            var uniqueId = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.CardAccepter,
                pulseCount: 3,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Assert
            _processedEvents.Should().NotBeNull();
            _processedEvents![0].Identifier.Should().Be(PulseIdentifier.CardAccepter);
        }

        #endregion

        #region Database Persistence Tests

        [TestMethod]
        public async Task InitializeAsync_LoadsUniqueIdsFromDatabase()
        {
            // Arrange
            var savedUniqueIds = new HashSet<string>
            {
                "9ed05b1cd10000000000",
                "4a78eb6e160000000000",
                "00000000000000000001"
            };
            _mockDatabaseService!
                .Setup(x => x.LoadProcessedPulseUniqueIdsAsync())
                .ReturnsAsync(Photobooth.Models.DatabaseResult<HashSet<string>>.SuccessResult(savedUniqueIds));

            // Act
            await _service!.InitializeAsync();

            // Assert
            // After initialization, duplicate unique IDs should be ignored
            var uniqueId = new byte[] { 0x9E, 0xD0, 0x5B, 0x1C, 0xD1, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            _processedEvents!.Clear();
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Should be ignored (already in database)
            _processedEvents.Should().BeEmpty();
        }

        [TestMethod]
        public async Task HandlePulseCountReceived_NewUniqueId_SavesToDatabase()
        {
            // Arrange
            var uniqueId = new byte[] { 0x9E, 0xD0, 0x5B, 0x1C, 0xD1, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            // Database service is already set in Setup() - no need to set again
            var expectedUniqueIdHex = Convert.ToHexString(uniqueId).ToLowerInvariant();
            
            // Use TaskCompletionSource to synchronize async database-save test instead of unsynchronized flag
            // This eliminates data race, removes manual polling, and handles timeout cleanly
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _mockDatabaseService!
                .Setup(x => x.SaveProcessedPulseUniqueIdAsync(
                    It.Is<string>(id => id == expectedUniqueIdHex),
                    "BillAccepter",
                    5,
                    5.00m))
                .ReturnsAsync(Photobooth.Models.DatabaseResult.SuccessResult())
                .Callback(() => tcs.TrySetResult(true));

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Wait for async save to complete (fire-and-forget Task.Run needs time to execute)
            // NOTE: Production code uses fire-and-forget pattern to avoid blocking event handler.
            // Using TaskCompletionSource ensures proper synchronization without polling.
            var maxWaitTime = TimeSpan.FromSeconds(2); // Reasonable timeout for test
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(maxWaitTime));
            
            // Assert: Verify the save was called within timeout
            completedTask.Should().BeSameAs(tcs.Task,
                $"Database save should have been called within {maxWaitTime.TotalSeconds} seconds");
            
            _mockDatabaseService.Verify(
                x => x.SaveProcessedPulseUniqueIdAsync(
                    It.Is<string>(id => id == expectedUniqueIdHex),
                    "BillAccepter",
                    5,
                    5.00m),
                Times.Once);
        }

        #endregion

        #region State Management Tests

        [TestMethod]
        public void IsRunning_DelegatesToDeviceClient()
        {
            // Arrange
            _mockDeviceClient!.Setup(x => x.IsRunning).Returns(true);

            // Act & Assert
            _service!.IsRunning.Should().BeTrue();
        }

        [TestMethod]
        public void CurrentPortName_DelegatesToDeviceClient()
        {
            // Arrange
            _mockDeviceClient!.Setup(x => x.CurrentPortName).Returns("COM5");

            // Act & Assert
            _service!.CurrentPortName.Should().Be("COM5");
        }

        [TestMethod]
        public async Task StartAsync_DelegatesToDeviceClient()
        {
            // Arrange
            _mockDeviceClient!.Setup(x => x.StartAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service!.StartAsync("COM5");

            // Assert
            _mockDeviceClient.Verify(x => x.StartAsync("COM5", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task StopAsync_DelegatesToDeviceClient()
        {
            // Arrange
            // Explicitly pass CancellationToken to avoid expression tree error with optional parameters
            _mockDeviceClient!.Setup(x => x.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            await _service!.StopAsync();

            // Assert
            _mockDeviceClient.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void ResetCounters_ClearsProcessedUniqueIds()
        {
            // Arrange: Process a unique ID first
            var uniqueId = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args1 = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 5,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);

            // Act: Reset counters
            _service!.ResetCounters();

            // Act: Try to process same unique ID again
            _processedEvents!.Clear();
            _mockDeviceClient.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args1);

            // Assert: Should be processed again (reset cleared the tracking)
            _processedEvents.Should().HaveCount(1);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void HandlePulseCountReceived_VeryLargePulseCount_ProcessesCorrectly()
        {
            // Arrange: Large pulse count (e.g., $100)
            var uniqueId = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 100,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Assert
            _processedEvents![0].Delta.Should().Be(100);
        }

        [TestMethod]
        public void HandlePulseCountReceived_ZeroPulseCount_ProcessesButCreditsZero()
        {
            // Arrange: Zero pulse count (edge case)
            var uniqueId = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var args = new PulseCountEventArgs(
                PulseIdentifier.BillAccepter,
                pulseCount: 0,
                uniqueId: uniqueId,
                timestampUtc: DateTime.UtcNow);

            // Act
            _mockDeviceClient!.Raise(x => x.PulseCountReceived += null, _mockDeviceClient.Object, args);

            // Assert
            _processedEvents![0].Delta.Should().Be(0);
        }

        #endregion
    }
}

