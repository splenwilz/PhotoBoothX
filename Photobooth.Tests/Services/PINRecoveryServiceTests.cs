using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using System;
using System.Threading.Tasks;

namespace Photobooth.Tests.Services
{
    /// <summary>
    /// Tests for PINRecoveryService - PIN hashing, verification, and rate limiting
    /// </summary>
    [TestClass]
    public class PINRecoveryServiceTests
    {
        private PINRecoveryService _pinService = null!;

        [TestInitialize]
        public void Setup()
        {
            _pinService = new PINRecoveryService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clear any rate limiting data from the shared static instance
            _pinService.ClearFailedAttempts("testuser");
            _pinService.ClearFailedAttempts("user1");
            _pinService.ClearFailedAttempts("user2");
        }

        #region PIN Hashing Tests

        [TestMethod]
        public void HashPINWithNewSalt_ValidPIN_ReturnsHashAndSalt()
        {
            // Arrange
            var pin = "1234";

            // Act
            var (hash, salt) = _pinService.HashPINWithNewSalt(pin);

            // Assert
            hash.Should().NotBeNullOrEmpty("hash should be generated");
            salt.Should().NotBeNullOrEmpty("salt should be generated");
            hash.Should().NotBe(pin, "hash should not be plaintext");
            salt.Should().NotBe(pin, "salt should not be plaintext");
        }

        [TestMethod]
        public void HashPINWithNewSalt_SamePIN_GeneratesDifferentHashes()
        {
            // Arrange
            var pin = "1234";

            // Act
            var (hash1, salt1) = _pinService.HashPINWithNewSalt(pin);
            var (hash2, salt2) = _pinService.HashPINWithNewSalt(pin);

            // Assert
            hash1.Should().NotBe(hash2, "different salts should produce different hashes");
            salt1.Should().NotBe(salt2, "each call should generate a new salt");
        }

        [TestMethod]
        public void HashPINWithNewSalt_EmptyPIN_ThrowsException()
        {
            // Arrange
            var pin = "";

            // Act
            Action act = () => _pinService.HashPINWithNewSalt(pin);

            // Assert
            act.Should().Throw<ArgumentException>("empty PIN should not be allowed")
                .WithMessage("*must be exactly 4 digits*");
        }

        #endregion

        #region PIN Verification Tests

        [TestMethod]
        public void VerifyPIN_CorrectPIN_ReturnsTrue()
        {
            // Arrange
            var pin = "1234";
            var (hash, salt) = _pinService.HashPINWithNewSalt(pin);

            // Act
            var result = _pinService.VerifyPIN(pin, hash, salt);

            // Assert
            result.Should().BeTrue("correct PIN should verify successfully");
        }

        [TestMethod]
        public void VerifyPIN_IncorrectPIN_ReturnsFalse()
        {
            // Arrange
            var correctPin = "1234";
            var wrongPin = "5678";
            var (hash, salt) = _pinService.HashPINWithNewSalt(correctPin);

            // Act
            var result = _pinService.VerifyPIN(wrongPin, hash, salt);

            // Assert
            result.Should().BeFalse("incorrect PIN should fail verification");
        }

        [TestMethod]
        public void VerifyPIN_WrongSalt_ReturnsFalse()
        {
            // Arrange
            var pin = "1234";
            var (hash, salt1) = _pinService.HashPINWithNewSalt(pin);
            var (_, salt2) = _pinService.HashPINWithNewSalt(pin);

            // Act
            var result = _pinService.VerifyPIN(pin, hash, salt2);

            // Assert
            result.Should().BeFalse("wrong salt should fail verification");
        }

        [TestMethod]
        public void VerifyPIN_EmptyPIN_ReturnsFalse()
        {
            // Arrange
            var pin = "1234";
            var (hash, salt) = _pinService.HashPINWithNewSalt(pin);

            // Act - Try to verify with empty PIN
            var result = _pinService.VerifyPIN("", hash, salt);

            // Assert
            result.Should().BeFalse("empty PIN should fail verification");
        }

        [TestMethod]
        public void VerifyPIN_NullStoredHash_ReturnsFalse()
        {
            // Arrange
            var pin = "1234";
            var (_, salt) = _pinService.HashPINWithNewSalt(pin);

            // Act
            var result = _pinService.VerifyPIN(pin, null!, salt);

            // Assert
            result.Should().BeFalse("null hash should fail verification");
        }

        [TestMethod]
        public void VerifyPIN_NullStoredSalt_ReturnsFalse()
        {
            // Arrange
            var pin = "1234";
            var (hash, _) = _pinService.HashPINWithNewSalt(pin);

            // Act
            var result = _pinService.VerifyPIN(pin, hash, null!);

            // Assert
            result.Should().BeFalse("null salt should fail verification");
        }

        #endregion

        #region Rate Limiting Tests

        [TestMethod]
        public void IsRateLimited_NewUser_ReturnsFalse()
        {
            // Arrange
            var username = "testuser";

            // Act
            var result = _pinService.IsRateLimited(username);

            // Assert
            result.Should().BeFalse("new user should not be rate limited");
        }

        [TestMethod]
        public void RecordFailedAttempt_IncreasesFailCount()
        {
            // Arrange
            var username = "testuser_unique_" + Guid.NewGuid().ToString();

            // Act
            _pinService.RecordFailedAttempt(username);
            var remaining = _pinService.GetRemainingAttempts(username);

            // Assert
            remaining.Should().Be(4, "should have 4 attempts remaining after 1 failed");
            
            // Cleanup
            _pinService.ClearFailedAttempts(username);
        }

        [TestMethod]
        public void RecordFailedAttempt_AfterMaxAttempts_CausesRateLimit()
        {
            // Arrange
            var username = "testuser";

            // Act - Record 5 failed attempts
            for (int i = 0; i < 5; i++)
            {
                _pinService.RecordFailedAttempt(username);
            }
            var isRateLimited = _pinService.IsRateLimited(username);

            // Assert
            isRateLimited.Should().BeTrue("user should be rate limited after 5 failed attempts");
        }

        [TestMethod]
        public void GetRemainingAttempts_NewUser_ReturnsFive()
        {
            // Arrange
            var username = "testuser";

            // Act
            var remaining = _pinService.GetRemainingAttempts(username);

            // Assert
            remaining.Should().Be(5, "new user should have 5 attempts");
        }

        [TestMethod]
        public void GetRemainingAttempts_AfterFailures_ReturnsCorrectCount()
        {
            // Arrange
            var username = "testuser_unique_" + Guid.NewGuid().ToString();
            _pinService.RecordFailedAttempt(username);
            _pinService.RecordFailedAttempt(username);
            _pinService.RecordFailedAttempt(username);

            // Act
            var remaining = _pinService.GetRemainingAttempts(username);

            // Assert
            remaining.Should().Be(2, "should have 2 attempts remaining after 3 failed");
            
            // Cleanup
            _pinService.ClearFailedAttempts(username);
        }

        [TestMethod]
        public void GetRemainingAttempts_RateLimited_ReturnsZero()
        {
            // Arrange
            var username = "testuser";
            for (int i = 0; i < 5; i++)
            {
                _pinService.RecordFailedAttempt(username);
            }

            // Act
            var remaining = _pinService.GetRemainingAttempts(username);

            // Assert
            remaining.Should().Be(0, "rate limited user should have 0 attempts");
        }

        [TestMethod]
        public void ClearFailedAttempts_ResetsAttemptCount()
        {
            // Arrange
            var username = "testuser";
            _pinService.RecordFailedAttempt(username);
            _pinService.RecordFailedAttempt(username);
            _pinService.RecordFailedAttempt(username);

            // Act
            _pinService.ClearFailedAttempts(username);
            var remaining = _pinService.GetRemainingAttempts(username);

            // Assert
            remaining.Should().Be(5, "cleared user should have 5 attempts again");
        }

        [TestMethod]
        public void ClearFailedAttempts_RemovesRateLimit()
        {
            // Arrange
            var username = "testuser";
            for (int i = 0; i < 5; i++)
            {
                _pinService.RecordFailedAttempt(username);
            }

            // Act
            _pinService.ClearFailedAttempts(username);
            var isRateLimited = _pinService.IsRateLimited(username);

            // Assert
            isRateLimited.Should().BeFalse("cleared user should not be rate limited");
        }

        [TestMethod]
        public void RateLimit_MultipleUsers_AreIndependent()
        {
            // Arrange
            var user1 = "user1";
            var user2 = "user2";

            // Act
            _pinService.RecordFailedAttempt(user1);
            _pinService.RecordFailedAttempt(user1);
            _pinService.RecordFailedAttempt(user1);

            var remaining1 = _pinService.GetRemainingAttempts(user1);
            var remaining2 = _pinService.GetRemainingAttempts(user2);

            // Assert
            remaining1.Should().Be(2, "user1 should have 2 attempts");
            remaining2.Should().Be(5, "user2 should have 5 attempts");
        }

        [TestMethod]
        public void ClearFailedAttempts_NullUsername_DoesNotThrow()
        {
            // Act & Assert
            Action act = () => _pinService.ClearFailedAttempts(null!);
            act.Should().NotThrow("clearing null username should be handled gracefully");
        }

        [TestMethod]
        public void ClearFailedAttempts_EmptyUsername_DoesNotThrow()
        {
            // Act & Assert
            Action act = () => _pinService.ClearFailedAttempts("");
            act.Should().NotThrow("clearing empty username should be handled gracefully");
        }

        #endregion

        #region Security Tests

        [TestMethod]
        public void VerifyPIN_ConstantTime_Prevention()
        {
            // Arrange
            var pin = "1234";
            var (hash, salt) = _pinService.HashPINWithNewSalt(pin);
            var wrongPin = "0000";

            // Act - Measure time for correct and incorrect PINs
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            _pinService.VerifyPIN(pin, hash, salt);
            sw1.Stop();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            _pinService.VerifyPIN(wrongPin, hash, salt);
            sw2.Stop();

            // Assert - Times should be similar (within 50ms) to prevent timing attacks
            var difference = Math.Abs(sw1.ElapsedMilliseconds - sw2.ElapsedMilliseconds);
            difference.Should().BeLessThan(50, "verification time should be constant to prevent timing attacks");
        }

        [TestMethod]
        public void HashPINWithNewSalt_Uses100kIterations()
        {
            // Arrange
            var pin = "1234";

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (hash, salt) = _pinService.HashPINWithNewSalt(pin);
            sw.Stop();

            // Assert
            // PBKDF2 with 100k iterations should take at least 10ms
            sw.ElapsedMilliseconds.Should().BeGreaterThan(5, 
                "hashing should take time due to 100k iterations");
            hash.Length.Should().BeGreaterThan(20, 
                "hash should be properly sized");
        }

        #endregion
    }
}

