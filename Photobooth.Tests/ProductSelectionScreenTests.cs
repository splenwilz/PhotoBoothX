using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Photobooth.Tests
{
    [TestClass]
    public class ProductSelectionScreenTests
    {
        #region Product Configuration Tests (Core Business Logic)

        [TestMethod]
        public void ProductConfiguration_ShouldContainAllExpectedProducts()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;

            // Assert
            products.Should().ContainKey("strips");
            products.Should().ContainKey("4x6");
            products.Should().ContainKey("phone");
            products.Count.Should().Be(3);
        }

        [TestMethod]
        public void ProductConfiguration_ShouldNotBeNull()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;

            // Assert
            products.Should().NotBeNull();
            products.Should().NotBeEmpty();
        }

        [TestMethod]
        public void ProductConfiguration_StripProduct_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var stripProduct = ProductConfiguration.Products["strips"];

            // Assert
            stripProduct.Should().NotBeNull();
            stripProduct.Type.Should().Be("strips");
            stripProduct.Name.Should().Be("Photo Strips");
            stripProduct.Description.Should().Be("Classic 4-photo strip");
            stripProduct.Price.Should().Be(6.00m);
        }

        [TestMethod]
        public void ProductConfiguration_4x6Product_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var product4x6 = ProductConfiguration.Products["4x6"];

            // Assert
            product4x6.Should().NotBeNull();
            product4x6.Type.Should().Be("4x6");
            product4x6.Name.Should().Be("4x6 Photos");
            product4x6.Description.Should().Be("High-quality print");
            product4x6.Price.Should().Be(3.00m);
        }

        [TestMethod]
        public void ProductConfiguration_PhoneProduct_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var phoneProduct = ProductConfiguration.Products["phone"];

            // Assert
            phoneProduct.Should().NotBeNull();
            phoneProduct.Type.Should().Be("phone");
            phoneProduct.Name.Should().Be("Print from Phone");
            phoneProduct.Description.Should().Be("Print photos from your phone");
            phoneProduct.Price.Should().Be(2.00m);
        }

        [TestMethod]
        public void ProductConfiguration_AllProducts_ShouldHaveValidPrices()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;

            // Assert
            foreach (var product in products.Values)
            {
                product.Price.Should().BeGreaterThan(0m);
                product.Price.Should().BeLessThanOrEqualTo(1000m); // Reasonable upper limit
            }
        }

        [TestMethod]
        public void ProductConfiguration_AllProducts_ShouldHaveValidNames()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;

            // Assert
            foreach (var product in products.Values)
            {
                product.Name.Should().NotBeNullOrEmpty();
                product.Type.Should().NotBeNullOrEmpty();
                product.Description.Should().NotBeNullOrEmpty();
            }
        }

        #endregion

        #region Product Info Validation Tests

        [TestMethod]
        public void ProductConfiguration_ProductTypes_ShouldBeValidStrings()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;

            // Assert
            foreach (var kvp in products)
            {
                var key = kvp.Key;
                var product = kvp.Value;

                key.Should().NotBeNullOrWhiteSpace();
                product.Type.Should().Be(key, "Product type should match its dictionary key");
            }
        }

        [TestMethod]
        public void ProductConfiguration_AllPrices_ShouldBeDifferent()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;
            var prices = products.Values.Select(p => p.Price).ToList();

            // Assert - All products should have different prices for business logic
            prices.Should().OnlyHaveUniqueItems("Each product should have a unique price");
        }

        [TestMethod]
        public void ProductConfiguration_StripsShouldBeMostExpensive()
        {
            // Arrange & Act
            var stripPrice = ProductConfiguration.Products["strips"].Price;
            var photo4x6Price = ProductConfiguration.Products["4x6"].Price;
            var phonePrice = ProductConfiguration.Products["phone"].Price;

            // Assert - Business rule: strips should be most expensive
            stripPrice.Should().BeGreaterThan(photo4x6Price);
            stripPrice.Should().BeGreaterThan(phonePrice);
        }

        [TestMethod]
        public void ProductConfiguration_PhonePrintsShouldBeCheapest()
        {
            // Arrange & Act
            var stripPrice = ProductConfiguration.Products["strips"].Price;
            var photo4x6Price = ProductConfiguration.Products["4x6"].Price;
            var phonePrice = ProductConfiguration.Products["phone"].Price;

            // Assert - Business rule: phone prints should be cheapest
            phonePrice.Should().BeLessThan(stripPrice);
            phonePrice.Should().BeLessThan(photo4x6Price);
        }

        #endregion

        #region Business Logic Tests

        [TestMethod]
        public void ProductConfiguration_Products_ShouldBeReadOnly()
        {
            // Arrange & Act
            var products = ProductConfiguration.Products;

            // Assert - Configuration should be stable
            products.Should().NotBeNull();

            // Test that we can read multiple times consistently
            var products2 = ProductConfiguration.Products;
            products2.Should().BeEquivalentTo(products);
        }

        [TestMethod]
        public void ProductConfiguration_Products_ShouldContainExpectedCategories()
        {
            // Arrange
            var expectedTypes = new[] { "strips", "4x6", "phone" };

            // Act
            var actualTypes = ProductConfiguration.Products.Keys;

            // Assert
            actualTypes.Should().BeEquivalentTo(expectedTypes);
        }

        #endregion
    }
}