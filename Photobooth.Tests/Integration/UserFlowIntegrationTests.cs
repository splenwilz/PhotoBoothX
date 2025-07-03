using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth.Tests.Integration
{
    /// <summary>
    /// Comprehensive integration tests for the core functionality we've implemented
    /// Tests functionality without UI components to avoid WPF threading issues:
    /// - Database integration and template loading
    /// - Data model functionality
    /// - Service layer operations
    /// </summary>
    [TestClass]
    public class UserFlowIntegrationTests : IDisposable
    {
        // Test execution framework support
        public TestContext? TestContext { get; set; }
        
        private IDatabaseService? _databaseService;
        private bool _disposed = false;

        [TestInitialize]
        public void TestInitialize()
        {
            _databaseService = new DatabaseService();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Dispose();
        }

        [TestMethod]
        public async Task Database_Integration_ShouldLoadTemplates()
        {
            // Arrange
            TestContext?.WriteLine("Testing database integration for template loading...");

            // Act
            var templatesResult = await _databaseService!.GetAllTemplatesAsync();

            // Assert
            templatesResult.Success.Should().BeTrue($"Database should load templates: {templatesResult.ErrorMessage}");
            templatesResult.Data.Should().NotBeNull("Templates data should not be null");
            TestContext?.WriteLine($"✓ Database loaded {templatesResult.Data?.Count ?? 0} templates successfully");

            if (templatesResult.Data?.Count > 0)
            {
                var firstTemplate = templatesResult.Data[0];
                firstTemplate.Name.Should().NotBeNullOrEmpty("Template name should not be empty");
                firstTemplate.Id.Should().BeGreaterThan(0, "Template ID should be positive");
                TestContext?.WriteLine($"✓ Template data integrity verified: {firstTemplate.Name}");
            }
        }

        [TestMethod]
        public void ProductInfo_Model_ShouldInitializeCorrectly()
        {
            // Arrange
            var product = new ProductInfo
            {
                Name = "Test Product",
                Type = "strips",
                Price = 5.00m,
                Description = "Test description"
            };

            // Act & Assert
            product.Name.Should().Be("Test Product");
            product.Type.Should().Be("strips");
            product.Price.Should().Be(5.00m);
            product.Description.Should().Be("Test description");
            TestContext?.WriteLine("✓ ProductInfo model works correctly");
        }

        [TestMethod]
        public void TemplateInfo_Model_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var template = new TemplateInfo
            {
                TemplateName = "Test Template",
                Category = "Classic",
                PreviewImagePath = "/path/to/image.jpg",
                FolderPath = "/path/to/folder"
            };

            // Assert
            template.TemplateName.Should().Be("Test Template");
            template.Category.Should().Be("Classic");
            template.PreviewImagePath.Should().Be("/path/to/image.jpg");
            template.FolderPath.Should().Be("/path/to/folder");
            TestContext?.WriteLine("✓ TemplateInfo model works correctly");
        }

        [TestMethod]
        public async Task DatabaseService_Initialization_ShouldSucceed()
        {
            // Arrange
            TestContext?.WriteLine("Testing database service initialization...");

            // Act
            var result = await _databaseService!.InitializeAsync();

            // Assert
            result.Success.Should().BeTrue($"Database initialization should succeed: {result.ErrorMessage}");
            TestContext?.WriteLine("✓ Database service initialized successfully");
        }

        [TestMethod]
        public async Task Template_Categories_ShouldLoadCorrectly()
        {
            // Arrange
            TestContext?.WriteLine("Testing template categories loading...");

            // Act
            var categoriesResult = await _databaseService!.GetTemplateCategoriesAsync();

            // Assert
            categoriesResult.Success.Should().BeTrue($"Should load categories: {categoriesResult.ErrorMessage}");
            TestContext?.WriteLine($"✓ Loaded {categoriesResult.Data?.Count ?? 0} template categories");

            if (categoriesResult.Data?.Count > 0)
            {
                var firstCategory = categoriesResult.Data[0];
                firstCategory.Name.Should().NotBeNullOrEmpty("Category name should not be empty");
                firstCategory.Id.Should().BeGreaterThan(0, "Category ID should be positive");
                TestContext?.WriteLine($"✓ Category data integrity verified: {firstCategory.Name}");
            }
        }

        [TestMethod]
        public void Navigation_EventArgs_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var productSelectedArgs = new ProductSelectedEventArgs(new ProductInfo 
            { 
                Name = "Test Product", 
                Type = "strips", 
                Price = 5.00m 
            });

            var templateSelectedArgs = new TemplateSelectedEventArgs(new TemplateInfo 
            { 
                TemplateName = "Test Template", 
                Category = "Classic" 
            });

            // Create a mock Template for PhotoSessionStartEventArgs
            var mockTemplate = new Template 
            { 
                Name = "Test Template",
                CategoryName = "Classic"
            };

            var photoSessionArgs = new PhotoSessionStartEventArgs(
                mockTemplate, 
                new ProductInfo { Name = "Test Product", Type = "strips", Price = 5.00m },
                new List<string>(), // customizations
                4, // photoCount
                3, // timerSeconds
                true // flashEnabled
            );

            // Assert
            productSelectedArgs.ProductInfo.Should().NotBeNull();
            productSelectedArgs.ProductInfo.Name.Should().Be("Test Product");

            templateSelectedArgs.Template.Should().NotBeNull();
            templateSelectedArgs.Template.TemplateName.Should().Be("Test Template");

            photoSessionArgs.Template.Should().NotBeNull();
            photoSessionArgs.PhotoCount.Should().Be(4);
            photoSessionArgs.TimerSeconds.Should().Be(3);
            photoSessionArgs.FlashEnabled.Should().BeTrue();

            TestContext?.WriteLine("✓ All event args work correctly");
        }

        [TestMethod]
        public async Task Complete_UserFlow_DataModels_ShouldWork()
        {
            // Arrange
            TestContext?.WriteLine("=== TESTING COMPLETE USER FLOW DATA MODELS ===");

            // Act & Assert - Test the complete data flow we've implemented

            // 1. Product Selection
            var product = new ProductInfo { Name = "Photo Strips", Type = "strips", Price = 5.00m };
            product.Name.Should().NotBeNullOrEmpty();
            TestContext?.WriteLine("✓ Product selection data model works");

            // 2. Template Loading from Database
            var templatesResult = await _databaseService!.GetAllTemplatesAsync();
            templatesResult.Success.Should().BeTrue();
            TestContext?.WriteLine("✓ Template loading from database works");

            // 3. Template Selection
            if (templatesResult.Data?.Count > 0)
            {
                var template = new TemplateInfo
                {
                    TemplateName = templatesResult.Data[0].Name,
                    Category = templatesResult.Data[0].CategoryName,
                    PreviewImagePath = templatesResult.Data[0].PreviewPath
                };
                template.TemplateName.Should().NotBeNullOrEmpty();
                TestContext?.WriteLine("✓ Template selection data model works");

                // 4. Photo Session Start
                var photoSessionArgs = new PhotoSessionStartEventArgs(
                    templatesResult.Data[0], // Use the actual Template from database
                    product,
                    new List<string>(), // customizations
                    4, // photoCount
                    3, // timerSeconds
                    true // flashEnabled
                );
                photoSessionArgs.Template.Should().NotBeNull();
                photoSessionArgs.PhotoCount.Should().BeGreaterThan(0);
                TestContext?.WriteLine("✓ Photo session start data model works");
            }

            TestContext?.WriteLine("=== COMPLETE USER FLOW DATA MODELS TEST PASSED ===");
        }

        [TestMethod]
        public void Memory_Management_Models_ShouldNotLeak()
        {
            // Arrange
            TestContext?.WriteLine("Testing memory management for data models...");
            var initialMemory = GC.GetTotalMemory(false);

            // Act - Create many instances
            for (int i = 0; i < 1000; i++)
            {
                var product = new ProductInfo { Name = $"Product {i}", Type = "strips", Price = 5.00m };
                var template = new TemplateInfo { TemplateName = $"Template {i}", Category = "Classic" };
                
                // Let them go out of scope
            }

            // Force garbage collection (appropriate for memory leak testing)
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            memoryIncrease.Should().BeLessThan(1_000_000, // Less than 1MB increase
                $"Memory increase should be minimal: {memoryIncrease:N0} bytes");
            
            TestContext?.WriteLine($"✓ Memory usage acceptable: {memoryIncrease:N0} bytes increase");
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // DatabaseService implements IDisposable, but IDatabaseService interface does not
                    if (_databaseService is IDisposable disposableService)
                    {
                        disposableService.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    TestContext?.WriteLine($"Disposal error: {ex.Message}");
                }
                _disposed = true;
            }
        }

        #endregion
    }
} 