using System.Collections.Generic;
using System.Globalization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Photobooth;

namespace Photobooth.Tests
{
    [TestClass]
    [TestCategory("ViewModels")]
    public class ProductViewModelTests
    {
        private ProductViewModel _viewModel = null!;

        [TestInitialize]
        public void Setup()
        {
            _viewModel = new ProductViewModel
            {
                Name = "Test Product",
                Description = "Test Description",
                Icon = "🧪",
                IconBackground = "#FF0000",
                PriceLabel = "Price per Item",
                ProductKey = "TestProduct"
            };
        }

        #region Basic Property Tests

        [TestMethod]
        public void ProductViewModel_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var viewModel = new ProductViewModel();

            // Assert
            viewModel.Name.Should().Be(string.Empty);
            viewModel.Description.Should().Be(string.Empty);
            viewModel.Icon.Should().Be(string.Empty);
            viewModel.IconBackground.Should().Be(string.Empty);
            viewModel.PriceLabel.Should().Be(string.Empty);
            viewModel.ProductKey.Should().Be(string.Empty);
            viewModel.IsEnabled.Should().BeFalse();
            viewModel.Price.Should().Be(0);
            viewModel.HasUnsavedChanges.Should().BeFalse();
            viewModel.ValidationError.Should().BeNull();
            viewModel.HasValidationError.Should().BeFalse();
        }

        [TestMethod]
        public void ProductViewModel_SetIsEnabled_ShouldUpdateRelatedProperties()
        {
            // Arrange
            _viewModel.IsEnabled = false;

            // Act
            _viewModel.IsEnabled = true;

            // Assert
            _viewModel.IsEnabled.Should().BeTrue();
            _viewModel.HasUnsavedChanges.Should().BeTrue();
            _viewModel.CardOpacity.Should().Be(1.0);
            _viewModel.PriceSectionEnabled.Should().BeTrue();
        }

        [TestMethod]
        public void ProductViewModel_SetIsEnabledToFalse_ShouldUpdateOpacity()
        {
            // Arrange
            _viewModel.IsEnabled = true;

            // Act
            _viewModel.IsEnabled = false;

            // Assert
            _viewModel.IsEnabled.Should().BeFalse();
            _viewModel.CardOpacity.Should().Be(0.6);
            _viewModel.PriceSectionEnabled.Should().BeFalse();
        }

        [TestMethod]
        public void ProductViewModel_SetPrice_ShouldUpdatePriceText()
        {
            // Act
            _viewModel.Price = 15.75m;

            // Assert
            _viewModel.Price.Should().Be(15.75m);
            _viewModel.PriceText.Should().Be("15.75");
            _viewModel.HasUnsavedChanges.Should().BeTrue();
        }

        #endregion

        #region Culture-Aware Decimal Parsing Tests

        [TestMethod]
        public void PriceText_SetValidDecimal_ShouldParseCorrectlyWithInvariantCulture()
        {
            // Arrange
            var originalCulture = CultureInfo.CurrentCulture;
            
            try
            {
                // Set German culture (uses comma as decimal separator)
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                // Act - Use invariant culture format (dot as decimal separator)
                _viewModel.PriceText = "12.50";

                // Assert
                _viewModel.Price.Should().Be(12.50m);
                _viewModel.ValidationError.Should().BeNull();
                _viewModel.HasValidationError.Should().BeFalse();
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public void PriceText_SetGermanFormat_ShouldFailBecauseInvariantCultureRequired()
        {
            // Arrange
            var originalCulture = CultureInfo.CurrentCulture;
            
            try
            {
                // Set German culture (uses comma as decimal separator)
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                // Act - Try German format (comma as decimal separator)
                // This should fail because we explicitly use InvariantCulture which requires dot as decimal separator
                _viewModel.PriceText = "12,50";

                // Assert - Should fail because we enforce InvariantCulture parsing
                // Note: NumberStyles.Number with InvariantCulture should reject comma as decimal separator
                _viewModel.ValidationError.Should().NotBeNull("because German comma format should be rejected when using InvariantCulture");
                _viewModel.ValidationError.Should().Contain("Invalid price format");
                _viewModel.HasValidationError.Should().BeTrue();
                
                // Verify that the Price was not updated due to parsing failure
                _viewModel.Price.Should().Be(0, "because invalid format should not update the price");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public void PriceText_GetValue_ShouldUseInvariantCulture()
        {
            // Arrange
            var originalCulture = CultureInfo.CurrentCulture;
            
            try
            {
                // Set German culture
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                _viewModel.Price = 15.75m;

                // Act
                var priceText = _viewModel.PriceText;

                // Assert - Should use invariant culture format (dot)
                priceText.Should().Be("15.75");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        #endregion

        #region Zero Value Support Tests

        [TestMethod]
        public void PriceText_SetZero_ShouldAllowFreeProducts()
        {
            // Act
            _viewModel.PriceText = "0";

            // Assert
            _viewModel.Price.Should().Be(0);
            _viewModel.ValidationError.Should().BeNull();
            _viewModel.HasValidationError.Should().BeFalse();
        }

        [TestMethod]
        public void PriceText_SetZeroDecimal_ShouldAllowFreeProducts()
        {
            // Act
            _viewModel.PriceText = "0.00";

            // Assert
            _viewModel.Price.Should().Be(0);
            _viewModel.ValidationError.Should().BeNull();
            _viewModel.HasValidationError.Should().BeFalse();
        }

        [TestMethod]
        public void Price_SetZeroDirectly_ShouldBeAllowed()
        {
            // Act
            _viewModel.Price = 0;

            // Assert
            _viewModel.Price.Should().Be(0);
            _viewModel.PriceText.Should().Be("0.00");
            _viewModel.ValidationError.Should().BeNull();
        }

        #endregion

        #region Negative Value Validation Tests

        [TestMethod]
        public void PriceText_SetNegativeValue_ShouldRejectWithValidationError()
        {
            // Act
            _viewModel.PriceText = "-5.00";

            // Assert
            _viewModel.ValidationError.Should().NotBeNull();
            _viewModel.ValidationError.Should().Be("Price cannot be negative");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        [TestMethod]
        public void PriceText_SetNegativeInteger_ShouldRejectWithValidationError()
        {
            // Act
            _viewModel.PriceText = "-10";

            // Assert
            _viewModel.ValidationError.Should().NotBeNull();
            _viewModel.ValidationError.Should().Be("Price cannot be negative");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        #endregion

        #region Invalid Format Validation Tests

        [TestMethod]
        public void PriceText_SetInvalidText_ShouldRejectWithValidationError()
        {
            // Act
            _viewModel.PriceText = "abc";

            // Assert
            _viewModel.ValidationError.Should().NotBeNull();
            _viewModel.ValidationError.Should().Contain("Invalid price format");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        [TestMethod]
        public void PriceText_SetEmptyString_ShouldRejectWithValidationError()
        {
            // Act
            _viewModel.PriceText = "";

            // Assert
            _viewModel.ValidationError.Should().NotBeNull();
            _viewModel.ValidationError.Should().Contain("Invalid price format");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        [TestMethod]
        public void PriceText_SetSpecialCharacters_ShouldRejectWithValidationError()
        {
            // Act
            _viewModel.PriceText = "5$00";

            // Assert
            _viewModel.ValidationError.Should().NotBeNull();
            _viewModel.ValidationError.Should().Contain("Invalid price format");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        [TestMethod]
        public void PriceText_SetMultipleDecimalPoints_ShouldRejectWithValidationError()
        {
            // Act
            _viewModel.PriceText = "5.0.0";

            // Assert
            _viewModel.ValidationError.Should().NotBeNull();
            _viewModel.ValidationError.Should().Contain("Invalid price format");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        #endregion

        #region Validation Error Clearing Tests

        [TestMethod]
        public void PriceText_SetValidValueAfterError_ShouldClearValidationError()
        {
            // Arrange - Set invalid value to trigger error
            _viewModel.PriceText = "invalid";
            _viewModel.ValidationError.Should().NotBeNull();

            // Act - Set valid value
            _viewModel.PriceText = "10.00";

            // Assert
            _viewModel.Price.Should().Be(10.00m);
            _viewModel.ValidationError.Should().BeNull();
            _viewModel.HasValidationError.Should().BeFalse();
        }

        [TestMethod]
        public void ValidationError_SetDirectly_ShouldUpdateHasValidationError()
        {
            // Act
            _viewModel.ValidationError = "Custom error message";

            // Assert
            _viewModel.ValidationError.Should().Be("Custom error message");
            _viewModel.HasValidationError.Should().BeTrue();
        }

        [TestMethod]
        public void ValidationError_SetToNull_ShouldClearHasValidationError()
        {
            // Arrange
            _viewModel.ValidationError = "Some error";

            // Act
            _viewModel.ValidationError = null;

            // Assert
            _viewModel.ValidationError.Should().BeNull();
            _viewModel.HasValidationError.Should().BeFalse();
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void PriceText_SetLargeDecimal_ShouldParseCorrectly()
        {
            // Act
            _viewModel.PriceText = "99999.99";

            // Assert
            _viewModel.Price.Should().Be(99999.99m);
            _viewModel.ValidationError.Should().BeNull();
        }

        [TestMethod]
        public void PriceText_SetSmallDecimal_ShouldParseCorrectly()
        {
            // Act
            _viewModel.PriceText = "0.01";

            // Assert
            _viewModel.Price.Should().Be(0.01m);
            _viewModel.ValidationError.Should().BeNull();
        }

        [TestMethod]
        public void PriceText_SetIntegerValue_ShouldParseCorrectly()
        {
            // Act
            _viewModel.PriceText = "5";

            // Assert
            _viewModel.Price.Should().Be(5.00m);
            _viewModel.PriceText.Should().Be("5.00");
            _viewModel.ValidationError.Should().BeNull();
        }

        [TestMethod]
        public void PriceText_SetTrailingSpaces_ShouldParseCorrectly()
        {
            // Act
            _viewModel.PriceText = "10.50   ";

            // Assert
            _viewModel.Price.Should().Be(10.50m);
            _viewModel.ValidationError.Should().BeNull();
        }

        [TestMethod]
        public void PriceText_SetLeadingSpaces_ShouldParseCorrectly()
        {
            // Act
            _viewModel.PriceText = "   7.25";

            // Assert
            _viewModel.Price.Should().Be(7.25m);
            _viewModel.ValidationError.Should().BeNull();
        }

        #endregion

        #region Property Change Notification Tests

        [TestMethod]
        public void ProductViewModel_PropertyChanged_ShouldFireForRelatedProperties()
        {
            // Arrange
            var propertyChangeEvents = new List<string>();
            _viewModel.PropertyChanged += (sender, e) => propertyChangeEvents.Add(e.PropertyName ?? "");

            // Act
            _viewModel.Price = 15.00m;

            // Assert
            propertyChangeEvents.Should().Contain("Price");
            propertyChangeEvents.Should().Contain("PriceText");
        }

        [TestMethod]
        public void ProductViewModel_ValidationErrorPropertyChanged_ShouldFireForRelatedProperties()
        {
            // Arrange
            var propertyChangeEvents = new List<string>();
            _viewModel.PropertyChanged += (sender, e) => propertyChangeEvents.Add(e.PropertyName ?? "");

            // Act
            _viewModel.ValidationError = "Test error";

            // Assert
            propertyChangeEvents.Should().Contain("ValidationError");
            propertyChangeEvents.Should().Contain("HasValidationError");
        }

        #endregion

        #region Cross-Culture Consistency Tests

        [TestMethod]
        public void PriceText_SameValueAcrossMultipleCultures_ShouldBeConsistent()
        {
            // Arrange
            var testCultures = new[]
            {
                new CultureInfo("en-US"), // US: 1,234.56
                new CultureInfo("de-DE"), // German: 1.234,56
                new CultureInfo("fr-FR"), // French: 1 234,56
                new CultureInfo("ja-JP")  // Japanese: 1,234.56
            };
            
            var originalCulture = CultureInfo.CurrentCulture;
            var testPrice = 1234.56m;

            try
            {
                foreach (var culture in testCultures)
                {
                    // Arrange
                    CultureInfo.CurrentCulture = culture;
                    var viewModel = new ProductViewModel();

                    // Act - Set using invariant format
                    viewModel.PriceText = "1234.56";

                    // Assert - Should work regardless of culture
                    viewModel.Price.Should().Be(testPrice, $"Failed for culture: {culture.Name}");
                    viewModel.ValidationError.Should().BeNull($"Validation error for culture: {culture.Name}");
                    
                    // Get should also be consistent
                    viewModel.PriceText.Should().Be("1234.56", $"PriceText output inconsistent for culture: {culture.Name}");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        #endregion
    }
} 