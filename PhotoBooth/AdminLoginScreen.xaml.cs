using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Admin login screen with two-level authentication
    /// Master access: Full admin capabilities
    /// User access: Sales and volume control only
    /// </summary>
    public partial class AdminLoginScreen : UserControl
    {
        #region Events

        /// <summary>
        /// Fired when admin login is successful
        /// </summary>
        public event EventHandler<AdminLoginEventArgs>? LoginSuccessful;

        /// <summary>
        /// Fired when user cancels login or closes the screen
        /// </summary>
        public event EventHandler? LoginCancelled;

        /// <summary>
        /// Fired when user clicks "Forgot Password?"
        /// </summary>
        public event EventHandler? ForgotPasswordRequested;

        #endregion

        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly MasterPasswordService _masterPasswordService;
        private readonly MasterPasswordConfigService _masterPasswordConfigService;
        private readonly MasterPasswordRateLimitService _rateLimitService;
        private bool _isPasswordVisible = false;

        #endregion

        #region Initialization

        public AdminLoginScreen()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _masterPasswordService = new MasterPasswordService();
            _masterPasswordConfigService = new MasterPasswordConfigService(_databaseService, _masterPasswordService);
            _rateLimitService = new MasterPasswordRateLimitService();
            
            // Set focus when the control is fully loaded and setup window event handlers
            this.Loaded += AdminLoginScreen_Loaded;
            
            // Handle cleanup when control is unloaded
            this.Unloaded += AdminLoginScreen_Unloaded;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide virtual keyboard when cancelling login
            VirtualKeyboardService.Instance.HideKeyboard();
            LoginCancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handle login button click
        /// </summary>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformLogin();
        }

        /// <summary>
        /// Handle Enter key press in password field
        /// </summary>
        private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformLogin();
                e.Handled = true; // Prevent duplicate handlers and system ding
            }
        }

        /// <summary>
        /// Handle password visibility toggle
        /// </summary>
        private void PasswordToggleButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePasswordVisibility();
        }

        /// <summary>
        /// Handle forgot password link click
        /// </summary>
        private void ForgotPasswordLink_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Application.Information("Forgot password requested from login screen");
            ForgotPasswordRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Authentication Logic

        /// <summary>
        /// Toggle password visibility between hidden and visible
        /// </summary>
        private void TogglePasswordVisibility()
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Show password as text
                PasswordTextInput.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                PasswordTextInput.Visibility = Visibility.Visible;
                PasswordToggleButton.Content = "🙈"; // Closed eye
                PasswordToggleButton.ToolTip = "Hide Password";
                PasswordTextInput.Focus();
                PasswordTextInput.CaretIndex = PasswordTextInput.Text.Length;
            }
            else
            {
                // Hide password
                PasswordInput.Password = PasswordTextInput.Text;
                PasswordTextInput.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordToggleButton.Content = "👁"; // Open eye
                PasswordToggleButton.ToolTip = "Show Password";
                PasswordInput.Focus();
            }
        }

        /// <summary>
        /// Get the current password value from the active control
        /// </summary>
        private string GetCurrentPassword()
        {
            return _isPasswordVisible ? PasswordTextInput.Text : PasswordInput.Password;
        }

        /// <summary>
        /// Clear the password from both controls
        /// </summary>
        private void ClearPassword()
        {
            PasswordInput.Password = "";
            PasswordTextInput.Text = "";
        }

        /// <summary>
        /// Attempt to authenticate the user
        /// </summary>
        private async System.Threading.Tasks.Task PerformLogin()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var username = UsernameInput.Text.Trim();
                var password = GetCurrentPassword().Trim();

                System.Diagnostics.Debug.WriteLine($"Login attempt for username: '{username}' - Started");

                if (string.IsNullOrEmpty(username))
                {
                    ShowError("Please enter a username.");
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    ShowError("Please enter a password.");
                    return;
                }

                // Show loading state
                LoginButton.IsEnabled = false;
                LoginButton.Content = "Authenticating...";
                LoadingOverlay.Visibility = Visibility.Visible;

                System.Diagnostics.Debug.WriteLine($"Calling database authentication... ({stopwatch.ElapsedMilliseconds}ms)");

                // Try master password authentication first
                var masterPasswordResult = await TryMasterPasswordAuthentication(username, password);
                if (masterPasswordResult.success)
                {
                    System.Diagnostics.Debug.WriteLine($"Master password authentication successful ({stopwatch.ElapsedMilliseconds}ms)");
                    return; // Login successful event already triggered
                }
                
                // If master-password failed or is locked out, DO NOT return - proceed to DB auth
                // This prevents DoS attacks where spamming master passwords blocks legitimate users
                if (!string.IsNullOrEmpty(masterPasswordResult.error))
                {
                    System.Diagnostics.Debug.WriteLine($"Master password authentication failed: {masterPasswordResult.error} ({stopwatch.ElapsedMilliseconds}ms)");
                    // Optionally surface a non-blocking hint; avoid clearing inputs/focus here
                    // Continue to database authentication below...
                }

                // Use database authentication with entered username and password
                var authResult = await _databaseService.AuthenticateAsync(username, password);

                System.Diagnostics.Debug.WriteLine($"Auth result received - Success: {authResult.Success}, Data: {authResult.Data?.Username ?? "null"}, Error: {authResult.ErrorMessage} ({stopwatch.ElapsedMilliseconds}ms)");

                AdminAccessLevel accessLevel = AdminAccessLevel.None;

                if (authResult.Success && authResult.Data != null)
                {
                    // Convert database access level to local enum
                    accessLevel = authResult.Data.AccessLevel switch
                    {
                        Models.AdminAccessLevel.Master => AdminAccessLevel.Master,
                        Models.AdminAccessLevel.User => AdminAccessLevel.User,
                        _ => AdminAccessLevel.None
                    };
                    System.Diagnostics.Debug.WriteLine($"Access level determined: {accessLevel} ({stopwatch.ElapsedMilliseconds}ms)");
                }

                if (accessLevel != AdminAccessLevel.None)
                {
                    System.Diagnostics.Debug.WriteLine($"Authentication successful, checking for setup credentials ({stopwatch.ElapsedMilliseconds}ms)");

                    // Check if this is using setup credentials
                    var isSetupCredentials = false;
                    try
                    {
                        var setupCheck = await _databaseService.IsUsingSetupCredentialsAsync(username, password);
                        isSetupCredentials = setupCheck.Success && setupCheck.Data == true;
                        System.Diagnostics.Debug.WriteLine($"Setup credentials check: {isSetupCredentials} ({stopwatch.ElapsedMilliseconds}ms)");
                    }
                    catch (Exception setupEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Setup credentials check failed: {setupEx.Message}");
                    }

                    // Clean up setup credentials folder after successful login (only if NOT using setup creds)
                    if (!isSetupCredentials)
                    {
                        try
                        {
                            await DatabaseService.CleanupSetupCredentialsAsync();
                        }
                        catch (Exception cleanupEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Setup cleanup warning: {cleanupEx.Message}");
                        }
                    }

                    // Trigger login successful event immediately - we already know authResult.Data is not null from the check above
                    var userData = authResult.Data!; // Use null-forgiving operator since we know it's not null
                    
                    // Hide virtual keyboard on successful login
                    VirtualKeyboardService.Instance.HideKeyboard();
                    
                    LoginSuccessful?.Invoke(this, new AdminLoginEventArgs(accessLevel, userData.UserId, userData.Username, userData.DisplayName, isSetupCredentials));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Authentication failed ({stopwatch.ElapsedMilliseconds}ms)");
                    ShowError("Invalid username or password. Please try again.");
                    ClearPassword();
                    UsernameInput.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Login failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Login error: {ex}");
            }
            finally
            {
                // Reset loading state
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Login";
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Show error message to user
        /// </summary>
        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Attempts to authenticate using master password (temporary admin access)
        /// </summary>
        private async System.Threading.Tasks.Task<(bool success, string? error)> TryMasterPasswordAuthentication(string username, string password)
        {
            try
            {
                Console.WriteLine("=== TryMasterPasswordAuthentication START ===");
                Console.WriteLine($"Username: {username}");
                Console.WriteLine($"Password length: {password?.Length ?? 0}");
                Console.WriteLine($"Password is 8-digit numeric: {(!string.IsNullOrEmpty(password) && password.Length == 8 && password.All(char.IsDigit))}");
                
                // Check rate limiting FIRST (before any crypto operations)
                var rateLimitKey = $"masterpass:{username}";
                
                if (_rateLimitService.IsLockedOut(rateLimitKey))
                {
                    var remainingMinutes = _rateLimitService.GetRemainingLockoutMinutes(rateLimitKey);
                    Console.WriteLine($"[RATE LIMIT] User is locked out. Remaining: {remainingMinutes} minutes");
                    return (false, $"Too many failed attempts. Please try again in {remainingMinutes} minute(s).");
                }
                
                Console.WriteLine("[OK] Rate limit check passed");
                
                // Get base secret from encrypted configuration
                string baseSecret;
                try
                {
                    Console.WriteLine("Calling GetBaseSecretAsync...");
                    baseSecret = await _masterPasswordConfigService.GetBaseSecretAsync();
                    Console.WriteLine($"[OK] Got base secret, length: {baseSecret?.Length ?? 0}");
                    
                    // Null check for compiler (GetBaseSecretAsync should never return null on success)
                    if (string.IsNullOrEmpty(baseSecret))
                    {
                        Console.WriteLine("[ERROR] Base secret was null or empty");
                        return (false, null);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Master password feature not configured - skip this authentication method
                    // This is NOT an error - it's an optional feature
                    Console.WriteLine($"[INFO] Master password not configured: {ex.Message}");
                    return (false, null);
                }
                
                // Null check for password parameter (defensive programming)
                if (string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("[ERROR] Password is null or empty");
                    return (false, null);
                }
                
                // Get machine MAC address
                Console.WriteLine("Getting MAC address...");
                var macAddress = _masterPasswordService.GetMacAddress();
                Console.WriteLine($"MAC address: {macAddress ?? "NULL"}");
                if (string.IsNullOrEmpty(macAddress))
                {
                    Console.WriteLine("[ERROR] MAC address not available");
                    return (false, null); // MAC address not available, skip master password check
                }

                // Derive private key
                Console.WriteLine("Deriving private key...");
                var privateKey = _masterPasswordService.DerivePrivateKey(baseSecret, macAddress);
                Console.WriteLine($"[OK] Private key derived, length: {privateKey?.Length ?? 0}");
                
                // Null check for private key (defensive programming)
                if (privateKey == null)
                {
                    Console.WriteLine("[ERROR] Private key derivation returned null");
                    return (false, null);
                }
                
                // Validate password structure and cryptographic signature
                Console.WriteLine("Validating password...");
                var (isValid, nonce) = _masterPasswordService.ValidatePassword(password, privateKey, macAddress);
                Console.WriteLine($"Validation result: isValid={isValid}, nonce={nonce ?? "NULL"}");
                
                if (!isValid || string.IsNullOrEmpty(nonce))
                {
                    Console.WriteLine("[FAILED] Password validation failed");
                    // If the user entered an 8-digit numeric code, treat it as a master-pass attempt
                    // Count it toward rate limit to prevent brute-force attacks (10,000 combinations)
                    if (!string.IsNullOrEmpty(password) && password.Length == 8 && password.All(char.IsDigit))
                    {
                        Console.WriteLine("[RATE LIMIT] Recording failed 8-digit attempt");
                        var remaining = _rateLimitService.RecordFailedAttempt(rateLimitKey);
                        Console.WriteLine($"Remaining attempts: {remaining}");
                        if (remaining == 0)
                        {
                            return (false, $"Too many failed attempts. Please try again in {_rateLimitService.GetRemainingLockoutMinutes(rateLimitKey)} minute(s).");
                        }
                    }
                    // Fall through to regular password auth
                    Console.WriteLine("=== TryMasterPasswordAuthentication END (fallthrough to DB auth) ===");
                    return (false, null);
                }

                // Get the actual admin user by username (before attempting to mark password as used)
                Console.WriteLine($"Looking up user: {username}");
                var userResult = await _databaseService.GetUserByUsernameAsync(username);
                if (!userResult.Success || userResult.Data == null)
                {
                    Console.WriteLine($"[ERROR] User not found: {username}");
                    // Record failed attempt for rate limiting
                    var remaining = _rateLimitService.RecordFailedAttempt(rateLimitKey);
                    
                    if (remaining == 0)
                    {
                        return (false, "Invalid username. Account locked due to too many failed attempts.");
                    }
                    
                    return (false, $"Invalid username. {remaining} attempt(s) remaining.");
                }

                var user = userResult.Data;
                Console.WriteLine($"[OK] User found: {user.Username}, IsActive: {user.IsActive}");
                
                if (!user.IsActive)
                {
                    Console.WriteLine($"[ERROR] User is inactive");
                    // Record failed attempt for rate limiting
                    var remaining = _rateLimitService.RecordFailedAttempt(rateLimitKey);
                    
                    if (remaining == 0)
                    {
                        return (false, "User account is inactive. Account locked due to too many failed attempts.");
                    }
                    
                    return (false, $"User account is inactive. {remaining} attempt(s) remaining.");
                }

                // Atomically mark password as used (single-use enforcement, no TOCTOU vulnerability)
                Console.WriteLine("Hashing password for replay detection...");
                var passwordHash = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(password));
                var passwordHashString = BitConverter.ToString(passwordHash).Replace("-", "");
                Console.WriteLine($"Password hash: {passwordHashString.Substring(0, 16)}...");
                
                Console.WriteLine("Marking password as used...");
                var markUsedResult = await _databaseService.MarkMasterPasswordUsedAsync(
                    passwordHashString, nonce, macAddress, user.UserId);
                
                if (!markUsedResult.Success)
                {
                    Console.WriteLine($"[ERROR] Failed to mark password as used: {markUsedResult.ErrorMessage}");
                    LoggingService.Application.Error(
                        $"Failed to mark master password as used: {markUsedResult.ErrorMessage}");
                    return (false, "System error. Please try again.");
                }
                
                // Check if password was already used (returned from atomic operation)
                if (markUsedResult.Data == true)
                {
                    Console.WriteLine("[ERROR] Password was already used (replay attack blocked)");
                    // Record failed attempt for rate limiting
                    var remaining = _rateLimitService.RecordFailedAttempt(rateLimitKey);
                    
                    if (remaining == 0)
                    {
                        return (false, "This master password has already been used. Account locked due to too many failed attempts.");
                    }
                    
                    return (false, $"This master password has already been used. {remaining} attempt(s) remaining.");
                }

                Console.WriteLine("[SUCCESS] Master password authentication successful!");
                
                // Determine access level
                var accessLevel = user.AccessLevel switch
                {
                    Models.AdminAccessLevel.Master => AdminAccessLevel.Master,
                    Models.AdminAccessLevel.User => AdminAccessLevel.User,
                    _ => AdminAccessLevel.None
                };

                // Reset rate limiting on successful authentication
                _rateLimitService.ResetAttempts(rateLimitKey);
                
                // Hide virtual keyboard
                VirtualKeyboardService.Instance.HideKeyboard();

                // Trigger login successful event
                LoginSuccessful?.Invoke(this, new AdminLoginEventArgs(
                    accessLevel, user.UserId, user.Username, user.DisplayName, false));

                Console.WriteLine("=== TryMasterPasswordAuthentication END (SUCCESS) ===");
                return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== TryMasterPasswordAuthentication EXCEPTION ===");
                Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                LoggingService.Application.Error("Master password authentication error", ex);
                return (false, null); // Continue with regular authentication
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reset the login screen to its initial state
        /// </summary>
        public void Reset()
        {
            Console.WriteLine("=== AdminLoginScreen.Reset() called ===");
            
            // Clear form fields
            UsernameInput.Text = "";
            ClearPassword();
            ErrorMessage.Visibility = Visibility.Collapsed;

            // Reset password visibility to hidden state
            if (_isPasswordVisible)
            {
                _isPasswordVisible = false;
                PasswordTextInput.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordToggleButton.Content = "👁"; // Open eye
                PasswordToggleButton.ToolTip = "Show Password";
            }

            // Reset any error states
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Login";
            LoadingOverlay.Visibility = Visibility.Collapsed;
            
            Console.WriteLine("AdminLoginScreen.Reset() - About to set focus with delay");
            
            // Don't immediately hide keyboard - let navigation handle it
            // Instead, use a delayed approach to ensure UI is fully loaded before setting focus
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    Console.WriteLine("AdminLoginScreen.Reset() - Delay callback started");
                    
                    // Small delay to ensure the control is fully loaded and visible
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    Console.WriteLine("AdminLoginScreen.Reset() - About to focus UsernameInput");
                    Console.WriteLine($"UsernameInput.IsLoaded: {UsernameInput.IsLoaded}");
                    Console.WriteLine($"UsernameInput.IsVisible: {UsernameInput.IsVisible}");
                    Console.WriteLine($"UsernameInput.Focusable: {UsernameInput.Focusable}");
                    
                    // Now set focus - this will trigger the virtual keyboard to show
                    var focusResult = UsernameInput.Focus();
                    Console.WriteLine($"UsernameInput.Focus() result: {focusResult}");
                    
                    // Also ensure keyboard focus
                    var keyboardFocusResult = System.Windows.Input.Keyboard.Focus(UsernameInput);
                    Console.WriteLine($"Keyboard.Focus() result: {keyboardFocusResult?.GetType().Name ?? "null"}");
                    
                    Console.WriteLine("AdminLoginScreen.Reset() - Focus attempt completed");
                }
                catch (Exception ex)
                {
                    LoggingService.Application.Error("Error during delayed focus operation in AdminLoginScreen", ex);
                    // Don't rethrow - this is a UI enhancement, not critical functionality
                }
            }));
            
            Console.WriteLine("=== AdminLoginScreen.Reset() completed ===");
        }

        /// <summary>
        /// Handle control loaded - setup window deactivation monitoring
        /// </summary>
        private void AdminLoginScreen_Loaded(object sender, RoutedEventArgs e)
        {
            // Set focus on username input
            UsernameInput.Focus();
            
            // Find parent window and setup deactivation handler
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                // Remove any existing handler to prevent duplicates
                parentWindow.Deactivated -= ParentWindow_Deactivated;
                // Add deactivation handler
                parentWindow.Deactivated += ParentWindow_Deactivated;
            }
        }

        /// <summary>
        /// Handle control unloaded - cleanup virtual keyboard
        /// </summary>
        private void AdminLoginScreen_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoggingService.Application.Information("AdminLoginScreen unloaded, cleaning up virtual keyboard");
                
                // Reset virtual keyboard state completely for navigation cleanup
                VirtualKeyboardService.Instance.ResetState();
                
                // Cleanup parent window event handler
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.Deactivated -= ParentWindow_Deactivated;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error during AdminLoginScreen cleanup", ex);
            }
        }

        /// <summary>
        /// Handle parent window deactivated - hide virtual keyboard
        /// </summary>
        private void ParentWindow_Deactivated(object? sender, EventArgs e)
        {
            try
            {
                LoggingService.Application.Information("Parent window deactivated, hiding virtual keyboard");
                
                // Hide keyboard when window loses focus
                VirtualKeyboardService.Instance.HideKeyboard();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error hiding keyboard on window deactivation", ex);
            }
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for successful admin login
    /// </summary>
    public class AdminLoginEventArgs : EventArgs
    {
        public AdminAccessLevel AccessLevel { get; }
        public string UserId { get; } // GUID stored as string in database
        public string Username { get; }
        public string DisplayName { get; }
        public bool IsUsingSetupCredentials { get; }

        public AdminLoginEventArgs(AdminAccessLevel accessLevel, string userId, string username, string displayName, bool isUsingSetupCredentials = false)
        {
            AccessLevel = accessLevel;
            UserId = userId;
            Username = username;
            DisplayName = displayName;
            IsUsingSetupCredentials = isUsingSetupCredentials;
        }
    }
}
