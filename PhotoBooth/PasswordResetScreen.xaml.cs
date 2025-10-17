using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// Password Reset screen after successful PIN verification
    /// Rationale: Simple password reset UI with validation, reuses existing password validation logic
    /// </summary>
    public partial class PasswordResetScreen : UserControl
    {
        private readonly IDatabaseService _databaseService;
        private readonly AdminUser _user;
        private bool _isPasswordValid;
        private bool _isPasswordMatch;

        public event EventHandler? PasswordResetSuccessful;
        public event EventHandler? PasswordResetCancelled;

        public PasswordResetScreen(IDatabaseService databaseService, AdminUser user)
        {
            InitializeComponent();

            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _user = user ?? throw new ArgumentNullException(nameof(user));

            UsernameDisplay.Text = $"Setting new password for: {user.Username}";

            // Set focus to new password input when loaded
            Loaded += (s, e) => NewPasswordInput.Focus();
        }

        #region Password Input Handlers

        private void NewPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Sync with text input if it was visible
            if (NewPasswordTextInput.Visibility == Visibility.Visible)
            {
                NewPasswordTextInput.Text = NewPasswordInput.Password;
            }

            ValidatePassword();
        }

        private void NewPasswordTextInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Sync with password box
            NewPasswordInput.Password = NewPasswordTextInput.Text;
            ValidatePassword();
        }

        private void NewPasswordTextInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResetPasswordButton.IsEnabled)
            {
                ResetPasswordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void ConfirmPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Sync with text input if it was visible
            if (ConfirmPasswordTextInput.Visibility == Visibility.Visible)
            {
                ConfirmPasswordTextInput.Text = ConfirmPasswordInput.Password;
            }

            ValidatePasswordMatch();
        }

        private void ConfirmPasswordTextInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Sync with password box
            ConfirmPasswordInput.Password = ConfirmPasswordTextInput.Text;
            ValidatePasswordMatch();
        }

        private void ConfirmPasswordTextInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResetPasswordButton.IsEnabled)
            {
                ResetPasswordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        #endregion

        #region Password Visibility Toggles

        private void PasswordVisibilityToggle_Changed(object sender, RoutedEventArgs e)
        {
            ToggleNewPasswordVisibility();
        }

        private void ConfirmPasswordVisibilityToggle_Changed(object sender, RoutedEventArgs e)
        {
            ToggleConfirmPasswordVisibility();
        }

        /// <summary>
        /// Toggle new password visibility between hidden and visible
        /// </summary>
        private void ToggleNewPasswordVisibility()
        {
            if (NewPasswordTextInput.Visibility == Visibility.Visible)
            {
                // Hide password
                NewPasswordInput.Password = NewPasswordTextInput.Text;
                NewPasswordTextInput.Visibility = Visibility.Collapsed;
                NewPasswordInput.Visibility = Visibility.Visible;
                NewPasswordVisibilityToggle.Content = "👁"; // Open eye
                NewPasswordVisibilityToggle.ToolTip = "Show Password";
                NewPasswordInput.Focus();
            }
            else
            {
                // Show password as text
                NewPasswordTextInput.Text = NewPasswordInput.Password;
                NewPasswordInput.Visibility = Visibility.Collapsed;
                NewPasswordTextInput.Visibility = Visibility.Visible;
                NewPasswordVisibilityToggle.Content = "🙈"; // Closed eye
                NewPasswordVisibilityToggle.ToolTip = "Hide Password";
                NewPasswordTextInput.Focus();
                NewPasswordTextInput.CaretIndex = NewPasswordTextInput.Text.Length;
            }
        }

        /// <summary>
        /// Toggle confirm password visibility between hidden and visible
        /// </summary>
        private void ToggleConfirmPasswordVisibility()
        {
            if (ConfirmPasswordTextInput.Visibility == Visibility.Visible)
            {
                // Hide password
                ConfirmPasswordInput.Password = ConfirmPasswordTextInput.Text;
                ConfirmPasswordTextInput.Visibility = Visibility.Collapsed;
                ConfirmPasswordInput.Visibility = Visibility.Visible;
                ConfirmPasswordVisibilityToggle.Content = "👁"; // Open eye
                ConfirmPasswordVisibilityToggle.ToolTip = "Show Password";
                ConfirmPasswordInput.Focus();
            }
            else
            {
                // Show password as text
                ConfirmPasswordTextInput.Text = ConfirmPasswordInput.Password;
                ConfirmPasswordInput.Visibility = Visibility.Collapsed;
                ConfirmPasswordTextInput.Visibility = Visibility.Visible;
                ConfirmPasswordVisibilityToggle.Content = "🙈"; // Closed eye
                ConfirmPasswordVisibilityToggle.ToolTip = "Hide Password";
                ConfirmPasswordTextInput.Focus();
                ConfirmPasswordTextInput.CaretIndex = ConfirmPasswordTextInput.Text.Length;
            }
        }

        #endregion

        #region Password Validation

        /// <summary>
        /// Validate new password meets requirements
        /// Rationale: Minimum 8 characters for basic security
        /// </summary>
        private void ValidatePassword()
        {
            var password = NewPasswordInput.Password;
            
            // Check length (at least 8 characters)
            // Rationale: Industry standard minimum for password security
            _isPasswordValid = password.Length >= 8;

            if (_isPasswordValid)
            {
                LengthIndicator.Text = "✓";
                LengthIndicator.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }
            else
            {
                LengthIndicator.Text = "✗";
                LengthIndicator.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            }

            // Re-validate match when password changes
            ValidatePasswordMatch();
            UpdateResetButton();
        }

        /// <summary>
        /// Validate passwords match
        /// </summary>
        private void ValidatePasswordMatch()
        {
            var password = NewPasswordInput.Password;
            var confirmPassword = ConfirmPasswordInput.Password;

            _isPasswordMatch = !string.IsNullOrEmpty(password) && password == confirmPassword;

            if (_isPasswordMatch && !string.IsNullOrEmpty(confirmPassword))
            {
                MatchIndicator.Text = "✓";
                MatchIndicator.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }
            else
            {
                MatchIndicator.Text = "✗";
                MatchIndicator.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            }

            UpdateResetButton();
        }

        /// <summary>
        /// Enable reset button only when all requirements are met
        /// </summary>
        private void UpdateResetButton()
        {
            ResetPasswordButton.IsEnabled = _isPasswordValid && _isPasswordMatch;
        }

        #endregion

        #region Password Reset

        private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetPasswordButton.IsEnabled = false;
                var newPassword = NewPasswordInput.Password;

                // Update password in database
                // Rationale: Using existing DatabaseService method for consistency
                var result = await _databaseService.UpdateUserPasswordByUserIdAsync(
                    _user.UserId,
                    newPassword,
                    updatedBy: null // Self-service password reset via PIN
                );

                if (result.Success)
                {
                    LoggingService.Application.Information("Password reset successful via PIN recovery",
                        ("UserId", _user.UserId),
                        ("Username", _user.Username));

                    ShowSuccess("✓ Password reset successful! Redirecting to login...");

                    // Wait a moment then navigate back to login
                    await System.Threading.Tasks.Task.Delay(1500);
                    PasswordResetSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    LoggingService.Application.Warning("Password reset failed", 
                        ("UserId", _user.UserId),
                        ("Username", _user.Username),
                        ("ErrorMessage", result.ErrorMessage ?? "Unknown error"));

                    ShowError($"Failed to reset password: {result.ErrorMessage}");
                    ResetPasswordButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Password reset error", ex,
                    ("UserId", _user.UserId),
                    ("Username", _user.Username));

                ShowError("An error occurred. Please try again.");
                ResetPasswordButton.IsEnabled = true;
            }
        }

        #endregion

        #region UI Helpers

        private void ShowError(string message)
        {
            StatusMessage.Text = message;
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            StatusMessage.Visibility = Visibility.Visible;
        }

        private void ShowSuccess(string message)
        {
            StatusMessage.Text = message;
            StatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            StatusMessage.Visibility = Visibility.Visible;
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Handle back button click - return to login
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Application.Information("User cancelled password reset - returning to login");
            PasswordResetCancelled?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}

