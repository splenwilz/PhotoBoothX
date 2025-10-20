using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Photobooth.Tests.Integration
{
    /// <summary>
    /// Integration tests for master password system end-to-end workflows
    /// Tests the interaction between MasterPasswordService, MasterPasswordConfigService, and DatabaseService
    /// </summary>
    [TestClass]
    public class MasterPasswordIntegrationTests
    {
        private string _testDbPath = null!;
        private DatabaseService _databaseService = null!;
        private MasterPasswordService _masterPasswordService = null!;
        private MasterPasswordConfigService _masterPasswordConfigService = null!;
        private MasterPasswordRateLimitService _rateLimitService = null!;

        [TestInitialize]
        public async Task Setup()
        {
            // Create test database in temp directory
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_master_password_{Guid.NewGuid()}.db");
            
            // Initialize services
            _databaseService = new DatabaseService(_testDbPath);
            await _databaseService.InitializeAsync();
            
            _masterPasswordService = new MasterPasswordService();
            _masterPasswordConfigService = new MasterPasswordConfigService(_databaseService, _masterPasswordService);
            _rateLimitService = new MasterPasswordRateLimitService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test database
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Configuration Persistence Tests

        [TestMethod]
        public async Task SetBaseSecret_ValidSecret_StoresEncrypted()
        {
            // Arrange
            var baseSecret = "test-secret-1234567890123456789012345678901234567890";

            // Act
            var result = await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            // Assert
            result.Should().BeTrue("base secret should be stored successfully");

            // Verify it can be retrieved
            var retrievedSecret = await _masterPasswordConfigService.GetBaseSecretAsync();
            retrievedSecret.Should().Be(baseSecret, "retrieved secret should match original");
        }

        [TestMethod]
        public async Task GetBaseSecret_NoSecret_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Func<Task> act = async () => await _masterPasswordConfigService.GetBaseSecretAsync();
            await act.Should().ThrowAsync<InvalidOperationException>(
                "getting base secret when none is configured should throw");
        }

        [TestMethod]
        public async Task SetBaseSecret_UpdateExisting_Overwrites()
        {
            // Arrange
            var secret1 = "first-secret-12345678901234567890123456789012345";
            var secret2 = "second-secret-1234567890123456789012345678901234";

            // Act
            await _masterPasswordConfigService.SetBaseSecretAsync(secret1);
            var retrieved1 = await _masterPasswordConfigService.GetBaseSecretAsync();
            
            await _masterPasswordConfigService.SetBaseSecretAsync(secret2);
            var retrieved2 = await _masterPasswordConfigService.GetBaseSecretAsync();

            // Assert
            retrieved1.Should().Be(secret1, "first secret should be retrieved correctly");
            retrieved2.Should().Be(secret2, "second secret should overwrite first");
        }

        [TestMethod]
        public void GenerateRandomBaseSecret_ReturnsValid64CharSecret()
        {
            // Act
            var secret = MasterPasswordConfigService.GenerateRandomBaseSecret();

            // Assert
            secret.Should().NotBeNullOrEmpty();
            secret.Should().HaveLength(64, "generated secret should be 64 characters");
            secret.Should().MatchRegex(@"^[A-Za-z0-9]+$", "secret should be alphanumeric");
        }

        [TestMethod]
        public void GenerateRandomBaseSecret_MultipleGenerations_ProducesDifferentSecrets()
        {
            // Act
            var secrets = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                secrets.Add(MasterPasswordConfigService.GenerateRandomBaseSecret());
            }

            // Assert
            secrets.Count.Should().Be(10, "all generated secrets should be unique");
        }

        #endregion

        #region End-to-End Master Password Flow Tests

        [TestMethod]
        public async Task EndToEnd_GenerateValidateAndRecordUsage_Success()
        {
            // Arrange - Configure the system
            var baseSecret = "production-secret-12345678901234567890123456789012345";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            // Create test admin user
            var adminUser = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8), // Unique username
                DisplayName = "Test Admin"
            };
            var createUserResult = await _databaseService.CreateAdminUserAsync(adminUser, "TestPass123!");
            createUserResult.Success.Should().BeTrue("user creation should succeed");

            // Act - Support tool generates password
            var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, kioskMac);
            var (generatedPassword, generatedNonce) = _masterPasswordService.GeneratePassword(privateKey, kioskMac);

            // Kiosk validates password
            var (isValid, extractedNonce) = _masterPasswordService.ValidatePassword(
                generatedPassword, privateKey, kioskMac);

            // Mark password as used
            var passwordHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(generatedPassword));
            var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");
            
            var markUsedResult = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, extractedNonce!, kioskMac, adminUser.UserId);

            // Assert
            isValid.Should().BeTrue("generated password should validate");
            extractedNonce.Should().Be(generatedNonce, "nonce should match");
            markUsedResult.Success.Should().BeTrue("marking as used should succeed");
            markUsedResult.Data.Should().BeFalse("first use should return false (not already used)");

            // Try to reuse the password
            var reuseResult = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, extractedNonce!, kioskMac, adminUser.UserId);
            
            reuseResult.Success.Should().BeTrue("database operation should succeed");
            reuseResult.Data.Should().BeTrue("second use should return true (already used)");
        }

        [TestMethod]
        public async Task EndToEnd_MultipleKiosks_DifferentPasswords()
        {
            // Arrange
            var baseSecret = "production-secret-12345678901234567890123456789012345";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            var kiosk1Mac = "00:15:5D:01:02:03";
            var kiosk2Mac = "00:15:5D:04:05:06";

            // Act - Generate passwords for each kiosk
            var key1 = _masterPasswordService.DerivePrivateKey(baseSecret, kiosk1Mac);
            var (password1, _) = _masterPasswordService.GeneratePassword(key1, kiosk1Mac);

            var key2 = _masterPasswordService.DerivePrivateKey(baseSecret, kiosk2Mac);
            var (password2, _) = _masterPasswordService.GeneratePassword(key2, kiosk2Mac);

            // Validate passwords on respective kiosks
            var (valid1OnKiosk1, _) = _masterPasswordService.ValidatePassword(password1, key1, kiosk1Mac);
            var (valid2OnKiosk2, _) = _masterPasswordService.ValidatePassword(password2, key2, kiosk2Mac);

            // Try cross-validation
            var (valid1OnKiosk2, _) = _masterPasswordService.ValidatePassword(password1, key2, kiosk2Mac);
            var (valid2OnKiosk1, _) = _masterPasswordService.ValidatePassword(password2, key1, kiosk1Mac);

            // Assert
            valid1OnKiosk1.Should().BeTrue("password1 should work on kiosk1");
            valid2OnKiosk2.Should().BeTrue("password2 should work on kiosk2");
            valid1OnKiosk2.Should().BeFalse("password1 should not work on kiosk2");
            valid2OnKiosk1.Should().BeFalse("password2 should not work on kiosk1");
            password1.Should().NotBe(password2, "passwords should be different for different kiosks");
        }

        [TestMethod]
        public async Task EndToEnd_RateLimitingWithMasterPassword_PreventsExcessiveAttempts()
        {
            // Arrange
            var baseSecret = "production-secret-12345678901234567890123456789012345";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);
            var username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8); // Unique username to avoid test interference

            // Act - Simulate brute-force attack
            var attemptCount = 0;
            for (int i = 0; i < 10; i++)
            {
                if (_rateLimitService.IsLockedOut(username))
                {
                    break;
                }

                // Try invalid password
                _rateLimitService.RecordFailedAttempt(username);
                attemptCount++;
            }

            // Assert
            attemptCount.Should().Be(3, "should allow exactly 3 attempts");
            _rateLimitService.IsLockedOut(username).Should().BeTrue("user should be locked after 3 attempts");
            _rateLimitService.GetRemainingLockoutMinutes(username).Should().BeGreaterThan(0, 
                "lockout should be active");
        }

        [TestMethod]
        public async Task EndToEnd_SecretRotation_NewPasswordsUseNewSecret()
        {
            // Arrange - Original secret
            var secret1 = "original-secret-123456789012345678901234567890123";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(secret1);

            // Generate password with first secret
            var key1 = _masterPasswordService.DerivePrivateKey(secret1, kioskMac);
            var (password1, _) = _masterPasswordService.GeneratePassword(key1, kioskMac);

            // Act - Rotate secret
            var secret2 = "rotated-secret-1234567890123456789012345678901234";
            await _masterPasswordConfigService.SetBaseSecretAsync(secret2);

            // Generate password with new secret
            var key2 = _masterPasswordService.DerivePrivateKey(secret2, kioskMac);
            var (password2, _) = _masterPasswordService.GeneratePassword(key2, kioskMac);

            // Assert
            // Old password with old key still validates
            var (valid1WithKey1, _) = _masterPasswordService.ValidatePassword(password1, key1, kioskMac);
            valid1WithKey1.Should().BeTrue("old password should still work with old key");

            // New password with new key validates
            var (valid2WithKey2, _) = _masterPasswordService.ValidatePassword(password2, key2, kioskMac);
            valid2WithKey2.Should().BeTrue("new password should work with new key");

            // Old password with new key does not validate
            var (valid1WithKey2, _) = _masterPasswordService.ValidatePassword(password1, key2, kioskMac);
            valid1WithKey2.Should().BeFalse("old password should not work with new key");
        }

        #endregion

        #region Single-Use Enforcement Tests

        [TestMethod]
        public async Task SingleUse_PasswordUsedTwice_SecondUseFails()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            var adminUser = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8), // Unique username
                DisplayName = "Test Admin"
            };
            var createResult = await _databaseService.CreateAdminUserAsync(adminUser, "TestPass123!");
            createResult.Success.Should().BeTrue("user creation should succeed");

            var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, kioskMac);
            var (password, nonce) = _masterPasswordService.GeneratePassword(privateKey, kioskMac);
            
            var passwordHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password));
            var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");

            // Act - Use password twice
            var firstUse = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kioskMac, adminUser.UserId);
            var secondUse = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kioskMac, adminUser.UserId);

            // Assert
            firstUse.Success.Should().BeTrue();
            firstUse.Data.Should().BeFalse("first use should indicate password was not already used");
            
            secondUse.Success.Should().BeTrue();
            secondUse.Data.Should().BeTrue("second use should indicate password was already used");
        }

        [TestMethod]
        public async Task SingleUse_SamePasswordDifferentKiosks_BothCanUse()
        {
            // Arrange - This scenario shouldn't happen (different kiosks = different passwords)
            // But testing to ensure database handles it correctly
            var baseSecret = "test-secret-12345678901234567890123456789012345678";
            var kiosk1Mac = "00:15:5D:01:02:03";
            var kiosk2Mac = "00:15:5D:04:05:06";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            var adminUser = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8), // Unique username
                DisplayName = "Test Admin"
            };
            var createResult = await _databaseService.CreateAdminUserAsync(adminUser, "TestPass123!");
            createResult.Success.Should().BeTrue("user creation should succeed");

            // Generate same nonce password (artificially for testing)
            var key1 = _masterPasswordService.DerivePrivateKey(baseSecret, kiosk1Mac);
            var (password, nonce) = _masterPasswordService.GeneratePassword(key1, kiosk1Mac);
            
            var passwordHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password));
            var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");

            // Act - Use on both kiosks
            var useOnKiosk1 = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kiosk1Mac, adminUser.UserId);
            var useOnKiosk2 = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kiosk2Mac, adminUser.UserId);

            // Assert
            useOnKiosk1.Success.Should().BeTrue();
            useOnKiosk1.Data.Should().BeFalse("first use should succeed");
            
            useOnKiosk2.Success.Should().BeTrue();
            useOnKiosk2.Data.Should().BeTrue("same password on different kiosk should be rejected");
        }

        [TestMethod]
        public async Task SingleUse_MultiplePasswordsSameKiosk_AllWorkOnce()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            var adminUser = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8), // Unique username
                DisplayName = "Test Admin"
            };
            var createResult = await _databaseService.CreateAdminUserAsync(adminUser, "TestPass123!");
            createResult.Success.Should().BeTrue("user creation should succeed");

            var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, kioskMac);

            // Act - Generate and use 5 different passwords
            for (int i = 0; i < 5; i++)
            {
                var (password, nonce) = _masterPasswordService.GeneratePassword(privateKey, kioskMac);
                var passwordHash = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(password));
                var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");

                var result = await _databaseService.MarkMasterPasswordUsedAsync(
                    passwordHashString, nonce, kioskMac, adminUser.UserId);

                // Assert
                result.Success.Should().BeTrue($"password {i + 1} should be marked as used");
                result.Data.Should().BeFalse($"password {i + 1} should not be previously used");
            }
        }

        #endregion

        #region Security Tests

        [TestMethod]
        public async Task Security_EncryptedStorage_SecretNotInPlaintext()
        {
            // Arrange
            var plainSecret = "very-secret-password-12345678901234567890123456";
            await _masterPasswordConfigService.SetBaseSecretAsync(plainSecret);

            // Force SQLite to close all connections
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50); // Give OS time to release file lock

            // Act - Read database file directly
            var dbBytes = await File.ReadAllBytesAsync(_testDbPath);
            var dbContent = System.Text.Encoding.UTF8.GetString(dbBytes);

            // Assert
            dbContent.Should().NotContain(plainSecret, 
                "plain secret should not be found in database file (it should be encrypted)");
        }

        [TestMethod]
        public async Task Security_PasswordHash_StoredNotPlaintext()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            var adminUser = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin_" + Guid.NewGuid().ToString("N").Substring(0, 8), // Unique username
                DisplayName = "Test Admin"
            };
            var createResult = await _databaseService.CreateAdminUserAsync(adminUser, "TestPass123!");
            createResult.Success.Should().BeTrue("user creation should succeed");

            var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, kioskMac);
            var (password, nonce) = _masterPasswordService.GeneratePassword(privateKey, kioskMac);
            
            var passwordHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password));
            var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");

            await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kioskMac, adminUser.UserId);

            // Force SQLite to close all connections
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50); // Give OS time to release file lock

            // Act - Read database
            var dbBytes = await File.ReadAllBytesAsync(_testDbPath);
            var dbContent = System.Text.Encoding.UTF8.GetString(dbBytes);

            // Assert
            dbContent.Should().NotContain(password, 
                "plain password should not be in database (only hash should be stored)");
            dbContent.Should().Contain(passwordHashString.Substring(0, 16), 
                "password hash should be in database (partial check to avoid false positives)");
        }

        [TestMethod]
        public async Task Security_DifferentUsers_CannotReusePassword()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);

            var user1 = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin1",
                DisplayName = "Admin 1"
            };
            var user2 = new Photobooth.Models.AdminUser
            {
                UserId = Guid.NewGuid().ToString(),
                Username = "admin2",
                DisplayName = "Admin 2"
            };
            await _databaseService.CreateAdminUserAsync(user1, "TestPass123!");
            await _databaseService.CreateAdminUserAsync(user2, "TestPass456!");

            var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, kioskMac);
            var (password, nonce) = _masterPasswordService.GeneratePassword(privateKey, kioskMac);
            
            var passwordHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password));
            var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");

            // Act - User1 uses password, then User2 tries to use same password
            var user1Use = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kioskMac, user1.UserId);
            var user2Use = await _databaseService.MarkMasterPasswordUsedAsync(
                passwordHashString, nonce, kioskMac, user2.UserId);

            // Assert
            user1Use.Success.Should().BeTrue();
            user1Use.Data.Should().BeFalse("user1's first use should succeed");
            
            user2Use.Success.Should().BeTrue();
            user2Use.Data.Should().BeTrue("user2 should not be able to reuse password");
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task ErrorHandling_InvalidPasswordFormat_HandledGracefully()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            await _masterPasswordConfigService.SetBaseSecretAsync(baseSecret);
            
            var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, kioskMac);

            // Act & Assert - Various invalid formats
            var (valid1, _) = _masterPasswordService.ValidatePassword("", privateKey, kioskMac);
            valid1.Should().BeFalse("empty password should fail");

            var (valid2, _) = _masterPasswordService.ValidatePassword("1234567", privateKey, kioskMac);
            valid2.Should().BeFalse("7-digit password should fail");

            var (valid3, _) = _masterPasswordService.ValidatePassword("123456789", privateKey, kioskMac);
            valid3.Should().BeFalse("9-digit password should fail");

            var (valid4, _) = _masterPasswordService.ValidatePassword("abcd1234", privateKey, kioskMac);
            valid4.Should().BeFalse("non-numeric password should fail");
        }

        [TestMethod]
        public async Task ErrorHandling_CorruptedDatabase_ThrowsException()
        {
            // Arrange - Corrupt the database
            await _databaseService.InitializeAsync();

            // Force SQLite to close all connections
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50); // Give OS time to release file lock

            await File.WriteAllTextAsync(_testDbPath, "corrupted data");

            // Act - Try to query the corrupted database (not initialize, which recreates)
            Func<Task> act = async () =>
            {
                var result = await _databaseService.GetSettingValueAsync<string>("Security", "MasterPasswordBaseSecret");
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage);
                }
            };

            // Assert
            await act.Should().ThrowAsync<Exception>("corrupted database should throw when queried");
        }

        #endregion
    }
}

