using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace PhotoBooth.Services
{
    public class LicenseService
    {
        private const string REGISTRY_KEY = @"SOFTWARE\PhotoBooth";
        private const string LICENSE_FILE = "license.dat";
        
        public class LicenseInfo
        {
            public string LicenseKey { get; set; } = string.Empty;
            public DateTime ExpiryDate { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string CustomerEmail { get; set; } = string.Empty;
            public bool IsValid { get; set; }
            public string ProductVersion { get; set; } = string.Empty;
        }

        public static LicenseInfo GetLicenseInfo()
        {
            try
            {
                // Check registry first
                var license = GetLicenseFromRegistry();
                if (license != null && IsLicenseValid(license))
                {
                    return license;
                }

                // Check local license file
                var localLicense = GetLicenseFromFile();
                if (localLicense != null && IsLicenseValid(localLicense))
                {
                    // Save valid license to registry
                    SaveLicenseToRegistry(localLicense);
                    return localLicense;
                }

                // Return invalid license
                return new LicenseInfo { IsValid = false };
            }
            catch
            {
                return new LicenseInfo { IsValid = false };
            }
        }

        public static bool ValidateLicense(string licenseKey)
        {
            try
            {
                // TODO: Implement proper license validation
                // This should connect to your license server or validate cryptographically
                
                // Placeholder validation - replace with real implementation
                if (string.IsNullOrEmpty(licenseKey) || licenseKey.Length < 16)
                    return false;

                // Create mock license for development
                var license = new LicenseInfo
                {
                    LicenseKey = licenseKey,
                    ExpiryDate = DateTime.Now.AddYears(1),
                    CustomerName = "Demo Customer",
                    CustomerEmail = "demo@customer.com",
                    IsValid = true,
                    ProductVersion = GetApplicationVersion()
                };

                SaveLicenseToRegistry(license);
                SaveLicenseToFile(license);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsLicenseExpired(LicenseInfo license)
        {
            return license == null || license.ExpiryDate < DateTime.Now;
        }

        public static int DaysUntilExpiry(LicenseInfo license)
        {
            if (license == null || !license.IsValid)
                return 0;

            var days = (license.ExpiryDate - DateTime.Now).Days;
            return Math.Max(0, days);
        }

        private static bool IsLicenseValid(LicenseInfo license)
        {
            return license != null && 
                   license.IsValid && 
                   !string.IsNullOrEmpty(license.LicenseKey) &&
                   license.ExpiryDate > DateTime.Now;
        }

        private static LicenseInfo? GetLicenseFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(REGISTRY_KEY);
                if (key == null) return null;

                var licenseData = key.GetValue("LicenseData") as string;
                if (string.IsNullOrEmpty(licenseData)) return null;

                // Decrypt and deserialize (basic implementation)
                var json = DecryptString(licenseData);
                return JsonSerializer.Deserialize<LicenseInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        private static LicenseInfo? GetLicenseFromFile()
        {
            try
            {
                var licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LICENSE_FILE);
                if (!File.Exists(licenseFile)) return null;

                var encryptedData = File.ReadAllText(licenseFile);
                var json = DecryptString(encryptedData);
                return JsonSerializer.Deserialize<LicenseInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveLicenseToRegistry(LicenseInfo license)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(REGISTRY_KEY);
                var json = JsonSerializer.Serialize(license);
                var encrypted = EncryptString(json);
                key.SetValue("LicenseData", encrypted);
            }
            catch
            {
                // Ignore registry save errors
            }
        }

        private static void SaveLicenseToFile(LicenseInfo license)
        {
            try
            {
                var licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LICENSE_FILE);
                var json = JsonSerializer.Serialize(license);
                var encrypted = EncryptString(json);
                File.WriteAllText(licenseFile, encrypted);
            }
            catch
            {
                // Ignore file save errors
            }
        }

        private static string EncryptString(string plainText)
        {
            // Basic encryption - replace with proper implementation
            var bytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(bytes);
        }

        private static string DecryptString(string cipherText)
        {
            // Basic decryption - replace with proper implementation
            var bytes = Convert.FromBase64String(cipherText);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string GetApplicationVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }

        public static void ShowLicenseDialog()
        {
            // TODO: Implement license activation dialog
            // This should show a professional license activation window
            System.Windows.MessageBox.Show(
                "PhotoBooth Professional requires a valid license.\n\n" +
                "Please contact sales@yourcompany.com to purchase a license.\n" +
                "Demo mode: Limited functionality available.",
                "License Required",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
} 