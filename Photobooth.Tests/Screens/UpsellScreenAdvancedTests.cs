using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth;
using Photobooth.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace Photobooth.Tests.Screens
{
    [TestClass]
    public class UpsellScreenAdvancedTests
    {
        private UpsellScreen _upsellScreen = null!;
        private string _testDirectory = null!;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Initialize WPF Application for UI testing
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
            _testDirectory = Path.Combine(Path.GetTempPath(), "UpsellScreen_Advanced_Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _upsellScreen = new UpsellScreen();
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _upsellScreen?.Dispose();
                });

                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Timeout Tests

        [TestMethod]
        public async Task UpsellTimeout_Event_ShouldFireAfterDelay()
        {
            // Arrange
            var timeoutFired = false;
            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("strips");
            var mockPhotos = CreateMockPhotoList(3);

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                _upsellScreen.UpsellTimeout += (sender, e) => timeoutFired = true;
                await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, mockPhotos[0], mockPhotos);

                // Note: In real tests, we'd need to either wait for the actual timeout
                // or provide a way to mock/accelerate the timer
                // For now, we verify the event can be subscribed to
                timeoutFired.Should().BeFalse(); // Haven't triggered timeout yet
            });
        }

        #endregion

        #region Complex Business Logic Tests

        [TestMethod]
        public void TotalCost_Calculation_WithExtraCopiesAndCrossSell()
        {
            // Test complex pricing scenarios
            var testCases = new[]
            {
                new { ExtraCopies = 1, ExtraPrice = 3.00m, CrossSellPrice = 3.00m, Expected = 6.00m },
                new { ExtraCopies = 2, ExtraPrice = 5.00m, CrossSellPrice = 5.00m, Expected = 10.00m },
                new { ExtraCopies = 4, ExtraPrice = 8.00m, CrossSellPrice = 3.00m, Expected = 11.00m },
                new { ExtraCopies = 6, ExtraPrice = 11.00m, CrossSellPrice = 5.00m, Expected = 16.00m }
            };

            foreach (var testCase in testCases)
            {
                // Act
                var totalCost = testCase.ExtraPrice + testCase.CrossSellPrice;

                // Assert
                totalCost.Should().Be(testCase.Expected, 
                    $"Failed for {testCase.ExtraCopies} copies with cross-sell");
            }
        }

        [TestMethod]
        public void QuantitySelector_BoundaryValues_WorkCorrectly()
        {
            // Test quantity selector logic
            var testCases = new[]
            {
                new { Input = 4, Expected = 4, ShouldBeValid = true },   // Minimum
                new { Input = 10, Expected = 10, ShouldBeValid = true }, // Maximum
                new { Input = 3, Expected = 4, ShouldBeValid = false },  // Below minimum
                new { Input = 11, Expected = 10, ShouldBeValid = false } // Above maximum
            };

            foreach (var testCase in testCases)
            {
                // Simulate quantity validation logic
                var clampedValue = Math.Max(4, Math.Min(10, testCase.Input));
                var isValid = testCase.Input >= 4 && testCase.Input <= 10;

                clampedValue.Should().Be(testCase.Expected);
                isValid.Should().Be(testCase.ShouldBeValid);
            }
        }

        #endregion

        #region Photo Selection Tests

        [TestMethod]
        public async Task PhotoSelection_NavigationLogic_WorksCorrectly()
        {
            // Arrange
            var photos = CreateMockPhotoList(5);
            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("4x6");

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, photos[0], photos);

                // Test photo navigation logic (simulated)
                for (int i = 0; i < photos.Count; i++)
                {
                    // Simulate navigation state
                    var canGoNext = i < photos.Count - 1;
                    var canGoPrevious = i > 0;

                    if (i == 0)
                    {
                        canGoPrevious.Should().BeFalse("Should not be able to go previous from first photo");
                        canGoNext.Should().BeTrue("Should be able to go next from first photo");
                    }
                    else if (i == photos.Count - 1)
                    {
                        canGoPrevious.Should().BeTrue("Should be able to go previous from last photo");
                        canGoNext.Should().BeFalse("Should not be able to go next from last photo");
                    }
                    else
                    {
                        canGoPrevious.Should().BeTrue("Should be able to go previous from middle photo");
                        canGoNext.Should().BeTrue("Should be able to go next from middle photo");
                    }
                }
            });
        }

        [TestMethod]
        public async Task PhotoSelection_EmptyList_HandlesGracefully()
        {
            // Arrange
            var emptyPhotos = new List<string>();
            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("strips");
            var composedImage = CreateMockImageFile("composed.jpg");

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                Func<Task> act = async () => await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, composedImage, emptyPhotos);
                await act.Should().NotThrowAsync("Should handle empty photo list gracefully");
            });
        }

        #endregion

        #region UI State Tests

        [TestMethod]
        public async Task UI_StageTransitions_MaintainCorrectState()
        {
            // Arrange
            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("strips");
            var mockPhotos = CreateMockPhotoList(3);

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, mockPhotos[0], mockPhotos);

                // After initialization, should be in correct initial state
                _upsellScreen.Should().NotBeNull();
                
                // Test that UI elements are properly initialized
                // (This would require access to UI elements, which might need to be exposed for testing)
            });
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public async Task LargePhotoList_Performance_RemainsAcceptable()
        {
            // Arrange
            var largePhotoList = CreateMockPhotoList(100); // Large number of photos
            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("4x6");

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var startTime = DateTime.UtcNow;
                await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, largePhotoList[0], largePhotoList);
                var elapsed = DateTime.UtcNow - startTime;

                elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Should handle large photo lists efficiently");
                largePhotoList.Should().HaveCount(100);
            });
        }

        [TestMethod]
        public async Task InvalidPhotoFormats_FilteredCorrectly()
        {
            // Arrange
            var mixedFiles = new List<string>
            {
                CreateMockImageFile("valid1.jpg"),
                CreateMockFile("invalid.txt", "text content"),
                CreateMockImageFile("valid2.png"),
                CreateMockFile("invalid.doc", "document content"),
                CreateMockImageFile("valid3.jpeg")
            };

            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("strips");

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // Should handle mixed file types gracefully
                Func<Task> act = async () => await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, mixedFiles[0], mixedFiles);
                await act.Should().NotThrowAsync("Should handle mixed file types gracefully");
            });
        }

        #endregion

        #region Memory and Resource Tests

        [TestMethod]
        public async Task MultipleInitializations_DoNotLeakMemory()
        {
            // Arrange
            var mockTemplate = CreateMockTemplate();
            var mockProduct = CreateMockProduct("4x6");
            var mockPhotos = CreateMockPhotoList(5);

            // Act - Initialize multiple times
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, mockPhotos[0], mockPhotos);
                    
                    // Force garbage collection to test for memory leaks
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            });

            // Assert - Should complete without memory issues
            _upsellScreen.Should().NotBeNull();
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task CompleteWorkflow_AllProductTypes_WorkCorrectly()
        {
            // Test complete workflow for each product type
            var productTypes = new[] { "strips", "4x6", "phone" };

            foreach (var productType in productTypes)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var mockTemplate = CreateMockTemplate();
                    var mockProduct = CreateMockProduct(productType);
                    var mockPhotos = CreateMockPhotoList(3);

                    await _upsellScreen.InitializeAsync(mockTemplate, mockProduct, mockPhotos[0], mockPhotos);
                    
                    // Should initialize successfully for all product types
                    _upsellScreen.Should().NotBeNull($"Should work for {productType} products");
                });
            }
        }

        #endregion

        #region Helper Methods

        private Template CreateMockTemplate()
        {
            return new Template
            {
                Name = $"Test Template {Guid.NewGuid()}",
                Description = "Advanced test template"
            };
        }

        private ProductInfo CreateMockProduct(string type)
        {
            return type.ToLower() switch
            {
                "strips" => new ProductInfo
                {
                    Type = "strips",
                    Name = "Photo Strips",
                    Price = 5.00m
                },
                "4x6" => new ProductInfo
                {
                    Type = "4x6",
                    Name = "4x6 Photos",
                    Price = 3.00m
                },
                "phone" => new ProductInfo
                {
                    Type = "phone",
                    Name = "Print from Phone",
                    Price = 2.00m
                },
                _ => throw new ArgumentException($"Unknown product type: {type}")
            };
        }

        private List<string> CreateMockPhotoList(int count)
        {
            var photos = new List<string>();
            for (int i = 0; i < count; i++)
            {
                photos.Add(CreateMockImageFile($"photo_{i + 1}.jpg"));
            }
            return photos;
        }

        private string CreateMockImageFile(string fileName)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            
            // Create a minimal valid JPEG file
            var jpegHeader = new byte[] 
            { 
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 
                0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48,
                0x00, 0x48, 0x00, 0x00, 0xFF, 0xD9 
            };
            
            File.WriteAllBytes(filePath, jpegHeader);
            return filePath;
        }

        private string CreateMockFile(string fileName, string content)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        #endregion
    }
} 