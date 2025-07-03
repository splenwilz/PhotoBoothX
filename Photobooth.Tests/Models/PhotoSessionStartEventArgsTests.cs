using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhotoBooth;
using Photobooth.Models;

namespace Photobooth.Tests.Models
{
    [TestClass]
    public class PhotoSessionStartEventArgsTests
    {
        [TestMethod]
        public void PhotoSessionStartEventArgs_ValidConstructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var template = new Template { Id = 1, Name = "Test Template" };
            var product = new ProductInfo { Type = "Strips", Name = "Photo Strips" };
            var customizations = new List<string> { "Filter1", "Icon1" };
            int photoCount = 4;
            int timerSeconds = 5;
            bool flashEnabled = true;

            // Act
            var eventArgs = new PhotoSessionStartEventArgs(template, product, customizations, photoCount, timerSeconds, flashEnabled);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.AreEqual(1, eventArgs.Template.Id);
            Assert.AreEqual("Test Template", eventArgs.Template.Name);
            Assert.IsNotNull(eventArgs.Product);
            Assert.AreEqual("Strips", eventArgs.Product.Type);
            Assert.AreEqual("Photo Strips", eventArgs.Product.Name);
            Assert.IsNotNull(eventArgs.Customizations);
            Assert.AreEqual(2, eventArgs.Customizations.Count);
            CollectionAssert.Contains(eventArgs.Customizations, "Filter1");
            CollectionAssert.Contains(eventArgs.Customizations, "Icon1");
            Assert.AreEqual(4, eventArgs.PhotoCount);
            Assert.AreEqual(5, eventArgs.TimerSeconds);
            Assert.IsTrue(eventArgs.FlashEnabled);
        }

        [TestMethod]
        public void PhotoSessionStartEventArgs_NullProduct_SetsProductToNull()
        {
            // Arrange
            var template = new Template { Id = 1, Name = "Test Template" };
            var customizations = new List<string>();
            int photoCount = 1;
            int timerSeconds = 3;
            bool flashEnabled = false;

            // Act
            var eventArgs = new PhotoSessionStartEventArgs(template, null, customizations, photoCount, timerSeconds, flashEnabled);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.IsNull(eventArgs.Product);
            Assert.IsNotNull(eventArgs.Customizations);
            Assert.AreEqual(0, eventArgs.Customizations.Count);
            Assert.AreEqual(1, eventArgs.PhotoCount);
            Assert.AreEqual(3, eventArgs.TimerSeconds);
            Assert.IsFalse(eventArgs.FlashEnabled);
        }

        [TestMethod]
        public void PhotoSessionStartEventArgs_EmptyCustomizations_SetsEmptyList()
        {
            // Arrange
            var template = new Template { Id = 2, Name = "Another Template" };
            var product = new ProductInfo { Type = "4x6", Name = "Photo 4x6" };
            var customizations = new List<string>();
            int photoCount = 2;
            int timerSeconds = 10;
            bool flashEnabled = true;

            // Act
            var eventArgs = new PhotoSessionStartEventArgs(template, product, customizations, photoCount, timerSeconds, flashEnabled);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.IsNotNull(eventArgs.Product);
            Assert.IsNotNull(eventArgs.Customizations);
            Assert.AreEqual(0, eventArgs.Customizations.Count);
            Assert.AreEqual(2, eventArgs.PhotoCount);
            Assert.AreEqual(10, eventArgs.TimerSeconds);
            Assert.IsTrue(eventArgs.FlashEnabled);
        }

        [TestMethod]
        public void PhotoSessionStartEventArgs_WithMultipleCustomizations_SetsAllCustomizations()
        {
            // Arrange
            var template = new Template { Id = 3, Name = "Custom Template" };
            var product = new ProductInfo { Type = "Phone", Name = "Smartphone Print" };
            var customizations = new List<string> { "Sepia Filter", "Heart Icon", "Birthday Text", "Gold Border" };
            int photoCount = 6;
            int timerSeconds = 7;
            bool flashEnabled = false;

            // Act
            var eventArgs = new PhotoSessionStartEventArgs(template, product, customizations, photoCount, timerSeconds, flashEnabled);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.IsNotNull(eventArgs.Product);
            Assert.IsNotNull(eventArgs.Customizations);
            Assert.AreEqual(4, eventArgs.Customizations.Count);
            CollectionAssert.Contains(eventArgs.Customizations, "Sepia Filter");
            CollectionAssert.Contains(eventArgs.Customizations, "Heart Icon");
            CollectionAssert.Contains(eventArgs.Customizations, "Birthday Text");
            CollectionAssert.Contains(eventArgs.Customizations, "Gold Border");
            Assert.AreEqual(6, eventArgs.PhotoCount);
            Assert.AreEqual(7, eventArgs.TimerSeconds);
            Assert.IsFalse(eventArgs.FlashEnabled);
        }

        [TestMethod]
        public void PhotoSessionStartEventArgs_ZeroValues_SetsCorrectly()
        {
            // Arrange
            var template = new Template { Id = 4, Name = "Zero Template" };
            var product = new ProductInfo { Type = "Test", Name = "Test Product" };
            var customizations = new List<string>();
            int photoCount = 0;
            int timerSeconds = 0;
            bool flashEnabled = false;

            // Act
            var eventArgs = new PhotoSessionStartEventArgs(template, product, customizations, photoCount, timerSeconds, flashEnabled);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.IsNotNull(eventArgs.Product);
            Assert.IsNotNull(eventArgs.Customizations);
            Assert.AreEqual(0, eventArgs.PhotoCount);
            Assert.AreEqual(0, eventArgs.TimerSeconds);
            Assert.IsFalse(eventArgs.FlashEnabled);
        }

        [TestMethod]
        public void PhotoSessionStartEventArgs_InheritsFromEventArgs()
        {
            // Arrange
            var template = new Template { Id = 5, Name = "Inheritance Test" };
            var customizations = new List<string>();

            // Act
            var eventArgs = new PhotoSessionStartEventArgs(template, null, customizations, 1, 1, true);

            // Assert
            Assert.IsInstanceOfType(eventArgs, typeof(EventArgs));
        }

        [TestMethod]
        public void TemplateCustomizedEventArgs_ValidConstructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var template = new Template { Id = 6, Name = "Customized Template" };
            var product = new ProductInfo { Type = "Strips", Name = "Photo Strips" };
            var customizations = new List<string> { "Custom1", "Custom2" };

            // Act
            var eventArgs = new TemplateCustomizedEventArgs(template, product, customizations);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.AreEqual(6, eventArgs.Template.Id);
            Assert.AreEqual("Customized Template", eventArgs.Template.Name);
            Assert.IsNotNull(eventArgs.Product);
            Assert.AreEqual("Strips", eventArgs.Product.Type);
            Assert.IsNotNull(eventArgs.Customizations);
            Assert.AreEqual(2, eventArgs.Customizations.Count);
            CollectionAssert.Contains(eventArgs.Customizations, "Custom1");
            CollectionAssert.Contains(eventArgs.Customizations, "Custom2");
        }

        [TestMethod]
        public void TemplateCustomizedEventArgs_NullProduct_AllowsNullProduct()
        {
            // Arrange
            var template = new Template { Id = 7, Name = "No Product Template" };
            var customizations = new List<string>();

            // Act
            var eventArgs = new TemplateCustomizedEventArgs(template, null, customizations);

            // Assert
            Assert.IsNotNull(eventArgs.Template);
            Assert.IsNull(eventArgs.Product);
            Assert.IsNotNull(eventArgs.Customizations);
        }

        [TestMethod]
        public void TemplateCustomizedEventArgs_InheritsFromEventArgs()
        {
            // Arrange
            var template = new Template { Id = 8, Name = "EventArgs Test" };
            var customizations = new List<string>();

            // Act
            var eventArgs = new TemplateCustomizedEventArgs(template, null, customizations);

            // Assert
            Assert.IsInstanceOfType(eventArgs, typeof(EventArgs));
        }
    }
} 