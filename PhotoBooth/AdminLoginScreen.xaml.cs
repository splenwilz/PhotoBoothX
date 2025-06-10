using System;
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

        #endregion

        #region Private Fields

        private readonly IDatabaseService _databaseService;

        #endregion

        #region Initialization

        public AdminLoginScreen()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            
            // Set focus when the control is fully loaded
            this.Loaded += (s, e) => UsernameInput.Focus();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
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
            }
        }

        #endregion

        #region Authentication Logic

        /// <summary>
        /// Attempt to authenticate the user
        /// </summary>
        private async System.Threading.Tasks.Task PerformLogin()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var username = UsernameInput.Text.Trim();
                var password = PasswordInput.Password.Trim();

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
                    LoginSuccessful?.Invoke(this, new AdminLoginEventArgs(accessLevel, userData.UserId, userData.Username, userData.DisplayName, isSetupCredentials));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Authentication failed ({stopwatch.ElapsedMilliseconds}ms)");
                    ShowError("Invalid username or password. Please try again.");
                    PasswordInput.Password = "";
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

        #endregion

        #region Public Methods

        /// <summary>
        /// Reset the login screen state
        /// </summary>
        public void Reset()
        {
            UsernameInput.Text = "";
            PasswordInput.Password = "";
            ErrorMessage.Visibility = Visibility.Collapsed;

            // Reset any error states
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Login";
            LoadingOverlay.Visibility = Visibility.Collapsed;
            
            // Set focus to username input (use Dispatcher to ensure it happens after UI updates)
            Dispatcher.BeginInvoke(new Action(() => UsernameInput.Focus()));
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