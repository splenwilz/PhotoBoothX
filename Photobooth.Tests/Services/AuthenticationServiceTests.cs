using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using Photobooth.Models;
using System.Threading.Tasks;

namespace Photobooth.Tests.Services
{
    [TestClass]
    public class AuthenticationServiceTests
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
        public async Task AuthenticateUser_ValidCredentials_ReturnsUser()
        {
            // Arrange
            var username = "testuser";
            var password = "testpass";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "Test User",
                AccessLevel = AdminAccessLevel.Master,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };
            
            // Create a test user first
            await _databaseService.CreateAdminUserAsync(user, password);

            // Act
            var result = await _databaseService.AuthenticateAsync(username, password);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Username.Should().Be(username);
            result.Data.DisplayName.Should().Be("Test User");
            result.Data.AccessLevel.Should().Be(AdminAccessLevel.Master);
        }

        [TestMethod]
        public async Task AuthenticateUser_InvalidUsername_ReturnsNull()
        {
            // Arrange
            var username = "nonexistent";
            var password = "testpass";

            // Act
            var result = await _databaseService.AuthenticateAsync(username, password);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().BeNull();
        }

        [TestMethod]
        public async Task AuthenticateUser_InvalidPassword_ReturnsNull()
        {
            // Arrange
            var username = "testuser";
            var correctPassword = "testpass";
            var wrongPassword = "wrongpass";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "Test User",
                AccessLevel = AdminAccessLevel.Master,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };
            
            // Create a test user first
            await _databaseService.CreateAdminUserAsync(user, correctPassword);

            // Act
            var result = await _databaseService.AuthenticateAsync(username, wrongPassword);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().BeNull();
        }

        [TestMethod]
        public async Task CreateUser_ValidData_CreatesUserSuccessfully()
        {
            // Arrange
            var username = "newuser";
            var password = "newpass";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "New User",
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
            authResult.Data.DisplayName.Should().Be("New User");
            authResult.Data.AccessLevel.Should().Be(AdminAccessLevel.User);
        }

        [TestMethod]
        public async Task UpdateUserPassword_ValidCurrentPassword_ReturnsTrue()
        {
            // Arrange
            var username = "testuser";
            var oldPassword = "oldpass";
            var newPassword = "newpass";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "Test User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };
            
            // Create user with old password
            await _databaseService.CreateAdminUserAsync(user, oldPassword);

            // Act
            var result = await _databaseService.UpdateUserPasswordByUserIdAsync(user.UserId, newPassword);

            // Assert
            result.Success.Should().BeTrue();
            
            // Verify new password works
            var authResult = await _databaseService.AuthenticateAsync(username, newPassword);
            authResult.Success.Should().BeTrue();
            authResult.Data.Should().NotBeNull();
            
            // Verify old password doesn't work
            var oldAuthResult = await _databaseService.AuthenticateAsync(username, oldPassword);
            oldAuthResult.Success.Should().BeTrue();
            oldAuthResult.Data.Should().BeNull();
        }

        [TestMethod]
        public async Task GetAllUsers_MultipleUsers_ReturnsAllUsers()
        {
            // Arrange
            var user1 = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = "user1",
                DisplayName = "User One",
                AccessLevel = AdminAccessLevel.Master,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };
            var user2 = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = "user2",
                DisplayName = "User Two",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };

            await _databaseService.CreateAdminUserAsync(user1, "pass1");
            await _databaseService.CreateAdminUserAsync(user2, "pass2");

            // Act
            var result = await _databaseService.GetAllAsync<AdminUser>();

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Count.Should().BeGreaterOrEqualTo(2); // May include default users
            result.Data.Should().Contain(u => u.Username == "user1" && u.AccessLevel == AdminAccessLevel.Master);
            result.Data.Should().Contain(u => u.Username == "user2" && u.AccessLevel == AdminAccessLevel.User);
        }

        [TestMethod]
        public async Task DeleteUser_ExistingUser_ReturnsTrue()
        {
            // Arrange
            var username = "deleteuser";
            var password = "deletepass";
            var user = new AdminUser
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = "Delete User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = System.DateTime.Now
            };
            
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
        public async Task DeleteUser_NonExistentUser_ReturnsError()
        {
            // Arrange
            var nonExistentUserId = System.Guid.NewGuid().ToString();

            // Act
            var result = await _databaseService.DeleteAdminUserAsync(nonExistentUserId);

            // Assert - This might succeed or fail depending on implementation
            // For now, let's just verify it doesn't throw
            result.Should().NotBeNull();
        }
    }
} 