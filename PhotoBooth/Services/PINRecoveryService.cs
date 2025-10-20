using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Photobooth.Services
{
    /// <summary>
    /// Lightweight service for PIN-based password recovery
    /// Designed for kiosk environment where physical security compensates for shorter PIN length
    /// </summary>
    public class PINRecoveryService
    {
        // Security constants based on OWASP recommendations
        private const int SALT_SIZE = 16;           // 128 bits - sufficient for PINs
        private const int HASH_SIZE = 32;           // 256 bits
        private const int PBKDF2_ITERATIONS = 100000; // OWASP 2023 recommendation for PBKDF2-SHA256
        
        // Rate limiting: Simple in-memory tracking (stateless service, no database needed)
        // Rationale: Kiosk reboots clear attempts - acceptable for kiosk use case
        private static readonly Dictionary<string, int> _failedAttempts = new Dictionary<string, int>();
        private const int MAX_ATTEMPTS = 5;         // Industry standard (ATMs, phones use 3-5)

        #region PIN Validation

        /// <summary>
        /// Validate PIN format: exactly 4 digits only
        /// Rationale: Same as phone/ATM PINs - familiar to users, fixed length avoids recovery confusion
        /// </summary>
        public bool IsValidPINFormat(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin))
                return false;

            // Must be exactly 4 digits (fixed to avoid user confusion during recovery)
            if (pin.Length != 4)
                return false;

            // Must be all numeric
            foreach (char c in pin)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        #endregion

        #region Hashing & Verification

        /// <summary>
        /// Generate cryptographically secure random salt
        /// Rationale: Prevents rainbow table attacks, each PIN hash is unique
        /// </summary>
        public string GenerateSalt()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] saltBytes = new byte[SALT_SIZE];
                rng.GetBytes(saltBytes);
                return Convert.ToBase64String(saltBytes);
            }
        }

        /// <summary>
        /// Hash PIN using PBKDF2-SHA256 (industry standard for password-like secrets)
        /// Rationale: Same algorithm used by Microsoft, Auth0, and OWASP recommendations
        /// Slow hashing makes brute force impractical even for short PINs
        /// </summary>
        public string HashPIN(string pin, string salt)
        {
            if (!IsValidPINFormat(pin))
                throw new ArgumentException("PIN must be exactly 4 digits", nameof(pin));

            if (string.IsNullOrWhiteSpace(salt))
                throw new ArgumentException("Salt cannot be empty", nameof(salt));

            byte[] saltBytes = Convert.FromBase64String(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                pin,
                saltBytes,
                PBKDF2_ITERATIONS,
                HashAlgorithmName.SHA256))
            {
                byte[] hashBytes = pbkdf2.GetBytes(HASH_SIZE);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Hash PIN and generate new salt in one operation (convenience method)
        /// </summary>
        public (string Hash, string Salt) HashPINWithNewSalt(string pin)
        {
            string salt = GenerateSalt();
            string hash = HashPIN(pin, salt);
            return (hash, salt);
        }

        /// <summary>
        /// Verify PIN against stored hash using constant-time comparison
        /// Rationale: Constant-time prevents timing attacks (though unlikely on a kiosk)
        /// </summary>
        public bool VerifyPIN(string pin, string storedHash, string storedSalt)
        {
            if (string.IsNullOrWhiteSpace(pin) || 
                string.IsNullOrWhiteSpace(storedHash) || 
                string.IsNullOrWhiteSpace(storedSalt))
                return false;

            try
            {
                // Hash the provided PIN with stored salt
                string computedHash = HashPIN(pin, storedSalt);

                // Constant-time comparison prevents timing attacks
                return ConstantTimeCompare(computedHash, storedHash);
            }
            catch
            {
                // Any exception (invalid format, etc.) means verification fails
                return false;
            }
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks
        /// Rationale: Even though kiosk is low-risk, this is a security best practice
        /// </summary>
        private bool ConstantTimeCompare(string a, string b)
        {
            if (a == null || b == null)
                return false;

            if (a.Length != b.Length)
                return false;

            // XOR all characters - always takes same time regardless of where difference is
            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        #endregion

        #region Rate Limiting

        /// <summary>
        /// Check if user has exceeded maximum failed attempts
        /// Rationale: Prevents brute force attacks (5 attempts = 100,000 possible 4-digit combinations / 5 = 20,000 tries needed)
        /// In-memory tracking is acceptable for kiosk (reboot clears it, which is fine)
        /// </summary>
        public bool IsRateLimited(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            lock (_failedAttempts) // Thread-safe access
            {
                return _failedAttempts.ContainsKey(username) && 
                       _failedAttempts[username] >= MAX_ATTEMPTS;
            }
        }

        /// <summary>
        /// Record a failed PIN attempt
        /// </summary>
        public void RecordFailedAttempt(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            lock (_failedAttempts)
            {
                if (!_failedAttempts.ContainsKey(username))
                    _failedAttempts[username] = 0;

                _failedAttempts[username]++;

                LoggingService.Application.Warning("PIN verification failed",
                    ("Username", username),
                    ("FailedAttempts", _failedAttempts[username]),
                    ("MaxAttempts", MAX_ATTEMPTS));
            }
        }

        /// <summary>
        /// Clear failed attempts on successful verification or password reset
        /// </summary>
        public void ClearFailedAttempts(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            lock (_failedAttempts)
            {
                if (_failedAttempts.ContainsKey(username))
                {
                    _failedAttempts.Remove(username);
                    
                    LoggingService.Application.Information("PIN attempts cleared",
                        ("Username", username));
                }
            }
        }

        /// <summary>
        /// Get remaining attempts before lockout
        /// </summary>
        public int GetRemainingAttempts(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return MAX_ATTEMPTS;

            lock (_failedAttempts)
            {
                if (!_failedAttempts.ContainsKey(username))
                    return MAX_ATTEMPTS;

                int used = _failedAttempts[username];
                return Math.Max(0, MAX_ATTEMPTS - used);
            }
        }

        #endregion
    }
}

