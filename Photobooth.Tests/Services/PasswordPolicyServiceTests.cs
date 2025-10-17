using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;

namespace Photobooth.Tests.Services
{
    /// <summary>
    /// Tests for PasswordPolicyService - centralized password validation
    /// </summary>
    [TestClass]
    public class PasswordPolicyServiceTests
    {
        #region Valid Password Tests

        [TestMethod]
        public void ValidatePassword_ValidPassword_ReturnsAllTrue()
        {
            // Arrange
            var password = "Test123!";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeTrue("password meets all requirements");
            result.MeetsLengthRequirement.Should().BeTrue("password is 8+ characters");
            result.HasUppercase.Should().BeTrue("password has uppercase");
            result.HasLowercase.Should().BeTrue("password has lowercase");
            result.HasNumber.Should().BeTrue("password has number");
        }

        [TestMethod]
        public void ValidatePassword_MinimumValid_ReturnsTrue()
        {
            // Arrange - Exactly 8 chars with upper, lower, and number
            var password = "Abcdef12";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeTrue("password meets minimum requirements");
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasUppercase.Should().BeTrue();
            result.HasLowercase.Should().BeTrue();
            result.HasNumber.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_LongPassword_ReturnsTrue()
        {
            // Arrange
            var password = "ThisIsAVeryLongPassword123WithLotsOfCharacters";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeTrue("long password should be valid");
            result.MeetsLengthRequirement.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_WithSpecialChars_ReturnsTrue()
        {
            // Arrange - Special chars are allowed but not required
            var password = "Test123!@#$%";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeTrue("special characters should be allowed");
        }

        #endregion

        #region Length Requirement Tests

        [TestMethod]
        public void ValidatePassword_TooShort_FailsLengthRequirement()
        {
            // Arrange - Only 7 chars
            var password = "Test12A";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse("password is too short");
            result.MeetsLengthRequirement.Should().BeFalse("password is less than 8 characters");
        }

        [TestMethod]
        public void ValidatePassword_Exactly8Chars_PassesLengthRequirement()
        {
            // Arrange
            var password = "Test123A";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.MeetsLengthRequirement.Should().BeTrue("8 characters should pass");
        }

        [TestMethod]
        public void ValidatePassword_EmptyString_FailsLengthRequirement()
        {
            // Arrange
            var password = "";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeFalse();
            result.HasUppercase.Should().BeFalse();
            result.HasLowercase.Should().BeFalse();
            result.HasNumber.Should().BeFalse();
        }

        [TestMethod]
        public void ValidatePassword_NullString_ReturnsAllFalse()
        {
            // Arrange
            string password = null!;

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeFalse();
            result.HasUppercase.Should().BeFalse();
            result.HasLowercase.Should().BeFalse();
            result.HasNumber.Should().BeFalse();
        }

        #endregion

        #region Uppercase Requirement Tests

        [TestMethod]
        public void ValidatePassword_NoUppercase_FailsUppercaseRequirement()
        {
            // Arrange
            var password = "testpass123";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse("missing uppercase");
            result.HasUppercase.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasLowercase.Should().BeTrue();
            result.HasNumber.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_MultipleUppercase_PassesUppercaseRequirement()
        {
            // Arrange
            var password = "TESTPASS123";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasUppercase.Should().BeTrue("multiple uppercase should pass");
        }

        [TestMethod]
        public void ValidatePassword_OneUppercase_PassesUppercaseRequirement()
        {
            // Arrange
            var password = "testPass123";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasUppercase.Should().BeTrue("single uppercase should pass");
        }

        #endregion

        #region Lowercase Requirement Tests

        [TestMethod]
        public void ValidatePassword_NoLowercase_FailsLowercaseRequirement()
        {
            // Arrange
            var password = "TESTPASS123";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse("missing lowercase");
            result.HasLowercase.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasUppercase.Should().BeTrue();
            result.HasNumber.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_MultipleLowercase_PassesLowercaseRequirement()
        {
            // Arrange
            var password = "testpass123";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasLowercase.Should().BeTrue("multiple lowercase should pass");
        }

        [TestMethod]
        public void ValidatePassword_OneLowercase_PassesLowercaseRequirement()
        {
            // Arrange
            var password = "TESTpASS123";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasLowercase.Should().BeTrue("single lowercase should pass");
        }

        #endregion

        #region Number Requirement Tests

        [TestMethod]
        public void ValidatePassword_NoNumber_FailsNumberRequirement()
        {
            // Arrange
            var password = "TestPassword";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse("missing number");
            result.HasNumber.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasUppercase.Should().BeTrue();
            result.HasLowercase.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_MultipleNumbers_PassesNumberRequirement()
        {
            // Arrange
            var password = "TestPass123456";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasNumber.Should().BeTrue("multiple numbers should pass");
        }

        [TestMethod]
        public void ValidatePassword_OneNumber_PassesNumberRequirement()
        {
            // Arrange
            var password = "TestPassword1";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasNumber.Should().BeTrue("single number should pass");
        }

        [TestMethod]
        public void ValidatePassword_NumberAtStart_PassesNumberRequirement()
        {
            // Arrange
            var password = "1TestPassword";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasNumber.Should().BeTrue("number at start should pass");
        }

        [TestMethod]
        public void ValidatePassword_NumberInMiddle_PassesNumberRequirement()
        {
            // Arrange
            var password = "Test1Password";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.HasNumber.Should().BeTrue("number in middle should pass");
        }

        #endregion

        #region Combined Failure Tests

        [TestMethod]
        public void ValidatePassword_OnlyNumbers_FailsMultipleRequirements()
        {
            // Arrange
            var password = "12345678";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasUppercase.Should().BeFalse("no uppercase letters");
            result.HasLowercase.Should().BeFalse("no lowercase letters");
            result.HasNumber.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_OnlyLowercase_FailsMultipleRequirements()
        {
            // Arrange
            var password = "testpassword";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasUppercase.Should().BeFalse("no uppercase");
            result.HasLowercase.Should().BeTrue();
            result.HasNumber.Should().BeFalse("no numbers");
        }

        [TestMethod]
        public void ValidatePassword_OnlyUppercase_FailsMultipleRequirements()
        {
            // Arrange
            var password = "TESTPASSWORD";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue();
            result.HasUppercase.Should().BeTrue();
            result.HasLowercase.Should().BeFalse("no lowercase");
            result.HasNumber.Should().BeFalse("no numbers");
        }

        [TestMethod]
        public void ValidatePassword_ShortWithAllTypes_FailsLength()
        {
            // Arrange - Only 7 chars but has upper, lower, number
            var password = "Test12A";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse("too short despite having all character types");
            result.MeetsLengthRequirement.Should().BeFalse();
            result.HasUppercase.Should().BeTrue();
            result.HasLowercase.Should().BeTrue();
            result.HasNumber.Should().BeTrue();
        }

        #endregion

        #region Requirements Description Test

        [TestMethod]
        public void GetRequirementsDescription_ReturnsFormattedString()
        {
            // Act
            var description = PasswordPolicyService.GetRequirementsDescription();

            // Assert
            description.Should().NotBeNullOrEmpty();
            description.Should().Contain("8 characters", "should mention length requirement");
            description.Should().Contain("uppercase", "should mention uppercase requirement");
            description.Should().Contain("lowercase", "should mention lowercase requirement");
            description.Should().Contain("number", "should mention number requirement");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void ValidatePassword_UnicodeCharacters_HandledCorrectly()
        {
            // Arrange - Unicode characters
            var password = "Tëst123Pāss";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.MeetsLengthRequirement.Should().BeTrue("unicode chars count toward length");
            result.HasNumber.Should().BeTrue("numbers should still be detected");
        }

        [TestMethod]
        public void ValidatePassword_WhitespaceAllowed_ReturnsTrue()
        {
            // Arrange - Password with spaces
            var password = "Test 123 Pass";

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeTrue("whitespace should be allowed");
            result.MeetsLengthRequirement.Should().BeTrue();
        }

        [TestMethod]
        public void ValidatePassword_OnlyWhitespace_FailsAllRequirements()
        {
            // Arrange
            var password = "        "; // 8 spaces

            // Act
            var result = PasswordPolicyService.ValidatePassword(password);

            // Assert
            result.IsValid.Should().BeFalse();
            result.MeetsLengthRequirement.Should().BeTrue("length is 8");
            result.HasUppercase.Should().BeFalse();
            result.HasLowercase.Should().BeFalse();
            result.HasNumber.Should().BeFalse();
        }

        #endregion
    }
}

