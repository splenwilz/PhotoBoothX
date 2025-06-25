using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhotoBooth;

namespace Photobooth.Tests.Models
{
    [TestClass]
    public class TemplateInfoTests
    {
        [TestMethod]
        public void TemplateInfo_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var templateInfo = new TemplateInfo();

            // Assert
            Assert.IsNotNull(templateInfo.Config);
            Assert.AreEqual("", templateInfo.PreviewImagePath);
            Assert.AreEqual("", templateInfo.TemplateImagePath);
            Assert.AreEqual("", templateInfo.FolderPath);
            Assert.AreEqual("", templateInfo.TemplateName);
            Assert.AreEqual("", templateInfo.Category);
            Assert.AreEqual("", templateInfo.Description);
            Assert.IsFalse(templateInfo.IsSeasonalTemplate);
            Assert.AreEqual(0, templateInfo.SeasonPriority);
            Assert.AreEqual(0, templateInfo.DisplayWidth);
            Assert.AreEqual(0, templateInfo.DisplayHeight);
            Assert.AreEqual("", templateInfo.DimensionText);
            Assert.AreEqual(0, templateInfo.AspectRatio);
            Assert.AreEqual("", templateInfo.AspectRatioText);
            Assert.AreEqual("", templateInfo.TemplateSize);
        }

        [TestMethod]
        public void TemplateInfo_WithValidData_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var templateInfo = new TemplateInfo
            {
                PreviewImagePath = "/path/to/preview.jpg",
                TemplateImagePath = "/path/to/template.jpg",
                FolderPath = "/path/to/folder",
                TemplateName = "Test Template",
                Category = "Strips",
                Description = "A test template",
                IsSeasonalTemplate = true,
                SeasonPriority = 5,
                DisplayWidth = 300.0,
                DisplayHeight = 200.0,
                DimensionText = "300x200",
                AspectRatio = 1.5,
                AspectRatioText = "3:2",
                TemplateSize = "Wide"
            };

            // Assert
            Assert.AreEqual("/path/to/preview.jpg", templateInfo.PreviewImagePath);
            Assert.AreEqual("/path/to/template.jpg", templateInfo.TemplateImagePath);
            Assert.AreEqual("/path/to/folder", templateInfo.FolderPath);
            Assert.AreEqual("Test Template", templateInfo.TemplateName);
            Assert.AreEqual("Strips", templateInfo.Category);
            Assert.AreEqual("A test template", templateInfo.Description);
            Assert.IsTrue(templateInfo.IsSeasonalTemplate);
            Assert.AreEqual(5, templateInfo.SeasonPriority);
            Assert.AreEqual(300.0, templateInfo.DisplayWidth);
            Assert.AreEqual(200.0, templateInfo.DisplayHeight);
            Assert.AreEqual("300x200", templateInfo.DimensionText);
            Assert.AreEqual(1.5, templateInfo.AspectRatio);
            Assert.AreEqual("3:2", templateInfo.AspectRatioText);
            Assert.AreEqual("Wide", templateInfo.TemplateSize);
        }

        [TestMethod]
        public void TemplateConfig_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var config = new TemplateConfig();

            // Assert
            Assert.AreEqual("", config.TemplateName);
            Assert.AreEqual("", config.TemplateId);
            Assert.IsNotNull(config.Dimensions);
            Assert.IsNotNull(config.PhotoAreas);
            Assert.AreEqual(0, config.PhotoAreas.Count);
            Assert.AreEqual(0, config.PhotoCount);
            Assert.AreEqual("", config.Category);
            Assert.AreEqual("", config.Description);
        }

        [TestMethod]
        public void TemplateConfig_WithValidData_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var config = new TemplateConfig
            {
                TemplateName = "Test Template",
                TemplateId = "test_template_1",
                PhotoCount = 4,
                Category = "Strips",
                Description = "A test template configuration"
            };

            // Assert
            Assert.AreEqual("Test Template", config.TemplateName);
            Assert.AreEqual("test_template_1", config.TemplateId);
            Assert.AreEqual(4, config.PhotoCount);
            Assert.AreEqual("Strips", config.Category);
            Assert.AreEqual("A test template configuration", config.Description);
        }

        [TestMethod]
        public void TemplateDimensions_DefaultConstructor_SetsZeroValues()
        {
            // Arrange & Act
            var dimensions = new TemplateDimensions();

            // Assert
            Assert.AreEqual(0, dimensions.Width);
            Assert.AreEqual(0, dimensions.Height);
        }

        [TestMethod]
        public void TemplateDimensions_WithValidData_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var dimensions = new TemplateDimensions
            {
                Width = 1920,
                Height = 1080
            };

            // Assert
            Assert.AreEqual(1920, dimensions.Width);
            Assert.AreEqual(1080, dimensions.Height);
        }

        [TestMethod]
        public void PhotoArea_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var photoArea = new PhotoArea();

            // Assert
            Assert.AreEqual("", photoArea.Id);
            Assert.AreEqual(0, photoArea.X);
            Assert.AreEqual(0, photoArea.Y);
            Assert.AreEqual(0, photoArea.Width);
            Assert.AreEqual(0, photoArea.Height);
        }

        [TestMethod]
        public void PhotoArea_WithValidData_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var photoArea = new PhotoArea
            {
                Id = "photo_area_1",
                X = 100,
                Y = 150,
                Width = 300,
                Height = 400
            };

            // Assert
            Assert.AreEqual("photo_area_1", photoArea.Id);
            Assert.AreEqual(100, photoArea.X);
            Assert.AreEqual(150, photoArea.Y);
            Assert.AreEqual(300, photoArea.Width);
            Assert.AreEqual(400, photoArea.Height);
        }
    }
} 