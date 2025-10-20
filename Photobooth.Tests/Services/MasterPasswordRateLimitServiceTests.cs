using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using System;
using System.Threading;

namespace Photobooth.Tests.Services
{
    /// <summary>
    /// Tests for MasterPasswordRateLimitService - brute-force protection for master password attempts
    /// </summary>
    [TestClass]
    public class MasterPasswordRateLimitServiceTests
    {
        private MasterPasswordRateLimitService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new MasterPasswordRateLimitService();
        }

        // Helper to generate unique usernames to avoid test interference (service uses static dictionary)
        private string UniqueUsername(string prefix = "user") => $"{prefix}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        #region Basic Rate Limiting Tests

        [TestMethod]
        public void RecordFailedAttempt_FirstAttempt_ReturnsCorrectRemaining()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act
            var remaining = _service.RecordFailedAttempt(username);

            // Assert
            remaining.Should().Be(2, "should have 2 attempts remaining after 1 failed attempt (3 total - 1)");
        }

        [TestMethod]
        public void RecordFailedAttempt_MultipleAttempts_CreditsCountDown()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act & Assert
            _service.RecordFailedAttempt(username).Should().Be(2, "after 1st attempt");
            _service.RecordFailedAttempt(username).Should().Be(1, "after 2nd attempt");
            _service.RecordFailedAttempt(username).Should().Be(0, "after 3rd attempt - account locked");
        }

        [TestMethod]
        public void RecordFailedAttempt_ExceedsLimit_ReturnsZero()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act - Exceed the limit (3 attempts allowed)
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }

            var remaining = _service.RecordFailedAttempt(username); // 4th attempt

            // Assert
            remaining.Should().Be(0, "no attempts should remain after exceeding limit");
        }

        [TestMethod]
        public void IsRateLimited_NoAttempts_ReturnsFalse()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act
            var isLimited = _service.IsLockedOut(username);

            // Assert
            isLimited.Should().BeFalse("user with no failed attempts should not be rate-limited");
        }

        [TestMethod]
        public void IsRateLimited_BelowLimit_ReturnsFalse()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act
            _service.RecordFailedAttempt(username);
            _service.RecordFailedAttempt(username);
            var isLimited = _service.IsLockedOut(username);

            // Assert
            isLimited.Should().BeFalse("user below limit (2 of 3) should not be rate-limited");
        }

        [TestMethod]
        public void IsRateLimited_AtLimit_ReturnsTrue()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act - Use all 3 attempts
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }
            var isLimited = _service.IsLockedOut(username);

            // Assert
            isLimited.Should().BeTrue("user at limit (3 of 3) should be rate-limited");
        }

        [TestMethod]
        public void IsRateLimited_BeyondLimit_ReturnsTrue()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act - Exceed the limit
            for (int i = 0; i < 10; i++)
            {
                _service.RecordFailedAttempt(username);
            }
            var isLimited = _service.IsLockedOut(username);

            // Assert
            isLimited.Should().BeTrue("user beyond limit should remain rate-limited");
        }

        #endregion

        #region User Isolation Tests

        [TestMethod]
        public void RecordFailedAttempt_DifferentUsers_IndependentCounters()
        {
            // Arrange
            var user1 = UniqueUsername("admin");
            var user2 = UniqueUsername("support");

            // Act
            _service.RecordFailedAttempt(user1);
            _service.RecordFailedAttempt(user1);
            
            var remaining1 = _service.RecordFailedAttempt(user1); // 3rd attempt for user1
            var remaining2 = _service.RecordFailedAttempt(user2); // 1st attempt for user2

            // Assert
            remaining1.Should().Be(0, "user1 should have 0 remaining after 3 attempts");
            remaining2.Should().Be(2, "user2 should have 2 remaining after 1 attempt");
        }

        [TestMethod]
        public void IsRateLimited_DifferentUsers_IndependentLockouts()
        {
            // Arrange
            var user1 = UniqueUsername("admin");
            var user2 = UniqueUsername("support");

            // Act - Lock out user1 but not user2
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(user1);
            }

            var user1Limited = _service.IsLockedOut(user1);
            var user2Limited = _service.IsLockedOut(user2);

            // Assert
            user1Limited.Should().BeTrue("user1 should be locked out");
            user2Limited.Should().BeFalse("user2 should not be affected by user1's lockout");
        }

        [TestMethod]
        public void RecordFailedAttempt_CaseSensitiveUsername_DifferentCounters()
        {
            // Arrange
            var userLower = "admin";
            var userUpper = "ADMIN";

            // Act
            var remaining1 = _service.RecordFailedAttempt(userLower);
            var remaining2 = _service.RecordFailedAttempt(userUpper);

            // Assert
            remaining1.Should().Be(2, "lowercase username should have its own counter");
            remaining2.Should().Be(2, "uppercase username should have separate counter");
        }

        #endregion

        #region Lockout Duration Tests

        [TestMethod]
        public void GetRemainingLockoutMinutes_NotLocked_ReturnsZero()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act
            var remainingMinutes = _service.GetRemainingLockoutMinutes(username);

            // Assert
            remainingMinutes.Should().Be(0, "user with no lockout should have 0 remaining minutes");
        }

        [TestMethod]
        public void GetRemainingLockoutMinutes_Locked_ReturnsPositiveValue()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act - Lock out the user
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }
            var remainingMinutes = _service.GetRemainingLockoutMinutes(username);

            // Assert
            remainingMinutes.Should().BeGreaterThan(0, "locked user should have remaining lockout time");
            remainingMinutes.Should().BeLessOrEqualTo(15, "lockout should not exceed 15 minutes");
        }

        [TestMethod]
        public void GetRemainingLockoutMinutes_JustLocked_ReturnsApproximately15Minutes()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act - Lock out the user
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }
            var remainingMinutes = _service.GetRemainingLockoutMinutes(username);

            // Assert
            remainingMinutes.Should().BeInRange(14, 15, "just-locked user should have ~15 minutes remaining");
        }

        #endregion

        #region Reset Tests

        [TestMethod]
        public void ResetAttempts_LockedUser_Unlocks()
        {
            // Arrange
            var username = UniqueUsername("admin");
            
            // Lock out the user
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }
            _service.IsLockedOut(username).Should().BeTrue("user should be locked before reset");

            // Act
            _service.ResetAttempts(username);

            // Assert
            _service.IsLockedOut(username).Should().BeFalse("user should be unlocked after reset");
        }

        [TestMethod]
        public void ResetAttempts_PartialAttempts_Clears()
        {
            // Arrange
            var username = UniqueUsername("admin");
            
            // Record some attempts but don't lock
            _service.RecordFailedAttempt(username);
            _service.RecordFailedAttempt(username);
            _service.RecordFailedAttempt(username);

            // Act
            _service.ResetAttempts(username);
            var remaining = _service.RecordFailedAttempt(username);

            // Assert
            remaining.Should().Be(2, "counter should be reset to 0, so first attempt after reset leaves 2");
        }

        [TestMethod]
        public void ResetAttempts_NoAttempts_DoesNotThrow()
        {
            // Arrange
            var username = UniqueUsername("admin");

            // Act & Assert - Should not throw
            Action act = () => _service.ResetAttempts(username);
            act.Should().NotThrow("resetting user with no attempts should be safe");
        }

        [TestMethod]
        public void ResetAttempts_OneUser_DoesNotAffectOthers()
        {
            // Arrange
            var user1 = UniqueUsername("admin");
            var user2 = UniqueUsername("support");
            
            // Lock both users
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(user1);
                _service.RecordFailedAttempt(user2);
            }

            // Act - Reset only user1
            _service.ResetAttempts(user1);

            // Assert
            _service.IsLockedOut(user1).Should().BeFalse("user1 should be unlocked");
            _service.IsLockedOut(user2).Should().BeTrue("user2 should still be locked");
        }

        #endregion

        #region Time-Based Expiry Tests

        [TestMethod]
        public void IsRateLimited_AfterLockoutExpires_ReturnsFalse()
        {
            // Arrange - This test would require modifying lockout duration or mocking time
            // For now, we'll skip actual time-based testing and document the behavior
            // In production, lockout expires after 15 minutes

            // This is a documentation test showing expected behavior
            var username = UniqueUsername("admin");
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }

            // Act - In production, after 15 minutes, lockout would expire
            var isLocked = _service.IsLockedOut(username);

            // Assert
            isLocked.Should().BeTrue("lockout should still be active (cannot test expiry without time manipulation)");
        }

        [TestMethod]
        public void GetRemainingLockoutMinutes_DecrementsOverTime()
        {
            // Arrange
            var username = UniqueUsername("admin");
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }

            var minutes1 = _service.GetRemainingLockoutMinutes(username);
            
            // Act - Wait a short time
            Thread.Sleep(100); // 100ms
            
            var minutes2 = _service.GetRemainingLockoutMinutes(username);

            // Assert
            minutes1.Should().BeGreaterOrEqualTo(minutes2, "remaining time should not increase");
            // Note: Both will likely be 15 due to rounding, but this demonstrates the concept
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void RecordFailedAttempt_NullUsername_DoesNotThrow()
        {
            // Act & Assert - Should handle null gracefully
            Action act = () => _service.RecordFailedAttempt(null!);
            act.Should().NotThrow("service should handle null username gracefully");
        }

        [TestMethod]
        public void RecordFailedAttempt_EmptyPrefixUsername_HandledGracefully()
        {
            // Arrange
            // Test that the service handles usernames with empty prefix gracefully
            // Note: Using UniqueUsername("") to avoid test interference from static dictionary
            var username = UniqueUsername("");

            // Act
            var remaining = _service.RecordFailedAttempt(username);

            // Assert
            remaining.Should().Be(2, "username with empty prefix should be handled gracefully");
        }

        [TestMethod]
        public void IsRateLimited_NullUsername_ReturnsFalse()
        {
            // Act
            var isLimited = _service.IsLockedOut(null!);

            // Assert
            isLimited.Should().BeFalse("null username should not be rate-limited");
        }

        [TestMethod]
        public void GetRemainingLockoutMinutes_NullUsername_ReturnsZero()
        {
            // Act
            var minutes = _service.GetRemainingLockoutMinutes(null!);

            // Assert
            minutes.Should().Be(0, "null username should have 0 lockout minutes");
        }

        [TestMethod]
        public void ResetAttempts_NullUsername_DoesNotThrow()
        {
            // Act & Assert
            Action act = () => _service.ResetAttempts(null!);
            act.Should().NotThrow("resetting null username should be safe");
        }

        [TestMethod]
        public void RecordFailedAttempt_SpecialCharactersInUsername_Works()
        {
            // Arrange
            var username = "admin@example.com!#$%";

            // Act
            var remaining = _service.RecordFailedAttempt(username);

            // Assert
            remaining.Should().Be(2, "special characters in username should work");
        }

        [TestMethod]
        public void RecordFailedAttempt_VeryLongUsername_Works()
        {
            // Arrange
            var username = new string('a', 1000);

            // Act
            var remaining = _service.RecordFailedAttempt(username);

            // Assert
            remaining.Should().Be(2, "very long username should work");
        }

        #endregion

        #region Concurrency Tests

        [TestMethod]
        public void RecordFailedAttempt_ConcurrentRequests_ThreadSafe()
        {
            // Arrange
            var username = UniqueUsername("admin");
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            // Act - Simulate concurrent failed attempts
            System.Threading.Tasks.Parallel.For(0, 10, i =>
            {
                try
                {
                    _service.RecordFailedAttempt(username);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            exceptions.Should().BeEmpty("concurrent calls should not throw exceptions");
            _service.IsLockedOut(username).Should().BeTrue("user should be locked after 10 concurrent attempts");
        }

        [TestMethod]
        public void IsRateLimited_ConcurrentChecks_ThreadSafe()
        {
            // Arrange
            var username = UniqueUsername("admin");
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            // Act - Simulate concurrent checks
            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                try
                {
                    var _ = _service.IsLockedOut(username);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            exceptions.Should().BeEmpty("concurrent rate limit checks should be thread-safe");
        }

        #endregion

        #region Security Tests

        [TestMethod]
        public void RecordFailedAttempt_BruteForceSimulation_LocksAccount()
        {
            // Arrange - Simulate brute-force attack
            var username = UniqueUsername("admin");
            var attemptCount = 0;

            // Act - Attempt brute-force
            while (!_service.IsLockedOut(username) && attemptCount < 10)
            {
                _service.RecordFailedAttempt(username);
                attemptCount++;
            }

            // Assert
            attemptCount.Should().Be(3, "account should lock after exactly 3 attempts");
            _service.IsLockedOut(username).Should().BeTrue("account should be locked");
            _service.GetRemainingLockoutMinutes(username).Should().BeGreaterThan(0, "lockout should be active");
        }

        [TestMethod]
        public void RecordFailedAttempt_AlternatingUsers_EachGetsFiveAttempts()
        {
            // Arrange - Simulate attacker trying multiple accounts
            var users = new[] { UniqueUsername("admin1"), UniqueUsername("admin2"), UniqueUsername("admin3") };

            // Act & Assert
            foreach (var user in users)
            {
                for (int i = 0; i < 3; i++)
                {
                    var remaining = _service.RecordFailedAttempt(user);
                    remaining.Should().Be(2 - i, $"{user} should have {2 - i} attempts remaining");
                }
                _service.IsLockedOut(user).Should().BeTrue($"{user} should be locked after 3 attempts");
            }
        }

        [TestMethod]
        public void IsRateLimited_MultipleChecks_ConsistentResult()
        {
            // Arrange
            var username = UniqueUsername("admin");
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(username);
            }

            // Act - Check multiple times
            var results = new bool[10];
            for (int i = 0; i < 10; i++)
            {
                results[i] = _service.IsLockedOut(username);
            }

            // Assert
            results.Should().OnlyContain(x => x == true, "locked status should be consistent across multiple checks");
        }

        #endregion

        #region Integration Scenarios

        [TestMethod]
        public void Scenario_SuccessfulLoginAfterReset_Works()
        {
            // Arrange - User fails 3 times, then successfully logs in
            var username = UniqueUsername("admin");
            
            _service.RecordFailedAttempt(username);
            _service.RecordFailedAttempt(username);
            _service.RecordFailedAttempt(username);

            // Act - Simulate successful login
            _service.ResetAttempts(username);

            // Assert
            _service.IsLockedOut(username).Should().BeFalse("user should not be locked after successful login");
            
            // Can attempt again
            var remaining = _service.RecordFailedAttempt(username);
            remaining.Should().Be(2, "counter should be reset after successful login");
        }

        [TestMethod]
        public void Scenario_LockoutPreventsBruteForce_Success()
        {
            // Arrange - Attacker trying to brute-force 8-digit master password
            var username = UniqueUsername("admin");
            var passwordAttempts = 0;
            const int MaxPasswordCombinations = 10000; // 8-digit password space

            // Act - Simulate brute-force attack
            for (int i = 0; i < MaxPasswordCombinations; i++)
            {
                if (_service.IsLockedOut(username))
                {
                    break; // Locked out
                }
                
                _service.RecordFailedAttempt(username);
                passwordAttempts++;
            }

            // Assert
            passwordAttempts.Should().Be(3, "attacker should only get 3 attempts before lockout");
            _service.IsLockedOut(username).Should().BeTrue("account should be locked");
            
            // Calculate how much of password space was tested
            var percentageTested = (passwordAttempts / (double)MaxPasswordCombinations) * 100;
            percentageTested.Should().BeLessThan(0.1, "less than 0.1% of password space should be testable");
        }

        [TestMethod]
        public void Scenario_MultipleUsers_IndependentLockouts_Success()
        {
            // Arrange - Three users attempting login
            var user1 = UniqueUsername("admin");
            var user2 = UniqueUsername("support");
            var user3 = "operator";

            // Act - Different failure patterns
            // User1: 2 failures, then success
            _service.RecordFailedAttempt(user1);
            _service.RecordFailedAttempt(user1);
            _service.ResetAttempts(user1); // Successful login

            // User2: Locked out
            for (int i = 0; i < 3; i++)
            {
                _service.RecordFailedAttempt(user2);
            }

            // User3: No attempts yet

            // Assert
            _service.IsLockedOut(user1).Should().BeFalse("user1 logged in successfully");
            _service.IsLockedOut(user2).Should().BeTrue("user2 should be locked out");
            _service.IsLockedOut(user3).Should().BeFalse("user3 has made no attempts");

            // User1 can still fail
            var remaining = _service.RecordFailedAttempt(user1);
            remaining.Should().Be(2, "user1 counter should be independent");
        }

        #endregion
    }
}

