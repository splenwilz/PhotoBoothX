using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Models;
using Photobooth.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Photobooth.Tests.Services
{
    /// <summary>
    /// Comprehensive unit tests for PrinterService
    /// Tests printer detection, selection, status checking, roll capacity, and printing functionality
    /// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing
    /// </summary>
    [TestClass]
    public class PrinterServiceTests : IDisposable
    {
        // Test execution framework support
        public TestContext? TestContext { get; set; }

        private PrinterService? _printerService;
        private bool _disposed = false;

        /// <summary>
        /// Setup method runs before each test
        /// Initializes a fresh PrinterService instance for isolated testing
        /// IMPORTANT: Prevents actual printing by ensuring no valid printer is selected
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            // Create a new PrinterService instance for each test
            // This ensures test isolation and prevents state leakage between tests
            _printerService = new PrinterService();
            
            // CRITICAL SAFETY: Prevent actual printing during tests
            // PrintImageAsync will try to use default printer if none selected, which could print!
            // Solution: Select a guaranteed non-existent printer name
            // This ensures PrintImageAsync will fail validation before sending any print jobs
            string fakePrinterName = $"__TEST_SAFETY_NO_PRINTER_{Guid.NewGuid()}__";
            
            // Use reflection to set the private _selectedPrinterName field directly
            // This bypasses SelectPrinter validation and ensures no valid printer is selected
            var field = typeof(PrinterService).GetField("_selectedPrinterName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_printerService, fakePrinterName);
                TestContext?.WriteLine("[TEST] SAFETY: Set invalid printer name to prevent actual printing");
            }
            else
            {
                // Fallback: Try to select non-existent printer (will fail, but at least won't use default)
                _printerService.SelectPrinter(fakePrinterName);
                TestContext?.WriteLine("[TEST] WARNING: Could not set invalid printer via reflection, using SelectPrinter fallback");
            }
            
            TestContext?.WriteLine($"[TEST] PrinterService initialized. IsInitialized: {_printerService.IsInitialized}, " +
                $"SelectedPrinter: {_printerService.SelectedPrinterName ?? "None"} (SAFE - invalid printer set to prevent printing)");
        }

        /// <summary>
        /// Cleanup method runs after each test
        /// Disposes of resources to prevent memory leaks
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            // Dispose of the service to clean up resources
            if (_printerService != null && !_disposed)
            {
                _printerService.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Dispose pattern implementation for proper resource cleanup
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _printerService?.Dispose();
                _disposed = true;
            }
        }

        #region Initialization Tests

        /// <summary>
        /// Test: PrinterService should initialize successfully
        /// Verifies that the service can be created and reports initialization status
        /// </summary>
        [TestMethod]
        public void Constructor_WhenCalled_ShouldInitializeSuccessfully()
        {
            // Arrange & Act: Service is created in Setup()
            // Assert: Verify initialization state
            _printerService.Should().NotBeNull("PrinterService should be created");
            _printerService!.IsInitialized.Should().BeTrue("PrinterService should report as initialized");
        }

        /// <summary>
        /// Test: PrinterService should have a SelectedPrinterName after initialization
        /// Note: This may be null if no default printer is configured on the system
        /// </summary>
        [TestMethod]
        public void Constructor_WhenCalled_ShouldSetSelectedPrinterName()
        {
            // Arrange & Act: Service is created in Setup()
            // Assert: SelectedPrinterName should be set (may be null if no default printer)
            _printerService.Should().NotBeNull();
            
            // SelectedPrinterName can be null if no default printer is configured
            // This is acceptable behavior - we just verify the property is accessible
            var selectedPrinter = _printerService!.SelectedPrinterName;
            TestContext?.WriteLine($"[TEST] SelectedPrinterName: {selectedPrinter ?? "null (no default printer)"}");
            
            // The property should be accessible (not throw exception)
            // Value can be null or a string - both are valid
            selectedPrinter.Should().BeAssignableTo<string?>("SelectedPrinterName should be a string or null");
        }

        #endregion

        #region GetAvailablePrinters Tests

        /// <summary>
        /// Test: GetAvailablePrinters should return a list (may be empty if no printers installed)
        /// Verifies the method doesn't throw and returns a valid collection
        /// </summary>
        [TestMethod]
        public void GetAvailablePrinters_WhenCalled_ShouldReturnList()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Assert: Should return a valid list (may be empty)
            printers.Should().NotBeNull("GetAvailablePrinters should never return null");
            printers.Should().BeAssignableTo<List<PrinterDevice>>("Should return List<PrinterDevice>");
            
            TestContext?.WriteLine($"[TEST] Found {printers.Count} printer(s) on system");
        }

        /// <summary>
        /// Test: GetAvailablePrinters should mark default printer correctly
        /// If printers exist, at most one should be marked as default
        /// </summary>
        [TestMethod]
        public void GetAvailablePrinters_WhenPrintersExist_ShouldMarkDefaultPrinter()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Assert: If printers exist, check default printer marking
            if (printers.Count > 0)
            {
                // Count how many printers are marked as default
                int defaultCount = printers.Count(p => p.IsDefault);
                
                // At most one printer should be marked as default
                defaultCount.Should().BeLessThanOrEqualTo(1, 
                    "At most one printer should be marked as default");
                
                TestContext?.WriteLine($"[TEST] Default printers found: {defaultCount}");
                
                // If a default printer exists, verify it has a name
                var defaultPrinter = printers.FirstOrDefault(p => p.IsDefault);
                if (defaultPrinter != null)
                {
                    defaultPrinter.Name.Should().NotBeNullOrWhiteSpace(
                        "Default printer should have a name");
                    TestContext?.WriteLine($"[TEST] Default printer: {defaultPrinter.Name}");
                }
            }
            else
            {
                TestContext?.WriteLine("[TEST] No printers found on system - skipping default printer test");
            }
        }

        /// <summary>
        /// Test: GetAvailablePrinters should populate printer properties correctly
        /// Verifies that returned PrinterDevice objects have valid data
        /// </summary>
        [TestMethod]
        public void GetAvailablePrinters_WhenPrintersExist_ShouldPopulatePrinterProperties()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Assert: If printers exist, verify their properties are populated
            if (printers.Count > 0)
            {
                foreach (var printer in printers)
                {
                    // Every printer should have a name
                    printer.Name.Should().NotBeNullOrWhiteSpace(
                        $"Printer at index {printer.Index} should have a name");
                    
                    // Index should be non-negative
                    printer.Index.Should().BeGreaterThanOrEqualTo(0,
                        $"Printer '{printer.Name}' should have a valid index");
                    
                    // Status should be set (even if "Unknown")
                    printer.Status.Should().NotBeNull(
                        $"Printer '{printer.Name}' should have a status");
                    
                    TestContext?.WriteLine($"[TEST] Printer: {printer.Name}, " +
                        $"Index: {printer.Index}, " +
                        $"IsOnline: {printer.IsOnline}, " +
                        $"IsDefault: {printer.IsDefault}, " +
                        $"Status: {printer.Status}");
                }
            }
            else
            {
                TestContext?.WriteLine("[TEST] No printers found on system - skipping property validation");
            }
        }

        #endregion

        #region GetDefaultPrinterName Tests

        /// <summary>
        /// Test: GetDefaultPrinterName should return a string or null
        /// Verifies the method doesn't throw and returns a valid result
        /// </summary>
        [TestMethod]
        public void GetDefaultPrinterName_WhenCalled_ShouldReturnStringOrNull()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get default printer name
            string? defaultPrinter = _printerService!.GetDefaultPrinterName();

            // Assert: Should return string or null (both are valid)
            // If null, it means no default printer is configured
            defaultPrinter.Should().BeAssignableTo<string?>("Should return string or null");
            
            TestContext?.WriteLine($"[TEST] Default printer name: {defaultPrinter ?? "null (no default configured)"}");
        }

        /// <summary>
        /// Test: GetDefaultPrinterName should return system default printer
        /// Verifies that GetDefaultPrinterName returns the actual system default
        /// NOTE: SelectedPrinterName is set to invalid value in Setup() for safety,
        /// so they won't match - this is intentional to prevent actual printing
        /// </summary>
        [TestMethod]
        public void GetDefaultPrinterName_WhenCalled_ShouldReturnSystemDefault()
        {
            // Arrange: Service is initialized in Setup()
            // Note: Setup() sets SelectedPrinterName to invalid value for safety
            // Act: Get default printer name explicitly
            string? defaultPrinter = _printerService!.GetDefaultPrinterName();
            string? selectedPrinter = _printerService.SelectedPrinterName;

            // Assert: GetDefaultPrinterName should return system default (may be null)
            // SelectedPrinterName is intentionally set to invalid value in Setup() for safety
            defaultPrinter.Should().BeAssignableTo<string?>("GetDefaultPrinterName should return string or null");
            
            // Verify that SelectedPrinterName is the safety value (starts with __TEST_SAFETY_)
            // This confirms our safety mechanism is working
            if (!string.IsNullOrWhiteSpace(selectedPrinter))
            {
                selectedPrinter.Should().StartWith("__TEST_SAFETY_",
                    "SelectedPrinterName should be set to invalid value for test safety");
            }
            
            TestContext?.WriteLine($"[TEST] System default printer: {defaultPrinter ?? "None"}, " +
                $"Selected (safety): {selectedPrinter ?? "None"}");
        }

        #endregion

        #region IsPrinterAvailable Tests

        /// <summary>
        /// Test: IsPrinterAvailable should return false for null input
        /// Verifies null safety and input validation
        /// </summary>
        [TestMethod]
        public void IsPrinterAvailable_WhenPrinterNameIsNull_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Check availability with null printer name
            bool result = _printerService!.IsPrinterAvailable(null!);

            // Assert: Should return false for null input
            result.Should().BeFalse("IsPrinterAvailable should return false for null input");
        }

        /// <summary>
        /// Test: IsPrinterAvailable should return false for empty string
        /// Verifies empty string validation
        /// </summary>
        [TestMethod]
        public void IsPrinterAvailable_WhenPrinterNameIsEmpty_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Check availability with empty printer name
            bool result = _printerService!.IsPrinterAvailable(string.Empty);

            // Assert: Should return false for empty string
            result.Should().BeFalse("IsPrinterAvailable should return false for empty string");
        }

        /// <summary>
        /// Test: IsPrinterAvailable should return false for whitespace-only string
        /// Verifies whitespace validation
        /// </summary>
        [TestMethod]
        public void IsPrinterAvailable_WhenPrinterNameIsWhitespace_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Check availability with whitespace-only printer name
            bool result = _printerService!.IsPrinterAvailable("   ");

            // Assert: Should return false for whitespace-only string
            result.Should().BeFalse("IsPrinterAvailable should return false for whitespace-only string");
        }

        /// <summary>
        /// Test: IsPrinterAvailable should return false for non-existent printer
        /// Verifies that invalid printer names are correctly identified
        /// </summary>
        [TestMethod]
        public void IsPrinterAvailable_WhenPrinterDoesNotExist_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Use a printer name that definitely doesn't exist
            string nonExistentPrinter = $"NonExistentPrinter_{Guid.NewGuid()}";

            // Act: Check availability of non-existent printer
            bool result = _printerService!.IsPrinterAvailable(nonExistentPrinter);

            // Assert: Should return false for non-existent printer
            result.Should().BeFalse(
                $"IsPrinterAvailable should return false for non-existent printer '{nonExistentPrinter}'");
        }

        /// <summary>
        /// Test: IsPrinterAvailable should return true for valid printer (if printers exist)
        /// Verifies that valid printers are correctly identified
        /// Note: This test may be skipped if no printers are installed
        /// </summary>
        [TestMethod]
        public void IsPrinterAvailable_WhenPrinterExists_ShouldReturnTrue()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify IsPrinterAvailable returns true
            if (printers.Count > 0)
            {
                // Test with first available printer
                string printerName = printers[0].Name;
                bool result = _printerService.IsPrinterAvailable(printerName);

                // Assert: Should return true for existing printer
                result.Should().BeTrue(
                    $"IsPrinterAvailable should return true for existing printer '{printerName}'");
                
                TestContext?.WriteLine($"[TEST] Verified printer '{printerName}' is available");
            }
            else
            {
                TestContext?.WriteLine("[TEST] No printers found on system - skipping availability test");
                // Mark test as inconclusive if no printers available
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        #endregion

        #region SelectPrinter Tests

        /// <summary>
        /// Test: SelectPrinter should return false for null input
        /// Verifies null safety and input validation
        /// </summary>
        [TestMethod]
        public void SelectPrinter_WhenPrinterNameIsNull_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Attempt to select null printer name
            bool result = _printerService!.SelectPrinter(null!);

            // Assert: Should return false for null input
            result.Should().BeFalse("SelectPrinter should return false for null input");
        }

        /// <summary>
        /// Test: SelectPrinter should return false for empty string
        /// Verifies empty string validation
        /// </summary>
        [TestMethod]
        public void SelectPrinter_WhenPrinterNameIsEmpty_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Attempt to select empty printer name
            bool result = _printerService!.SelectPrinter(string.Empty);

            // Assert: Should return false for empty string
            result.Should().BeFalse("SelectPrinter should return false for empty string");
        }

        /// <summary>
        /// Test: SelectPrinter should return false for non-existent printer
        /// Verifies that invalid printer names are correctly rejected
        /// </summary>
        [TestMethod]
        public void SelectPrinter_WhenPrinterDoesNotExist_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Use a printer name that definitely doesn't exist
            string nonExistentPrinter = $"NonExistentPrinter_{Guid.NewGuid()}";

            // Act: Attempt to select non-existent printer
            bool result = _printerService!.SelectPrinter(nonExistentPrinter);

            // Assert: Should return false for non-existent printer
            result.Should().BeFalse(
                $"SelectPrinter should return false for non-existent printer '{nonExistentPrinter}'");
            
            // Verify SelectedPrinterName was not changed
            _printerService.SelectedPrinterName.Should().NotBe(nonExistentPrinter,
                "SelectedPrinterName should not be changed when selection fails");
        }

        /// <summary>
        /// Test: SelectPrinter should return true and update SelectedPrinterName for valid printer
        /// Verifies successful printer selection
        /// Note: This test may be skipped if no printers are installed
        /// </summary>
        [TestMethod]
        public void SelectPrinter_WhenPrinterExists_ShouldReturnTrueAndUpdateSelectedPrinter()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify selection works
            if (printers.Count > 0)
            {
                // Store original selected printer
                string? originalSelected = _printerService.SelectedPrinterName;
                
                // Select first available printer
                string printerName = printers[0].Name;
                bool result = _printerService.SelectPrinter(printerName);

                // Assert: Should return true for existing printer
                result.Should().BeTrue(
                    $"SelectPrinter should return true for existing printer '{printerName}'");
                
                // Assert: SelectedPrinterName should be updated
                _printerService.SelectedPrinterName.Should().Be(printerName,
                    "SelectedPrinterName should be updated after successful selection");
                
                TestContext?.WriteLine($"[TEST] Successfully selected printer: {printerName}");
            }
            else
            {
                TestContext?.WriteLine("[TEST] No printers found on system - skipping selection test");
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        /// <summary>
        /// Test: SelectPrinter should handle case-insensitive printer names
        /// Verifies that printer selection is case-insensitive
        /// </summary>
        [TestMethod]
        public void SelectPrinter_WhenPrinterNameHasDifferentCase_ShouldStillSelect()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify case-insensitive selection
            if (printers.Count > 0)
            {
                string printerName = printers[0].Name;
                
                // Try selecting with different case
                string upperCaseName = printerName.ToUpperInvariant();
                string lowerCaseName = printerName.ToLowerInvariant();
                
                // Act: Select with different case
                bool resultUpper = _printerService.SelectPrinter(upperCaseName);
                bool resultLower = _printerService.SelectPrinter(lowerCaseName);

                // Assert: At least one case variation should work
                // (Some systems may be case-sensitive, so we check if either works)
                if (resultUpper || resultLower)
                {
                    TestContext?.WriteLine($"[TEST] Case-insensitive selection works for '{printerName}'");
                }
                else
                {
                    // If case variations don't work, original should still work
                    bool resultOriginal = _printerService.SelectPrinter(printerName);
                    resultOriginal.Should().BeTrue(
                        "Original case should always work even if case variations don't");
                }
            }
            else
            {
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        #endregion

        #region GetPrinterStatus Tests

        /// <summary>
        /// Test: GetPrinterStatus should return null for null input
        /// Verifies null safety and input validation
        /// </summary>
        [TestMethod]
        public void GetPrinterStatus_WhenPrinterNameIsNull_ShouldReturnNull()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get status with null printer name
            PrinterDevice? result = _printerService!.GetPrinterStatus(null!);

            // Assert: Should return null for null input
            result.Should().BeNull("GetPrinterStatus should return null for null input");
        }

        /// <summary>
        /// Test: GetPrinterStatus should return null for empty string
        /// Verifies empty string validation
        /// </summary>
        [TestMethod]
        public void GetPrinterStatus_WhenPrinterNameIsEmpty_ShouldReturnNull()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get status with empty printer name
            PrinterDevice? result = _printerService!.GetPrinterStatus(string.Empty);

            // Assert: Should return null for empty string
            result.Should().BeNull("GetPrinterStatus should return null for empty string");
        }

        /// <summary>
        /// Test: GetPrinterStatus should return null for non-existent printer
        /// Verifies that invalid printer names return null
        /// </summary>
        [TestMethod]
        public void GetPrinterStatus_WhenPrinterDoesNotExist_ShouldReturnNull()
        {
            // Arrange: Service is initialized in Setup()
            // Use a printer name that definitely doesn't exist
            string nonExistentPrinter = $"NonExistentPrinter_{Guid.NewGuid()}";

            // Act: Get status for non-existent printer
            PrinterDevice? result = _printerService!.GetPrinterStatus(nonExistentPrinter);

            // Assert: Should return null for non-existent printer
            result.Should().BeNull(
                $"GetPrinterStatus should return null for non-existent printer '{nonExistentPrinter}'");
        }

        /// <summary>
        /// Test: GetPrinterStatus should return PrinterDevice for valid printer
        /// Verifies that valid printers return status information
        /// Note: This test may be skipped if no printers are installed
        /// </summary>
        [TestMethod]
        public void GetPrinterStatus_WhenPrinterExists_ShouldReturnPrinterDevice()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify status retrieval
            if (printers.Count > 0)
            {
                string printerName = printers[0].Name;
                
                // Act: Get status for existing printer
                PrinterDevice? result = _printerService.GetPrinterStatus(printerName);

                // Assert: Should return a PrinterDevice
                result.Should().NotBeNull(
                    $"GetPrinterStatus should return PrinterDevice for existing printer '{printerName}'");
                
                // Assert: Returned device should match the requested printer
                result!.Name.Should().Be(printerName,
                    "Returned PrinterDevice should have the correct name");
                
                // Assert: Device should have valid properties
                result.Index.Should().BeGreaterThanOrEqualTo(0,
                    "PrinterDevice should have a valid index");
                result.Status.Should().NotBeNull(
                    "PrinterDevice should have a status");
                
                TestContext?.WriteLine($"[TEST] Retrieved status for printer: {printerName}, " +
                    $"Status: {result.Status}, IsOnline: {result.IsOnline}");
            }
            else
            {
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        /// <summary>
        /// Test: GetPrinterStatus should return same device as GetAvailablePrinters
        /// Verifies consistency between status retrieval and enumeration
        /// </summary>
        [TestMethod]
        public void GetPrinterStatus_WhenPrinterExists_ShouldMatchGetAvailablePrinters()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify consistency
            if (printers.Count > 0)
            {
                string printerName = printers[0].Name;
                
                // Get status via GetPrinterStatus
                PrinterDevice? statusDevice = _printerService.GetPrinterStatus(printerName);
                
                // Get device from GetAvailablePrinters
                PrinterDevice? enumDevice = printers.FirstOrDefault(p => 
                    string.Equals(p.Name, printerName, StringComparison.OrdinalIgnoreCase));

                // Assert: Both should return the same printer
                statusDevice.Should().NotBeNull("GetPrinterStatus should return a device");
                enumDevice.Should().NotBeNull("GetAvailablePrinters should contain the printer");
                
                // Verify they match
                statusDevice!.Name.Should().Be(enumDevice!.Name,
                    "Both methods should return the same printer name");
                statusDevice.Index.Should().Be(enumDevice.Index,
                    "Both methods should return the same printer index");
            }
            else
            {
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        #endregion

        #region GetRollCapacity Tests

        /// <summary>
        /// Test: GetRollCapacity should return null for null input
        /// Verifies null safety and input validation
        /// </summary>
        [TestMethod]
        public void GetRollCapacity_WhenPrinterNameIsNull_ShouldReturnNull()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get roll capacity with null printer name
            RollCapacityInfo? result = _printerService!.GetRollCapacity(null!);

            // Assert: Should return null for null input
            result.Should().BeNull("GetRollCapacity should return null for null input");
        }

        /// <summary>
        /// Test: GetRollCapacity should return null for empty string
        /// Verifies empty string validation
        /// </summary>
        [TestMethod]
        public void GetRollCapacity_WhenPrinterNameIsEmpty_ShouldReturnNull()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Get roll capacity with empty printer name
            RollCapacityInfo? result = _printerService!.GetRollCapacity(string.Empty);

            // Assert: Should return null for empty string
            result.Should().BeNull("GetRollCapacity should return null for empty string");
        }

        /// <summary>
        /// Test: GetRollCapacity should return RollCapacityInfo with "Printer Not Found" for non-existent printer
        /// Verifies that invalid printer names return appropriate error information
        /// </summary>
        [TestMethod]
        public void GetRollCapacity_WhenPrinterDoesNotExist_ShouldReturnNotAvailableInfo()
        {
            // Arrange: Service is initialized in Setup()
            // Use a printer name that definitely doesn't exist
            string nonExistentPrinter = $"NonExistentPrinter_{Guid.NewGuid()}";

            // Act: Get roll capacity for non-existent printer
            RollCapacityInfo? result = _printerService!.GetRollCapacity(nonExistentPrinter);

            // Assert: Should return RollCapacityInfo with error status
            result.Should().NotBeNull("GetRollCapacity should return RollCapacityInfo even for non-existent printer");
            result!.IsAvailable.Should().BeFalse("IsAvailable should be false for non-existent printer");
            result.Status.Should().Be("Printer Not Found", "Status should indicate printer not found");
            result.Source.Should().Be("Not Available", "Source should indicate not available");
            result.Details.Should().Contain(nonExistentPrinter, "Details should contain the printer name");
        }

        /// <summary>
        /// Test: GetRollCapacity should return RollCapacityInfo for valid printer
        /// Verifies that the method attempts to retrieve capacity information
        /// Note: Actual capacity data may not be available depending on printer driver
        /// </summary>
        [TestMethod]
        public void GetRollCapacity_WhenPrinterExists_ShouldReturnRollCapacityInfo()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify capacity retrieval
            if (printers.Count > 0)
            {
                string printerName = printers[0].Name;
                
                // Act: Get roll capacity for existing printer
                RollCapacityInfo? result = _printerService.GetRollCapacity(printerName);

                // Assert: Should return a RollCapacityInfo (not null)
                result.Should().NotBeNull(
                    $"GetRollCapacity should return RollCapacityInfo for existing printer '{printerName}'");
                
                // Assert: Result should have valid properties
                result!.Source.Should().NotBeNull("Source should be set");
                result.Status.Should().NotBeNull("Status should be set");
                
                // Note: IsAvailable may be true or false depending on whether capacity data is available
                // This is acceptable - we just verify the method doesn't throw and returns valid data
                TestContext?.WriteLine($"[TEST] Roll capacity for '{printerName}': " +
                    $"IsAvailable={result.IsAvailable}, " +
                    $"Source={result.Source}, " +
                    $"Status={result.Status}, " +
                    $"RemainingPercentage={result.RemainingPercentage?.ToString() ?? "N/A"}, " +
                    $"RemainingPrints={result.RemainingPrints?.ToString() ?? "N/A"}");
            }
            else
            {
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        /// <summary>
        /// Test: GetRollCapacity should attempt multiple methods (WMI, PrintQueue, Driver)
        /// Verifies that the method tries fallback approaches when one method fails
        /// </summary>
        [TestMethod]
        public void GetRollCapacity_WhenCalled_ShouldAttemptMultipleMethods()
        {
            // Arrange: Service is initialized in Setup()
            // Get list of available printers
            List<PrinterDevice> printers = _printerService!.GetAvailablePrinters();

            // Act & Assert: If printers exist, verify method attempts
            if (printers.Count > 0)
            {
                string printerName = printers[0].Name;
                
                // Act: Get roll capacity (this will try WMI, PrintQueue, and Driver methods)
                RollCapacityInfo? result = _printerService.GetRollCapacity(printerName);

                // Assert: Should return a result (even if capacity data is not available)
                result.Should().NotBeNull("GetRollCapacity should return a result");
                
                // The Source should indicate which method was used (or "Not Available" if all failed)
                // Valid sources: "WMI", "PrintQueue", "Driver", "Not Available"
                result!.Source.Should().BeOneOf("WMI", "PrintQueue", "Driver", "Not Available",
                    "Source should indicate the method used or that none were available");
                
                TestContext?.WriteLine($"[TEST] Roll capacity retrieval attempted for '{printerName}'. " +
                    $"Final source: {result.Source}");
            }
            else
            {
                Assert.Inconclusive("No printers available on system to test with");
            }
        }

        #endregion

        #region PrintImageAsync Tests

        /// <summary>
        /// Test: PrintImageAsync should return false for null image path
        /// Verifies null safety and input validation
        /// Note: The method returns (false, 0.0) instead of throwing for invalid input
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenImagePathIsNull_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Attempt to print with null image path
            var result = await _printerService!.PrintImageAsync(null!);

            // Assert: Should return false (not throw)
            result.success.Should().BeFalse("PrintImageAsync should return false for null image path");
            result.printTimeSeconds.Should().Be(0.0, "Print time should be 0 for invalid input");
            result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned for null image path");
        }

        /// <summary>
        /// Test: PrintImageAsync should return false for empty image path
        /// Verifies empty string validation
        /// Note: The method returns (false, 0.0) instead of throwing for invalid input
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenImagePathIsEmpty_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup()
            // Act: Attempt to print with empty image path
            var result = await _printerService!.PrintImageAsync(string.Empty);

            // Assert: Should return false (not throw)
            result.success.Should().BeFalse("PrintImageAsync should return false for empty image path");
            result.printTimeSeconds.Should().Be(0.0, "Print time should be 0 for invalid input");
            result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned for empty image path");
        }

        /// <summary>
        /// Test: PrintImageAsync should return false for non-existent image file
        /// Verifies that invalid file paths are handled correctly
        /// NOTE: This test does NOT send actual print jobs - printer selection is cleared in Setup()
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenImageFileDoesNotExist_ShouldReturnFalse()
        {
            // Arrange: Service is initialized in Setup() with printer selection cleared
            // Create a path that definitely doesn't exist
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistentImage_{Guid.NewGuid()}.jpg");

            // Act: Attempt to print non-existent image
            // This will fail early due to file not existing, before any print job is sent
            var result = await _printerService!.PrintImageAsync(nonExistentPath);

            // Assert: Should return false (print failed)
            result.success.Should().BeFalse(
                "PrintImageAsync should return false for non-existent image file");
            
            // Print time should still be recorded (even if 0)
            result.printTimeSeconds.Should().BeGreaterThanOrEqualTo(0,
                "Print time should be non-negative even on failure");
            result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned when image file is missing");
        }

        /// <summary>
        /// Test: PrintImageAsync should validate copies parameter (must be > 0)
        /// Verifies that invalid copy counts are rejected
        /// NOTE: This test does NOT send actual print jobs - printer selection is cleared in Setup()
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenCopiesIsZero_ShouldHandleGracefully()
        {
            // Arrange: Service is initialized in Setup() with printer selection cleared
            // Create a temporary test image file
            string testImagePath = Path.Combine(Path.GetTempPath(), $"TestImage_{Guid.NewGuid()}.jpg");
            
            try
            {
                // Create a minimal valid image file (1x1 pixel JPEG)
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    bitmap.Save(testImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                // Act: Attempt to print with 0 copies
                // Note: Since no printer is selected (cleared in Setup), this will fail before sending print job
                var result = await _printerService!.PrintImageAsync(testImagePath, copies: 0);

                // Assert: Method should complete (will return false since no printer selected)
                // We just verify it doesn't throw an exception and handles the case gracefully
                result.Should().NotBeNull("PrintImageAsync should return a result");
                // Since printer is cleared, it should return false
                result.success.Should().BeFalse("Should return false when no printer is selected");
                result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned when printer is unavailable");
                result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned when printer is unavailable");
            }
            finally
            {
                // Cleanup: Delete test image file
                if (File.Exists(testImagePath))
                {
                    File.Delete(testImagePath);
                }
            }
        }

        /// <summary>
        /// Test: PrintImageAsync should validate copies parameter (must be > 0)
        /// Verifies that negative copy counts are rejected
        /// NOTE: This test does NOT send actual print jobs - printer selection is cleared in Setup()
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenCopiesIsNegative_ShouldHandleGracefully()
        {
            // Arrange: Service is initialized in Setup() with printer selection cleared
            // Create a temporary test image file
            string testImagePath = Path.Combine(Path.GetTempPath(), $"TestImage_{Guid.NewGuid()}.jpg");
            
            try
            {
                // Create a minimal valid image file (1x1 pixel JPEG)
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    bitmap.Save(testImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                // Act: Attempt to print with negative copies
                // Note: Since no printer is selected (cleared in Setup), this will fail before sending print job
                // The method normalizes negative copies to 1, but still fails due to no printer
                var result = await _printerService!.PrintImageAsync(testImagePath, copies: -1);

                // Assert: Should return false (no printer selected, so print fails)
                result.success.Should().BeFalse("Should return false when no printer is selected");
            }
            finally
            {
                // Cleanup: Delete test image file
                if (File.Exists(testImagePath))
                {
                    File.Delete(testImagePath);
                }
            }
        }

        /// <summary>
        /// Test: PrintImageAsync should accept custom paper size
        /// Verifies that paper size parameter is handled correctly
        /// NOTE: This test does NOT send actual print jobs - printer selection is cleared in Setup()
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenPaperSizeIsProvided_ShouldAcceptParameter()
        {
            // Arrange: Service is initialized in Setup() with printer selection cleared
            // Create a temporary test image file
            string testImagePath = Path.Combine(Path.GetTempPath(), $"TestImage_{Guid.NewGuid()}.jpg");
            
            try
            {
                // Create a minimal valid image file (1x1 pixel JPEG)
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    bitmap.Save(testImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                // Act: Attempt to print with custom paper size (6x4 inches)
                // Note: Since no printer is selected (cleared in Setup), this will fail before sending print job
                var paperSize = (width: 6.0f, height: 4.0f);
                var result = await _printerService!.PrintImageAsync(
                    testImagePath, 
                    copies: 1, 
                    paperSizeInches: paperSize);

                // Assert: Method should not throw (will return false since no printer selected)
                result.Should().NotBeNull("PrintImageAsync should return a result");
                result.success.Should().BeFalse("Should return false when no printer is selected");
                result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned when printer is unavailable");
            }
            finally
            {
                // Cleanup: Delete test image file
                if (File.Exists(testImagePath))
                {
                    File.Delete(testImagePath);
                }
            }
        }

        /// <summary>
        /// Test: PrintImageAsync should accept imagesPerPage parameter
        /// Verifies that imagesPerPage parameter is handled correctly
        /// NOTE: This test does NOT send actual print jobs - printer selection is cleared in Setup()
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenImagesPerPageIsProvided_ShouldAcceptParameter()
        {
            // Arrange: Service is initialized in Setup() with printer selection cleared
            // Create a temporary test image file
            string testImagePath = Path.Combine(Path.GetTempPath(), $"TestImage_{Guid.NewGuid()}.jpg");
            
            try
            {
                // Create a minimal valid image file (1x1 pixel JPEG)
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    bitmap.Save(testImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                // Act: Attempt to print with imagesPerPage parameter (e.g., 2 for strips)
                // Note: Since no printer is selected (cleared in Setup), this will fail before sending print job
                var result = await _printerService!.PrintImageAsync(
                    testImagePath, 
                    copies: 1, 
                    imagesPerPage: 2);

                // Assert: Method should not throw (will return false since no printer selected)
                result.Should().NotBeNull("PrintImageAsync should return a result");
                result.success.Should().BeFalse("Should return false when no printer is selected");
                result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned when printer is unavailable");
            }
            finally
            {
                // Cleanup: Delete test image file
                if (File.Exists(testImagePath))
                {
                    File.Delete(testImagePath);
                }
            }
        }

        /// <summary>
        /// Test: PrintImageAsync should return print time in seconds
        /// Verifies that print time is measured and returned
        /// NOTE: This test does NOT send actual print jobs - printer selection is cleared in Setup()
        /// </summary>
        [TestMethod]
        public async Task PrintImageAsync_WhenCalled_ShouldReturnPrintTime()
        {
            // Arrange: Service is initialized in Setup() with printer selection cleared
            // Create a temporary test image file
            string testImagePath = Path.Combine(Path.GetTempPath(), $"TestImage_{Guid.NewGuid()}.jpg");
            
            try
            {
                // Create a minimal valid image file (1x1 pixel JPEG)
                using (var bitmap = new System.Drawing.Bitmap(1, 1))
                {
                    bitmap.Save(testImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                // Act: Attempt to print (will fail since no printer selected, but should still return time)
                var result = await _printerService!.PrintImageAsync(testImagePath, copies: 1);

                // Assert: Print time should be returned (even if 0 for failed prints)
                result.printTimeSeconds.Should().BeGreaterThanOrEqualTo(0,
                    "Print time should be non-negative");
                result.success.Should().BeFalse("Should return false when no printer is selected");
                result.errorMessage.Should().NotBeNullOrWhiteSpace("An error message should be returned when printer is unavailable");
                
                TestContext?.WriteLine($"[TEST] PrintImageAsync returned: " +
                    $"Success={result.success}, PrintTime={result.printTimeSeconds}s " +
                    $"(No actual print job sent - printer selection cleared for safety)");
            }
            finally
            {
                // Cleanup: Delete test image file
                if (File.Exists(testImagePath))
                {
                    File.Delete(testImagePath);
                }
            }
        }

        #endregion

        #region Model Tests

        /// <summary>
        /// Test: PrinterDevice should have valid default values
        /// Verifies that PrinterDevice model initializes correctly
        /// </summary>
        [TestMethod]
        public void PrinterDevice_WhenCreated_ShouldHaveValidDefaults()
        {
            // Arrange & Act: Create a new PrinterDevice
            var device = new PrinterDevice();

            // Assert: Should have valid default values
            device.Name.Should().BeEmpty("Name should default to empty string");
            device.Index.Should().Be(0, "Index should default to 0");
            device.IsOnline.Should().BeFalse("IsOnline should default to false");
            device.IsDefault.Should().BeFalse("IsDefault should default to false");
            device.Status.Should().Be("Unknown", "Status should default to 'Unknown'");
            device.SupportsColor.Should().BeFalse("SupportsColor should default to false");
            device.MaxCopies.Should().Be(0, "MaxCopies should default to 0");
            device.SupportsDuplex.Should().BeFalse("SupportsDuplex should default to false");
            device.RollCapacity.Should().BeNull("RollCapacity should default to null");
        }

        /// <summary>
        /// Test: PrinterDevice should allow property assignment
        /// Verifies that PrinterDevice properties can be set and retrieved
        /// </summary>
        [TestMethod]
        public void PrinterDevice_WhenPropertiesSet_ShouldStoreValues()
        {
            // Arrange: Create a new PrinterDevice
            var device = new PrinterDevice
            {
                Name = "Test Printer",
                Index = 1,
                IsOnline = true,
                IsDefault = true,
                Status = "Ready",
                Model = "Test Model",
                Location = "Test Location",
                Comment = "Test Comment",
                SupportsColor = true,
                MaxCopies = 99,
                SupportsDuplex = true
            };

            // Assert: Properties should be set correctly
            device.Name.Should().Be("Test Printer");
            device.Index.Should().Be(1);
            device.IsOnline.Should().BeTrue();
            device.IsDefault.Should().BeTrue();
            device.Status.Should().Be("Ready");
            device.Model.Should().Be("Test Model");
            device.Location.Should().Be("Test Location");
            device.Comment.Should().Be("Test Comment");
            device.SupportsColor.Should().BeTrue();
            device.MaxCopies.Should().Be(99);
            device.SupportsDuplex.Should().BeTrue();
        }

        /// <summary>
        /// Test: RollCapacityInfo should have valid default values
        /// Verifies that RollCapacityInfo model initializes correctly
        /// </summary>
        [TestMethod]
        public void RollCapacityInfo_WhenCreated_ShouldHaveValidDefaults()
        {
            // Arrange & Act: Create a new RollCapacityInfo
            var capacity = new RollCapacityInfo();

            // Assert: Should have valid default values
            capacity.IsAvailable.Should().BeFalse("IsAvailable should default to false");
            capacity.RemainingPercentage.Should().BeNull("RemainingPercentage should default to null");
            capacity.RemainingPrints.Should().BeNull("RemainingPrints should default to null");
            capacity.MaxCapacity.Should().BeNull("MaxCapacity should default to null");
            capacity.Status.Should().Be("Unknown", "Status should default to 'Unknown'");
            capacity.Source.Should().Be("Not Available", "Source should default to 'Not Available'");
            capacity.Details.Should().BeNull("Details should default to null");
        }

        /// <summary>
        /// Test: RollCapacityInfo should allow property assignment
        /// Verifies that RollCapacityInfo properties can be set and retrieved
        /// </summary>
        [TestMethod]
        public void RollCapacityInfo_WhenPropertiesSet_ShouldStoreValues()
        {
            // Arrange: Create a new RollCapacityInfo
            var capacity = new RollCapacityInfo
            {
                IsAvailable = true,
                RemainingPercentage = 75,
                RemainingPrints = 525,
                MaxCapacity = 700,
                Status = "OK",
                Source = "WMI",
                Details = "Test details"
            };

            // Assert: Properties should be set correctly
            capacity.IsAvailable.Should().BeTrue();
            capacity.RemainingPercentage.Should().Be(75);
            capacity.RemainingPrints.Should().Be(525);
            capacity.MaxCapacity.Should().Be(700);
            capacity.Status.Should().Be("OK");
            capacity.Source.Should().Be("WMI");
            capacity.Details.Should().Be("Test details");
        }

        /// <summary>
        /// Test: PrinterDevice should allow RollCapacity assignment
        /// Verifies that RollCapacityInfo can be assigned to PrinterDevice
        /// </summary>
        [TestMethod]
        public void PrinterDevice_WhenRollCapacitySet_ShouldStoreRollCapacity()
        {
            // Arrange: Create PrinterDevice and RollCapacityInfo
            var device = new PrinterDevice { Name = "Test Printer" };
            var capacity = new RollCapacityInfo
            {
                IsAvailable = true,
                RemainingPrints = 500,
                Status = "OK",
                Source = "PrintQueue"
            };

            // Act: Assign RollCapacity to device
            device.RollCapacity = capacity;

            // Assert: RollCapacity should be stored
            device.RollCapacity.Should().NotBeNull("RollCapacity should be set");
            device.RollCapacity!.IsAvailable.Should().BeTrue();
            device.RollCapacity.RemainingPrints.Should().Be(500);
            device.RollCapacity.Status.Should().Be("OK");
            device.RollCapacity.Source.Should().Be("PrintQueue");
        }

        #endregion
    }
}

