# PCB Payment Pulse System - Comprehensive Test Suite

## Overview

This test suite provides comprehensive coverage for the PCB payment pulse functionality, including packet parsing, duplicate detection, unique ID tracking, and database persistence.

## Test Files

### 1. `PulseDeviceClientTests.cs` - Packet Parsing & Serial Communication
**Location**: `Photobooth.Tests/Services/Payment/PulseDeviceClientTests.cs`

**Coverage**:
- ✅ New format packet parsing (16-byte payload with unique ID)
- ✅ Old format packet parsing (6-byte payload, backward compatibility)
- ✅ Unique ID extraction from packets
- ✅ Packet header validation
- ✅ Multiple packet handling
- ✅ Incomplete packet handling
- ✅ State management (IsRunning, CurrentPortName)
- ✅ Error handling (null/empty port names)
- ✅ Edge cases (invalid headers, incomplete packets)

**Key Tests**:
- `ParsePacket_NewFormat_ValidBillAccepter_ExtractsCorrectly` - Validates new 16-byte format
- `ParsePacket_NewFormat_ExtractsUniqueIdCorrectly` - Verifies unique ID extraction
- `ParsePacket_OldFormat_ValidPacket_ExtractsCorrectly` - Backward compatibility
- `ParsePacket_MultiplePackets_ShouldParseAll` - Multiple packet handling

### 2. `PaymentPulseServiceTests.cs` - Business Logic & Duplicate Detection
**Location**: `Photobooth.Tests/Services/Payment/PaymentPulseServiceTests.cs`

**Coverage**:
- ✅ Duplicate detection using unique IDs
- ✅ Multiple transactions with same amount (different unique IDs)
- ✅ Old format handling (all-zero unique IDs)
- ✅ Counter reset detection
- ✅ Identifier handling (BillAccepter vs CardAccepter)
- ✅ Database persistence integration
- ✅ State management
- ✅ Edge cases (zero pulse count, large amounts)

**Key Tests**:
- `HandlePulseCountReceived_NewUniqueId_ProcessesAndCredits` - New transaction processing
- `HandlePulseCountReceived_DuplicateUniqueId_IgnoresDuplicate` - Duplicate prevention
- `HandlePulseCountReceived_DifferentUniqueIds_BothProcessed` - Multiple $5 bills scenario
- `HandlePulseCountReceived_SameAmountDifferentIds_MultipleCredits` - Same amount, different IDs
- `InitializeAsync_LoadsUniqueIdsFromDatabase` - Database persistence on startup
- `HandlePulseCountReceived_NewUniqueId_SavesToDatabase` - Database save verification

### 3. `PaymentPulseIntegrationTests.cs` - End-to-End Integration
**Location**: `Photobooth.Tests/Integration/PaymentPulseIntegrationTests.cs`

**Coverage**:
- ✅ Database persistence (save/load unique IDs)
- ✅ Cross-restart persistence (simulating app restart)
- ✅ Multiple unique ID handling
- ✅ Duplicate prevention across restarts
- ✅ Database cleanup functionality
- ✅ Error handling (table doesn't exist, database errors)

**Key Tests**:
- `SaveAndLoadUniqueIds_PersistsAcrossRestarts` - Database persistence
- `InitializeAsync_LoadsUniqueIdsFromDatabase_PreventsDuplicates` - Restart simulation
- `SaveProcessedPulseUniqueId_DuplicateUniqueId_IgnoresDuplicate` - Duplicate handling
- `ProcessMultiplePulses_DifferentUniqueIds_AllProcessed` - Multiple transactions
- `CleanupOldProcessedPulseUniqueIds_RemovesOldRecords` - Database cleanup

## Running the Tests

### Run All PCB Tests
```bash
dotnet test --filter "FullyQualifiedName~PaymentPulse"
```

### Run Specific Test Classes
```bash
# Packet parsing tests
dotnet test --filter "FullyQualifiedName~PulseDeviceClientTests"

# Business logic tests
dotnet test --filter "FullyQualifiedName~PaymentPulseServiceTests"

# Integration tests
dotnet test --filter "FullyQualifiedName~PaymentPulseIntegrationTests"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~HandlePulseCountReceived_DuplicateUniqueId_IgnoresDuplicate"
```

## Test Coverage Summary

### PulseDeviceClient (Packet Parsing)
- **Total Tests**: 15+
- **Coverage Areas**:
  - Packet structure validation
  - Unique ID extraction
  - Old/new format compatibility
  - Error handling
  - State management

### PaymentPulseService (Business Logic)
- **Total Tests**: 20+
- **Coverage Areas**:
  - Duplicate detection
  - Unique ID tracking
  - Credit processing
  - Database integration
  - Edge cases

### Integration Tests
- **Total Tests**: 8+
- **Coverage Areas**:
  - Database persistence
  - Cross-restart behavior
  - End-to-end workflows
  - Error scenarios

**Total Test Count**: 40+ comprehensive tests

## Test Scenarios Covered

### ✅ Happy Path
- New unique ID → Processed and credited
- Multiple unique IDs → All processed
- Database save/load → Persists correctly

### ✅ Duplicate Prevention
- Same unique ID twice → Second ignored
- App restart → Loads from database, prevents duplicates
- Multiple $5 bills → Each has unique ID, all credited

### ✅ Edge Cases
- Zero pulse count → Handled gracefully
- Large pulse counts → Processed correctly
- Counter reset → Detected and handled
- Old format (no unique ID) → Falls back to pulse count tracking
- Incomplete packets → Waits for more data
- Invalid headers → Ignored

### ✅ Error Handling
- Null/empty port names → Throws ArgumentException
- Database errors → Handled gracefully
- Missing database table → Creates on-demand
- Network errors → Logged, doesn't crash

## Test Data

### Sample Packets Used in Tests

**New Format (16-byte payload)**:
```
02 02 10 00 01 00 00 00 05 00 00 00 00 00 08 9E D0 5B 1C D1
│  │  │  │  │  │  │  │  │  │  │  │  │  │  │  └─└─└─└─└─└─└─└─└─ Unique ID
│  │  │  │  │  │  │  └─└─ Pulse Count (5)
│  │  │  │  │  └─└─└─ Padding
│  │  │  │  └─ Identifier (BillAccepter = 0x01)
│  │  └─└─ Length (16 bytes)
└─└─ Header (0x02, 0x02)
```

**Old Format (6-byte payload)**:
```
02 02 06 00 01 05 00 00 00 00
│  │  │  │  │  └─└─ Pulse Count (5)
│  │  │  │  └─ Identifier
│  │  └─└─ Length (6 bytes)
└─└─ Header
```

## Notes

- Tests use **Moq** for mocking `IPulseDeviceClient` and `IDatabaseService`
- Integration tests use **real database instances** in temp directories
- Tests clean up after themselves (dispose resources, delete temp files)
- Reflection is used to access internal constructors (via `InternalsVisibleTo` attribute)
- All tests follow MSTest conventions with FluentAssertions

## Future Enhancements

Potential areas for additional testing:
- [ ] Concurrent packet processing (thread safety)
- [ ] Serial port error scenarios
- [ ] Database connection failures
- [ ] Performance testing (large number of unique IDs)
- [ ] Memory leak testing (long-running monitoring)

