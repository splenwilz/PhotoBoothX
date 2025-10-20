using System.Text.RegularExpressions;

namespace Photobooth.Services
{
    /// <summary>
    /// Centralized password policy validation
    /// Ensures consistent password requirements across all password entry points
    /// </summary>
    public static class PasswordPolicyService
    {
        /// <summary>
        /// Validate password against all policy requirements
        /// </summary>
        /// <param name="password">Password to validate</param>
        /// <returns>ValidationResult with detailed requirement status</returns>
        public static PasswordValidationResult ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return new PasswordValidationResult
                {
                    IsValid = false,
                    MeetsLengthRequirement = false,
                    HasUppercase = false,
                    HasLowercase = false,
                    HasNumber = false
                };
            }

            var result = new PasswordValidationResult
            {
                MeetsLengthRequirement = password.Length >= 8,
                HasUppercase = Regex.IsMatch(password, @"[A-Z]"),
                HasLowercase = Regex.IsMatch(password, @"[a-z]"),
                HasNumber = Regex.IsMatch(password, @"[0-9]")
            };

            // Password is valid only if ALL requirements are met
            result.IsValid = result.MeetsLengthRequirement &&
                            result.HasUppercase &&
                            result.HasLowercase &&
                            result.HasNumber;

            return result;
        }

        /// <summary>
        /// Get human-readable description of password requirements
        /// </summary>
        public static string GetRequirementsDescription()
        {
            return "Password must contain:\n" +
                   "• At least 8 characters\n" +
                   "• One uppercase letter (A-Z)\n" +
                   "• One lowercase letter (a-z)\n" +
                   "• One number (0-9)";
        }
    }

    /// <summary>
    /// Result of password validation with detailed requirement status
    /// </summary>
    public class PasswordValidationResult
    {
        /// <summary>
        /// True if password meets ALL requirements
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// True if password has at least 8 characters
        /// </summary>
        public bool MeetsLengthRequirement { get; set; }

        /// <summary>
        /// True if password contains at least one uppercase letter
        /// </summary>
        public bool HasUppercase { get; set; }

        /// <summary>
        /// True if password contains at least one lowercase letter
        /// </summary>
        public bool HasLowercase { get; set; }

        /// <summary>
        /// True if password contains at least one number
        /// </summary>
        public bool HasNumber { get; set; }
    }
}

