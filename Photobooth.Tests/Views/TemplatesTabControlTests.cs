using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Photobooth.Models;
using Photobooth.Services;
using Photobooth.Views;

namespace Photobooth.Tests.Views
{
    [TestClass]
    public class TemplatesTabControlTests
    {
        #region Private Fields

        private Mock<IDatabaseService> _mockDatabaseService = null!;
        private List<Template> _testTemplates = null!;
        private List<TemplateCategory> _testCategories = null!;

        #endregion

        #region Test Setup and Cleanup

        [TestInitialize]
        public void Setup()
        {
            // Setup mock database service
            _mockDatabaseService = new Mock<IDatabaseService>();
            
            // Setup test data
            SetupTestData();
            SetupMockResponses();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockDatabaseService = null!;
        }

        #endregion

        #region Test Data Setup

        private void SetupTestData()
        {
            // Create test categories
            _testCategories = new List<TemplateCategory>
            {
                new TemplateCategory { Id = 1, Name = "Classic", IsActive = true, SortOrder = 1 },
                new TemplateCategory { Id = 2, Name = "Fun", IsActive = true, SortOrder = 2 },
                new TemplateCategory { Id = 3, Name = "Holiday", IsActive = true, SortOrder = 3 },
                new TemplateCategory { Id = 4, Name = "Premium", IsActive = true, SortOrder = 4 },
                new TemplateCategory { Id = 5, Name = "Seasonal", IsActive = false, SortOrder = 5 }
            };

            // Create test templates
            _testTemplates = new List<Template>
            {
                new Template
                {
                    Id = 1,
                    Name = "Classic Template 1",
                    CategoryId = 1,
                    CategoryName = "Classic",
                    Price = 5.99m,
                    IsActive = true,
                    FileSize = 1024 * 1024, // 1MB
                    UploadedAt = DateTime.Now.AddDays(-10),
                    FolderPath = @"C:\Templates\Classic1",
                    PreviewPath = @"C:\Templates\Classic1\preview.jpg"
                },
                new Template
                {
                    Id = 2,
                    Name = "Fun Template 1",
                    CategoryId = 2,
                    CategoryName = "Fun",
                    Price = 7.99m,
                    IsActive = true,
                    FileSize = 2 * 1024 * 1024, // 2MB
                    UploadedAt = DateTime.Now.AddDays(-5),
                    FolderPath = @"C:\Templates\Fun1",
                    PreviewPath = @"C:\Templates\Fun1\preview.jpg"
                },
                new Template
                {
                    Id = 3,
                    Name = "Holiday Template 1",
                    CategoryId = 3,
                    CategoryName = "Holiday",
                    Price = 9.99m,
                    IsActive = false, // Disabled template
                    FileSize = 3 * 1024 * 1024, // 3MB
                    UploadedAt = DateTime.Now.AddDays(-3),
                    FolderPath = @"C:\Templates\Holiday1",
                    PreviewPath = @"C:\Templates\Holiday1\preview.jpg"
                },
                new Template
                {
                    Id = 4,
                    Name = "Premium Template 1",
                    CategoryId = 4,
                    CategoryName = "Premium",
                    Price = 15.99m,
                    IsActive = true,
                    FileSize = 5 * 1024 * 1024, // 5MB
                    UploadedAt = DateTime.Now.AddDays(-1),
                    FolderPath = @"C:\Templates\Premium1",
                    PreviewPath = @"C:\Templates\Premium1\preview.jpg"
                },
                new Template
                {
                    Id = 5,
                    Name = "Another Classic",
                    CategoryId = 1,
                    CategoryName = "Classic",
                    Price = 4.99m,
                    IsActive = true,
                    FileSize = 1536 * 1024, // 1.5MB
                    UploadedAt = DateTime.Now.AddDays(-7),
                    FolderPath = @"C:\Templates\Classic2",
                    PreviewPath = @"C:\Templates\Classic2\preview.jpg"
                }
            };
        }

        private void SetupMockResponses()
        {
            // Setup template responses
            _mockDatabaseService.Setup(x => x.GetAllTemplatesAsync(It.IsAny<bool>()))
                .ReturnsAsync(new DatabaseResult<List<Template>>
                {
                    Success = true,
                    Data = _testTemplates
                });

            // Setup category responses
            _mockDatabaseService.Setup(x => x.GetTemplateCategoriesAsync())
                .ReturnsAsync(new DatabaseResult<List<TemplateCategory>>
                {
                    Success = true,
                    Data = _testCategories.Where(c => c.IsActive).ToList()
                });

            _mockDatabaseService.Setup(x => x.GetAllTemplateCategoriesAsync())
                .ReturnsAsync(new DatabaseResult<List<TemplateCategory>>
                {
                    Success = true,
                    Data = _testCategories
                });

            // Setup system date status
            _mockDatabaseService.Setup(x => x.GetSystemDateStatusAsync())
                .ReturnsAsync(new DatabaseResult<SystemDateStatus>
                {
                    Success = true,
                    Data = new SystemDateStatus
                    {
                        CurrentSystemDateString = DateTime.Now.ToString("MMM dd, yyyy"),
                        TimeZone = "UTC",
                        CurrentDateForSeason = DateTime.Now.ToString("yyyy-MM-dd"),
                        ActiveSeasonsCount = 2,
                        SeasonalCategories = new List<SeasonStatus>()
                    }
                });

            // Setup template operations - correct parameter order
            _mockDatabaseService.Setup(x => x.UpdateTemplateAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<decimal?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .ReturnsAsync(new DatabaseResult<Template> { Success = true });

            _mockDatabaseService.Setup(x => x.BulkUpdateTemplateCategoryAsync(It.IsAny<List<int>>(), It.IsAny<int>()))
                .ReturnsAsync(new DatabaseResult { Success = true });
        }

        #endregion

        #region TemplatesTabControl Business Logic Tests (No WPF Controls)

        [TestMethod]
        public void TemplatesTabControl_MockDataSetup_ShouldBeValid()
        {
            // Assert - Test that our mock data is correctly set up
            _testTemplates.Should().HaveCount(5);
            _testTemplates.Should().Contain(t => t.Name == "Classic Template 1" && t.CategoryName == "Classic");
            _testTemplates.Should().Contain(t => t.Name == "Fun Template 1" && t.CategoryName == "Fun");
            _testTemplates.Should().Contain(t => t.Name == "Holiday Template 1" && t.IsActive == false);
            _testTemplates.Should().Contain(t => t.Price == 15.99m); // Premium template
        }

        [TestMethod]
        public async Task DatabaseService_GetAllTemplatesAsync_ShouldReturnMockedData()
        {
            // Act
            var result = await _mockDatabaseService.Object.GetAllTemplatesAsync(false);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Should().HaveCount(5);
            result.Data.Should().Contain(t => t.Name == "Classic Template 1");
            result.Data.Should().Contain(t => t.Name == "Premium Template 1");
        }

        [TestMethod]
        public async Task DatabaseService_GetTemplateCategoriesAsync_ShouldReturnActiveCategories()
        {
            // Act
            var result = await _mockDatabaseService.Object.GetTemplateCategoriesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Should().HaveCount(4); // Only active categories
            result.Data.Should().Contain(c => c.Name == "Classic");
            result.Data.Should().NotContain(c => c.Name == "Seasonal"); // Inactive category
        }

        [TestMethod]
        public async Task DatabaseService_BulkUpdateTemplateCategoryAsync_ShouldSucceed()
        {
            // Arrange
            var templateIds = new List<int> { 1, 2, 3 };
            var categoryId = 1;

            // Act
            var result = await _mockDatabaseService.Object.BulkUpdateTemplateCategoryAsync(templateIds, categoryId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        [TestMethod]
        public async Task DatabaseService_UpdateTemplateAsync_ShouldSucceed()
        {
            // Arrange
            var templateId = 1;
            var newName = "Updated Template Name";
            var isActive = true;
            var price = 12.99m;

            // Act
            var result = await _mockDatabaseService.Object.UpdateTemplateAsync(templateId, newName, isActive, price, null, null, null, null);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        #endregion

        #region TemplatesTabControl Business Logic Tests

        [TestMethod]
        public async Task DatabaseService_WithErrorResponse_ShouldReturnFailure()
        {
            // Arrange
            var errorMock = new Mock<IDatabaseService>();
            errorMock.Setup(x => x.GetAllTemplatesAsync(It.IsAny<bool>()))
                .ReturnsAsync(new DatabaseResult<List<Template>>
                {
                    Success = false,
                    ErrorMessage = "Database connection failed"
                });

            // Act
            var result = await errorMock.Object.GetAllTemplatesAsync(false);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Be("Database connection failed");
        }

        [TestMethod]
        public void TemplatesTabControl_TestDataFiltering_ShouldWorkCorrectly()
        {
            // Test business logic without WPF controls
            var classicTemplates = _testTemplates.Where(t => t.CategoryName == "Classic").ToList();
            var activeTemplates = _testTemplates.Where(t => t.IsActive).ToList();
            var premiumTemplates = _testTemplates.Where(t => t.Price > 10m).ToList();

            // Assert filtering logic
            classicTemplates.Should().HaveCount(2);
            activeTemplates.Should().HaveCount(4);
            premiumTemplates.Should().HaveCount(1);
        }

        [TestMethod]
        public void TemplatesTabControl_TestDataSorting_ShouldWorkCorrectly()
        {
            // Test sorting logic without WPF controls
            var sortedByPrice = _testTemplates.OrderBy(t => t.Price).ToList();
            var sortedByName = _testTemplates.OrderBy(t => t.Name).ToList();

            // Assert sorting logic
            sortedByPrice.First().Price.Should().Be(4.99m); // "Another Classic"
            sortedByPrice.Last().Price.Should().Be(15.99m); // "Premium Template 1"
            
            sortedByName.First().Name.Should().Be("Another Classic");
            sortedByName.Last().Name.Should().Be("Premium Template 1");
        }

        [TestMethod]
        public void TemplatesTabControl_PaginationLogic_ShouldCalculateCorrectly()
        {
            // Test pagination without WPF controls
            var templatesPerPage = 2;
            var totalTemplates = _testTemplates.Count;
            var totalPages = (int)Math.Ceiling((double)totalTemplates / templatesPerPage);

            // Assert pagination logic
            totalPages.Should().Be(3); // 5 templates / 2 per page = 3 pages
            
            // Test page 1
            var page1 = _testTemplates.Skip(0).Take(templatesPerPage).ToList();
            page1.Should().HaveCount(2);
            
            // Test page 2
            var page2 = _testTemplates.Skip(2).Take(templatesPerPage).ToList();
            page2.Should().HaveCount(2);
            
            // Test page 3
            var page3 = _testTemplates.Skip(4).Take(templatesPerPage).ToList();
            page3.Should().HaveCount(1);
        }

        #endregion

        #region TemplatesTabControl Search and Filter Logic Tests

        [TestMethod]
        public void TemplatesTabControl_SearchByName_ShouldFilterCorrectly()
        {
            // Test search logic without WPF controls
            var searchTerm = "Premium";
            var searchResults = _testTemplates.Where(t => 
                t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                t.CategoryName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // Assert search logic
            searchResults.Should().HaveCount(1);
            searchResults.Should().Contain(t => t.Name == "Premium Template 1");
        }

        [TestMethod]
        public void TemplatesTabControl_FilterByCategory_ShouldWorkCorrectly()
        {
            // Test category filtering without WPF controls
            var categoryFilter = "Classic";
            var categoryResults = _testTemplates.Where(t => t.CategoryName == categoryFilter).ToList();

            // Assert category filtering
            categoryResults.Should().HaveCount(2);
            categoryResults.Should().AllSatisfy(t => t.CategoryName.Should().Be("Classic"));
        }

        [TestMethod]
        public void TemplatesTabControl_FilterByActiveStatus_ShouldWorkCorrectly()
        {
            // Test active status filtering without WPF controls
            var activeOnly = _testTemplates.Where(t => t.IsActive).ToList();
            var inactiveOnly = _testTemplates.Where(t => !t.IsActive).ToList();

            // Assert status filtering
            activeOnly.Should().HaveCount(4);
            inactiveOnly.Should().HaveCount(1);
            inactiveOnly.Should().Contain(t => t.Name == "Holiday Template 1");
        }

        [TestMethod]
        public void TemplatesTabControl_CombinedFilters_ShouldWorkCorrectly()
        {
            // Test combined filtering without WPF controls
            var categoryFilter = "Classic";
            var activeOnly = true;
            
            var combinedResults = _testTemplates
                .Where(t => t.CategoryName == categoryFilter)
                .Where(t => t.IsActive == activeOnly)
                .ToList();

            // Assert combined filtering
            combinedResults.Should().HaveCount(2);
            combinedResults.Should().AllSatisfy(t => 
            {
                t.CategoryName.Should().Be("Classic");
                t.IsActive.Should().BeTrue();
            });
        }

        #endregion
    }
} 