using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Photobooth.Tests
{
    [TestClass]
    public class ProductInfoTests
    {
        [TestMethod]
        public void ProductInfo_DefaultConstructor_ShouldInitializeProperties()
        {
            // Arrange & Act
            var productInfo = new ProductInfo();

            // Assert
            productInfo.Type.Should().Be("");
            productInfo.Name.Should().Be("");
            productInfo.Description.Should().Be("");
            productInfo.Price.Should().Be(0);
        }

        [TestMethod]
        public void ProductInfo_SetProperties_ShouldRetainValues()
        {
            // Arrange
            var productInfo = new ProductInfo();

            // Act
            productInfo.Type = "test-type";
            productInfo.Name = "Test Product";
            productInfo.Description = "Test Description";
            productInfo.Price = 12.50m;

            // Assert
            productInfo.Type.Should().Be("test-type");
            productInfo.Name.Should().Be("Test Product");
            productInfo.Description.Should().Be("Test Description");
            productInfo.Price.Should().Be(12.50m);
        }
    }
}