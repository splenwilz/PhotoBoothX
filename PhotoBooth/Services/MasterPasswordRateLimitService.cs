using System;
using System.Collections.Concurrent;

namespace Photobooth.Services
{
    /// <summary>
    /// Rate limiting service for master password attempts to prevent brute force attacks
    /// Uses in-memory tracking (kiosk reboot clears - acceptable for physical security)
    /// </summary>
    public class MasterPasswordRateLimitService
    {
        // Thread-safe dictionary for concurrent access
        private static readonly ConcurrentDictionary<string, AttemptInfo> _attempts = new();
        
        private const int MAX_ATTEMPTS = 3;           // Strict: 3 attempts only
        private const int LOCKOUT_MINUTES = 15;       // 15 minute lockout
        
        private class AttemptInfo
        {
            public int FailedAttempts { get; set; }
            public DateTime? LockoutUntil { get; set; }
            public DateTime LastAttemptAt { get; set; }
        }

        /// <summary>
        /// Checks if the user/IP is currently locked out
        /// </summary>
        /// <param name="identifier">Username or IP address</param>
        /// <returns>True if locked out, False if attempts allowed</returns>
        public bool IsLockedOut(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            if (!_attempts.TryGetValue(identifier, out var info))
                return false;

            // Check if lockout period has expired
            if (info.LockoutUntil.HasValue && DateTime.UtcNow < info.LockoutUntil.Value)
            {
                return true; // Still locked out
            }

            // Lockout expired - reset
            if (info.LockoutUntil.HasValue && DateTime.UtcNow >= info.LockoutUntil.Value)
            {
                ResetAttempts(identifier);
                return false;
            }

            return false;
        }

        /// <summary>
        /// Gets remaining lockout time in minutes
        /// </summary>
        public int GetRemainingLockoutMinutes(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return 0;

            if (!_attempts.TryGetValue(identifier, out var info) || !info.LockoutUntil.HasValue)
                return 0;

            var remaining = (info.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes;
            return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
        }

        /// <summary>
        /// Records a failed authentication attempt
        /// </summary>
        /// <param name="identifier">Username or IP address</param>
        /// <returns>Remaining attempts before lockout (0 if locked out)</returns>
        public int RecordFailedAttempt(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return MAX_ATTEMPTS;

            var info = _attempts.AddOrUpdate(
                identifier,
                // Add new entry
                _ => new AttemptInfo
                {
                    FailedAttempts = 1,
                    LastAttemptAt = DateTime.UtcNow,
                    LockoutUntil = null
                },
                // Update existing entry
                (_, existing) =>
                {
                    existing.FailedAttempts++;
                    existing.LastAttemptAt = DateTime.UtcNow;

                    // Lock out after MAX_ATTEMPTS
                    if (existing.FailedAttempts >= MAX_ATTEMPTS)
                    {
                        existing.LockoutUntil = DateTime.UtcNow.AddMinutes(LOCKOUT_MINUTES);
                        
                        LoggingService.Application.Warning(
                            $"Master password lockout triggered for {identifier} - too many failed attempts");
                    }

                    return existing;
                });

            // Calculate remaining attempts
            var remaining = MAX_ATTEMPTS - info.FailedAttempts;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// Resets failed attempts (called on successful authentication)
        /// </summary>
        public void ResetAttempts(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return;

            _attempts.TryRemove(identifier, out _);
        }

        /// <summary>
        /// Gets current attempt count for an identifier
        /// </summary>
        public int GetAttemptCount(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return 0;

            if (!_attempts.TryGetValue(identifier, out var info))
                return 0;

            return info.FailedAttempts;
        }

        /// <summary>
        /// Cleans up old entries (optional maintenance)
        /// Should be called periodically to prevent memory bloat
        /// </summary>
        public void CleanupOldEntries()
        {
            var cutoff = DateTime.UtcNow.AddHours(-1); // Remove entries older than 1 hour

            foreach (var kvp in _attempts)
            {
                if (kvp.Value.LastAttemptAt < cutoff && 
                    (!kvp.Value.LockoutUntil.HasValue || kvp.Value.LockoutUntil.Value < DateTime.UtcNow))
                {
                    _attempts.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}

