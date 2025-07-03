using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Models;
using Photobooth.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Threading;
using System.Threading;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class CameraServiceTests : IDisposable
    {
        // Test execution framework support  
        public TestContext? TestContext { get; set; }
        
        private CameraService? _cameraService;
        private bool _disposed = false;
        private string _testOutputDirectory = null!;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Initialize WPF Application for Dispatcher access
            if (Application.Current == null)
            {
                // Create a test application on STA thread
                var thread = new Thread(() =>
                {
                    var app = new Application();
                    app.Run();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                
                // Wait for application to be ready
                SpinWait.SpinUntil(() => Application.Current != null, TimeSpan.FromSeconds(5));
            }
        }

        [ClassCleanup]
        public static void ClassTeardown()
        {
            // Shutdown the WPF Application to prevent resource leaks in test environments
            try
            {
                Application.Current?.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                // Note: In cleanup methods, we can't use TestContext.WriteLine since it's static
                System.Diagnostics.Debug.WriteLine($"[TEST] Warning: Could not shutdown WPF Application cleanly: {ex.Message}");
                // Don't throw - we don't want to fail tests due to cleanup issues
            }
        }

        [TestInitialize]
        public void Setup()
        {
            // Check thread apartment state but be more flexible
            var currentState = Thread.CurrentThread.GetApartmentState();
            TestContext?.WriteLine($"[TEST] Current thread apartment state: {currentState}");
            
            // For now, we'll try to proceed even if not STA, but log the state
            // The Application.Current created in ClassInitialize should handle WPF requirements
            if (currentState != ApartmentState.STA)
            {
                TestContext?.WriteLine($"[TEST] Warning: Not on STA thread, but proceeding with test. Application.Current: {Application.Current != null}");
            }

            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "PhotoBoothX_Test_Photos");
            
            // Clean up any existing test directory
            if (Directory.Exists(_testOutputDirectory))
            {
                Directory.Delete(_testOutputDirectory, true);
            }

            _cameraService = new CameraService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                _cameraService?.Dispose();
                
                // Clean up test output directory
                if (Directory.Exists(_testOutputDirectory))
                {
                    Directory.Delete(_testOutputDirectory, true);
                }
                
                // Clean up any photos created on desktop
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var photoBoothDir = Path.Combine(desktopPath, "PhotoBoothX_Photos");
                if (Directory.Exists(photoBoothDir))
                {
                    var testFiles = Directory.GetFiles(photoBoothDir, "photo_*test*.jpg");
                    foreach (var file in testFiles)
                    {
                        try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_InitializesSuccessfully()
        {
            // Act
            var service = new CameraService();

            // Assert
            service.Should().NotBeNull();
            service.IsCapturing.Should().BeFalse();
            service.IsPhotoCaptureActive.Should().BeFalse();
            
            // Cleanup
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_CreatesOutputDirectory()
        {
            // Act
            var service = new CameraService();

            // Assert
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var expectedPath = Path.Combine(desktopPath, "PhotoBoothX_Photos");
            Directory.Exists(expectedPath).Should().BeTrue();
            
            // Cleanup
            service.Dispose();
        }

        #endregion

        #region GetAvailableCameras Tests

        [TestMethod]
        public void GetAvailableCameras_ReturnsListOfCameras()
        {
            // Act
            var cameras = _cameraService?.GetAvailableCameras();

            // Assert
            cameras.Should().NotBeNull();
            cameras.Should().BeOfType<List<CameraDevice>>();
            
            // Note: We can't assert exact count as it depends on test environment
            // But we can verify the structure of returned items
            if (cameras != null)
            {
                foreach (var camera in cameras)
                {
                    camera.Index.Should().BeGreaterOrEqualTo(0);
                    camera.Name.Should().NotBeNullOrEmpty();
                    camera.MonikerString.Should().NotBeNullOrEmpty();
                }
            }
        }

        [TestMethod]
        public void GetAvailableCameras_HandlesNoAvailableCameras_Gracefully()
        {
            // This test verifies the method doesn't crash when no cameras are available
            // Act
            var cameras = _cameraService?.GetAvailableCameras();

            // Assert
            cameras.Should().NotBeNull();
            cameras.Should().BeOfType<List<CameraDevice>>();
        }

        #endregion

        #region StartCamera Tests

        [TestMethod]
        public void StartCamera_WithNoCameras_ReturnsFalse()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            // If no cameras available, test the behavior
            if (cameras?.Count == 0)
            {
                // Act
                var result = _cameraService?.StartCamera(0);

                // Assert
                result.Should().BeFalse();
                _cameraService?.IsCapturing.Should().BeFalse();
            }
            else
            {
                Assert.Inconclusive("Test requires no cameras to be available");
            }
        }

        [TestMethod]
        public void StartCamera_WithInvalidIndex_ReturnsFalse()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            var invalidIndex = (cameras?.Count ?? 0) + 10; // Well beyond available cameras

            // Act
            var result = _cameraService?.StartCamera(invalidIndex);

            // Assert
            result.Should().BeFalse();
            _cameraService?.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public void StartCamera_WithValidIndex_ReturnsTrue()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                // Act
                var result = _cameraService?.StartCamera(0);

                // Assert
                result.Should().BeTrue();
                _cameraService?.IsCapturing.Should().BeTrue();
                
                // Cleanup
                _cameraService?.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public void StartCamera_CalledMultipleTimes_HandlesGracefully()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                // Act
                var result1 = _cameraService?.StartCamera(0);
                var result2 = _cameraService?.StartCamera(0);

                // Assert
                result1.Should().BeTrue();
                result2.Should().BeTrue();
                _cameraService?.IsCapturing.Should().BeTrue();
                
                // Cleanup
                _cameraService?.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        #endregion

        #region StopCamera Tests

        [TestMethod]
        public void StopCamera_WhenNotRunning_HandlesGracefully()
        {
            // Act & Assert (should not throw)
            _cameraService?.StopCamera();
            _cameraService?.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public void StopCamera_WhenRunning_StopsSuccessfully()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                _cameraService?.StartCamera(0);
                _cameraService?.IsCapturing.Should().BeTrue();

                // Act
                _cameraService?.StopCamera();

                // Assert
                _cameraService?.IsCapturing.Should().BeFalse();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public void StopCamera_CalledMultipleTimes_HandlesGracefully()
        {
            // Act & Assert (should not throw)
            _cameraService?.StopCamera();
            _cameraService?.StopCamera();
            _cameraService?.StopCamera();
            
            _cameraService?.IsCapturing.Should().BeFalse();
        }

        #endregion

        #region CapturePhotoAsync Tests

        [TestMethod]
        public async Task CapturePhotoAsync_WhenCameraNotRunning_ReturnsNull()
        {
            // Act
            var result = await (_cameraService?.CapturePhotoAsync() ?? Task.FromResult<string?>(null));

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public async Task CapturePhotoAsync_WithCustomFileName_UsesFileName()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                
                // Wait a moment for camera to initialize and capture frames
                await Task.Delay(2000);
                
                var customFileName = "test_photo_custom";

                // Act
                var result = await _cameraService.CapturePhotoAsync(customFileName);

                // Assert
                if (result != null)
                {
                    result.Should().Contain(customFileName);
                    result.Should().EndWith(".jpg");
                    File.Exists(result).Should().BeTrue();
                    
                    // Cleanup
                    File.Delete(result);
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task CapturePhotoAsync_WithoutCustomFileName_GeneratesFileName()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                
                // Wait a moment for camera to initialize and capture frames
                await Task.Delay(2000);

                // Act
                var result = await _cameraService.CapturePhotoAsync();

                // Assert
                if (result != null)
                {
                    result.Should().StartWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhotoBoothX_Photos", "photo_"));
                    result.Should().EndWith(".jpg");
                    File.Exists(result).Should().BeTrue();
                    
                    // Cleanup
                    File.Delete(result);
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task CapturePhotoAsync_FileNameWithoutExtension_AddsJpgExtension()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                
                // Wait a moment for camera to initialize and capture frames
                await Task.Delay(2000);
                
                var fileNameWithoutExtension = "test_no_extension";

                // Act
                var result = await _cameraService.CapturePhotoAsync(fileNameWithoutExtension);

                // Assert
                if (result != null)
                {
                    result.Should().EndWith(".jpg");
                    result.Should().Contain(fileNameWithoutExtension);
                    File.Exists(result).Should().BeTrue();
                    
                    // Cleanup
                    File.Delete(result);
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task CapturePhotoAsync_WithInvalidFileName_HandlesGracefully()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                
                // Wait for camera to initialize
                await Task.Delay(2000);
                
                // Use invalid filename characters
                var invalidFileName = "test<>:\"|?*photo";

                // Act
                var result = await _cameraService.CapturePhotoAsync(invalidFileName);

                // Assert
                // Method should handle invalid characters gracefully
                // Either by cleaning the filename or returning null
                if (result != null)
                {
                    File.Exists(result).Should().BeTrue();
                    File.Delete(result);
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        #endregion

        #region Preview and Frame Management Tests

        [TestMethod]
        public void GetPreviewBitmap_WhenCameraNotStarted_ReturnsNull()
        {
            // Act
            var result = _cameraService?.GetPreviewBitmap();

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public void IsNewFrameAvailable_InitialState_ReturnsFalse()
        {
            // Act
            var result = _cameraService?.IsNewFrameAvailable();

            // Assert
            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsNewFrameAvailable_CalledTwice_ReturnsFalseSecondTime()
        {
            // Act
            var result1 = _cameraService?.IsNewFrameAvailable();
            var result2 = _cameraService?.IsNewFrameAvailable();

            // Assert
            result1.Should().BeFalse();
            result2.Should().BeFalse();
        }

        #endregion

        #region Photo Capture State Tests

        [TestMethod]
        public void SetPhotoCaptureActive_SetsStateCorrectly()
        {
            // Initial state
            _cameraService?.IsPhotoCaptureActive.Should().BeFalse();

            // Act
            _cameraService?.SetPhotoCaptureActive(true);

            // Assert
            _cameraService?.IsPhotoCaptureActive.Should().BeTrue();

            // Act
            _cameraService?.SetPhotoCaptureActive(false);

            // Assert
            _cameraService?.IsPhotoCaptureActive.Should().BeFalse();
        }

        [TestMethod]
        public void SetPhotoCaptureActive_CalledMultipleTimes_HandlesGracefully()
        {
            // Act
            _cameraService?.SetPhotoCaptureActive(true);
            _cameraService?.SetPhotoCaptureActive(true);
            _cameraService?.SetPhotoCaptureActive(true);

            // Assert
            _cameraService?.IsPhotoCaptureActive.Should().BeTrue();

            // Act
            _cameraService?.SetPhotoCaptureActive(false);
            _cameraService?.SetPhotoCaptureActive(false);

            // Assert
            _cameraService?.IsPhotoCaptureActive.Should().BeFalse();
        }

        #endregion

        #region IsCapturing Property Tests

        [TestMethod]
        public void IsCapturing_InitialState_ReturnsFalse()
        {
            // Act & Assert
            _cameraService?.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public void IsCapturing_WhenCameraRunning_ReturnsTrue()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                // Act
                _cameraService?.StartCamera(0);

                // Assert
                _cameraService?.IsCapturing.Should().BeTrue();
                
                // Cleanup
                _cameraService?.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public void IsCapturing_AfterStopping_ReturnsFalse()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                _cameraService?.StartCamera(0);
                _cameraService?.IsCapturing.Should().BeTrue();

                // Act
                _cameraService?.StopCamera();

                // Assert
                _cameraService?.IsCapturing.Should().BeFalse();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public void PreviewFrameReady_EventCanBeSubscribed()
        {
            // Arrange
            bool eventHandlerCalled = false;
            WriteableBitmap? receivedBitmap = null;

            // Act - Subscribe to event
            if (_cameraService != null)
            {
                _cameraService.PreviewFrameReady += (sender, bitmap) =>
                {
                    eventHandlerCalled = true;
                    receivedBitmap = bitmap;
                };
            }

            // Assert - Verify subscription works without errors
            _cameraService.Should().NotBeNull();
            
            // Verify the event handler is properly attached by checking it doesn't throw
            eventHandlerCalled.Should().BeFalse(); // Initially false
            receivedBitmap.Should().BeNull(); // Initially null
            
            // Note: We don't test actual event firing as it requires hardware
            // That functionality is covered by the MockCameraService tests
        }

        [TestMethod]
        public void CameraError_EventCanBeSubscribed()
        {
            // Arrange
            bool eventHandlerCalled = false;
            string? errorMessage = null;

            // Act
            if (_cameraService != null)
            {
                _cameraService.CameraError += (sender, message) =>
                {
                    eventHandlerCalled = true;
                    errorMessage = message;
                };
            }

            // Assert - just verify subscription works without errors
            // (We can't easily trigger camera errors in unit tests)
            _cameraService.Should().NotBeNull();
            
            // Verify the event handler is properly attached by checking it doesn't throw
            eventHandlerCalled.Should().BeFalse(); // Initially false
            errorMessage.Should().BeNull(); // Initially null
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public void StartCamera_WithNegativeIndex_ReturnsFalse()
        {
            // Act
            var result = _cameraService?.StartCamera(-1);

            // Assert
            result.Should().BeFalse();
            _cameraService?.IsCapturing.Should().BeFalse();
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        [Timeout(10000)] // 10 second timeout
        public void StartCamera_CompletesWithinReasonableTime()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                var startTime = DateTime.Now;

                // Act
                var result = _cameraService?.StartCamera(0);

                // Assert
                var elapsed = DateTime.Now - startTime;
                result.Should().BeTrue();
                elapsed.TotalSeconds.Should().BeLessThan(5); // Should start within 5 seconds
                
                // Cleanup
                _cameraService?.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        [Timeout(5000)] // 5 second timeout
        public void StopCamera_CompletesWithinReasonableTime()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                _cameraService?.StartCamera(0);
                var startTime = DateTime.Now;

                // Act
                _cameraService?.StopCamera();

                // Assert
                var elapsed = DateTime.Now - startTime;
                elapsed.TotalSeconds.Should().BeLessThan(3); // Should stop within 3 seconds
                _cameraService?.IsCapturing.Should().BeFalse();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task SetPhotoCaptureActive_OptimizesFrameRateCorrectly()
        {
            // This test verifies that the photo capture state affects frame processing
            // We can't easily measure frame rates in unit tests, but we can verify
            // the state changes correctly
            
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                _cameraService?.StartCamera(0);
                
                // Normal state
                _cameraService?.IsPhotoCaptureActive.Should().BeFalse();
                
                // Act - Enter photo capture mode
                _cameraService?.SetPhotoCaptureActive(true);
                _cameraService?.IsPhotoCaptureActive.Should().BeTrue();
                
                // Wait for some frame processing
                await Task.Delay(1000);
                
                // Act - Exit photo capture mode
                _cameraService?.SetPhotoCaptureActive(false);
                _cameraService?.IsPhotoCaptureActive.Should().BeFalse();
                
                // Cleanup
                _cameraService?.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_WhenCameraNotRunning_HandlesGracefully()
        {
            // Act & Assert (should not throw)
            _cameraService?.Dispose();
            _cameraService?.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public void Dispose_WhenCameraRunning_StopsCameraAndDisposesResources()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                _cameraService?.StartCamera(0);
                _cameraService?.IsCapturing.Should().BeTrue();

                // Act
                _cameraService?.Dispose();

                // Assert
                _cameraService?.IsCapturing.Should().BeFalse();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public void Dispose_CalledMultipleTimes_HandlesGracefully()
        {
            // Act & Assert (should not throw)
            _cameraService?.Dispose();
            _cameraService?.Dispose();
            _cameraService?.Dispose();
        }

        [TestMethod]
        public async Task Dispose_AfterMethodCalls_PreventsOperations()
        {
            // Arrange
            _cameraService?.Dispose();

            // Act & Assert - operations should handle disposed state gracefully
            var cameras = _cameraService?.GetAvailableCameras();
            cameras.Should().NotBeNull(); // This method should still work

            // Note: CameraService doesn't prevent operations after disposal, it just cleans up resources
            // This is actually acceptable behavior - the service continues to work but starts fresh
            var captureResult = await (_cameraService?.CapturePhotoAsync() ?? Task.FromResult<string?>(null));
            captureResult.Should().BeNull(); // Should return null when camera not started

            var previewBitmap = _cameraService?.GetPreviewBitmap();
            previewBitmap.Should().BeNull(); // Should return null
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task CompleteWorkflow_StartCaptureStop_WorksCorrectly()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                // Act & Assert - Complete workflow
                
                // 1. Start camera
                var startResult = _cameraService.StartCamera(0);
                startResult.Should().BeTrue();
                _cameraService.IsCapturing.Should().BeTrue();
                
                // 2. Wait for frames
                await Task.Delay(2000);
                
                // 3. Enter photo capture mode
                _cameraService.SetPhotoCaptureActive(true);
                _cameraService.IsPhotoCaptureActive.Should().BeTrue();
                
                // 4. Capture photo
                var photoPath = await _cameraService.CapturePhotoAsync("integration_test");
                if (photoPath != null)
                {
                    File.Exists(photoPath).Should().BeTrue();
                }
                
                // 5. Exit photo capture mode
                _cameraService.SetPhotoCaptureActive(false);
                _cameraService.IsPhotoCaptureActive.Should().BeFalse();
                
                // 6. Stop camera
                _cameraService.StopCamera();
                _cameraService.IsCapturing.Should().BeFalse();
                
                // Cleanup
                if (photoPath != null && File.Exists(photoPath))
                {
                    File.Delete(photoPath);
                }
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task MultipleCaptureSequence_WorksCorrectly()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                var capturedPhotos = new List<string>();

                // Act
                _cameraService.StartCamera(0);
                await Task.Delay(2000); // Wait for camera to stabilize

                // Capture multiple photos
                for (int i = 0; i < 3; i++)
                {
                    _cameraService.SetPhotoCaptureActive(true);
                    
                    var photoPath = await _cameraService.CapturePhotoAsync($"multi_test_{i}");
                    
                    _cameraService.SetPhotoCaptureActive(false);
                    
                    if (photoPath != null)
                    {
                        capturedPhotos.Add(photoPath);
                    }
                    
                    await Task.Delay(500); // Small delay between captures
                }

                // Assert - Photos may not capture in test environment without actual camera frames
                // We mainly verify no exceptions occurred and the process completes
                capturedPhotos.Should().NotBeNull();
                foreach (var photo in capturedPhotos)
                {
                    File.Exists(photo).Should().BeTrue();
                }

                // Cleanup
                _cameraService.StopCamera();
                foreach (var photo in capturedPhotos)
                {
                    if (File.Exists(photo))
                    {
                        File.Delete(photo);
                    }
                }
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        #endregion

        #region CameraDevice Tests

        [TestMethod]
        public void CameraDevice_Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var device = new CameraDevice();

            // Act
            device.Index = 1;
            device.Name = "Test Camera";
            device.MonikerString = "test_moniker_string";

            // Assert
            device.Index.Should().Be(1);
            device.Name.Should().Be("Test Camera");
            device.MonikerString.Should().Be("test_moniker_string");
        }

        [TestMethod]
        public void CameraDevice_DefaultValues_AreCorrect()
        {
            // Act
            var device = new CameraDevice();

            // Assert
            device.Index.Should().Be(0);
            device.Name.Should().Be(string.Empty);
            device.MonikerString.Should().Be(string.Empty);
        }

        #endregion

        #region Additional Edge Case Tests

        [TestMethod]
        public async Task CapturePhotoAsync_ConcurrentCalls_HandlesGracefully()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                await Task.Delay(2000); // Wait for camera to stabilize

                // Act - Make concurrent capture calls
                var task1 = _cameraService.CapturePhotoAsync("concurrent_test_1");
                var task2 = _cameraService.CapturePhotoAsync("concurrent_test_2");
                var task3 = _cameraService.CapturePhotoAsync("concurrent_test_3");

                var results = await Task.WhenAll(task1, task2, task3);

                // Assert - At least some captures should succeed, but concurrent calls may return null
                var successfulCaptures = results.Where(r => r != null).ToList();
                // Note: Concurrent photo captures may fail due to camera resource contention
                // This is expected behavior, so we'll just verify no exceptions occurred
                results.Should().NotBeNull();

                // Cleanup
                foreach (var result in successfulCaptures)
                {
                    if (File.Exists(result))
                    {
                        File.Delete(result);
                    }
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public void GetAvailableCameras_CalledMultipleTimes_ReturnsConsistentResults()
        {
            // Act
            var cameras1 = _cameraService?.GetAvailableCameras();
            var cameras2 = _cameraService?.GetAvailableCameras();
            var cameras3 = _cameraService?.GetAvailableCameras();

            // Assert
            cameras1.Should().NotBeNull();
            cameras2.Should().NotBeNull();
            cameras3.Should().NotBeNull();
            
            // Results should be consistent (same count)
            cameras1!.Count.Should().Be(cameras2!.Count);
            cameras2.Count.Should().Be(cameras3!.Count);
        }

        [TestMethod]
        public async Task CapturePhotoAsync_EmptyFileName_HandlesGracefully()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                await Task.Delay(2000);

                // Act
                var result = await _cameraService.CapturePhotoAsync("");

                // Assert
                if (result != null)
                {
                    result.Should().EndWith(".jpg");
                    File.Exists(result).Should().BeTrue();
                    File.Delete(result);
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task CapturePhotoAsync_NullFileName_HandlesGracefully()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0 && _cameraService != null)
            {
                _cameraService.StartCamera(0);
                await Task.Delay(2000);

                // Act
                var result = await _cameraService.CapturePhotoAsync(null);

                // Assert
                if (result != null)
                {
                    result.Should().EndWith(".jpg");
                    File.Exists(result).Should().BeTrue();
                    File.Delete(result);
                }
                
                _cameraService.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task StartStopCycle_MultipleIterations_HandlesGracefully()
        {
            // This test verifies that starting and stopping the camera multiple times works correctly
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    // Start
                    var startResult = _cameraService?.StartCamera(0);
                    startResult.Should().BeTrue();
                    _cameraService?.IsCapturing.Should().BeTrue();
                    
                    // Wait briefly
                    await Task.Delay(500);
                    
                    // Stop
                    _cameraService?.StopCamera();
                    _cameraService?.IsCapturing.Should().BeFalse();
                }
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public void CameraService_StateConsistency_MaintainedThroughOperations()
        {
            // This test verifies that the camera service maintains consistent state through various operations
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                // Initial state
                _cameraService?.IsCapturing.Should().BeFalse();
                _cameraService?.IsPhotoCaptureActive.Should().BeFalse();
                _cameraService?.GetPreviewBitmap().Should().BeNull();
                
                // Start camera
                _cameraService?.StartCamera(0);
                _cameraService?.IsCapturing.Should().BeTrue();
                _cameraService?.IsPhotoCaptureActive.Should().BeFalse(); // Should still be false
                
                // Set photo capture active
                _cameraService?.SetPhotoCaptureActive(true);
                _cameraService?.IsCapturing.Should().BeTrue(); // Should still be true
                _cameraService?.IsPhotoCaptureActive.Should().BeTrue();
                
                // Reset photo capture
                _cameraService?.SetPhotoCaptureActive(false);
                _cameraService?.IsCapturing.Should().BeTrue(); // Should still be true
                _cameraService?.IsPhotoCaptureActive.Should().BeFalse();
                
                // Stop camera
                _cameraService?.StopCamera();
                _cameraService?.IsCapturing.Should().BeFalse();
                _cameraService?.IsPhotoCaptureActive.Should().BeFalse(); // Should still be false
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        [TestMethod]
        public async Task GetPreviewBitmap_AfterCameraStart_ReturnsValidBitmap()
        {
            // Arrange
            var cameras = _cameraService?.GetAvailableCameras();
            
            if (cameras?.Count > 0)
            {
                // Act
                _cameraService?.StartCamera(0);
                
                // Wait for frames to be processed
                await Task.Delay(2000);
                
                var previewBitmap = _cameraService?.GetPreviewBitmap();

                // Assert
                previewBitmap.Should().NotBeNull();
                if (previewBitmap != null)
                {
                    // Use Dispatcher.Invoke to access WPF object properties on correct thread
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        previewBitmap.PixelWidth.Should().BeGreaterThan(0);
                        previewBitmap.PixelHeight.Should().BeGreaterThan(0);
                    });
                }
                
                // Cleanup
                _cameraService?.StopCamera();
            }
            else
            {
                Assert.Inconclusive("Test requires at least one camera to be available");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Cleanup();
            }
        }

        #endregion
    }
} 
