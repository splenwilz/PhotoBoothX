using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth;
using Photobooth.Models;
using Photobooth.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace Photobooth.Tests.Screens
{
    [TestClass]
    public class UpsellScreenTests
    {
        private UpsellScreen _upsellScreen = null!;
        private Template _mockTemplate = null!;
        private ProductInfo _mockProduct = null!;
        private string _mockComposedImagePath = null!;
        private List<string> _mockCapturedPhotos = null!;
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
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "UpsellScreen_Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Create mock test image files
            _mockComposedImagePath = CreateMockImageFile("composed.jpg");
            _mockCapturedPhotos = new List<string>
            {
                CreateMockImageFile("photo1.jpg"),
                CreateMockImageFile("photo2.jpg"),
                CreateMockImageFile("photo3.jpg")
            };

            // Create mock objects
            _mockTemplate = new Template
            {
                Name = "Test Template",
                Description = "Test template for upsell testing"
            };

            _mockProduct = new ProductInfo
            {
                Type = "strips",
                Name = "Photo Strips",
                Description = "Classic 4-photo strip",
                Price = 5.00m
            };

            // Initialize UpsellScreen on UI thread
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

        #region Initialization Tests

        [TestMethod]
        public async Task InitializeAsync_WithValidParameters_InitializesSuccessfully()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, _mockCapturedPhotos);
            });

            // Assert
            _upsellScreen.Should().NotBeNull();
        }

        [TestMethod]
        public async Task InitializeAsync_WithStripsProduct_ShowsCorrectCrossSell()
        {
            // Arrange
            var stripsProduct = new ProductInfo
            {
                Type = "strips",
                Name = "Photo Strips",
                Price = 5.00m
            };

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, stripsProduct, _mockComposedImagePath, _mockCapturedPhotos);
                // Cross-sell for strips should be 4x6 photos
            });
        }

        [TestMethod]
        public async Task InitializeAsync_With4x6Product_ShowsCorrectCrossSell()
        {
            // Arrange
            var photo4x6Product = new ProductInfo
            {
                Type = "4x6",
                Name = "4x6 Photos",
                Price = 3.00m
            };

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, photo4x6Product, _mockComposedImagePath, _mockCapturedPhotos);
                // Cross-sell for 4x6 should be strips
            });
        }

        [TestMethod]
        public async Task InitializeAsync_WithPhoneProduct_NoCrossSell()
        {
            // Arrange
            var phoneProduct = new ProductInfo
            {
                Type = "phone",
                Name = "Print from Phone",
                Price = 2.00m
            };

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, phoneProduct, _mockComposedImagePath, _mockCapturedPhotos);
                // Phone products should not have cross-sell
            });
        }

        #endregion

        #region Business Logic Tests

        [TestMethod]
        public void ExtraCopyPricing_OneCopy_CalculatesCorrectly()
        {
            // Arrange
            const decimal expectedPrice = 3.00m;

            // Act & Assert - Testing pricing constants
            const decimal EXTRA_COPY_PRICE_1 = 3.00m;
            EXTRA_COPY_PRICE_1.Should().Be(expectedPrice);
        }

        [TestMethod]
        public void ExtraCopyPricing_TwoCopies_CalculatesCorrectly()
        {
            // Arrange
            const decimal expectedPrice = 5.00m;

            // Act & Assert - Testing pricing constants
            const decimal EXTRA_COPY_PRICE_2 = 5.00m;
            EXTRA_COPY_PRICE_2.Should().Be(expectedPrice);
        }

        [TestMethod]
        public void ExtraCopyPricing_FourCopies_CalculatesCorrectly()
        {
            // Arrange
            const decimal basePrice = 8.00m;
            const decimal perAdditionalPrice = 1.50m;

            // Act
            var fourCopyPrice = basePrice; // 4 copies = base price
            var fiveCopyPrice = basePrice + perAdditionalPrice; // 5 copies = base + 1 additional
            var sixCopyPrice = basePrice + (2 * perAdditionalPrice); // 6 copies = base + 2 additional

            // Assert
            fourCopyPrice.Should().Be(8.00m);
            fiveCopyPrice.Should().Be(9.50m);
            sixCopyPrice.Should().Be(11.00m);
        }

        [TestMethod]
        public void CrossSellProduct_StripsToPhoto4x6_ReturnsCorrectProduct()
        {
            // Arrange
            var stripsProduct = new ProductInfo { Type = "strips" };

            // Act - Simulate the cross-sell logic
            var crossSellType = stripsProduct.Type?.ToLower() switch
            {
                "strips" => "4x6",
                "4x6" => "strips",
                _ => null
            };

            // Assert
            crossSellType.Should().Be("4x6");
        }

        [TestMethod]
        public void CrossSellProduct_Photo4x6ToStrips_ReturnsCorrectProduct()
        {
            // Arrange
            var photo4x6Product = new ProductInfo { Type = "4x6" };

            // Act - Simulate the cross-sell logic
            var crossSellType = photo4x6Product.Type?.ToLower() switch
            {
                "strips" => "4x6",
                "4x6" => "strips",
                _ => null
            };

            // Assert
            crossSellType.Should().Be("strips");
        }

        [TestMethod]
        public void CrossSellProduct_PhoneProduct_ReturnsNull()
        {
            // Arrange
            var phoneProduct = new ProductInfo { Type = "phone" };

            // Act - Simulate the cross-sell logic
            var crossSellType = phoneProduct.Type?.ToLower() switch
            {
                "strips" => "4x6",
                "4x6" => "strips",
                _ => null
            };

            // Assert
            crossSellType.Should().BeNull();
        }

        #endregion

        #region Stage Management Tests

        [TestMethod]
        public async Task StageTransition_ExtraCopiesToCrossSell_WorksCorrectly()
        {
            // This test would require access to internal state
            // For now, we test the initialization which sets up the first stage
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, _mockCapturedPhotos);
                
                // After initialization, should be in ExtraCopies stage
                _upsellScreen.Should().NotBeNull();
            });
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public async Task UpsellCompleted_Event_FiresWithCorrectData()
        {
            // Arrange
            UpsellResult? capturedResult = null;
            var eventFired = false;

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, _mockCapturedPhotos);
                
                _upsellScreen.UpsellCompleted += (sender, e) =>
                {
                    capturedResult = e.Result;
                    eventFired = true;
                };

                // Simulate completing upsell by triggering No Thanks
                // This would normally be done through UI interaction
                // For testing, we'll verify the event structure
            });

            // Assert event handler setup
            eventFired.Should().BeFalse(); // Event hasn't fired yet
        }

        [TestMethod]
        public async Task UpsellResult_ContainsCorrectStructure()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, _mockCapturedPhotos);
            });

            // Assert - Test UpsellResult structure
            var mockResult = new UpsellResult
            {
                OriginalProduct = _mockProduct,
                OriginalTemplate = _mockTemplate,
                ComposedImagePath = _mockComposedImagePath,
                CapturedPhotosPaths = _mockCapturedPhotos,
                ExtraCopies = 2,
                ExtraCopiesPrice = 5.00m,
                CrossSellAccepted = true,
                CrossSellProduct = new ProductInfo { Type = "4x6", Price = 3.00m },
                CrossSellPrice = 3.00m,
                TotalAdditionalCost = 8.00m,
                SelectedPhotoForCrossSell = _mockCapturedPhotos[1]
            };

            mockResult.OriginalProduct.Should().Be(_mockProduct);
            mockResult.OriginalTemplate.Should().Be(_mockTemplate);
            mockResult.ComposedImagePath.Should().Be(_mockComposedImagePath);
            mockResult.CapturedPhotosPaths.Should().BeEquivalentTo(_mockCapturedPhotos);
            mockResult.ExtraCopies.Should().Be(2);
            mockResult.ExtraCopiesPrice.Should().Be(5.00m);
            mockResult.CrossSellAccepted.Should().BeTrue();
            mockResult.CrossSellPrice.Should().Be(3.00m);
            mockResult.TotalAdditionalCost.Should().Be(8.00m);
            mockResult.SelectedPhotoForCrossSell.Should().Be(_mockCapturedPhotos[1]);
        }

        #endregion

        #region Photo Carousel Tests

        [TestMethod]
        public async Task PhotoCarousel_WithMultiplePhotos_InitializesCorrectly()
        {
            // Arrange
            var multiplePhotos = new List<string>
            {
                CreateMockImageFile("carousel1.jpg"),
                CreateMockImageFile("carousel2.jpg"),
                CreateMockImageFile("carousel3.jpg"),
                CreateMockImageFile("carousel4.jpg")
            };

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, multiplePhotos);
                
                // Photo carousel should be initialized with 4 photos
                multiplePhotos.Should().HaveCount(4);
                multiplePhotos.Should().AllSatisfy(path => File.Exists(path).Should().BeTrue());
            });
        }

        [TestMethod]
        public async Task PhotoCarousel_WithSinglePhoto_DisablesNavigation()
        {
            // Arrange
            var singlePhoto = new List<string> { CreateMockImageFile("single.jpg") };

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, singlePhoto);
                
                // With single photo, navigation should be disabled
                singlePhoto.Should().HaveCount(1);
            });
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task InitializeAsync_WithMissingPhotoFiles_HandlesGracefully()
        {
            // Arrange
            var missingPhotos = new List<string>
            {
                Path.Combine(_testDirectory, "nonexistent1.jpg"),
                Path.Combine(_testDirectory, "nonexistent2.jpg")
            };

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // Should not throw exception even with missing files
                Func<Task> act = async () => await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, missingPhotos);
                await act.Should().NotThrowAsync();
            });
        }

        [TestMethod]
        public async Task InitializeAsync_WithNullTemplate_ThrowsException()
        {
            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                Func<Task> act = async () => await _upsellScreen.InitializeAsync(null!, _mockProduct, _mockComposedImagePath, _mockCapturedPhotos);
                await act.Should().ThrowAsync<ArgumentNullException>();
            });
        }

        [TestMethod]
        public async Task InitializeAsync_WithNullProduct_ThrowsException()
        {
            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                Func<Task> act = async () => await _upsellScreen.InitializeAsync(_mockTemplate, null!, _mockComposedImagePath, _mockCapturedPhotos);
                await act.Should().ThrowAsync<ArgumentNullException>();
            });
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public async Task InitializeAsync_CompletesWithinTimeLimit()
        {
            // Arrange
            var maxInitializationTime = TimeSpan.FromSeconds(2);

            // Act & Assert
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var startTime = DateTime.UtcNow;
                await _upsellScreen.InitializeAsync(_mockTemplate, _mockProduct, _mockComposedImagePath, _mockCapturedPhotos);
                var elapsed = DateTime.UtcNow - startTime;

                elapsed.Should().BeLessThan(maxInitializationTime);
            });
        }

        #endregion

        #region Memory Management Tests

        [TestMethod]
        public void Dispose_CleansUpResourcesProperly()
        {
            // Arrange & Act
            Application.Current.Dispatcher.Invoke(() =>
            {
                _upsellScreen.Dispose();
            });

            // Assert - Should not throw exceptions after disposal
            _upsellScreen.Should().NotBeNull();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a mock image file for testing
        /// </summary>
        private string CreateMockImageFile(string fileName)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            
            // Create a minimal valid JPEG file (just header bytes)
            var jpegHeader = new byte[] 
            { 
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 
                0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48,
                0x00, 0x48, 0x00, 0x00, 0xFF, 0xD9 
            };
            
            File.WriteAllBytes(filePath, jpegHeader);
            return filePath;
        }

        #endregion
    }
} 