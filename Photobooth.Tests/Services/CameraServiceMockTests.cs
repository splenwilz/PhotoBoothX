using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using Photobooth.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Threading;

namespace Photobooth.Tests.Services
{
    /// <summary>
    /// CI/CD friendly camera service tests using mocks - no hardware required
    /// </summary>
    [TestClass]
    public class CameraServiceMockTests
    {
        private MockCameraService _mockCameraService = null!;
        private string _testOutputDirectory = null!;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Initialize WPF Application for Dispatcher access
            if (Application.Current == null)
            {
                var thread = new Thread(() =>
                {
                    var app = new Application();
                    app.Run();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                
                SpinWait.SpinUntil(() => Application.Current != null, TimeSpan.FromSeconds(5));
            }
        }

        [TestInitialize]
        public void Setup()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "PhotoBoothX_Mock_Test_Photos");
            
            if (Directory.Exists(_testOutputDirectory))
            {
                Directory.Delete(_testOutputDirectory, true);
            }

            _mockCameraService = new MockCameraService(mockCameraCount: 2);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                _mockCameraService?.Dispose();
                
                if (Directory.Exists(_testOutputDirectory))
                {
                    Directory.Delete(_testOutputDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Constructor and Basic Tests

        [TestMethod]
        public void MockCameraService_Constructor_InitializesCorrectly()
        {
            // Act
            var service = new MockCameraService(3);

            // Assert
            service.Should().NotBeNull();
            service.IsCapturing.Should().BeFalse();
            service.IsPhotoCaptureActive.Should().BeFalse();
            service.GetAvailableCameras().Should().HaveCount(3);
            
            // Cleanup
            service.Dispose();
        }

        [TestMethod]
        public void GetAvailableCameras_ReturnsExpectedCount()
        {
            // Act
            var cameras = _mockCameraService.GetAvailableCameras();

            // Assert
            cameras.Should().HaveCount(2);
            cameras[0].Name.Should().Be("Mock Camera 1");
            cameras[1].Name.Should().Be("Mock Camera 2");
            cameras.All(c => c.Index >= 0).Should().BeTrue();
            cameras.All(c => !string.IsNullOrEmpty(c.MonikerString)).Should().BeTrue();
        }

        [TestMethod]
        public void GetAvailableCameras_NoCameras_ReturnsEmptyList()
        {
            // Arrange
            var service = new MockCameraService(0);

            // Act
            var cameras = service.GetAvailableCameras();

            // Assert
            cameras.Should().BeEmpty();
            
            // Cleanup
            service.Dispose();
        }

        #endregion

        #region Camera Lifecycle Tests

        [TestMethod]
        public async Task StartCamera_ValidIndex_ReturnsTrue()
        {
            // Act
            var result = await _mockCameraService.StartCameraAsync(0);

            // Assert
            result.Should().BeTrue();
            _mockCameraService.IsCapturing.Should().BeTrue();
        }

        [TestMethod]
        public async Task StartCamera_InvalidIndex_ReturnsFalse()
        {
            // Act
            var result = await _mockCameraService.StartCameraAsync(10);

            // Assert
            result.Should().BeFalse();
            _mockCameraService.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public async Task StartCamera_NegativeIndex_ReturnsFalse()
        {
            // Act
            var result = await _mockCameraService.StartCameraAsync(-1);

            // Assert
            result.Should().BeFalse();
            _mockCameraService.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public async Task StopCamera_AfterStart_StopsSuccessfully()
        {
            // Arrange
            await _mockCameraService.StartCameraAsync(0);
            _mockCameraService.IsCapturing.Should().BeTrue();

            // Act
            _mockCameraService.StopCamera();

            // Assert
            _mockCameraService.IsCapturing.Should().BeFalse();
        }

        [TestMethod]
        public void StopCamera_WhenNotRunning_HandlesGracefully()
        {
            // Act & Assert
            _mockCameraService.StopCamera();
            _mockCameraService.IsCapturing.Should().BeFalse();
        }

        #endregion

        #region Photo Capture Tests

        [TestMethod]
        public async Task CapturePhotoAsync_WhenCameraRunning_ReturnsFilePath()
        {
            // Arrange
            await _mockCameraService.StartCameraAsync(0);

            // Act
            var result = await _mockCameraService.CapturePhotoAsync("test_photo");

            // Assert
            result.Should().NotBeNull();
            result.Should().EndWith(".jpg");
            result.Should().Contain("test_photo");
            File.Exists(result).Should().BeTrue();
            
            // Cleanup
            if (result != null) File.Delete(result);
        }

        [TestMethod]
        public async Task CapturePhotoAsync_WhenCameraNotRunning_ReturnsNull()
        {
            // Act
            var result = await _mockCameraService.CapturePhotoAsync();

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public async Task CapturePhotoAsync_WithoutExtension_AddsJpgExtension()
        {
            // Arrange
            await _mockCameraService.StartCameraAsync(0);

            // Act
            var result = await _mockCameraService.CapturePhotoAsync("test_no_ext");

            // Assert
            result.Should().NotBeNull();
            result.Should().EndWith(".jpg");
            result.Should().Contain("test_no_ext");
            
            // Cleanup
            if (result != null) File.Delete(result);
        }

        [TestMethod]
        public async Task CapturePhotoAsync_NullFileName_GeneratesFileName()
        {
            // Arrange
            await _mockCameraService.StartCameraAsync(0);

            // Act
            var result = await _mockCameraService.CapturePhotoAsync(null);

            // Assert
            result.Should().NotBeNull();
            result.Should().EndWith(".jpg");
            result.Should().Contain("photo_");
            
            // Cleanup
            if (result != null) File.Delete(result);
        }

        [TestMethod]
        public async Task CapturePhotoAsync_EmptyFileName_GeneratesFileName()
        {
            // Arrange
            await _mockCameraService.StartCameraAsync(0);

            // Act
            var result = await _mockCameraService.CapturePhotoAsync("");

            // Assert
            result.Should().NotBeNull();
            result.Should().EndWith(".jpg");
            
            // Cleanup
            if (result != null) File.Delete(result);
        }

        #endregion

        #region State Management Tests

        [TestMethod]
        public void SetPhotoCaptureActive_TogglesStateCorrectly()
        {
            // Initial state
            _mockCameraService.IsPhotoCaptureActive.Should().BeFalse();

            // Act
            _mockCameraService.SetPhotoCaptureActive(true);

            // Assert
            _mockCameraService.IsPhotoCaptureActive.Should().BeTrue();

            // Act
            _mockCameraService.SetPhotoCaptureActive(false);

            // Assert
            _mockCameraService.IsPhotoCaptureActive.Should().BeFalse();
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task CompleteWorkflow_MockCamera_WorksCorrectly()
        {
            // 1. Start camera
            var startResult = await _mockCameraService.StartCameraAsync(0);
            startResult.Should().BeTrue();
            _mockCameraService.IsCapturing.Should().BeTrue();

            // 2. Set photo capture active
            _mockCameraService.SetPhotoCaptureActive(true);
            _mockCameraService.IsPhotoCaptureActive.Should().BeTrue();

            // 3. Capture photo
            var photoPath = await _mockCameraService.CapturePhotoAsync("workflow_test");
            photoPath.Should().NotBeNull();
            File.Exists(photoPath!).Should().BeTrue();

            // 4. Reset photo capture
            _mockCameraService.SetPhotoCaptureActive(false);
            _mockCameraService.IsPhotoCaptureActive.Should().BeFalse();

            // 5. Stop camera
            _mockCameraService.StopCamera();
            _mockCameraService.IsCapturing.Should().BeFalse();

            // Cleanup
            if (photoPath != null) File.Delete(photoPath);
        }

        #endregion
    }
}
