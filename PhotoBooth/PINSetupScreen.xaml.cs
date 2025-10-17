using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Photobooth.Models;
using Photobooth.Services;

namespace Photobooth
{
    /// <summary>
    /// PIN Setup screen with square boxes for PIN entry (phone-style UX)
    /// Shown after successful password change to set up recovery PIN
    /// </summary>
    public partial class PINSetupScreen : UserControl
    {
        private readonly IDatabaseService _databaseService;
        private readonly AdminUser _currentUser;
        private readonly TextBox[] _pinBoxes;
        private readonly Border[] _pinBorders;
        private string _enteredPIN = string.Empty;

        public event EventHandler? PINSetupCompleted;

        public PINSetupScreen(IDatabaseService databaseService, AdminUser currentUser)
        {
            InitializeComponent();

            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

            // Rationale: Store references to all boxes for easier iteration and manipulation
            _pinBoxes = new[] { PIN1, PIN2, PIN3, PIN4 };
            _pinBorders = new[] { PIN1Border, PIN2Border, PIN3Border, PIN4Border };

            // Set focus to first box when loaded
            Loaded += (s, e) => PIN1.Focus();
        }

        #region PIN Input Handling

        /// <summary>
        /// Handle numeric-only input validation
        /// Rationale: PINs should only contain digits
        /// </summary>
        private void PINBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        /// <summary>
        /// Handle auto-advance and PIN completion
        /// Rationale: Auto-advance provides smooth UX similar to phone PIN entry
        /// </summary>
        private void PINBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            // Handle virtual keyboard input - ensure only one character per box
            if (textBox.Text.Length > 1)
            {
                // Take only the last character (for virtual keyboard input)
                var lastChar = textBox.Text.Last();
                if (char.IsDigit(lastChar))
                {
                    textBox.Text = lastChar.ToString();
                    textBox.CaretIndex = 1; // Position cursor at end
                }
                else
                {
                    // If last character is not a digit, clear the box
                    textBox.Text = "";
                }
            }
            else if (textBox.Text.Length == 1 && !char.IsDigit(textBox.Text[0]))
            {
                // Clear non-digit characters
                textBox.Text = "";
            }

            // Auto-advance to next box when digit is entered
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                var currentIndex = Array.IndexOf(_pinBoxes, textBox);
                
                // Highlight filled box with same styling as login screen
                if (currentIndex >= 0 && currentIndex < _pinBorders.Length)
                {
                    _pinBorders[currentIndex].BorderBrush = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)); // #66FFFFFF - same as login screen
                }

                // Move to next box if not at the end
                if (currentIndex < _pinBoxes.Length - 1)
                {
                    _pinBoxes[currentIndex + 1].Focus();
                }
            }

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
        /// Handle KeyDown events as fallback for virtual keyboard Enter key
        /// </summary>
        private void PINBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            if (e.Key == Key.Enter)
            {
                // Handle Enter key - submit PIN if complete
                if (ContinueButton.IsEnabled)
                {
                    ContinueButton_Click(ContinueButton, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Handle backspace navigation and Enter key
        /// Rationale: Backspace should go to previous box for intuitive editing, Enter should submit PIN
        /// </summary>
        private void PINBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            if (e.Key == Key.Back)
            {
                var currentIndex = Array.IndexOf(_pinBoxes, textBox);
                
                // If current box is empty and backspace is pressed, go to previous box
                if (string.IsNullOrEmpty(textBox.Text) && currentIndex > 0)
                {
                    _pinBoxes[currentIndex - 1].Focus();
                    _pinBoxes[currentIndex - 1].SelectAll();
                    e.Handled = true;
                }
                else if (!string.IsNullOrEmpty(textBox.Text))
                {
                    // Reset border color when deleting - use same as login screen
                    if (currentIndex >= 0 && currentIndex < _pinBorders.Length)
                    {
                        _pinBorders[currentIndex].BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF - same as login screen default
                    }
                }
            }
            else if (e.Key == Key.Enter)
            {
                // Handle Enter key - submit PIN if complete
                if (ContinueButton.IsEnabled)
                {
                    ContinueButton_Click(ContinueButton, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Check if PIN entry is complete (exactly 4 digits)
        /// </summary>
        private void CheckPINCompletion()
        {
            // Count filled boxes (only first 4, since we only show 4 boxes)
            // Rationale: Fixed to 4 digits to avoid user confusion during recovery
            var filledCount = _pinBoxes.Count(box => !string.IsNullOrEmpty(box.Text));

            // PIN must be exactly 4 digits
            if (filledCount == 4)
            {
                // Get the entered PIN
                _enteredPIN = string.Join("", _pinBoxes.Select(box => box.Text));

                // Show success message and enable continue button
                ShowStatusMessage($"✓ 4-digit PIN entered. Click Continue to save.");
                ContinueButton.IsEnabled = true;
            }
            else
            {
                // Hide messages and disable continue button if PIN is incomplete
                HideMessages();
                ContinueButton.IsEnabled = false;
            }
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// Handle Continue button - save PIN and proceed
        /// </summary>
        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ContinueButton.IsEnabled = false;
                ContinueButton.Content = "Saving...";

                LoggingService.Application.Information("Saving recovery PIN",
                    ("UserId", _currentUser.UserId),
                    ("PINLength", _enteredPIN.Length));

                // Hash and save PIN
                var pinService = new PINRecoveryService();
                var (hash, salt) = pinService.HashPINWithNewSalt(_enteredPIN);

                var result = await _databaseService.UpdateUserRecoveryPINAsync(_currentUser.UserId, hash, salt);

                if (result.Success)
                {
                    LoggingService.Application.Information("Recovery PIN saved successfully",
                        ("UserId", _currentUser.UserId),
                        ("Username", _currentUser.Username));

                    ShowStatusMessage("✓ PIN saved successfully!");
                    await System.Threading.Tasks.Task.Delay(1000); // Brief pause to show success

                    // Hide virtual keyboard on successful PIN save
                    VirtualKeyboardService.Instance?.HideKeyboard();

                    // Trigger completion event
                    PINSetupCompleted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ShowErrorMessage($"Failed to save PIN: {result.ErrorMessage ?? "Unknown error"}");
                    ContinueButton.Content = "Continue";
                    
                    // Only re-enable if PIN is still complete
                    var hasCompletePIN = _pinBoxes.All(box => !string.IsNullOrEmpty(box.Text));
                    ContinueButton.IsEnabled = hasCompletePIN;
                    
                    if (!hasCompletePIN)
                    {
                        _enteredPIN = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Error saving PIN", ex,
                    ("UserId", _currentUser.UserId));
                ShowErrorMessage("An error occurred. Please try again.");
                ContinueButton.Content = "Continue";
                
                // Only re-enable if PIN is still complete
                var hasCompletePIN = _pinBoxes.All(box => !string.IsNullOrEmpty(box.Text));
                ContinueButton.IsEnabled = hasCompletePIN;
                
                if (!hasCompletePIN)
                {
                    _enteredPIN = string.Empty;
                }
            }
        }

        #endregion

        #region UI Helpers

        private void ShowStatusMessage(string message)
        {
            StatusMessage.Text = message;
            StatusMessage.Visibility = Visibility.Visible;
            ErrorMessage.Visibility = Visibility.Collapsed;
        }

        private void ShowErrorMessage(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
            StatusMessage.Visibility = Visibility.Collapsed;
        }

        private void HideMessages()
        {
            StatusMessage.Visibility = Visibility.Collapsed;
            ErrorMessage.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}

