using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Photobooth.Models;

namespace Photobooth.Services
{
    /// <summary>
    /// Service for managing encrypted storage of master password base secret
    /// Uses Windows DPAPI CurrentUser scope - encrypts per Windows user profile (secure for kiosks running under dedicated user accounts)
    /// </summary>
    public class MasterPasswordConfigService
    {
        private readonly IDatabaseService _databaseService;
        private readonly MasterPasswordService _masterPasswordService;
        private const string SETTINGS_CATEGORY = "Security";
        private const string BASE_SECRET_KEY = "MasterPasswordBaseSecret";
        private const string CONFIG_FILENAME = "master-password.config";

        public MasterPasswordConfigService(IDatabaseService databaseService, MasterPasswordService masterPasswordService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _masterPasswordService = masterPasswordService ?? throw new ArgumentNullException(nameof(masterPasswordService));
        }

        /// <summary>
        /// Gets the base secret, decrypting if necessary.
        /// First checks database, then falls back to config file if not set.
        /// </summary>
        public async Task<string> GetBaseSecretAsync()
        {
            Console.WriteLine("=== MasterPasswordConfigService.GetBaseSecretAsync START ===");
            try
            {
                // Try to get from database first (already configured)
                Console.WriteLine("Checking database for base secret...");
                var result = await _databaseService.GetSettingValueAsync<string>(SETTINGS_CATEGORY, BASE_SECRET_KEY);
                Console.WriteLine($"Database result: Success={result.Success}, HasData={!string.IsNullOrEmpty(result.Data)}");

                
                if (result.Success && !string.IsNullOrEmpty(result.Data))
                {
                    Console.WriteLine("Found in database, decrypting...");
                    // Decrypt the stored encrypted value
                    var decrypted = DecryptSecret(result.Data);
                    Console.WriteLine($"Decrypted successfully, length={decrypted?.Length ?? 0}");
                    if (string.IsNullOrEmpty(decrypted))
                    {
                        throw new InvalidOperationException("Failed to decrypt base secret - data may be corrupted");
                    }
                    return decrypted;
                }

                // Not in database - try to load from config file and initialize
                Console.WriteLine("Not in database, trying config file...");
                var configSecret = LoadFromConfigFile();
                Console.WriteLine($"Config file result: {(configSecret != null ? $"Found (length={configSecret.Length})" : "Not found")}");

                
                if (!string.IsNullOrEmpty(configSecret))
                {
                    Console.WriteLine("Storing config secret in database...");
                    // Store in database for future use (encrypted)
                    var saveSuccess = await SetBaseSecretAsync(configSecret);
                    Console.WriteLine($"Database save result: {saveSuccess}");
                    
                    if (!saveSuccess)
                    {
                        Console.WriteLine("[ERROR] Failed to save secret to database!");
                        LoggingService.Application.Error("Failed to save master password secret to database");
                        throw new InvalidOperationException("Failed to initialize master password configuration");
                    }
                    
                    LoggingService.Application.Information("Master password base secret initialized from config file");
                    Console.WriteLine("Successfully initialized from config file");
                    
                    // SECURITY: Delete the config file to remove plain text secret
                    DeleteConfigFile();
                    
                    return configSecret;
                }

                // No config file - master password feature disabled
                // This is NOT an error - it's an optional feature for self-installed systems
                Console.WriteLine("No config file found - master password feature disabled");

                throw new InvalidOperationException(
                    "Master password feature is not available. " +
                    "This feature is only available in enterprise installations with support contracts.");
            }
            catch (InvalidOperationException)
            {
                // Rethrow configuration errors (master password not configured)
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                LoggingService.Application.Error($"Failed to get base secret: {ex.Message}");
                throw new InvalidOperationException(
                    "Failed to retrieve master password base secret.", ex);
            }
            finally
            {
                Console.WriteLine("=== MasterPasswordConfigService.GetBaseSecretAsync END ===");
            }
        }

        /// <summary>
        /// Deletes the config file after successfully reading it (security measure)
        /// </summary>
        private void DeleteConfigFile()
        {
            try
            {
                var configPath = GetConfigFilePath();
                
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                    LoggingService.Application.Information($"Deleted master password config file for security: {configPath}");
                    Console.WriteLine($"[SECURITY] Deleted config file: {configPath}");
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - deletion failure shouldn't break the app
                LoggingService.Application.Warning($"Failed to delete master password config file: {ex.Message}");
                Console.WriteLine($"[WARNING] Could not delete config file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the path to the master password config file.
        /// Checks both ProgramData (new location) and application directory (legacy) for backwards compatibility.
        /// </summary>
        private string GetConfigFilePath()
        {
            // Primary location: ProgramData (C:\ProgramData\PhotoBoothX\master-password.config)
            var programDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PhotoBoothX",
                CONFIG_FILENAME);
            
            if (File.Exists(programDataPath))
            {
                return programDataPath;
            }
            
            // Fallback: Application directory (for backwards compatibility with older installers)
            var appDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
            
            return appDirPath;
        }

        /// <summary>
        /// Loads base secret from config file (if exists)
        /// Config file is only included in enterprise installer packages
        /// </summary>
        private string? LoadFromConfigFile()
        {
            Console.WriteLine("--- LoadFromConfigFile START ---");
            try
            {
                var configPath = GetConfigFilePath();
                Console.WriteLine($"Looking for config at: {configPath}");

                if (!File.Exists(configPath))
                {
                    Console.WriteLine("Config file does NOT exist");
                    return null; // Config file not present - self-installed version
                }

                Console.WriteLine("Config file EXISTS, reading...");
                var json = File.ReadAllText(configPath);
                Console.WriteLine($"Config JSON length: {json.Length}");
                // Avoid logging sensitive config contents (baseSecret is plaintext/encrypted secret)

                
                var config = System.Text.Json.JsonDocument.Parse(json);
                
                var encrypted = config.RootElement.GetProperty("encrypted").GetBoolean();
                var secret = config.RootElement.GetProperty("baseSecret").GetString();
                Console.WriteLine($"Config parsed: encrypted={encrypted}, secret length={secret?.Length ?? 0}");

                if (string.IsNullOrEmpty(secret))
                {
                    Console.WriteLine("Secret is empty in config file");
                    return null;
                }

                // If encrypted in config, decrypt it
                if (encrypted)
                {
                    Console.WriteLine("Secret is encrypted, decrypting...");
                    var decrypted = DecryptSecret(secret);
                    Console.WriteLine($"Decrypted length: {decrypted?.Length ?? 0}");
                    return decrypted;
                }

                Console.WriteLine("Secret is not encrypted, returning as-is");
                return secret;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in LoadFromConfigFile: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                LoggingService.Application.Error($"Failed to load config file: {ex.Message}");
                return null;
            }
            finally
            {
                Console.WriteLine("--- LoadFromConfigFile END ---");
            }
        }

        /// <summary>
        /// Sets the base secret, encrypting before storage
        /// </summary>
        public async Task<bool> SetBaseSecretAsync(string baseSecret)
        {
            Console.WriteLine("--- SetBaseSecretAsync START ---");
            Console.WriteLine($"Input secret length: {baseSecret?.Length ?? 0}");
            
            if (string.IsNullOrWhiteSpace(baseSecret))
            {
                Console.WriteLine("[ERROR] Base secret is empty!");
                throw new ArgumentException("Base secret cannot be empty", nameof(baseSecret));
            }

            if (baseSecret.Length < 32)
            {
                Console.WriteLine($"[ERROR] Base secret too short: {baseSecret.Length} chars (need 32+)");
                throw new ArgumentException("Base secret must be at least 32 characters for security", nameof(baseSecret));
            }

            try
            {
                Console.WriteLine("Encrypting secret with DPAPI...");
                // Encrypt using DPAPI CurrentUser scope (user-profile specific, secure for kiosk)
                var encrypted = EncryptSecret(baseSecret);
                Console.WriteLine($"Encrypted secret length: {encrypted?.Length ?? 0}");

                Console.WriteLine($"Calling database SetSettingValueAsync (Category: {SETTINGS_CATEGORY}, Key: {BASE_SECRET_KEY})...");

                // Store in database
                // Use NULL for ModifiedBy to avoid FK constraint (system initialization)
                var result = await _databaseService.SetSettingValueAsync(
                    SETTINGS_CATEGORY, 
                    BASE_SECRET_KEY, 
                    encrypted, 
                    null);

                Console.WriteLine($"Database SetSettingValueAsync result: Success={result.Success}");
                if (!result.Success)
                {
                    Console.WriteLine($"[ERROR] Database error: {result.ErrorMessage}");
                }

                if (result.Success)
                {
                    LoggingService.Application.Information("Master password base secret updated");
                    Console.WriteLine("Secret successfully saved to database");
                }

                Console.WriteLine("--- SetBaseSecretAsync END ---");
                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in SetBaseSecretAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                LoggingService.Application.Error($"Failed to set base secret: {ex.Message}");
                Console.WriteLine("--- SetBaseSecretAsync END (with error) ---");
                return false;
            }
        }

        /// <summary>
        /// Generates a cryptographically secure random base secret (64 characters)
        /// </summary>
        public static string GenerateRandomBaseSecret()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var result = new StringBuilder(64);
            
            // Use GetInt32 for uniform distribution (no modulo bias)
            for (int i = 0; i < 64; i++)
            {
                var idx = RandomNumberGenerator.GetInt32(chars.Length);
                result.Append(chars[idx]);
            }

            return result.ToString();
        }

        #region Encryption/Decryption (Windows DPAPI)

        /// <summary>
        /// Gets entropy for DPAPI encryption (MAC address for additional security)
        /// </summary>
        private byte[]? GetDpapiEntropy()
        {
            try
            {
                // Use MAC address as entropy for defense-in-depth
                var macAddress = _masterPasswordService.GetMacAddress();
                if (!string.IsNullOrEmpty(macAddress))
                {
                    return Encoding.UTF8.GetBytes(macAddress);
                }
            }
            catch
            {
                // Fall back to no entropy if MAC address unavailable
            }
            
            return null;
        }

        /// <summary>
        /// Encrypts data using Windows DPAPI with CurrentUser scope
        /// Data can only be decrypted by the same Windows user account on this machine
        /// MAC address used as additional entropy for defense-in-depth
        /// </summary>
        private string EncryptSecret(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var entropy = GetDpapiEntropy();
            
            // Encrypt using DPAPI with CurrentUser scope
            // CurrentUser = Only this Windows user can decrypt (not machine-wide)
            // Entropy (MAC address) adds an additional layer of protection
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                entropy,
                DataProtectionScope.CurrentUser);

            // Return as Base64 string for storage
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts data using Windows DPAPI
        /// </summary>
        private string DecryptSecret(string encryptedText)
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var entropy = GetDpapiEntropy();
            
            // Decrypt using DPAPI with same entropy used during encryption
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                entropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        #endregion
    }
}

