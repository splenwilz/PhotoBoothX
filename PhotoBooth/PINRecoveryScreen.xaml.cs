using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// PIN Recovery screen for password reset
    /// Rationale: Simple PIN-based recovery appropriate for kiosk environment
    /// </summary>
    public partial class PINRecoveryScreen : UserControl
    {
        private readonly IDatabaseService _databaseService;
        private readonly PINRecoveryService _pinService;
        private readonly TextBox[] _pinBoxes;
        private readonly Border[] _pinBorders;
        private AdminUser? _selectedUser;
        private DispatcherTimer? _lockoutTimer;

        public event EventHandler<AdminUser>? RecoverySuccessful;
        public event EventHandler? BackToLogin;

        public PINRecoveryScreen(IDatabaseService databaseService)
        {
            InitializeComponent();

            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _pinService = new PINRecoveryService();
            
            // Rationale: Store references to boxes for easier iteration
            _pinBoxes = new[] { PIN1, PIN2, PIN3, PIN4 };
            _pinBorders = new[] { PIN1Border, PIN2Border, PIN3Border, PIN4Border };

            // Load users when screen loads
            Loaded += async (s, e) => await LoadUsersAsync();
        }

        /// <summary>
        /// Load all admin users into the dropdown
        /// Rationale: Show only users with recovery PINs set
        /// </summary>
        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            try
            {
                var usersResult = await _databaseService.GetAllAsync<AdminUser>();
                
                if (!usersResult.Success || usersResult.Data == null)
                {
                    ShowError("Failed to load user list. Please try again.");
                    return;
                }

                // Filter to only users with recovery PINs
                var usersWithPINs = usersResult.Data
                    .Where(u => !string.IsNullOrWhiteSpace(u.RecoveryPIN))
                    .OrderBy(u => u.DisplayName)
                    .ToList();

                if (usersWithPINs.Count == 0)
                {
                    ShowError("No users have recovery PINs set up. Please contact support.");
                    UserComboBox.IsEnabled = false;
                    return;
                }

                UserComboBox.ItemsSource = usersWithPINs;
                UserComboBox.Focus();
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to load users for PIN recovery", ex);
                ShowError("An error occurred loading users. Please try again.");
            }
        }

        /// <summary>
        /// Handle user selection from dropdown
        /// </summary>
        private void UserComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (UserComboBox.SelectedItem is AdminUser selectedUser)
            {
                _selectedUser = selectedUser;
                
                // Hide placeholder text when item is selected
                ComboBoxPlaceholder.Visibility = Visibility.Collapsed;
                
                // Check if this user is rate-limited
                if (_pinService.IsRateLimited(selectedUser.Username))
                {
                    PINEntrySection.Visibility = Visibility.Collapsed;
                    ShowError("Too many failed attempts for this account. Please try again in 1 minute.");
                    VerifyButton.IsEnabled = false;
                    return;
                }

                // Show PIN entry and attempts warning if applicable
                PINEntrySection.Visibility = Visibility.Visible;
                HideMessages();
                
                var remaining = _pinService.GetRemainingAttempts(selectedUser.Username);
                if (remaining < 5)
                {
                    AttemptsWarning.Text = $"⚠️ {remaining} attempt{(remaining == 1 ? "" : "s")} remaining";
                    AttemptsWarning.Visibility = Visibility.Visible;
                }
                else
                {
                    AttemptsWarning.Visibility = Visibility.Collapsed;
                }
                
                ClearPINBoxes();
                PIN1.Focus();
            }
            else
            {
                _selectedUser = null;
                ComboBoxPlaceholder.Visibility = Visibility.Visible;
                PINEntrySection.Visibility = Visibility.Collapsed;
                HideMessages();
            }
        }

        #region PIN Input Handling

        /// <summary>
        /// Handle numeric-only input validation
        /// Rationale: PINs should only contain digits
        /// </summary>
        /// <summary>
        /// Handle text input validation - only allow digits
        /// </summary>
        private void PINBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !e.Text.All(char.IsDigit);
        }

        /// <summary>
        /// Handle auto-advance to next box and PIN completion check
        /// Enhanced to work with virtual keyboard input
        /// </summary>
        private void PINBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox currentBox)
                return;

            var currentIndex = Array.IndexOf(_pinBoxes, currentBox);
            
            // Debug logging to understand virtual keyboard behavior
            System.Diagnostics.Debug.WriteLine($"PINBox_TextChanged: Box {currentIndex + 1}, Text: '{currentBox.Text}', Length: {currentBox.Text.Length}");
            
            // Handle virtual keyboard input - ensure only one character per box
            if (currentBox.Text.Length > 1)
            {
                // Take only the last character (for virtual keyboard input)
                var lastChar = currentBox.Text.Last();
                if (char.IsDigit(lastChar))
                {
                    currentBox.Text = lastChar.ToString();
                    currentBox.CaretIndex = 1; // Position cursor at end
                    System.Diagnostics.Debug.WriteLine($"Virtual keyboard input handled: '{lastChar}' in box {currentIndex + 1}");
                }
                else
                {
                    // If last character is not a digit, clear the box
                    currentBox.Text = "";
                }
            }
            else if (currentBox.Text.Length == 1 && !char.IsDigit(currentBox.Text[0]))
            {
                // Clear non-digit characters
                currentBox.Text = "";
            }

            // Auto-advance to next box if current box is filled
            if (!string.IsNullOrEmpty(currentBox.Text) && currentIndex < _pinBoxes.Length - 1)
            {
                // Clear focus from current box first
                currentBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                
                // Focus next box
                _pinBoxes[currentIndex + 1].Focus();
                _pinBoxes[currentIndex + 1].SelectAll();
                
                System.Diagnostics.Debug.WriteLine($"Auto-advanced to box {currentIndex + 2}");
            }

            // Check if PIN is complete
            CheckPINCompletion();
        }

        /// <summary>
        /// Handle PIN box focus events to ensure virtual keyboard targets correct box
        /// </summary>
        private void PINBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox currentBox)
            {
                var currentIndex = Array.IndexOf(_pinBoxes, currentBox);
                System.Diagnostics.Debug.WriteLine($"PINBox_GotFocus: Box {currentIndex + 1} is now focused");
                
                // Ensure the virtual keyboard knows this is the active input
                currentBox.SelectAll();
            }
        }

        /// <summary>
        /// Handle backspace navigation
        /// </summary>
        private void PINBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Back || sender is not TextBox currentBox)
                return;

            var currentIndex = Array.IndexOf(_pinBoxes, currentBox);

            // If current box is empty and backspace is pressed, move to previous box
            if (string.IsNullOrEmpty(currentBox.Text) && currentIndex > 0)
            {
                _pinBoxes[currentIndex - 1].Focus();
                _pinBoxes[currentIndex - 1].SelectAll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Check if PIN entry is complete (exactly 4 digits)
        /// </summary>
        private void CheckPINCompletion()
        {
            var filledCount = _pinBoxes.Count(box => !string.IsNullOrEmpty(box.Text));

            if (filledCount == 4)
            {
                VerifyButton.IsEnabled = true;
                
                // Highlight all boxes as ready - use same styling as login screen
                foreach (var border in _pinBorders)
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)); // #66FFFFFF - same as login screen
                }
            }
            else
            {
                VerifyButton.IsEnabled = false;
                
                // Reset border colors - use same styling as login screen
                foreach (var border in _pinBorders)
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF - same as login screen default
                }
            }
        }

        #endregion

        #region PIN Verification

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                ShowError("Please select a user first.");
                return;
            }

            try
            {
                VerifyButton.IsEnabled = false;
                var enteredPIN = string.Join("", _pinBoxes.Select(box => box.Text));

                // Verify PIN against the selected user only
                // Rationale: Secure - prevents PIN collision, only checks selected user's PIN
                var isValid = _pinService.VerifyPIN(
                    enteredPIN,
                    _selectedUser.RecoveryPIN!,
                    _selectedUser.RecoveryPINSalt!
                );

                if (isValid)
                {
                    // Success! Clear failed attempts and proceed to password reset
                    _pinService.ClearFailedAttempts(_selectedUser.Username);
                    
                    LoggingService.Application.Information("PIN recovery successful",
                        ("UserId", _selectedUser.UserId),
                        ("Username", _selectedUser.Username));

                    // Highlight boxes as success - use brighter white for success
                    foreach (var border in _pinBorders)
                    {
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255)); // #99FFFFFF - brighter white for success
                    }

                    ShowSuccess("✓ PIN verified! Redirecting to password reset...");
                    
                    // Hide virtual keyboard on successful PIN verification
                    VirtualKeyboardService.Instance?.HideKeyboard();
                    
                    // Wait a moment then navigate
                    await System.Threading.Tasks.Task.Delay(1500);
                    RecoverySuccessful?.Invoke(this, _selectedUser);
                }
                else
                {
                    // Failed attempt - record it for this specific user
                    _pinService.RecordFailedAttempt(_selectedUser.Username);
                    var remaining = _pinService.GetRemainingAttempts(_selectedUser.Username);

                    LoggingService.Application.Warning("PIN recovery failed",
                        ("UserId", _selectedUser.UserId),
                        ("Username", _selectedUser.Username),
                        ("RemainingAttempts", remaining));

                    // Highlight boxes as error - use red tinted white for error
                    foreach (var border in _pinBorders)
                    {
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(153, 255, 100, 100)); // #99FF6464 - red tinted white for error
                    }

                    if (remaining > 0)
                    {
                        ShowError($"✗ Incorrect PIN. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
                        AttemptsWarning.Text = $"⚠️ {remaining} attempt{(remaining == 1 ? "" : "s")} remaining";
                        AttemptsWarning.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ShowError("✗ Too many failed attempts for this account. Please try again in 1 minute.");
                        AttemptsWarning.Visibility = Visibility.Collapsed;
                        PINEntrySection.Visibility = Visibility.Collapsed;
                        
                        // Start 1-minute lockout timer
                        StartLockoutTimer();
                    }

                    // Clear PIN boxes for retry - do NOT hide keyboard
                    ClearPINBoxes();
                    
                    // Small delay to ensure keyboard doesn't disappear during clearing
                    await System.Threading.Tasks.Task.Delay(50);
                    
                    // Re-focus and ensure keyboard stays visible
                    PIN1.Focus();
                    var parentWindow = Window.GetWindow(this);
                    if (parentWindow != null)
                    {
                        await VirtualKeyboardService.Instance.ShowKeyboardAsync(PIN1, parentWindow);
                    }
                }

                VerifyButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("PIN verification error", ex,
                    ("UserId", _selectedUser?.UserId ?? "Unknown"));
                ShowError("An error occurred. Please try again.");
                VerifyButton.IsEnabled = true;
            }
        }

        #endregion

        #region UI Helpers

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackToLogin?.Invoke(this, EventArgs.Empty);
        }

        private void ClearPINBoxes()
        {
            foreach (var box in _pinBoxes)
            {
                box.Text = string.Empty;
            }

            foreach (var border in _pinBorders)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF - same as login screen default
            }
        }

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

        private void HideMessages()
        {
            StatusMessage.Visibility = Visibility.Collapsed;
            AttemptsWarning.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Start a 1-minute timer to automatically re-enable the UI after lockout
        /// Rationale: Prevents permanent lockout - allows other admins to use the recovery system
        /// 1-minute lockout provides good security while maintaining usability in kiosk environments
        /// </summary>
        private void StartLockoutTimer()
        {
            // Stop existing timer if any
            _lockoutTimer?.Stop();
            
            // Create a new timer for 1 minute lockout
            _lockoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            
            _lockoutTimer.Tick += (s, e) =>
            {
                // Re-enable the UI after lockout period
                UserComboBox.IsEnabled = true;
                HideMessages();
                
                // If a user is still selected, show the PIN entry section again
                if (_selectedUser != null)
                {
                    PINEntrySection.Visibility = Visibility.Visible;
                    ClearPINBoxes();
                    PIN1.Focus();
                }
                
                LoggingService.Application.Information("PIN recovery lockout timer expired - UI re-enabled");
                
                // Stop and dispose timer
                _lockoutTimer?.Stop();
                _lockoutTimer = null;
            };
            
            _lockoutTimer.Start();
            
            LoggingService.Application.Information("PIN recovery lockout timer started - 1 minute",
                ("LockedUser", _selectedUser?.Username ?? "Unknown"));
        }

        #endregion
    }
}

