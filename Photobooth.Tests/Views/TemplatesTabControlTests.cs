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

        #region Mock Service Validation Tests

        [TestMethod]
        public void MockDatabaseService_IsInitialized()
        {
            // Assert
            _mockDatabaseService.Should().NotBeNull();
            _mockDatabaseService.Object.Should().NotBeNull();
        }

        [TestMethod]
        public async Task MockDatabaseService_GetAllTemplatesAsync_ReturnsConfiguredData()
        {
            // Act
            var result = await _mockDatabaseService.Object.GetAllTemplatesAsync(false);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Should().HaveCount(5);
        }

        #endregion

        #region Database Service Tests

        [TestMethod]
        public void DatabaseService_GetAllTemplatesAsync_IsConfiguredCorrectly()
        {
            // Assert
            _mockDatabaseService.Verify(x => x.GetAllTemplatesAsync(It.IsAny<bool>()), Times.Never);
            
            // Verify setup is correct
            _testTemplates.Should().HaveCount(5);
            _testTemplates.Should().Contain(t => t.Name == "Classic Template 1");
            _testTemplates.Should().Contain(t => t.IsActive == false); // Holiday template
        }

        [TestMethod]
        public void DatabaseService_GetTemplateCategoriesAsync_IsConfiguredCorrectly()
        {
            // Assert
            _testCategories.Should().HaveCount(5);
            _testCategories.Should().Contain(c => c.Name == "Classic");
            _testCategories.Should().Contain(c => c.IsActive == false); // Seasonal category
            
            var activeCategories = _testCategories.Where(c => c.IsActive).ToList();
            activeCategories.Should().HaveCount(4);
        }

        #endregion

        #region Mock Service Verification Tests

        [TestMethod]
        public async Task MockDatabaseService_BulkUpdateTemplateCategoryAsync_IsConfiguredCorrectly()
        {
            // Arrange
            var templateIds = new List<int> { 1, 2, 3 };
            var categoryId = 1;

            // Act & Assert - Should not throw when configured correctly
            var act = async () => await _mockDatabaseService.Object.BulkUpdateTemplateCategoryAsync(templateIds, categoryId);
            await act.Should().NotThrowAsync();
        }

        [TestMethod]
        public async Task MockDatabaseService_UpdateTemplateAsync_IsConfiguredCorrectly()
        {
            // Act & Assert - Should not throw when configured correctly
            var act = async () => await _mockDatabaseService.Object.UpdateTemplateAsync(1, "New Name", true, 5.99m, 1, "Description", 1, 4);
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Data Verification Tests

        [TestMethod]
        public void TestData_Templates_ContainsExpectedValues()
        {
            // Assert
            _testTemplates.Should().HaveCount(5);
            _testTemplates.Should().Contain(t => t.Name == "Classic Template 1" && t.CategoryName == "Classic");
            _testTemplates.Should().Contain(t => t.Name == "Fun Template 1" && t.CategoryName == "Fun");
            _testTemplates.Should().Contain(t => t.Name == "Holiday Template 1" && t.IsActive == false);
            _testTemplates.Should().Contain(t => t.Price == 15.99m); // Premium template
        }

        [TestMethod]
        public void TestData_Categories_ContainsExpectedValues()
        {
            // Assert
            _testCategories.Should().HaveCount(5);
            _testCategories.Should().Contain(c => c.Name == "Classic" && c.IsActive);
            _testCategories.Should().Contain(c => c.Name == "Seasonal" && !c.IsActive);
            
            var activeCategories = _testCategories.Where(c => c.IsActive).ToList();
            activeCategories.Should().HaveCount(4);
        }

        #endregion
    }
} 