using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using Photobooth.Services;
using System;
using System.Text;

namespace Photobooth.Tests.Services
{
    /// <summary>
    /// Tests for MasterPasswordService - cryptographic operations for master password generation and validation
    /// </summary>
    [TestClass]
    public class MasterPasswordServiceTests
    {
        private MasterPasswordService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new MasterPasswordService();
        }

        #region Private Key Generation Tests

        [TestMethod]
        public void GeneratePrivateKey_ValidInputs_ReturnsNonEmptyKey()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";

            // Act
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Assert
            privateKey.Should().NotBeNullOrEmpty("private key should be generated");
            privateKey.Length.Should().Be(32, "PBKDF2 with SHA256 produces 32-byte key");
        }

        [TestMethod]
        public void GeneratePrivateKey_SameInputs_ProducesSameKey()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";

            // Act
            var key1 = _service.DerivePrivateKey(baseSecret, macAddress);
            var key2 = _service.DerivePrivateKey(baseSecret, macAddress);

            // Assert
            key1.Should().BeEquivalentTo(key2, "same inputs should produce deterministic output");
        }

        [TestMethod]
        public void GeneratePrivateKey_DifferentBaseSecret_ProducesDifferentKey()
        {
            // Arrange
            var baseSecret1 = "test-secret-1234567890123456789012345678901";
            var baseSecret2 = "different-secret-1234567890123456789012345";
            var macAddress = "00:11:22:33:44:55";

            // Act
            var key1 = _service.DerivePrivateKey(baseSecret1, macAddress);
            var key2 = _service.DerivePrivateKey(baseSecret2, macAddress);

            // Assert
            key1.Should().NotBeEquivalentTo(key2, "different base secrets should produce different keys");
        }

        [TestMethod]
        public void GeneratePrivateKey_DifferentMacAddress_ProducesDifferentKey()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress1 = "00:11:22:33:44:55";
            var macAddress2 = "AA:BB:CC:DD:EE:FF";

            // Act
            var key1 = _service.DerivePrivateKey(baseSecret, macAddress1);
            var key2 = _service.DerivePrivateKey(baseSecret, macAddress2);

            // Assert
            key1.Should().NotBeEquivalentTo(key2, "different MAC addresses should produce different keys");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GeneratePrivateKey_NullBaseSecret_ThrowsArgumentNullException()
        {
            // Arrange
            string baseSecret = null!;
            var macAddress = "00:11:22:33:44:55";

            // Act
            _service.DerivePrivateKey(baseSecret, macAddress);

            // Assert - ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GeneratePrivateKey_NullMacAddress_ThrowsArgumentNullException()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            string macAddress = null!;

            // Act
            _service.DerivePrivateKey(baseSecret, macAddress);

            // Assert - ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GeneratePrivateKey_EmptyBaseSecret_ProducesValidKey()
        {
            // Arrange
            var baseSecret = "";
            var macAddress = "00:11:22:33:44:55";

            // Act
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Assert - ExpectedException (empty base secret should be rejected for security)
        }

        [TestMethod]
        public void GeneratePrivateKey_MacAddressWithDifferentFormats_ProducesDifferentKeys()
        {
            // Arrange - MAC addresses with different separators
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddressColons = "00:11:22:33:44:55";
            var macAddressDashes = "00-11-22-33-44-55";

            // Act
            var key1 = _service.DerivePrivateKey(baseSecret, macAddressColons);
            var key2 = _service.DerivePrivateKey(baseSecret, macAddressDashes);

            // Assert
            key1.Should().NotBeEquivalentTo(key2, "MAC format is treated as-is (case-sensitive)");
        }

        #endregion

        #region Password Generation Tests

        [TestMethod]
        public void GeneratePassword_ValidInputs_Returns8DigitPassword()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act
            var (password, nonce) = _service.GeneratePassword(privateKey, macAddress);

            // Assert
            password.Should().NotBeNullOrEmpty("password should be generated");
            password.Should().HaveLength(8, "password should be 8 digits");
            password.Should().MatchRegex(@"^\d{8}$", "password should be numeric only");
        }

        [TestMethod]
        public void GeneratePassword_ValidInputs_Returns4DigitNonce()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act
            var (password, nonce) = _service.GeneratePassword(privateKey, macAddress);

            // Assert
            nonce.Should().NotBeNullOrEmpty("nonce should be generated");
            nonce.Should().HaveLength(4, "nonce should be 4 digits");
            nonce.Should().MatchRegex(@"^\d{4}$", "nonce should be numeric only");
        }

        [TestMethod]
        public void GeneratePassword_MultipleGenerations_ProducesDifferentPasswords()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act - Generate 10 passwords
            var passwords = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                var (password, _) = _service.GeneratePassword(privateKey, macAddress);
                passwords.Add(password);
            }

            // Assert
            passwords.Count.Should().BeGreaterThan(1, "random nonces should produce different passwords");
        }

        [TestMethod]
        public void GeneratePassword_NonceIsPartOfPassword()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act
            var (password, nonce) = _service.GeneratePassword(privateKey, macAddress);

            // Assert
            password.Should().StartWith(nonce, "password should start with nonce");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GeneratePassword_NullPrivateKey_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] privateKey = null!;
            var macAddress = "00:11:22:33:44:55";

            // Act
            _service.GeneratePassword(privateKey, macAddress);

            // Assert - ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GeneratePassword_NullMacAddress_ThrowsArgumentNullException()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act
            _service.GeneratePassword(privateKey, null!);

            // Assert - ExpectedException
        }

        #endregion

        #region Password Validation Tests

        [TestMethod]
        public void ValidatePassword_ValidPassword_ReturnsTrue()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var (password, expectedNonce) = _service.GeneratePassword(privateKey, macAddress);

            // Act
            var (isValid, actualNonce) = _service.ValidatePassword(password, privateKey, macAddress);

            // Assert
            isValid.Should().BeTrue("generated password should be valid");
            actualNonce.Should().Be(expectedNonce, "nonce should be extracted correctly");
        }

        [TestMethod]
        public void ValidatePassword_InvalidPassword_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var invalidPassword = "12345678"; // Random 8 digits

            // Act
            var (isValid, nonce) = _service.ValidatePassword(invalidPassword, privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("random password should not validate");
            nonce.Should().BeNull("nonce should be null for invalid password");
        }

        [TestMethod]
        public void ValidatePassword_TamperedPassword_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var (password, _) = _service.GeneratePassword(privateKey, macAddress);
            
            // Tamper with the password (change last digit)
            var lastDigit = int.Parse(password.Substring(7, 1));
            var tamperedDigit = (lastDigit + 1) % 10;
            var tamperedPassword = password.Substring(0, 7) + tamperedDigit;

            // Act
            var (isValid, nonce) = _service.ValidatePassword(tamperedPassword, privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("tampered password should not validate");
            nonce.Should().BeNull("nonce should be null for tampered password");
        }

        [TestMethod]
        public void ValidatePassword_WrongMacAddress_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var correctMac = "00:11:22:33:44:55";
            var wrongMac = "AA:BB:CC:DD:EE:FF";
            var privateKey = _service.DerivePrivateKey(baseSecret, correctMac);
            var (password, _) = _service.GeneratePassword(privateKey, correctMac);

            // Act
            var (isValid, nonce) = _service.ValidatePassword(password, privateKey, wrongMac);

            // Assert
            isValid.Should().BeFalse("password for different MAC should not validate");
            nonce.Should().BeNull();
        }

        [TestMethod]
        public void ValidatePassword_WrongPrivateKey_ReturnsFalse()
        {
            // Arrange
            var baseSecret1 = "test-secret-12345678901234567890123456789012";
            var baseSecret2 = "different-secret-1234567890123456789012345";
            var macAddress = "00:11:22:33:44:55";
            
            var correctKey = _service.DerivePrivateKey(baseSecret1, macAddress);
            var wrongKey = _service.DerivePrivateKey(baseSecret2, macAddress);
            
            var (password, _) = _service.GeneratePassword(correctKey, macAddress);

            // Act
            var (isValid, nonce) = _service.ValidatePassword(password, wrongKey, macAddress);

            // Assert
            isValid.Should().BeFalse("password with wrong key should not validate");
            nonce.Should().BeNull();
        }

        [TestMethod]
        public void ValidatePassword_TooShort_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var shortPassword = "1234567"; // 7 digits

            // Act
            var (isValid, nonce) = _service.ValidatePassword(shortPassword, privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("short password should not validate");
            nonce.Should().BeNull();
        }

        [TestMethod]
        public void ValidatePassword_TooLong_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var longPassword = "123456789"; // 9 digits

            // Act
            var (isValid, nonce) = _service.ValidatePassword(longPassword, privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("long password should not validate");
            nonce.Should().BeNull();
        }

        [TestMethod]
        public void ValidatePassword_NonNumeric_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var alphaPassword = "abcd1234"; // Contains letters

            // Act
            var (isValid, nonce) = _service.ValidatePassword(alphaPassword, privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("non-numeric password should not validate");
            nonce.Should().BeNull();
        }

        [TestMethod]
        public void ValidatePassword_EmptyString_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act
            var (isValid, nonce) = _service.ValidatePassword("", privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("empty password should not validate");
            nonce.Should().BeNull();
        }

        [TestMethod]
        public void ValidatePassword_NullPassword_ReturnsFalse()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act
            var (isValid, nonce) = _service.ValidatePassword(null!, privateKey, macAddress);

            // Assert
            isValid.Should().BeFalse("null password should not validate");
            nonce.Should().BeNull();
        }

        #endregion

        #region MAC Address Tests

        [TestMethod]
        public void GetMacAddress_ReturnsNonEmpty()
        {
            // Act
            var macAddress = _service.GetMacAddress();

            // Assert - System-dependent, might be null on some systems
            if (macAddress != null)
            {
                macAddress.Should().NotBeEmpty("MAC address should not be empty if found");
                macAddress.Should().Contain(":", "MAC address should be formatted with colons");
            }
        }

        [TestMethod]
        public void GetMacAddress_ReturnsConsistentValue()
        {
            // Act
            var mac1 = _service.GetMacAddress();
            var mac2 = _service.GetMacAddress();

            // Assert
            mac1.Should().Be(mac2, "MAC address should be consistent across calls");
        }

        #endregion

        #region Cryptographic Quality Tests

        [TestMethod]
        public void GeneratePassword_NonceDistribution_IsUniform()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);
            var nonces = new System.Collections.Generic.Dictionary<string, int>();

            // Act - Generate 1000 passwords
            for (int i = 0; i < 1000; i++)
            {
                var (_, nonce) = _service.GeneratePassword(privateKey, macAddress);
                if (!nonces.ContainsKey(nonce))
                    nonces[nonce] = 0;
                nonces[nonce]++;
            }

            // Assert
            nonces.Count.Should().BeGreaterThan(900, "nonces should be highly random (>90% unique in 1000 samples)");
            nonces.Values.Max().Should().BeLessOrEqualTo(5, "no nonce should appear more than a few times");
        }

        [TestMethod]
        public void GeneratePassword_NoModuloBias_AllDigitsValid()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act - Generate 100 passwords
            for (int i = 0; i < 100; i++)
            {
                var (password, nonce) = _service.GeneratePassword(privateKey, macAddress);

                // Assert
                password.Should().MatchRegex(@"^\d{8}$", "password should always be 8 digits");
                nonce.Should().MatchRegex(@"^\d{4}$", "nonce should always be 4 digits");
                
                // Verify no negative values or invalid ranges (would indicate Math.Abs edge case)
                password.Should().NotContain("-", "password should never contain negative sign");
                int.Parse(nonce).Should().BeInRange(0, 9999, "nonce should be in valid range");
            }
        }

        [TestMethod]
        public void GeneratePassword_PaddingWorks_LeadingZerosPreserved()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macAddress = "00:11:22:33:44:55";
            var privateKey = _service.DerivePrivateKey(baseSecret, macAddress);

            // Act - Generate many passwords to increase chance of getting one with leading zeros
            var foundLeadingZero = false;
            for (int i = 0; i < 100; i++)
            {
                var (password, nonce) = _service.GeneratePassword(privateKey, macAddress);
                
                // Check if password or nonce starts with '0'
                if (password.StartsWith("0") || nonce.StartsWith("0"))
                {
                    foundLeadingZero = true;
                    password.Should().HaveLength(8, "password with leading zero should still be 8 chars");
                    nonce.Should().HaveLength(4, "nonce with leading zero should still be 4 chars");
                }
            }

            // Assert - At least one should have leading zeros (statistical check)
            foundLeadingZero.Should().BeTrue("at least one password should have leading zeros in 100 attempts");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void GeneratePrivateKey_LongBaseSecret_ProducesValidKey()
        {
            // Arrange - 128-character base secret
            var longSecret = new string('a', 128);
            var macAddress = "00:11:22:33:44:55";

            // Act
            var privateKey = _service.DerivePrivateKey(longSecret, macAddress);

            // Assert
            privateKey.Should().NotBeNullOrEmpty();
            privateKey.Length.Should().Be(32);
        }

        [TestMethod]
        public void GeneratePrivateKey_SpecialCharactersInInputs_ProducesValidKey()
        {
            // Arrange
            var specialSecret = "test!@#$%^&*()_+-=[]{}|;':\",./<>?`~";
            var macAddress = "00:11:22:33:44:55";

            // Act
            var privateKey = _service.DerivePrivateKey(specialSecret, macAddress);

            // Assert
            privateKey.Should().NotBeNullOrEmpty();
            privateKey.Length.Should().Be(32);
        }

        [TestMethod]
        public void GeneratePrivateKey_UnicodeCharacters_ProducesValidKey()
        {
            // Arrange
            var unicodeSecret = "тест-секрет-密码-🔐-test"; // Cyrillic, Chinese, Emoji
            var macAddress = "00:11:22:33:44:55";

            // Act
            var privateKey = _service.DerivePrivateKey(unicodeSecret, macAddress);

            // Assert
            privateKey.Should().NotBeNullOrEmpty();
            privateKey.Length.Should().Be(32);
        }

        [TestMethod]
        public void ValidatePassword_CaseInsensitiveMac_PassesValidation()
        {
            // Arrange
            var baseSecret = "test-secret-12345678901234567890123456789012";
            var macLower = "aa:bb:cc:dd:ee:ff";
            var macUpper = "AA:BB:CC:DD:EE:FF";
            var privateKey = _service.DerivePrivateKey(baseSecret, macLower);
            var (password, _) = _service.GeneratePassword(privateKey, macLower);

            // Act - Validate with different case
            var (isValid, _) = _service.ValidatePassword(password, privateKey, macUpper);

            // Assert
            isValid.Should().BeTrue("MAC address validation is case-insensitive (normalized to uppercase)");
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void EndToEnd_GenerateAndValidate_Success()
        {
            // Arrange - Simulate support tool generating password
            var baseSecret = "production-secret-1234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            
            // Support tool generates private key and password
            var privateKey = _service.DerivePrivateKey(baseSecret, kioskMac);
            var (generatedPassword, generatedNonce) = _service.GeneratePassword(privateKey, kioskMac);

            // Act - Simulate kiosk validating the password
            var (isValid, extractedNonce) = _service.ValidatePassword(generatedPassword, privateKey, kioskMac);

            // Assert
            isValid.Should().BeTrue("generated password should validate on kiosk");
            extractedNonce.Should().Be(generatedNonce, "nonce should match");
        }

        [TestMethod]
        public void EndToEnd_DifferentKiosk_Fails()
        {
            // Arrange
            var baseSecret = "production-secret-1234567890123456789012345678";
            var kiosk1Mac = "00:15:5D:01:02:03";
            var kiosk2Mac = "00:15:5D:04:05:06";
            
            // Generate password for kiosk 1
            var key1 = _service.DerivePrivateKey(baseSecret, kiosk1Mac);
            var (password, _) = _service.GeneratePassword(key1, kiosk1Mac);

            // Try to use password on kiosk 2
            var key2 = _service.DerivePrivateKey(baseSecret, kiosk2Mac);

            // Act
            var (isValid, _) = _service.ValidatePassword(password, key2, kiosk2Mac);

            // Assert
            isValid.Should().BeFalse("password for kiosk 1 should not work on kiosk 2");
        }

        [TestMethod]
        public void EndToEnd_MultiplePasswordsForSameKiosk_AllValidate()
        {
            // Arrange
            var baseSecret = "production-secret-1234567890123456789012345678";
            var kioskMac = "00:15:5D:01:02:03";
            var privateKey = _service.DerivePrivateKey(baseSecret, kioskMac);

            // Act - Generate and validate 10 different passwords
            for (int i = 0; i < 10; i++)
            {
                var (password, nonce) = _service.GeneratePassword(privateKey, kioskMac);
                var (isValid, extractedNonce) = _service.ValidatePassword(password, privateKey, kioskMac);

                // Assert
                isValid.Should().BeTrue($"password {i + 1} should validate");
                extractedNonce.Should().Be(nonce, $"nonce {i + 1} should match");
            }
        }

        #endregion
    }
}

