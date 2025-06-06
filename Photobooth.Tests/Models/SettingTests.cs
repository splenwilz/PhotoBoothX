using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Models;
using System;

namespace Photobooth.Tests.Models
{
    [TestClass]
    public class SettingTests
    {
        [TestMethod]
        public void Setting_DefaultConstructor_InitializesCorrectly()
        {
            // Act
            var setting = new Setting();

            // Assert
            setting.Should().NotBeNull();
            setting.Id.Should().NotBeNullOrEmpty();
            setting.Category.Should().BeEmpty();
            setting.Key.Should().BeEmpty();
            setting.Value.Should().BeEmpty();
            setting.DataType.Should().Be("String");
            setting.Description.Should().BeNull();
            setting.IsUserEditable.Should().BeTrue();
            setting.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            setting.UpdatedBy.Should().BeNull();
        }

        [TestMethod]
        public void Setting_SetAllProperties_UpdatesCorrectly()
        {
            // Arrange
            var setting = new Setting();
            var settingId = Guid.NewGuid().ToString();
            var category = "System";
            var key = "Volume";
            var value = "75";
            var dataType = "Integer";
            var description = "System volume level";
            var isUserEditable = false;
            var updatedAt = DateTime.Now.AddMinutes(30);
            var updatedBy = "admin";

            // Act
            setting.Id = settingId;
            setting.Category = category;
            setting.Key = key;
            setting.Value = value;
            setting.DataType = dataType;
            setting.Description = description;
            setting.IsUserEditable = isUserEditable;
            setting.UpdatedAt = updatedAt;
            setting.UpdatedBy = updatedBy;

            // Assert
            setting.Id.Should().Be(settingId);
            setting.Category.Should().Be(category);
            setting.Key.Should().Be(key);
            setting.Value.Should().Be(value);
            setting.DataType.Should().Be(dataType);
            setting.Description.Should().Be(description);
            setting.IsUserEditable.Should().Be(isUserEditable);
            setting.UpdatedAt.Should().Be(updatedAt);
            setting.UpdatedBy.Should().Be(updatedBy);
        }

        [TestMethod]
        public void Setting_SystemCategorySettings_ValidConfigurations()
        {
            // Test various system settings
            var systemSettings = new[]
            {
                new Setting { Category = "System", Key = "Volume", Value = "75", DataType = "Integer" },
                new Setting { Category = "System", Key = "LightsEnabled", Value = "true", DataType = "Boolean" },
                new Setting { Category = "System", Key = "Mode", Value = "Coin", DataType = "String" },
                new Setting { Category = "System", Key = "MaintenanceMode", Value = "false", DataType = "Boolean" },
                new Setting { Category = "System", Key = "Language", Value = "en-US", DataType = "String" }
            };

            // Assert
            foreach (var setting in systemSettings)
            {
                setting.Category.Should().Be("System");
                setting.Key.Should().NotBeNullOrEmpty();
                setting.Value.Should().NotBeNullOrEmpty();
                setting.DataType.Should().NotBeNullOrEmpty();
            }

            systemSettings[0].Key.Should().Be("Volume");
            systemSettings[0].DataType.Should().Be("Integer");
            systemSettings[1].Value.Should().Be("true");
            systemSettings[1].DataType.Should().Be("Boolean");
            systemSettings[2].Value.Should().Be("Coin");
            systemSettings[3].Value.Should().Be("false");
            systemSettings[4].Value.Should().Be("en-US");
        }

        [TestMethod]
        public void Setting_PricingCategorySettings_ValidConfigurations()
        {
            // Test pricing-related settings
            var pricingSettings = new[]
            {
                new Setting { Category = "Pricing", Key = "StripPrice", Value = "5.00", DataType = "Decimal" },
                new Setting { Category = "Pricing", Key = "Photo4x6Price", Value = "3.00", DataType = "Decimal" },
                new Setting { Category = "Pricing", Key = "SmartphonePrice", Value = "2.00", DataType = "Decimal" },
                new Setting { Category = "Pricing", Key = "Currency", Value = "USD", DataType = "String" },
                new Setting { Category = "Pricing", Key = "TaxRate", Value = "0.08", DataType = "Decimal" }
            };

            // Assert
            foreach (var setting in pricingSettings)
            {
                setting.Category.Should().Be("Pricing");
                setting.Key.Should().NotBeNullOrEmpty();
                setting.Value.Should().NotBeNullOrEmpty();
                setting.DataType.Should().NotBeNullOrEmpty();
            }

            // Verify specific price values can be parsed
            decimal.Parse(pricingSettings[0].Value!).Should().Be(5.00m);
            decimal.Parse(pricingSettings[1].Value!).Should().Be(3.00m);
            decimal.Parse(pricingSettings[2].Value!).Should().Be(2.00m);
            decimal.Parse(pricingSettings[4].Value!).Should().Be(0.08m);
        }

        [TestMethod]
        public void Setting_PaymentCategorySettings_ValidConfigurations()
        {
            // Test payment-related settings
            var paymentSettings = new[]
            {
                new Setting { Category = "Payment", Key = "AcceptCash", Value = "true", DataType = "Boolean" },
                new Setting { Category = "Payment", Key = "AcceptCard", Value = "true", DataType = "Boolean" },
                new Setting { Category = "Payment", Key = "AcceptContactless", Value = "false", DataType = "Boolean" },
                new Setting { Category = "Payment", Key = "CoinSlots", Value = "4", DataType = "Integer" },
                new Setting { Category = "Payment", Key = "BillAcceptor", Value = "enabled", DataType = "String" }
            };

            // Assert
            foreach (var setting in paymentSettings)
            {
                setting.Category.Should().Be("Payment");
                setting.Key.Should().NotBeNullOrEmpty();
                setting.Value.Should().NotBeNullOrEmpty();
                setting.DataType.Should().NotBeNullOrEmpty();
            }

            bool.Parse(paymentSettings[0].Value!).Should().BeTrue();
            bool.Parse(paymentSettings[1].Value!).Should().BeTrue();
            bool.Parse(paymentSettings[2].Value!).Should().BeFalse();
            int.Parse(paymentSettings[3].Value!).Should().Be(4);
        }

        [TestMethod]
        public void Setting_RFIDCategorySettings_ValidConfigurations()
        {
            // Test RFID-related settings
            var rfidSettings = new[]
            {
                new Setting { Category = "RFID", Key = "Enabled", Value = "false", DataType = "Boolean" },
                new Setting { Category = "RFID", Key = "ReaderPort", Value = "COM3", DataType = "String" },
                new Setting { Category = "RFID", Key = "Frequency", Value = "13.56", DataType = "Decimal" },
                new Setting { Category = "RFID", Key = "Timeout", Value = "30", DataType = "Integer" },
                new Setting { Category = "RFID", Key = "AutoRead", Value = "true", DataType = "Boolean" }
            };

            // Assert
            foreach (var setting in rfidSettings)
            {
                setting.Category.Should().Be("RFID");
                setting.Key.Should().NotBeNullOrEmpty();
                setting.Value.Should().NotBeNullOrEmpty();
                setting.DataType.Should().NotBeNullOrEmpty();
            }

            rfidSettings[1].Value.Should().StartWith("COM");
            double.Parse(rfidSettings[2].Value!).Should().Be(13.56);
            int.Parse(rfidSettings[3].Value!).Should().Be(30);
        }

        [TestMethod]
        public void Setting_SeasonalCategorySettings_ValidConfigurations()
        {
            // Test seasonal/event-related settings
            var seasonalSettings = new[]
            {
                new Setting { Category = "Seasonal", Key = "ChristmasMode", Value = "false", DataType = "Boolean" },
                new Setting { Category = "Seasonal", Key = "HalloweenTheme", Value = "false", DataType = "Boolean" },
                new Setting { Category = "Seasonal", Key = "WeddingMode", Value = "true", DataType = "Boolean" },
                new Setting { Category = "Seasonal", Key = "EventName", Value = "Smith Wedding 2024", DataType = "String" },
                new Setting { Category = "Seasonal", Key = "ThemeColor", Value = "#FF6B9D", DataType = "String" }
            };

            // Assert
            foreach (var setting in seasonalSettings)
            {
                setting.Category.Should().Be("Seasonal");
                setting.Key.Should().NotBeNullOrEmpty();
                setting.Value.Should().NotBeNullOrEmpty();
                setting.DataType.Should().NotBeNullOrEmpty();
            }

            seasonalSettings[3].Value.Should().Contain("Wedding");
            seasonalSettings[4].Value.Should().StartWith("#");
        }

        [TestMethod]
        public void Setting_ValueTypes_HandleDifferentDataTypes()
        {
            // Test settings with different value types
            var settings = new[]
            {
                new Setting { Key = "StringValue", Value = "Hello World", DataType = "String" },
                new Setting { Key = "IntegerValue", Value = "42", DataType = "Integer" },
                new Setting { Key = "BooleanValue", Value = "true", DataType = "Boolean" },
                new Setting { Key = "DecimalValue", Value = "3.14159", DataType = "Decimal" },
                new Setting { Key = "JsonValue", Value = "{\"key\":\"value\",\"number\":123}", DataType = "Json" },
                new Setting { Key = "UrlValue", Value = "https://example.com/api/endpoint", DataType = "Url" },
                new Setting { Key = "PathValue", Value = "/path/to/file.txt", DataType = "Path" }
            };

            // Assert - Values can be parsed to expected types
            settings[0].Value.Should().Be("Hello World");
            settings[0].DataType.Should().Be("String");
            int.Parse(settings[1].Value!).Should().Be(42);
            settings[1].DataType.Should().Be("Integer");
            bool.Parse(settings[2].Value!).Should().BeTrue();
            settings[2].DataType.Should().Be("Boolean");
            double.Parse(settings[3].Value!).Should().BeApproximately(3.14159, 0.00001);
            settings[3].DataType.Should().Be("Decimal");
            settings[4].Value.Should().Contain("key");
            settings[4].DataType.Should().Be("Json");
            settings[5].Value.Should().StartWith("https://");
            settings[5].DataType.Should().Be("Url");
            settings[6].Value.Should().StartWith("/path/");
            settings[6].DataType.Should().Be("Path");
        }

        [TestMethod]
        public void Setting_UpdatedAtTimeTracking()
        {
            // Arrange
            var setting = new Setting
            {
                Category = "Test",
                Key = "TimeTest",
                Value = "InitialValue"
            };

            var initialTime = setting.UpdatedAt;

            // Act - Simulate an update
            System.Threading.Thread.Sleep(100); // Small delay to ensure different time
            setting.Value = "UpdatedValue";
            setting.UpdatedAt = DateTime.Now;

            // Assert
            setting.UpdatedAt.Should().BeAfter(initialTime);
            setting.Value.Should().Be("UpdatedValue");
        }

        [TestMethod]
        public void Setting_UpdatedByCanBeNull_ForNewSettings()
        {
            // Arrange & Act
            var setting = new Setting
            {
                Category = "Test",
                Key = "NewSetting",
                Value = "NewValue",
                UpdatedBy = null
            };

            // Assert
            setting.UpdatedBy.Should().BeNull();
            setting.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
        }

        [TestMethod]
        public void Setting_EmptyAndNullValues_HandleCorrectly()
        {
            // Test empty and null scenarios
            var emptyValueSetting = new Setting { Category = "Test", Key = "EmptyKey", Value = "" };
            var nullDescriptionSetting = new Setting { Category = "Test", Key = "NullKey", Value = "Value", Description = null };

            // Assert
            emptyValueSetting.Value.Should().Be("");
            nullDescriptionSetting.Description.Should().BeNull();
        }

        [TestMethod]
        public void Setting_LongValues_HandleCorrectly()
        {
            // Arrange
            var longValue = new string('X', 10000); // 10KB string
            var setting = new Setting
            {
                Category = "Test",
                Key = "LongValue",
                Value = longValue
            };

            // Assert
            setting.Value.Should().Be(longValue);
            setting.Value!.Length.Should().Be(10000);
        }

        [TestMethod]
        public void Setting_SpecialCharacters_HandleCorrectly()
        {
            // Test settings with special characters
            var specialSettings = new[]
            {
                new Setting { Key = "UnicodeValue", Value = "Hello 世界 🌍", DataType = "String" },
                new Setting { Key = "XmlValue", Value = "<config><item value=\"test\"/></config>", DataType = "Xml" },
                new Setting { Key = "SqlValue", Value = "SELECT * FROM Users WHERE Name = 'O''Brien'", DataType = "String" },
                new Setting { Key = "RegexValue", Value = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", DataType = "Regex" },
                new Setting { Key = "PathValue", Value = @"C:\Program Files\PhotoBooth\config.json", DataType = "Path" }
            };

            // Assert
            specialSettings[0].Value.Should().Contain("世界");
            specialSettings[0].Value.Should().Contain("🌍");
            specialSettings[1].Value.Should().StartWith("<config>");
            specialSettings[2].Value.Should().Contain("O''Brien");
            specialSettings[3].Value.Should().Contain(@"[a-zA-Z");
            specialSettings[4].Value.Should().Contain(@"C:\Program Files");
        }

        [TestMethod]
        public void Setting_CategoryKeyPairs_UniqueIdentifiers()
        {
            // Test that Category + Key pairs work as expected identifiers
            var settings = new[]
            {
                new Setting { Category = "System", Key = "Volume", Value = "75", DataType = "Integer" },
                new Setting { Category = "System", Key = "Brightness", Value = "80", DataType = "Integer" },
                new Setting { Category = "Pricing", Key = "Volume", Value = "5.00", DataType = "Decimal" }, // Same key, different category
                new Setting { Category = "Audio", Key = "Volume", Value = "90", DataType = "Integer" } // Same key, different category again
            };

            // Assert - Same key can exist in different categories
            settings[0].Category.Should().Be("System");
            settings[0].Key.Should().Be("Volume");
            settings[0].Value.Should().Be("75");
            settings[0].DataType.Should().Be("Integer");

            settings[2].Category.Should().Be("Pricing");
            settings[2].Key.Should().Be("Volume");
            settings[2].Value.Should().Be("5.00");
            settings[2].DataType.Should().Be("Decimal");

            settings[3].Category.Should().Be("Audio");
            settings[3].Key.Should().Be("Volume");
            settings[3].Value.Should().Be("90");
            settings[3].DataType.Should().Be("Integer");

            // All have the same key but different categories and values
            settings.Where(s => s.Key == "Volume" || s.Key == "Brightness").Should().HaveCount(4);
            settings.Select(s => s.Category).Distinct().Count().Should().Be(3);
        }

        [TestMethod]
        public void Setting_IdFormats_GuidAndCustom()
        {
            // Test different ID formats
            var guidId = Guid.NewGuid().ToString();
            var customId = "SETTING_001";

            var guidSetting = new Setting { Id = guidId };
            var customSetting = new Setting { Id = customId };

            // Assert
            guidSetting.Id.Should().Be(guidId);
            Guid.TryParse(guidSetting.Id, out _).Should().BeTrue();

            customSetting.Id.Should().Be(customId);
            customSetting.Id.Should().StartWith("SETTING_");
        }

        [TestMethod]
        public void Setting_IsUserEditable_DefaultsToTrue()
        {
            // Arrange & Act
            var setting = new Setting();

            // Assert
            setting.IsUserEditable.Should().BeTrue();
        }

        [TestMethod]
        public void Setting_DataType_DefaultsToString()
        {
            // Arrange & Act
            var setting = new Setting();

            // Assert
            setting.DataType.Should().Be("String");
        }
    }
} 