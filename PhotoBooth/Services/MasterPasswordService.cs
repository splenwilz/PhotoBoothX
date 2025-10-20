using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace Photobooth.Services
{
    /// <summary>
    /// Service for generating and validating machine-specific master passwords for temporary admin access.
    /// Uses PBKDF2 for key derivation and HMAC-SHA256 for password generation.
    /// Passwords are 8 digits: [4-digit nonce][4-digit HMAC-derived]
    /// </summary>
    public class MasterPasswordService
    {
        private const int PBKDF2_ITERATIONS = 100000; // Industry standard for PBKDF2
        private const int DERIVED_KEY_LENGTH = 32; // 256 bits
        private const int NONCE_LENGTH = 4; // 4 digits (0000-9999)
        private const int HMAC_LENGTH = 4; // 4 digits from HMAC

        /// <summary>
        /// Derives a machine-specific private key from base secret and MAC address using PBKDF2.
        /// This ensures each kiosk has a unique key even with the same base secret.
        /// </summary>
        /// <param name="baseSecret">Shared base secret (kept secure by support team)</param>
        /// <param name="macAddress">MAC address of the kiosk (e.g., "00:1A:2B:3C:4D:5E")</param>
        /// <returns>32-byte derived key</returns>
        public byte[] DerivePrivateKey(string baseSecret, string macAddress)
        {
            if (string.IsNullOrWhiteSpace(baseSecret))
                throw new ArgumentException("Base secret cannot be empty", nameof(baseSecret));
            
            if (string.IsNullOrWhiteSpace(macAddress))
                throw new ArgumentException("MAC address cannot be empty", nameof(macAddress));

            // Combine base secret with MAC address for machine-specific derivation
            var data = Encoding.UTF8.GetBytes(baseSecret + "|" + macAddress.ToUpperInvariant());
            
            // Use PBKDF2 with HMAC-SHA256 for key derivation
            using var pbkdf2 = new Rfc2898DeriveBytes(
                data, 
                Encoding.UTF8.GetBytes("PhotoBoothX.MasterPassword.v1"), // Salt
                PBKDF2_ITERATIONS,
                HashAlgorithmName.SHA256
            );
            
            return pbkdf2.GetBytes(DERIVED_KEY_LENGTH);
        }

        /// <summary>
        /// Generates an 8-digit master password using the private key.
        /// Format: [4-digit nonce][4-digit HMAC]
        /// Example: "12345678" where "1234" is nonce and "5678" is derived from HMAC
        /// </summary>
        /// <param name="privateKey">32-byte private key from DerivePrivateKey</param>
        /// <param name="macAddress">MAC address to include in HMAC computation</param>
        /// <returns>8-digit password and the nonce used (for tracking)</returns>
        public (string password, string nonce) GeneratePassword(byte[] privateKey, string macAddress)
        {
            if (privateKey == null || privateKey.Length != DERIVED_KEY_LENGTH)
                throw new ArgumentException($"Private key must be {DERIVED_KEY_LENGTH} bytes", nameof(privateKey));

            if (string.IsNullOrWhiteSpace(macAddress))
                throw new ArgumentException("MAC address cannot be empty", nameof(macAddress));

            // Generate cryptographically secure random 4-digit nonce
            var nonce = GenerateSecureNonce();

            // Compute HMAC with nonce and MAC address
            var data = Encoding.UTF8.GetBytes(nonce + "|" + macAddress.ToUpperInvariant());
            using var hmac = new HMACSHA256(privateKey);
            var hash = hmac.ComputeHash(data);

            // Extract 4 digits from HMAC (unsigned for guaranteed positive, no Math.Abs overflow)
            var hmacUInt = BitConverter.ToUInt32(hash, 0);
            var hmacDigits = (hmacUInt % 10000u).ToString("D4");

            var password = nonce + hmacDigits;

            return (password, nonce);
        }

        /// <summary>
        /// Validates an 8-digit master password against the private key.
        /// Extracts nonce from password and re-computes HMAC to verify authenticity.
        /// </summary>
        /// <param name="password">8-digit password to validate</param>
        /// <param name="privateKey">32-byte private key from DerivePrivateKey</param>
        /// <param name="macAddress">MAC address of the kiosk</param>
        /// <returns>Validation result with nonce if successful</returns>
        public (bool isValid, string? nonce) ValidatePassword(string password, byte[] privateKey, string macAddress)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length != 8)
                return (false, null);

            if (privateKey == null || privateKey.Length != DERIVED_KEY_LENGTH)
                return (false, null);

            if (string.IsNullOrWhiteSpace(macAddress))
                return (false, null);

            // Extract nonce (first 4 digits) and HMAC part (last 4 digits)
            var nonce = password.Substring(0, NONCE_LENGTH);
            var providedHmac = password.Substring(NONCE_LENGTH, HMAC_LENGTH);

            // Re-compute HMAC with the extracted nonce
            var data = Encoding.UTF8.GetBytes(nonce + "|" + macAddress.ToUpperInvariant());
            using var hmac = new HMACSHA256(privateKey);
            var hash = hmac.ComputeHash(data);

            // Extract expected HMAC digits (unsigned for guaranteed positive)
            var hmacUInt = BitConverter.ToUInt32(hash, 0);
            var expectedHmac = (hmacUInt % 10000u).ToString("D4");

            // Constant-time comparison to prevent timing attacks
            var isValid = ConstantTimeCompare(providedHmac, expectedHmac);

            return (isValid, isValid ? nonce : null);
        }

        /// <summary>
        /// Gets the MAC address of the first active network interface.
        /// Prioritizes Ethernet, then WiFi, then any available interface.
        /// </summary>
        /// <returns>MAC address in format "00:1A:2B:3C:4D:5E" or null if none found</returns>
        public string? GetMacAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up 
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderByDescending(ni => 
                        ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 2 :
                        ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0)
                    .ToList();

                var primaryInterface = interfaces.FirstOrDefault();
                if (primaryInterface == null)
                    return null;

                var mac = primaryInterface.GetPhysicalAddress().ToString();
                
                // Format as XX:XX:XX:XX:XX:XX
                if (mac.Length == 12)
                {
                    return string.Join(":", Enumerable.Range(0, 6)
                        .Select(i => mac.Substring(i * 2, 2)));
                }

                return mac;
            }
            catch
            {
                return null;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Generates a cryptographically secure 4-digit random nonce (0000-9999)
        /// </summary>
        private string GenerateSecureNonce()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            var randomInt = BitConverter.ToInt32(bytes, 0);
            var nonce = (Math.Abs(randomInt) % 10000).ToString().PadLeft(NONCE_LENGTH, '0');
            
            return nonce;
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks
        /// </summary>
        private bool ConstantTimeCompare(string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        #endregion
    }
}

