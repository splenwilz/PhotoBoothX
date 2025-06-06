using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Models;
using System;

namespace Photobooth.Tests.Models
{
    [TestClass]
    public class BusinessInfoTests
    {
        [TestMethod]
        public void BusinessInfo_DefaultConstructor_InitializesCorrectly()
        {
            // Act
            var businessInfo = new BusinessInfo();

            // Assert
            businessInfo.Should().NotBeNull();
            businessInfo.Id.Should().NotBeNullOrEmpty();
            businessInfo.BusinessName.Should().BeEmpty();
            businessInfo.Address.Should().BeNull();
            businessInfo.LogoPath.Should().BeNull();
            businessInfo.ShowLogoOnPrints.Should().BeTrue();
            businessInfo.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            businessInfo.UpdatedBy.Should().BeNull();
        }

        [TestMethod]
        public void BusinessInfo_SetAllProperties_UpdatesCorrectly()
        {
            // Arrange
            var businessInfo = new BusinessInfo();
            var businessId = Guid.NewGuid().ToString();
            var businessName = "PhotoBooth Pro LLC";
            var address = "123 Main Street, City, State 12345";
            var logoPath = "/assets/logos/company-logo.png";
            var showLogoOnPrints = false;
            var updatedAt = DateTime.Now.AddMinutes(30);
            var updatedBy = "admin";

            // Act
            businessInfo.Id = businessId;
            businessInfo.BusinessName = businessName;
            businessInfo.Address = address;
            businessInfo.LogoPath = logoPath;
            businessInfo.ShowLogoOnPrints = showLogoOnPrints;
            businessInfo.UpdatedAt = updatedAt;
            businessInfo.UpdatedBy = updatedBy;

            // Assert
            businessInfo.Id.Should().Be(businessId);
            businessInfo.BusinessName.Should().Be(businessName);
            businessInfo.Address.Should().Be(address);
            businessInfo.LogoPath.Should().Be(logoPath);
            businessInfo.ShowLogoOnPrints.Should().Be(showLogoOnPrints);
            businessInfo.UpdatedAt.Should().Be(updatedAt);
            businessInfo.UpdatedBy.Should().Be(updatedBy);
        }

        [TestMethod]
        public void BusinessInfo_CompleteBusinessConfiguration_ValidSetup()
        {
            // Arrange & Act
            var businessInfo = new BusinessInfo
            {
                Id = Guid.NewGuid().ToString(),
                BusinessName = "ABC Photo Events",
                Address = "456 Wedding Lane, Celebration City, CA 90210",
                LogoPath = "/logos/abc-photo-events.svg",
                ShowLogoOnPrints = true,
                UpdatedAt = DateTime.Now,
                UpdatedBy = "setup"
            };

            // Assert
            businessInfo.Id.Should().NotBeNullOrEmpty();
            businessInfo.BusinessName.Should().Be("ABC Photo Events");
            businessInfo.Address.Should().Contain("Wedding Lane");
            businessInfo.LogoPath.Should().StartWith("/logos/");
            businessInfo.ShowLogoOnPrints.Should().BeTrue();
            businessInfo.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            businessInfo.UpdatedBy.Should().Be("setup");
        }

        [TestMethod]
        public void BusinessInfo_LogoDisabledConfiguration()
        {
            // Arrange & Act
            var businessInfo = new BusinessInfo
            {
                Id = Guid.NewGuid().ToString(),
                BusinessName = "Simple Photo Services",
                Address = "Basic Business Address",
                LogoPath = null,
                ShowLogoOnPrints = false,
                UpdatedAt = DateTime.Now.AddYears(-1)
            };

            // Assert
            businessInfo.ShowLogoOnPrints.Should().BeFalse();
            businessInfo.BusinessName.Should().Be("Simple Photo Services");
            businessInfo.LogoPath.Should().BeNull();
            businessInfo.UpdatedAt.Should().BeCloseTo(DateTime.Now.AddYears(-1), TimeSpan.FromDays(30));
        }

        [TestMethod]
        public void BusinessInfo_LogoPath_VariousFormats()
        {
            // Test various logo path formats
            var logoFormats = new[]
            {
                "/assets/logos/company.png",
                "C:\\Images\\logo.jpg",
                "https://cdn.example.com/logo.svg",
                "../assets/company-brand.gif",
                "./logos/brand.webp"
            };

            foreach (var logoPath in logoFormats)
            {
                // Act
                var businessInfo = new BusinessInfo { LogoPath = logoPath };

                // Assert
                businessInfo.LogoPath.Should().Be(logoPath);
                businessInfo.LogoPath.Should().NotBeNullOrEmpty();
            }
        }

        [TestMethod]
        public void BusinessInfo_AddressFormats_VariousValid()
        {
            // Test various address formats
            var addresses = new[]
            {
                "123 Main St, City, State 12345",
                "456 Wedding Avenue\nSuite 200\nEventCity, CA 90210",
                "789 Photo Boulevard, Unit B",
                "PO Box 123, Small Town, TX 75001",
                "1000 Corporate Drive\nFloor 5\nBusiness District, NY 10001"
            };

            foreach (var address in addresses)
            {
                // Act
                var businessInfo = new BusinessInfo { Address = address };

                // Assert
                businessInfo.Address.Should().Be(address);
                businessInfo.Address.Should().NotBeNullOrEmpty();
            }
        }

        [TestMethod]
        public void BusinessInfo_BusinessIdFormats_GuidAndCustom()
        {
            // Test GUID format
            var guidId = Guid.NewGuid().ToString();
            var customId = "BUSINESS_001";

            // Act
            var businessWithGuid = new BusinessInfo { Id = guidId };
            var businessWithCustomId = new BusinessInfo { Id = customId };

            // Assert
            businessWithGuid.Id.Should().Be(guidId);
            Guid.TryParse(businessWithGuid.Id, out _).Should().BeTrue();

            businessWithCustomId.Id.Should().Be(customId);
            businessWithCustomId.Id.Should().StartWith("BUSINESS_");
        }

        [TestMethod]
        public void BusinessInfo_UpdatedAtDates_PastPresentFuture()
        {
            // Test various date scenarios
            var pastDate = DateTime.Now.AddYears(-3);
            var presentDate = DateTime.Now;
            var futureDate = DateTime.Now.AddMonths(6); // Future planned update

            // Act
            var oldBusiness = new BusinessInfo { UpdatedAt = pastDate };
            var newBusiness = new BusinessInfo { UpdatedAt = presentDate };
            var plannedBusiness = new BusinessInfo { UpdatedAt = futureDate };

            // Assert
            oldBusiness.UpdatedAt.Should().BeBefore(DateTime.Now);
            newBusiness.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            plannedBusiness.UpdatedAt.Should().BeAfter(DateTime.Now);
        }

        [TestMethod]
        public void BusinessInfo_EmptyStringProperties_HandleCorrectly()
        {
            // Arrange & Act
            var businessInfo = new BusinessInfo
            {
                Id = "",
                BusinessName = "",
                Address = "",
                LogoPath = ""
            };

            // Assert
            businessInfo.Id.Should().Be("");
            businessInfo.BusinessName.Should().Be("");
            businessInfo.Address.Should().Be("");
            businessInfo.LogoPath.Should().Be("");
        }

        [TestMethod]
        public void BusinessInfo_NullProperties_HandleCorrectly()
        {
            // Arrange & Act
            var businessInfo = new BusinessInfo
            {
                Id = null!,
                BusinessName = null!,
                Address = null,
                LogoPath = null,
                UpdatedBy = null
            };

            // Assert
            businessInfo.Id.Should().BeNull();
            businessInfo.BusinessName.Should().BeNull();
            businessInfo.Address.Should().BeNull();
            businessInfo.LogoPath.Should().BeNull();
            businessInfo.UpdatedBy.Should().BeNull();
        }

        [TestMethod]
        public void BusinessInfo_LongStringProperties_HandleCorrectly()
        {
            // Arrange
            var longBusinessName = new string('B', 300);
            var longAddress = new string('A', 500);
            var longLogoPath = "/very/long/path/to/logo/" + new string('x', 200) + ".png";

            // Act
            var businessInfo = new BusinessInfo
            {
                BusinessName = longBusinessName,
                Address = longAddress,
                LogoPath = longLogoPath
            };

            // Assert
            businessInfo.BusinessName.Should().Be(longBusinessName);
            businessInfo.BusinessName.Length.Should().Be(300);
            businessInfo.Address.Should().Be(longAddress);
            businessInfo.Address.Length.Should().Be(500);
            businessInfo.LogoPath.Should().Be(longLogoPath);
            businessInfo.LogoPath.Should().Contain("/very/long/path/");
        }

        [TestMethod]
        public void BusinessInfo_ShowLogoOnPrints_Toggle_WorksCorrectly()
        {
            // Arrange
            var businessInfo = new BusinessInfo { ShowLogoOnPrints = false };

            // Act & Assert - Toggle show logo state
            businessInfo.ShowLogoOnPrints.Should().BeFalse();

            businessInfo.ShowLogoOnPrints = true;
            businessInfo.ShowLogoOnPrints.Should().BeTrue();

            businessInfo.ShowLogoOnPrints = false;
            businessInfo.ShowLogoOnPrints.Should().BeFalse();
        }

        [TestMethod]
        public void BusinessInfo_BusinessScenarios_MultipleConfigurations()
        {
            // Test multiple business scenarios
            var scenarios = new[]
            {
                new BusinessInfo
                {
                    BusinessName = "Wedding Photo Specialists",
                    LogoPath = "/logos/wedding-specialists.png",
                    ShowLogoOnPrints = true
                },
                new BusinessInfo
                {
                    BusinessName = "Corporate Event Photography",
                    LogoPath = "/logos/corporate-events.svg",
                    ShowLogoOnPrints = true
                },
                new BusinessInfo
                {
                    BusinessName = "Party Photo Booth Rentals",
                    LogoPath = null,
                    ShowLogoOnPrints = false
                }
            };

            // Assert each scenario
            scenarios[0].BusinessName.Should().Contain("Wedding");
            scenarios[0].LogoPath.Should().EndWith(".png");
            scenarios[0].ShowLogoOnPrints.Should().BeTrue();

            scenarios[1].BusinessName.Should().Contain("Corporate");
            scenarios[1].LogoPath.Should().EndWith(".svg");
            scenarios[1].ShowLogoOnPrints.Should().BeTrue();

            scenarios[2].BusinessName.Should().Contain("Party");
            scenarios[2].LogoPath.Should().BeNull();
            scenarios[2].ShowLogoOnPrints.Should().BeFalse();
        }

        [TestMethod]
        public void BusinessInfo_UpdatedBy_TrackingCorrectly()
        {
            // Arrange & Act
            var businessInfo = new BusinessInfo
            {
                BusinessName = "Test Business",
                UpdatedBy = "admin_user"
            };

            // Assert
            businessInfo.UpdatedBy.Should().Be("admin_user");

            // Act - Update by different user
            businessInfo.BusinessName = "Updated Business Name";
            businessInfo.UpdatedBy = "different_admin";

            // Assert
            businessInfo.UpdatedBy.Should().Be("different_admin");
        }

        [TestMethod]
        public void BusinessInfo_DefaultShowLogoOnPrints_IsTrue()
        {
            // Arrange & Act
            var businessInfo = new BusinessInfo();

            // Assert
            businessInfo.ShowLogoOnPrints.Should().BeTrue();
        }
    }
} 