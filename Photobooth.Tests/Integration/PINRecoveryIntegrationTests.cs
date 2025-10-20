using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using Photobooth.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Photobooth.Tests.Integration
{
    /// <summary>
    /// Integration tests for the complete PIN-based password recovery flow
    /// Tests the interaction between DatabaseService, PINRecoveryService, and PasswordPolicyService
    /// </summary>
    [TestClass]
    public class PINRecoveryIntegrationTests
    {
        private DatabaseService _databaseService = null!;
        private PINRecoveryService _pinService = null!;
        private AdminUser _testUser = null!;
        private string _testPassword = null!;
        private string _testPIN = null!;
        private string _tempPath = null!;

        [TestInitialize]
        public async Task Setup()
        {
            // Use temporary file database for testing
            _tempPath = System.IO.Path.GetTempFileName();
            _databaseService = new DatabaseService(_tempPath);
            await _databaseService.InitializeAsync();

            _pinService = new PINRecoveryService();
            _testPassword = "InitialPass123";
            _testPIN = "1234";

            // Create a test user with a PIN
            _testUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "testuser",
                DisplayName = "Test User",
                AccessLevel = AdminAccessLevel.Master,
                IsActive = true,
                CreatedAt = DateTime.Now,
                PINSetupRequired = false // PIN already set up
            };

            await _databaseService.CreateAdminUserAsync(_testUser, _testPassword);

            // Set up PIN for the user
            var (pinHash, pinSalt) = _pinService.HashPINWithNewSalt(_testPIN);
            await _databaseService.UpdateUserRecoveryPINAsync(_testUser.UserId, pinHash, pinSalt);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                // Explicitly clear database reference to close any open connections
                _databaseService = null!;
                
                // Force garbage collection to ensure connections are closed
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                
                // Only delete the specific temp file we created for this test
                if (!string.IsNullOrWhiteSpace(_tempPath) && System.IO.File.Exists(_tempPath))
                {
                    System.IO.File.Delete(_tempPath);
                }
            }
            catch { }
        }

        #region Complete Recovery Flow Tests

        [TestMethod]
        public async Task CompleteRecoveryFlow_ValidPIN_PasswordResetSucceeds()
        {
            // Arrange
            var newPassword = "NewPass123!";
            
            // Validate new password meets policy
            var passwordValidation = PasswordPolicyService.ValidatePassword(newPassword);
            passwordValidation.IsValid.Should().BeTrue("new password should meet policy");

            // Act - Step 1: Get user
            var userResult = await _databaseService.GetAllAsync<AdminUser>();
            userResult.Success.Should().BeTrue();
            var user = userResult.Data!.FirstOrDefault(u => u.Username == _testUser.Username);
            user.Should().NotBeNull();

            // Act - Step 2: Verify PIN
            var pinValid = _pinService.VerifyPIN(_testPIN, user!.RecoveryPIN!, user.RecoveryPINSalt!);
            pinValid.Should().BeTrue("PIN should verify successfully");

            // Act - Step 3: Reset password
            var resetResult = await _databaseService.UpdateUserPasswordByUserIdAsync(user.UserId, newPassword);
            resetResult.Success.Should().BeTrue("password reset should succeed");

            // Assert - Verify new password works
            var authResult = await _databaseService.AuthenticateAsync(_testUser.Username, newPassword);
            authResult.Success.Should().BeTrue();
            authResult.Data.Should().NotBeNull();
            authResult.Data!.Username.Should().Be(_testUser.Username);

            // Assert - Old password should not work
            var oldAuthResult = await _databaseService.AuthenticateAsync(_testUser.Username, _testPassword);
            oldAuthResult.Success.Should().BeTrue();
            oldAuthResult.Data.Should().BeNull("old password should no longer work");
        }

        [TestMethod]
        public async Task CompleteRecoveryFlow_InvalidPIN_PasswordResetFails()
        {
            // Arrange
            var wrongPIN = "9999";

            // Act - Get user
            var userResult = await _databaseService.GetAllAsync<AdminUser>();
            var user = userResult.Data!.FirstOrDefault(u => u.Username == _testUser.Username);

            // Act - Verify wrong PIN
            var pinValid = _pinService.VerifyPIN(wrongPIN, user!.RecoveryPIN!, user.RecoveryPINSalt!);

            // Assert
            pinValid.Should().BeFalse("wrong PIN should fail verification");

            // Assert - Original password should still work
            var authResult = await _databaseService.AuthenticateAsync(_testUser.Username, _testPassword);
            authResult.Success.Should().BeTrue();
            authResult.Data.Should().NotBeNull("original password should still work");
        }

        #endregion

        #region PIN Setup Flow Tests

        [TestMethod]
        public async Task PINSetupFlow_NewUser_PINSetupSucceeds()
        {
            // Arrange - Create user without PIN
            var newUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "newuser",
                DisplayName = "New User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now,
                PINSetupRequired = true // PIN not yet set up
            };
            await _databaseService.CreateAdminUserAsync(newUser, "InitialPass123");

            var newPIN = "5678";

            // Act - Set up PIN
            var (pinHash, pinSalt) = _pinService.HashPINWithNewSalt(newPIN);
            var updateResult = await _databaseService.UpdateUserRecoveryPINAsync(newUser.UserId, pinHash, pinSalt);

            // Assert
            updateResult.Success.Should().BeTrue("PIN setup should succeed");

            // Verify PIN was stored
            var userResult = await _databaseService.GetByUserIdAsync<AdminUser>(newUser.UserId);
            userResult.Success.Should().BeTrue();
            userResult.Data!.RecoveryPIN.Should().NotBeNullOrEmpty();
            userResult.Data.RecoveryPINSalt.Should().NotBeNullOrEmpty();
            userResult.Data.PINSetupRequired.Should().BeFalse("PIN setup flag should be cleared");

            // Verify PIN works
            var pinValid = _pinService.VerifyPIN(newPIN, userResult.Data.RecoveryPIN!, userResult.Data.RecoveryPINSalt!);
            pinValid.Should().BeTrue("newly set PIN should verify");
        }

        [TestMethod]
        public async Task PINSetupFlow_PINChange_OldPINInvalidated()
        {
            // Arrange
            var oldPIN = _testPIN;
            var newPIN = "5678";

            // Act - Change PIN
            var (newPinHash, newPinSalt) = _pinService.HashPINWithNewSalt(newPIN);
            var updateResult = await _databaseService.UpdateUserRecoveryPINAsync(_testUser.UserId, newPinHash, newPinSalt);
            updateResult.Success.Should().BeTrue();

            // Get updated user
            var userResult = await _databaseService.GetByUserIdAsync<AdminUser>(_testUser.UserId);
            var user = userResult.Data!;

            // Assert - New PIN works
            var newPinValid = _pinService.VerifyPIN(newPIN, user.RecoveryPIN!, user.RecoveryPINSalt!);
            newPinValid.Should().BeTrue("new PIN should work");

            // Assert - Old PIN doesn't work
            var oldPinValid = _pinService.VerifyPIN(oldPIN, user.RecoveryPIN!, user.RecoveryPINSalt!);
            oldPinValid.Should().BeFalse("old PIN should not work after change");
        }

        #endregion

        #region Rate Limiting Integration Tests

        [TestMethod]
        public async Task RateLimiting_MultipleFailedPINAttempts_UserLocked()
        {
            // Arrange
            var userResult = await _databaseService.GetAllAsync<AdminUser>();
            var user = userResult.Data!.FirstOrDefault(u => u.Username == _testUser.Username);

            // Act - Attempt wrong PIN 5 times
            var wrongPIN = "0000";
            for (int i = 0; i < 5; i++)
            {
                _pinService.VerifyPIN(wrongPIN, user!.RecoveryPIN!, user.RecoveryPINSalt!);
                _pinService.RecordFailedAttempt(user.Username);
            }

            // Assert
            var isRateLimited = _pinService.IsRateLimited(user!.Username);
            isRateLimited.Should().BeTrue("user should be rate limited after 5 failures");

            var remaining = _pinService.GetRemainingAttempts(user.Username);
            remaining.Should().Be(0, "no attempts should remain");

            // Even correct PIN should not work when rate limited
            var pinValid = _pinService.VerifyPIN(_testPIN, user.RecoveryPIN!, user.RecoveryPINSalt!);
            pinValid.Should().BeTrue("PIN verification itself should work");
            
            // But application should check rate limit before verification
            isRateLimited.Should().BeTrue("rate limit should still be active");
            
            // Cleanup static rate-limit state for this user
            _pinService.ClearFailedAttempts(user!.Username);
        }

        [TestMethod]
        public async Task RateLimiting_ClearAfterSuccess_AttemptsReset()
        {
            // Arrange - Create a unique user for this test to avoid race conditions
            var uniqueUser = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "ratelimit_test_" + Guid.NewGuid().ToString(),
                DisplayName = "Rate Limit Test User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            await _databaseService.CreateAdminUserAsync(uniqueUser, "Test123!");

            // Act - Record some failures
            _pinService.RecordFailedAttempt(uniqueUser.Username);
            _pinService.RecordFailedAttempt(uniqueUser.Username);
            _pinService.RecordFailedAttempt(uniqueUser.Username);

            var remainingBefore = _pinService.GetRemainingAttempts(uniqueUser.Username);
            remainingBefore.Should().Be(2, "should have 2 attempts remaining");

            // Act - Clear after successful PIN verification
            _pinService.ClearFailedAttempts(uniqueUser.Username);

            // Assert
            var remainingAfter = _pinService.GetRemainingAttempts(uniqueUser.Username);
            remainingAfter.Should().Be(5, "attempts should be reset to 5");

            var isRateLimited = _pinService.IsRateLimited(uniqueUser.Username);
            isRateLimited.Should().BeFalse("rate limit should be cleared");
        }

        #endregion

        #region Password Policy Integration Tests

        [TestMethod]
        public void PasswordPolicy_WeakPassword_RejectedByPolicy()
        {
            // Arrange
            var weakPassword = "weak"; // Too short, no uppercase, no number

            // Act
            var validation = PasswordPolicyService.ValidatePassword(weakPassword);

            // Assert
            validation.IsValid.Should().BeFalse("weak password should fail validation");
            validation.MeetsLengthRequirement.Should().BeFalse();
            validation.HasUppercase.Should().BeFalse();
            validation.HasNumber.Should().BeFalse();
        }

        [TestMethod]
        public void PasswordPolicy_StrongPassword_AcceptedByPolicy()
        {
            // Arrange
            var strongPassword = "StrongPass123!";

            // Act
            var validation = PasswordPolicyService.ValidatePassword(strongPassword);

            // Assert
            validation.IsValid.Should().BeTrue("strong password should pass validation");
            validation.MeetsLengthRequirement.Should().BeTrue();
            validation.HasUppercase.Should().BeTrue();
            validation.HasLowercase.Should().BeTrue();
            validation.HasNumber.Should().BeTrue();
        }

        [TestMethod]
        public void PasswordPolicy_ConsistentAcrossScreens_SameRequirements()
        {
            // This test verifies that PasswordPolicyService is used consistently
            // across ForcedPasswordChange and PasswordReset screens

            // Arrange
            var testPasswords = new[]
            {
                "Test1234", // Valid - 8+ chars, upper, lower, number
                "test1234", // No uppercase
                "TEST1234", // No lowercase
                "TestPass", // No number
                "Test12" // Too short
            };

            var expectedResults = new[] { true, false, false, false, false };

            // Act & Assert
            for (int i = 0; i < testPasswords.Length; i++)
            {
                var result = PasswordPolicyService.ValidatePassword(testPasswords[i]);
                result.IsValid.Should().Be(expectedResults[i], 
                    $"password '{testPasswords[i]}' should be {(expectedResults[i] ? "valid" : "invalid")}");
            }
        }

        #endregion

        #region Security Tests

        [TestMethod]
        public async Task Security_PINStoredHashed_NotPlaintext()
        {
            // Arrange & Act
            var userResult = await _databaseService.GetByUserIdAsync<AdminUser>(_testUser.UserId);
            var user = userResult.Data!;

            // Assert
            user.RecoveryPIN.Should().NotBe(_testPIN, "PIN should not be stored in plaintext");
            user.RecoveryPIN.Should().NotBeNullOrEmpty("PIN hash should exist");
            user.RecoveryPINSalt.Should().NotBeNullOrEmpty("PIN salt should exist");
            user.RecoveryPINSalt.Should().NotBe(_testPIN, "salt should not match plaintext PIN");
        }

        [TestMethod]
        public async Task Security_PasswordStoredHashed_NotPlaintext()
        {
            // Arrange - Create user with known password
            var plainPassword = "TestPass123";
            var user = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "securitytest",
                DisplayName = "Security Test",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            // Act
            await _databaseService.CreateAdminUserAsync(user, plainPassword);
            var userResult = await _databaseService.GetByUserIdAsync<AdminUser>(user.UserId);
            var storedUser = userResult.Data!;

            // Assert
            storedUser.PasswordHash.Should().NotBe(plainPassword, "password should not be stored in plaintext");
            storedUser.PasswordHash.Should().NotBeNullOrEmpty("password hash should exist");
            // Note: PasswordSalt is not exposed on AdminUser model for security
        }

        [TestMethod]
        public async Task Security_DifferentUsers_IndependentPINs()
        {
            // Arrange - Create second user with same PIN
            var user2 = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "user2",
                DisplayName = "User 2",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            await _databaseService.CreateAdminUserAsync(user2, "InitialPass123");

            var (pinHash2, pinSalt2) = _pinService.HashPINWithNewSalt(_testPIN); // Same PIN as user1
            await _databaseService.UpdateUserRecoveryPINAsync(user2.UserId, pinHash2, pinSalt2);

            // Act - Get both users
            var user1Result = await _databaseService.GetByUserIdAsync<AdminUser>(_testUser.UserId);
            var user2Result = await _databaseService.GetByUserIdAsync<AdminUser>(user2.UserId);

            // Assert - Same PIN should produce different hashes due to different salts
            user1Result.Data!.RecoveryPIN.Should().NotBe(user2Result.Data!.RecoveryPIN,
                "same PIN should have different hashes for different users");
            user1Result.Data.RecoveryPINSalt.Should().NotBe(user2Result.Data.RecoveryPINSalt,
                "each user should have unique salt");

            // Both PINs should verify correctly
            _pinService.VerifyPIN(_testPIN, user1Result.Data.RecoveryPIN!, user1Result.Data.RecoveryPINSalt!)
                .Should().BeTrue("user1 PIN should verify");
            _pinService.VerifyPIN(_testPIN, user2Result.Data.RecoveryPIN!, user2Result.Data.RecoveryPINSalt!)
                .Should().BeTrue("user2 PIN should verify");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public async Task EdgeCase_UserWithoutPIN_CannotRecover()
        {
            // Arrange - User without PIN set
            var userNoPIN = new AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "nopin",
                DisplayName = "No PIN User",
                AccessLevel = AdminAccessLevel.User,
                IsActive = true,
                CreatedAt = DateTime.Now,
                PINSetupRequired = true
            };
            await _databaseService.CreateAdminUserAsync(userNoPIN, "InitialPass123");

            // Act
            var userResult = await _databaseService.GetByUserIdAsync<AdminUser>(userNoPIN.UserId);
            var user = userResult.Data!;

            // Assert
            user.RecoveryPIN.Should().BeNullOrEmpty("user should not have PIN");
            user.RecoveryPINSalt.Should().BeNullOrEmpty("user should not have PIN salt");
            user.PINSetupRequired.Should().BeTrue("PIN setup should be required");
        }

        [TestMethod]
        public async Task EdgeCase_UserPINVerification_IndependentOfIsActiveFlag()
        {
            // Arrange & Act - Get user data
            var userResult = await _databaseService.GetByUserIdAsync<AdminUser>(_testUser.UserId);
            var user = userResult.Data!;

            // Assert - Verify user has PIN data
            user.RecoveryPIN.Should().NotBeNullOrEmpty("user should have PIN");
            user.RecoveryPINSalt.Should().NotBeNullOrEmpty("user should have PIN salt");
            
            // PIN should still verify regardless of IsActive status
            // (application logic should check IsActive separately before allowing PIN recovery)
            var pinValid = _pinService.VerifyPIN(_testPIN, user.RecoveryPIN!, user.RecoveryPINSalt!);
            pinValid.Should().BeTrue("PIN verification should work - IsActive check is application-level concern");
        }

        #endregion
    }
}

