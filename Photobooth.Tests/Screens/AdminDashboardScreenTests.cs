using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth;
using Photobooth.Services;
using Photobooth.Models;
using System.Threading.Tasks;

namespace Photobooth.Tests.Screens
{
    [TestClass]
    public class AdminDashboardScreenTests
    {
        private DatabaseService _databaseService = null!;

        [TestInitialize]
        public async Task Setup()
        {
            // Use temporary file database for testing instead of in-memory
            var tempPath = System.IO.Path.GetTempFileName();
            _databaseService = new DatabaseService(tempPath);
            await _databaseService.InitializeAsync();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up temp database files
            try
            {
                var tempFiles = System.IO.Directory.GetFiles(System.IO.Path.GetTempPath(), "tmp*.tmp");
                foreach (var file in tempFiles)
                {
                    try
                    {
                        if (file.Contains("tmp") && System.IO.File.Exists(file))
                        {
                            System.IO.File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [TestMethod]
        public void DatabaseService_Initialization_ShouldSucceed()
        {
            // Test that we can initialize the database service without UI
            // Act & Assert
            _databaseService.Should().NotBeNull();
        }

        [TestMethod]
        public void ValidatePasswordStrength_ValidPassword_ReturnsTrue()
        {
            // Test password validation logic that would be used in the dashboard
            // Arrange
            var validPasswords = new[]
            {
                "StrongPass123!",
                "SecurePassword1@",
                "MyP@ssw0rd"
            };

            foreach (var password in validPasswords)
            {
                // Act
                var isValid = IsPasswordValid(password);

                // Assert
                isValid.Should().BeTrue($"Password '{password}' should be valid");
            }
        }

        [TestMethod]
        public void ValidatePasswordStrength_WeakPassword_ReturnsFalse()
        {
            // Arrange
            var weakPasswords = new[]
            {
                "123",          // Too short
                "password",     // No numbers or symbols
                "PASSWORD",     // No lowercase or numbers
                "12345678",     // Only numbers
                ""              // Empty
            };

            foreach (var password in weakPasswords)
            {
                // Act
                var isValid = IsPasswordValid(password);

                // Assert
                isValid.Should().BeFalse($"Password '{password}' should be invalid");
            }
        }

        [TestMethod]
        public async Task BusinessInfoUpdate_ValidData_UpdatesSuccessfully()
        {
            // Test using custom settings categories that won't conflict with defaults
            // Arrange
            var businessName = "Updated Business Name";
            var location = "Updated Location";
            var showLogo = false;

            // Simulate business info update logic
            await _databaseService.SetSettingValueAsync("TestBusiness", "BusinessName", businessName);
            await _databaseService.SetSettingValueAsync("TestBusiness", "Location", location);
            await _databaseService.SetSettingValueAsync("TestBusiness", "ShowLogo", showLogo);

            // Act
            var savedBusinessName = await _databaseService.GetSettingValueAsync<string>("TestBusiness", "BusinessName");
            var savedLocation = await _databaseService.GetSettingValueAsync<string>("TestBusiness", "Location");
            var savedShowLogo = await _databaseService.GetSettingValueAsync<bool>("TestBusiness", "ShowLogo");

            // Assert
            savedBusinessName.Data.Should().Be(businessName);
            savedLocation.Data.Should().Be(location);
            savedShowLogo.Data.Should().Be(showLogo);
        }

        [TestMethod]
        public async Task PricingUpdate_ValidPrices_UpdatesCorrectly()
        {
            // Test using the actual "Pricing" category that exists in the application
            // Arrange
            var stripPrice = "4.00";
            var photo4x6Price = "6.00";
            var smartphonePrice = "3.00";

            // Act
            await _databaseService.SetSettingValueAsync("Pricing", "StripPrice", stripPrice);
            await _databaseService.SetSettingValueAsync("Pricing", "Photo4x6Price", photo4x6Price);
            await _databaseService.SetSettingValueAsync("Pricing", "SmartphonePrice", smartphonePrice);

            // Assert
            var savedStripPrice = await _databaseService.GetSettingValueAsync<string>("Pricing", "StripPrice");
            var savedPhoto4x6Price = await _databaseService.GetSettingValueAsync<string>("Pricing", "Photo4x6Price");
            var savedSmartphonePrice = await _databaseService.GetSettingValueAsync<string>("Pricing", "SmartphonePrice");

            savedStripPrice.Data.Should().Be(stripPrice);
            savedPhoto4x6Price.Data.Should().Be(photo4x6Price);
            savedSmartphonePrice.Data.Should().Be(smartphonePrice);
        }

        [TestMethod]
        public async Task SystemSettings_UpdateVolume_SavesCorrectly()
        {
            // Test using the actual "System" category and "Volume" key
            // Arrange
            var volume = "85";

            // Act
            await _databaseService.SetSettingValueAsync("System", "Volume", volume);

            // Assert
            var savedVolume = await _databaseService.GetSettingValueAsync<string>("System", "Volume");
            savedVolume.Data.Should().Be(volume);
        }

        [TestMethod]
        public async Task OperationMode_SwitchToFreePlay_UpdatesCorrectly()
        {
            // Test using the actual "System" category and "Mode" key
            // Arrange
            var operationMode = "Free";

            // Act
            await _databaseService.SetSettingValueAsync("System", "Mode", operationMode);

            // Assert
            var savedMode = await _databaseService.GetSettingValueAsync<string>("System", "Mode");
            savedMode.Data.Should().Be(operationMode);
        }

        [TestMethod]
        public async Task ToggleSettings_UpdatesCorrectly()
        {
            // Test using the actual system settings that exist
            // Arrange
            var settings = new[]
            {
                ("LightsEnabled", "false"),
                ("MaintenanceMode", "true"),
                ("RFIDEnabled", "false"),
                ("AutoTemplates", "true")
            };

            // Act
            foreach (var (key, value) in settings)
            {
                await _databaseService.SetSettingValueAsync("System", key, value);
            }

            // Assert
            foreach (var (key, expectedValue) in settings)
            {
                var savedValue = await _databaseService.GetSettingValueAsync<string>("System", key);
                savedValue.Data.Should().Be(expectedValue, $"Setting {key} should be {expectedValue}");
            }
        }

        [TestMethod]
        public async Task UserManagement_CreateUser_Success()
        {
            // Arrange
            var username = "newtestuser";
            var password = "TestPass123!";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "New Test User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };

            // Act
            var result = await _databaseService.CreateAdminUserAsync(user, password);

            // Assert
            result.Success.Should().BeTrue();

            // Verify user was created
            var authResult = await _databaseService.AuthenticateAsync(username, password);
            authResult.Success.Should().BeTrue();
            authResult.Data.Should().NotBeNull();
            authResult.Data!.Username.Should().Be(username);
            authResult.Data.DisplayName.Should().Be("New Test User");
            authResult.Data.AccessLevel.Should().Be(AdminAccessLevel.User);
        }

        [TestMethod]
        public async Task UserManagement_DeleteUser_Success()
        {
            // Arrange
            var username = "usertodelete";
            var password = "DeletePass123!";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "User To Delete",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };
            
            // Create user first
            await _databaseService.CreateAdminUserAsync(user, password);

            // Act
            var result = await _databaseService.DeleteAdminUserAsync(user.UserId);

            // Assert
            result.Success.Should().BeTrue();

            // Verify user was deleted
            var authResult = await _databaseService.AuthenticateAsync(username, password);
            authResult.Success.Should().BeTrue();
            authResult.Data.Should().BeNull();
        }

        [TestMethod]
        public void AccessLevelValidation_AdminUser_HasAllPermissions()
        {
            // Arrange
            var accessLevel = AdminAccessLevel.Master;

            // Act & Assert
            HasPermissionToManageUsers(accessLevel).Should().BeTrue();
            HasPermissionToChangeSettings(accessLevel).Should().BeTrue();
            HasPermissionToViewReports(accessLevel).Should().BeTrue();
        }

        [TestMethod]
        public void AccessLevelValidation_RegularUser_HasLimitedPermissions()
        {
            // Arrange
            var accessLevel = AdminAccessLevel.User;

            // Act & Assert
            HasPermissionToManageUsers(accessLevel).Should().BeFalse();
            HasPermissionToChangeSettings(accessLevel).Should().BeFalse();
            HasPermissionToViewReports(accessLevel).Should().BeTrue(); // Users can view reports
        }

        // Helper methods that would typically be in the actual dashboard logic
        private bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
                return false;

            bool hasLower = false, hasUpper = false, hasDigit = false, hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            return hasLower && (hasUpper || hasDigit || hasSpecial);
        }

        private bool HasPermissionToManageUsers(AdminAccessLevel accessLevel)
        {
            return accessLevel == AdminAccessLevel.Master;
        }

        private bool HasPermissionToChangeSettings(AdminAccessLevel accessLevel)
        {
            return accessLevel == AdminAccessLevel.Master;
        }

        private bool HasPermissionToViewReports(AdminAccessLevel accessLevel)
        {
            return accessLevel == AdminAccessLevel.Master || accessLevel == AdminAccessLevel.User;
        }
    }
} 