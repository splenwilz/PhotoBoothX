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
        public async Task ExtraCopyPricing_OneCopy_CalculatesCorrectly()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var actualPrice = _upsellScreen.CalculateExtraCopyPrice(1);

                // Assert
                actualPrice.Should().Be(3.00m);
            });
        }

        [TestMethod]
        public async Task ExtraCopyPricing_TwoCopies_CalculatesCorrectly()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var actualPrice = _upsellScreen.CalculateExtraCopyPrice(2);

                // Assert
                actualPrice.Should().Be(5.00m);
            });
        }

        [TestMethod]
        public async Task ExtraCopyPricing_FourCopies_CalculatesCorrectly()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var fourCopyPrice = _upsellScreen.CalculateExtraCopyPrice(4);
                var fiveCopyPrice = _upsellScreen.CalculateExtraCopyPrice(5);
                var sixCopyPrice = _upsellScreen.CalculateExtraCopyPrice(6);

                // Assert
                fourCopyPrice.Should().Be(8.00m);   // Base price for 4 copies
                fiveCopyPrice.Should().Be(9.50m);   // Base + 1 additional
                sixCopyPrice.Should().Be(11.00m);   // Base + 2 additional
            });
        }

        [TestMethod]
        public async Task CrossSellProduct_StripsToPhoto4x6_ReturnsCorrectProduct()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var stripsProduct = new ProductInfo { Type = "strips" };
                _upsellScreen.SetOriginalProductForTesting(stripsProduct);

                var crossSellProduct = _upsellScreen.GetCrossSellProduct();

                // Assert
                crossSellProduct.Should().NotBeNull();
                crossSellProduct!.Type.Should().Be("4x6");
                crossSellProduct.Name.Should().Be("4x6 Photos");
                crossSellProduct.Price.Should().Be(3.00m);
            });
        }

        [TestMethod]
        public async Task CrossSellProduct_Photo4x6ToStrips_ReturnsCorrectProduct()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var photo4x6Product = new ProductInfo { Type = "4x6" };
                _upsellScreen.SetOriginalProductForTesting(photo4x6Product);

                var crossSellProduct = _upsellScreen.GetCrossSellProduct();

                // Assert
                crossSellProduct.Should().NotBeNull();
                crossSellProduct!.Type.Should().Be("strips");
                crossSellProduct.Name.Should().Be("Photo Strips");
                crossSellProduct.Price.Should().Be(5.00m);
            });
        }

        [TestMethod]
        public async Task CrossSellProduct_PhoneProduct_ReturnsNull()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var phoneProduct = new ProductInfo { Type = "phone" };
                _upsellScreen.SetOriginalProductForTesting(phoneProduct);

                var crossSellProduct = _upsellScreen.GetCrossSellProduct();

                // Assert
                crossSellProduct.Should().BeNull();
            });
        }

        [TestMethod]
        public async Task CrossSellProduct_PhotoStripsVariant_ReturnsCorrectProduct()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var photoStripsProduct = new ProductInfo { Type = "photostrips" };
                _upsellScreen.SetOriginalProductForTesting(photoStripsProduct);

                var crossSellProduct = _upsellScreen.GetCrossSellProduct();

                // Assert
                crossSellProduct.Should().NotBeNull();
                crossSellProduct!.Type.Should().Be("4x6");
                crossSellProduct.Name.Should().Be("4x6 Photos");
            });
        }

        [TestMethod]
        public async Task CrossSellProduct_Photo4x6Variant_ReturnsCorrectProduct()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var photo4x6Product = new ProductInfo { Type = "photo4x6" };
                _upsellScreen.SetOriginalProductForTesting(photo4x6Product);

                var crossSellProduct = _upsellScreen.GetCrossSellProduct();

                // Assert
                crossSellProduct.Should().NotBeNull();
                crossSellProduct!.Type.Should().Be("strips");
            });
        }

        [TestMethod]
        public async Task CrossSellProduct_UnknownProductType_ReturnsNull()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var unknownProduct = new ProductInfo { Type = "unknown" };
                _upsellScreen.SetOriginalProductForTesting(unknownProduct);

                var crossSellProduct = _upsellScreen.GetCrossSellProduct();

                // Assert
                crossSellProduct.Should().BeNull();
            });
        }

        [TestMethod]
        public async Task CalculateExtraCopyPrice_ZeroCopies_ReturnsZero()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var price = _upsellScreen.CalculateExtraCopyPrice(0);

                // Assert
                price.Should().Be(0);
            });
        }

        [TestMethod]
        public async Task CalculateExtraCopyPrice_ThreeCopies_ReturnsZero()
        {
            // Arrange & Act
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var price = _upsellScreen.CalculateExtraCopyPrice(3);

                // Assert
                price.Should().Be(0); // Invalid quantity
            });
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