using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Forced password change screen shown when users login with setup credentials
    /// Prevents access to admin dashboard until password is changed
    /// </summary>
    public partial class ForcedPasswordChangeScreen : UserControl
    {
        #region Events

        /// <summary>
        /// Fired when password is successfully changed and user can proceed to dashboard
        /// </summary>
        public event EventHandler<PasswordChangeCompletedEventArgs>? PasswordChangeCompleted;

        #endregion

        #region Private Fields

        private readonly IDatabaseService _databaseService;
        private readonly AdminUser _currentUser;
        private readonly AdminAccessLevel _accessLevel;

        // Password validation state
        private bool _isLength8Valid = false;
        private bool _isUppercaseValid = false;
        private bool _isLowercaseValid = false;
        private bool _isNumberValid = false;
        private bool _isPasswordMatch = false;

        // Password visibility state
        private bool _isNewPasswordVisible = false;
        private bool _isConfirmPasswordVisible = false;

        #endregion

        #region Initialization

        public ForcedPasswordChangeScreen(AdminUser currentUser, AdminAccessLevel accessLevel, IDatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _accessLevel = accessLevel;
            
            InitializeUserInfo();
            
            // Set focus when the control is fully loaded
            this.Loaded += (s, e) => NewPasswordInput.Focus();
        }

        /// <summary>
        /// Initialize the user information display
        /// </summary>
        private void InitializeUserInfo()
        {
            UsernameValue.Text = _currentUser.Username;
            AccessLevelValue.Text = _accessLevel switch
            {
                AdminAccessLevel.Master => "Master Administrator (Full Access)",
                AdminAccessLevel.User => "User Administrator (Limited Access)", 
                _ => "Unknown"
            };
        }

        #endregion

        #region Password Validation

        /// <summary>
        /// Handle new password input changes for real-time validation
        /// </summary>
        private void NewPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
            UpdatePasswordRequirements();
            UpdateChangePasswordButton();
        }

        /// <summary>
        /// Handle confirm password input changes
        /// </summary>
        private void ConfirmPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswordMatch();
            UpdatePasswordRequirements();
            UpdateChangePasswordButton();
        }

        /// <summary>
        /// Handle Enter key press in confirm password field
        /// </summary>
        private async void ConfirmPasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ChangePasswordButton.IsEnabled)
            {
                await PerformPasswordChange();
            }
        }

        /// <summary>
        /// Validate the new password against requirements
        /// </summary>
        private void ValidatePassword()
        {
            var password = GetCurrentNewPassword();

            // Check length (at least 8 characters)
            _isLength8Valid = password.Length >= 8;

            // Check for uppercase letter
            _isUppercaseValid = Regex.IsMatch(password, @"[A-Z]");

            // Check for lowercase letter
            _isLowercaseValid = Regex.IsMatch(password, @"[a-z]");

            // Check for number
            _isNumberValid = Regex.IsMatch(password, @"[0-9]");

            // Revalidate password match
            ValidatePasswordMatch();
        }

        /// <summary>
        /// Validate that passwords match
        /// </summary>
        private void ValidatePasswordMatch()
        {
            var newPassword = GetCurrentNewPassword();
            var confirmPassword = GetCurrentConfirmPassword();
            _isPasswordMatch = !string.IsNullOrEmpty(newPassword) && newPassword == confirmPassword;
        }

        /// <summary>
        /// Update the visual indicators for password requirements
        /// </summary>
        private void UpdatePasswordRequirements()
        {
            var validBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
            var invalidBrush = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray

            // Update both color and symbol for accessibility
            UpdateRequirementIndicator(Length8ReqIndicator, Length8Req, _isLength8Valid, validBrush, invalidBrush);
            UpdateRequirementIndicator(UppercaseReqIndicator, UppercaseReq, _isUppercaseValid, validBrush, invalidBrush);
            UpdateRequirementIndicator(LowercaseReqIndicator, LowercaseReq, _isLowercaseValid, validBrush, invalidBrush);
            UpdateRequirementIndicator(NumberReqIndicator, NumberReq, _isNumberValid, validBrush, invalidBrush);
            UpdateRequirementIndicator(MatchReqIndicator, MatchReq, _isPasswordMatch, validBrush, invalidBrush);
        }

        /// <summary>
        /// Update both the symbol and color of a requirement indicator for accessibility
        /// </summary>
        private void UpdateRequirementIndicator(Run indicatorRun, TextBlock textBlock, bool isValid, SolidColorBrush validBrush, SolidColorBrush invalidBrush)
        {
            // Update symbol: ● for satisfied, ○ for unsatisfied
            indicatorRun.Text = isValid ? "● " : "○ ";
            
            // Update color for additional visual feedback
            textBlock.Foreground = isValid ? validBrush : invalidBrush;
        }

        /// <summary>
        /// Update the enabled state of the change password button
        /// </summary>
        private void UpdateChangePasswordButton()
        {
            ChangePasswordButton.IsEnabled = _isLength8Valid && _isUppercaseValid && 
                                           _isLowercaseValid && _isNumberValid && _isPasswordMatch;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle change password button click
        /// </summary>
        private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformPasswordChange();
        }

        /// <summary>
        /// Handle new password visibility toggle
        /// </summary>
        private void NewPasswordToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleNewPasswordVisibility();
        }

        /// <summary>
        /// Handle confirm password visibility toggle
        /// </summary>
        private void ConfirmPasswordToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleConfirmPasswordVisibility();
        }

        /// <summary>
        /// Handle new password text input changes (visible mode)
        /// </summary>
        private void NewPasswordTextInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidatePassword();
            UpdatePasswordRequirements();
            UpdateChangePasswordButton();
        }

        /// <summary>
        /// Handle confirm password text input changes (visible mode)
        /// </summary>
        private void ConfirmPasswordTextInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidatePasswordMatch();
            UpdatePasswordRequirements();
            UpdateChangePasswordButton();
        }

        #endregion

        #region Password Visibility

        /// <summary>
        /// Toggle new password visibility between hidden and visible
        /// </summary>
        private void ToggleNewPasswordVisibility()
        {
            _isNewPasswordVisible = !_isNewPasswordVisible;

            if (_isNewPasswordVisible)
            {
                // Show password as text
                NewPasswordTextInput.Text = NewPasswordInput.Password;
                NewPasswordInput.Visibility = Visibility.Collapsed;
                NewPasswordTextInput.Visibility = Visibility.Visible;
                NewPasswordToggleButton.Content = "🙈"; // Closed eye
                NewPasswordToggleButton.ToolTip = "Hide Password";
                NewPasswordTextInput.Focus();
                NewPasswordTextInput.CaretIndex = NewPasswordTextInput.Text.Length;
            }
            else
            {
                // Hide password
                NewPasswordInput.Password = NewPasswordTextInput.Text;
                NewPasswordTextInput.Visibility = Visibility.Collapsed;
                NewPasswordInput.Visibility = Visibility.Visible;
                NewPasswordToggleButton.Content = "👁"; // Open eye
                NewPasswordToggleButton.ToolTip = "Show Password";
                NewPasswordInput.Focus();
            }
        }

        /// <summary>
        /// Toggle confirm password visibility between hidden and visible
        /// </summary>
        private void ToggleConfirmPasswordVisibility()
        {
            _isConfirmPasswordVisible = !_isConfirmPasswordVisible;

            if (_isConfirmPasswordVisible)
            {
                // Show password as text
                ConfirmPasswordTextInput.Text = ConfirmPasswordInput.Password;
                ConfirmPasswordInput.Visibility = Visibility.Collapsed;
                ConfirmPasswordTextInput.Visibility = Visibility.Visible;
                ConfirmPasswordToggleButton.Content = "🙈"; // Closed eye
                ConfirmPasswordToggleButton.ToolTip = "Hide Password";
                ConfirmPasswordTextInput.Focus();
                ConfirmPasswordTextInput.CaretIndex = ConfirmPasswordTextInput.Text.Length;
            }
            else
            {
                // Hide password
                ConfirmPasswordInput.Password = ConfirmPasswordTextInput.Text;
                ConfirmPasswordTextInput.Visibility = Visibility.Collapsed;
                ConfirmPasswordInput.Visibility = Visibility.Visible;
                ConfirmPasswordToggleButton.Content = "👁"; // Open eye
                ConfirmPasswordToggleButton.ToolTip = "Show Password";
                ConfirmPasswordInput.Focus();
            }
        }

        /// <summary>
        /// Get the current new password value from the active control
        /// </summary>
        private string GetCurrentNewPassword()
        {
            return _isNewPasswordVisible ? NewPasswordTextInput.Text : NewPasswordInput.Password;
        }

        /// <summary>
        /// Get the current confirm password value from the active control
        /// </summary>
        private string GetCurrentConfirmPassword()
        {
            return _isConfirmPasswordVisible ? ConfirmPasswordTextInput.Text : ConfirmPasswordInput.Password;
        }

        /// <summary>
        /// Clear both password controls
        /// </summary>
        private void ClearPasswords()
        {
            NewPasswordInput.Password = "";
            NewPasswordTextInput.Text = "";
            ConfirmPasswordInput.Password = "";
            ConfirmPasswordTextInput.Text = "";
        }

        #endregion

        #region Password Change Logic

        /// <summary>
        /// Perform the password change operation
        /// </summary>
        private async System.Threading.Tasks.Task PerformPasswordChange()
        {
            try
            {
                var newPassword = GetCurrentNewPassword();

                // Show loading state
                ChangePasswordButton.IsEnabled = false;
                ChangePasswordButton.Content = "Changing...";
                LoadingOverlay.Visibility = Visibility.Visible;
                HideError();

                LoggingService.Application.Information("Setup password change initiated",
                    ("UserId", _currentUser.UserId),
                    ("Username", _currentUser.Username),
                    ("AccessLevel", _accessLevel.ToString()));

                // Update password in database
                var result = await _databaseService.UpdateUserPasswordByUserIdAsync(_currentUser.UserId, newPassword, _currentUser.UserId);

                if (result.Success)
                {
                    LoggingService.Application.Information("Setup password changed successfully",
                        ("UserId", _currentUser.UserId),
                        ("Username", _currentUser.Username));
                    
                    LoggingService.Transaction.Information("SECURITY", "Setup password changed - user can now access admin panel",
                        ("UserId", _currentUser.UserId),
                        ("Username", _currentUser.Username),
                        ("AccessLevel", _accessLevel.ToString()),
                        ("SetupCredentialsRemoved", true));

                    // Hide virtual keyboard on successful password change
                    VirtualKeyboardService.Instance.HideKeyboard();

                    // Trigger successful password change event
                    PasswordChangeCompleted?.Invoke(this, new PasswordChangeCompletedEventArgs(_currentUser, _accessLevel));
                }
                else
                {
                    LoggingService.Application.Error("Setup password change failed", null,
                        ("UserId", _currentUser.UserId),
                        ("ErrorMessage", result.ErrorMessage ?? "Unknown error"));

                    ShowError($"Failed to change password: {result.ErrorMessage ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Setup password change error", ex,
                    ("UserId", _currentUser.UserId),
                    ("Username", _currentUser.Username));

                ShowError($"Password change failed: {ex.Message}");
            }
            finally
            {
                // Reset loading state
                ChangePasswordButton.Content = "Change Password";
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UpdateChangePasswordButton(); // Re-check validation and set correct enabled state
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
        /// Hide error message
        /// </summary>
        private void HideError()
        {
            ErrorMessage.Visibility = Visibility.Collapsed;
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for successful password change completion
    /// </summary>
    public class PasswordChangeCompletedEventArgs : EventArgs
    {
        public AdminUser User { get; }
        public AdminAccessLevel AccessLevel { get; }

        public PasswordChangeCompletedEventArgs(AdminUser user, AdminAccessLevel accessLevel)
        {
            User = user;
            AccessLevel = accessLevel;
        }
    }
} 
