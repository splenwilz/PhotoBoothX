using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Photobooth.Tests
{
    [TestClass]
    public class ProductSelectedEventArgsTests
    {
        [TestMethod]
        public void Constructor_WithValidProductInfo_ShouldSetProperty()
        {
            // Arrange
            var productInfo = new ProductInfo
            {
                Type = "strips",
                Name = "Photo Strips",
                Description = "Classic 4-photo strip",
                Price = 5.00m
            };

            // Act
            var eventArgs = new ProductSelectedEventArgs(productInfo);

            // Assert
            eventArgs.ProductInfo.Should().BeSameAs(productInfo);
        }

        [TestMethod]
        public void Constructor_WithNullProductInfo_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Action constructor = () => new ProductSelectedEventArgs(null!);
            constructor.Should().Throw<ArgumentNullException>()
                .And.ParamName.Should().Be("productInfo");
        }
    }
}