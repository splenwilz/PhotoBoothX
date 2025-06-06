using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Models;
using System;

namespace Photobooth.Tests.Models
{
    [TestClass]
    public class AdminUserTests
    {
        [TestMethod]
        public void AdminUser_DefaultConstructor_InitializesCorrectly()
        {
            // Act
            var adminUser = new AdminUser();

            // Assert
            adminUser.Should().NotBeNull();
            adminUser.UserId.Should().NotBeNullOrEmpty();
            adminUser.Username.Should().BeEmpty();
            adminUser.DisplayName.Should().BeEmpty();
            adminUser.PasswordHash.Should().BeEmpty();
            adminUser.AccessLevel.Should().Be(AdminAccessLevel.User); // Default is User, not None
            adminUser.IsActive.Should().BeTrue(); // Default is true
            adminUser.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            adminUser.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            adminUser.LastLoginAt.Should().BeNull();
            adminUser.CreatedBy.Should().BeNull();
            adminUser.UpdatedBy.Should().BeNull();
        }

        [TestMethod]
        public void AdminUser_SetProperties_UpdatesCorrectly()
        {
            // Arrange
            var adminUser = new AdminUser();
            var userId = Guid.NewGuid().ToString();
            var username = "testuser";
            var displayName = "Test User";
            var passwordHash = "hashedpassword123";
            var accessLevel = AdminAccessLevel.Master;
            var isActive = true;
            var createdAt = DateTime.Now;
            var updatedAt = DateTime.Now.AddMinutes(5);
            var lastLoginAt = DateTime.Now.AddMinutes(-30);
            var createdBy = "admin";
            var updatedBy = "admin2";

            // Act
            adminUser.UserId = userId;
            adminUser.Username = username;
            adminUser.DisplayName = displayName;
            adminUser.PasswordHash = passwordHash;
            adminUser.AccessLevel = accessLevel;
            adminUser.IsActive = isActive;
            adminUser.CreatedAt = createdAt;
            adminUser.UpdatedAt = updatedAt;
            adminUser.LastLoginAt = lastLoginAt;
            adminUser.CreatedBy = createdBy;
            adminUser.UpdatedBy = updatedBy;

            // Assert
            adminUser.UserId.Should().Be(userId);
            adminUser.Username.Should().Be(username);
            adminUser.DisplayName.Should().Be(displayName);
            adminUser.PasswordHash.Should().Be(passwordHash);
            adminUser.AccessLevel.Should().Be(accessLevel);
            adminUser.IsActive.Should().Be(isActive);
            adminUser.CreatedAt.Should().Be(createdAt);
            adminUser.UpdatedAt.Should().Be(updatedAt);
            adminUser.LastLoginAt.Should().Be(lastLoginAt);
            adminUser.CreatedBy.Should().Be(createdBy);
            adminUser.UpdatedBy.Should().Be(updatedBy);
        }

        [TestMethod]
        public void AdminUser_ValidUserConfiguration_MasterAccess()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin",
                DisplayName = "System Administrator",
                PasswordHash = "securehashedpassword",
                AccessLevel = AdminAccessLevel.Master,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedBy = "system"
            };

            // Assert
            adminUser.AccessLevel.Should().Be(AdminAccessLevel.Master);
            adminUser.IsActive.Should().BeTrue();
            adminUser.Username.Should().Be("admin");
            adminUser.DisplayName.Should().Be("System Administrator");
            adminUser.PasswordHash.Should().NotBeNullOrEmpty();
            adminUser.CreatedBy.Should().Be("system");
        }

        [TestMethod]
        public void AdminUser_ValidUserConfiguration_UserAccess()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "user1",
                DisplayName = "Regular User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            // Assert
            adminUser.AccessLevel.Should().Be(AdminAccessLevel.User);
            adminUser.IsActive.Should().BeTrue();
            adminUser.Username.Should().Be("user1");
            adminUser.DisplayName.Should().Be("Regular User");
        }

        [TestMethod]
        public void AdminUser_InactiveUser_Configuration()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "inactiveuser",
                DisplayName = "Inactive User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = false,
                CreatedAt = DateTime.Now.AddMonths(-6)
            };

            // Assert
            adminUser.IsActive.Should().BeFalse();
            adminUser.AccessLevel.Should().Be(AdminAccessLevel.User);
            adminUser.CreatedAt.Should().BeCloseTo(DateTime.Now.AddMonths(-6), TimeSpan.FromDays(1));
        }

        [TestMethod]
        public void AdminUser_LastLoginAt_CanBeNull()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "newuser",
                DisplayName = "New User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now,
                LastLoginAt = null // Never logged in
            };

            // Assert
            adminUser.LastLoginAt.Should().BeNull();
            adminUser.IsActive.Should().BeTrue();
        }

        [TestMethod]
        public void AdminUser_LastLoginAt_CanBeSet()
        {
            // Arrange
            var adminUser = new AdminUser();
            var loginTime = DateTime.Now.AddHours(-2);

            // Act
            adminUser.LastLoginAt = loginTime;

            // Assert
            adminUser.LastLoginAt.Should().Be(loginTime);
            adminUser.LastLoginAt.Should().NotBeNull();
        }

        [TestMethod]
        public void AdminUser_AccessLevelEnum_AllValues()
        {
            // Test that all access levels can be assigned
            var users = new[]
            {
                new AdminUser { AccessLevel = AdminAccessLevel.None },
                new AdminUser { AccessLevel = AdminAccessLevel.User },
                new AdminUser { AccessLevel = AdminAccessLevel.Master }
            };

            // Assert
            users[0].AccessLevel.Should().Be(AdminAccessLevel.None);
            users[1].AccessLevel.Should().Be(AdminAccessLevel.User);
            users[2].AccessLevel.Should().Be(AdminAccessLevel.Master);
        }

        [TestMethod]
        public void AdminUser_UserIdFormat_CanBeGuid()
        {
            // Arrange
            var guidUserId = Guid.NewGuid().ToString();
            var adminUser = new AdminUser { UserId = guidUserId };

            // Act & Assert
            adminUser.UserId.Should().Be(guidUserId);
            Guid.TryParse(adminUser.UserId, out _).Should().BeTrue("UserId should be a valid GUID format");
        }

        [TestMethod]
        public void AdminUser_UserIdFormat_CanBeCustomString()
        {
            // Arrange
            var customUserId = "USER_001";
            var adminUser = new AdminUser { UserId = customUserId };

            // Act & Assert
            adminUser.UserId.Should().Be(customUserId);
        }

        [TestMethod]
        public void AdminUser_CreatedAt_CanBeSetToPastDate()
        {
            // Arrange
            var pastDate = DateTime.Now.AddYears(-1);
            var adminUser = new AdminUser { CreatedAt = pastDate };

            // Act & Assert
            adminUser.CreatedAt.Should().Be(pastDate);
            adminUser.CreatedAt.Should().BeBefore(DateTime.Now);
        }

        [TestMethod]
        public void AdminUser_CompleteUserLifecycle_Simulation()
        {
            // Arrange - Simulate complete user lifecycle
            var adminUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "lifecycle_user",
                DisplayName = "Lifecycle Test User",
                PasswordHash = "initialpasswordhash",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedBy = "admin"
            };

            // Act - Simulate user login
            adminUser.LastLoginAt = DateTime.Now;
            adminUser.UpdatedAt = DateTime.Now;
            adminUser.UpdatedBy = "system";

            // Assert - User is properly configured for active use
            adminUser.UserId.Should().NotBeNullOrEmpty();
            adminUser.Username.Should().Be("lifecycle_user");
            adminUser.DisplayName.Should().Be("Lifecycle Test User");
            adminUser.PasswordHash.Should().NotBeNullOrEmpty();
            adminUser.AccessLevel.Should().Be(AdminAccessLevel.User);
            adminUser.IsActive.Should().BeTrue();
            adminUser.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            adminUser.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            adminUser.LastLoginAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
            adminUser.CreatedBy.Should().Be("admin");
            adminUser.UpdatedBy.Should().Be("system");
        }

        [TestMethod]
        public void AdminUser_EmptyStringProperties_HandleCorrectly()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                UserId = "",
                Username = "",
                DisplayName = "",
                PasswordHash = ""
            };

            // Assert
            adminUser.UserId.Should().Be("");
            adminUser.Username.Should().Be("");
            adminUser.DisplayName.Should().Be("");
            adminUser.PasswordHash.Should().Be("");
        }

        [TestMethod]
        public void AdminUser_NullStringProperties_HandleCorrectly()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                UserId = null!,
                Username = null!,
                DisplayName = null!,
                PasswordHash = null!,
                CreatedBy = null,
                UpdatedBy = null
            };

            // Assert
            adminUser.UserId.Should().BeNull();
            adminUser.Username.Should().BeNull();
            adminUser.DisplayName.Should().BeNull();
            adminUser.PasswordHash.Should().BeNull();
            adminUser.CreatedBy.Should().BeNull();
            adminUser.UpdatedBy.Should().BeNull();
        }

        [TestMethod]
        public void AdminUser_LongStringProperties_HandleCorrectly()
        {
            // Arrange
            var longUserId = new string('A', 500);
            var longUsername = new string('B', 255);
            var longDisplayName = new string('C', 300);
            var longPasswordHash = new string('D', 100);

            // Act
            var adminUser = new AdminUser
            {
                UserId = longUserId,
                Username = longUsername,
                DisplayName = longDisplayName,
                PasswordHash = longPasswordHash
            };

            // Assert
            adminUser.UserId.Should().Be(longUserId);
            adminUser.Username.Should().Be(longUsername);
            adminUser.DisplayName.Should().Be(longDisplayName);
            adminUser.PasswordHash.Should().Be(longPasswordHash);
            adminUser.UserId.Length.Should().Be(500);
            adminUser.Username.Length.Should().Be(255);
            adminUser.DisplayName.Length.Should().Be(300);
            adminUser.PasswordHash.Length.Should().Be(100);
        }

        [TestMethod]
        public void AdminUser_PasswordHash_ShouldNotBeEmpty()
        {
            // Arrange & Act
            var adminUser = new AdminUser
            {
                Username = "testuser",
                PasswordHash = "hashed_secure_password_123"
            };

            // Assert
            adminUser.PasswordHash.Should().NotBeNullOrEmpty();
            adminUser.PasswordHash.Should().Be("hashed_secure_password_123");
        }

        [TestMethod]
        public void AdminUser_UpdatedAt_TracksChanges()
        {
            // Arrange
            var adminUser = new AdminUser();
            var initialUpdateTime = adminUser.UpdatedAt;

            // Act - Simulate a change
            System.Threading.Thread.Sleep(100);
            adminUser.DisplayName = "Updated Name";
            adminUser.UpdatedAt = DateTime.Now;
            adminUser.UpdatedBy = "admin";

            // Assert
            adminUser.UpdatedAt.Should().BeAfter(initialUpdateTime);
            adminUser.UpdatedBy.Should().Be("admin");
            adminUser.DisplayName.Should().Be("Updated Name");
        }
    }
} 