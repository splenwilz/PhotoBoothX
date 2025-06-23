using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Photobooth.Controls;
using System.Collections.Generic;

namespace Photobooth.Services
{
    public class VirtualKeyboardService
    {
        private static VirtualKeyboardService? _instance;
        private VirtualKeyboard? _currentKeyboard;
        private Panel? _keyboardContainer;
        private Control? _activeInput;
        private Window? _parentWindow;

        public static VirtualKeyboardService Instance => _instance ??= new VirtualKeyboardService();

        private VirtualKeyboardService() { }

        /// <summary>
        /// Show virtual keyboard for the specified input control
        /// </summary>
        public void ShowKeyboard(Control inputControl, Window parentWindow)
        {
            try
            {
                // Determine keyboard mode based on input type
                var keyboardMode = DetermineKeyboardMode(inputControl);

                // Check if we can reuse the existing keyboard
                if (_currentKeyboard != null && _activeInput != null && _parentWindow == parentWindow)
                {
                    // Check if the keyboard mode matches and we're on the same window
                    var currentMode = _currentKeyboard.GetCurrentMode();
                    
                    // Allow reusing keyboard for all text-based modes (Text and Password) on same page
                    // Only recreate for truly different input types (like Numeric)
                    bool canReuse = (currentMode == keyboardMode) || 
                                   (IsTextBasedMode(currentMode) && IsTextBasedMode(keyboardMode));
                    
                    if (canReuse)
                    {
                        // Just switch the active input and update mode if needed - preserve keyboard position
                        _activeInput = inputControl;
                        
                        // Ensure the new input has focus
                        EnsureInputFocus();
                        
                        // If the mode is different but compatible, update the keyboard mode
                        if (currentMode != keyboardMode)
                        {
                            _currentKeyboard.ChangeMode(keyboardMode);
                            Console.WriteLine($"Reusing existing keyboard, changed mode from {currentMode} to {keyboardMode}");
                        }
                        else
                        {
                            Console.WriteLine($"Reusing existing keyboard, switched active input to: {_activeInput?.GetType().Name}");
                        }
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"Keyboard mode changed from {currentMode} to {keyboardMode}, recreating keyboard");
                    }
                }

                // Need to create a new keyboard - hide existing one first
                if (_currentKeyboard != null && _keyboardContainer != null)
                {
                    // Find and remove the canvas wrapper that contains the keyboard
                    Canvas? canvasWrapper = null;
                    foreach (var child in _keyboardContainer.Children.OfType<Canvas>())
                    {
                        if (child.Children.Contains(_currentKeyboard))
                        {
                            canvasWrapper = child;
                            break;
                        }
                    }

                    if (canvasWrapper != null)
                    {
                        _keyboardContainer.Children.Remove(canvasWrapper);
                    }
                    else
                    {
                        // Fallback - try to remove directly (for older keyboards)
                        _keyboardContainer.Children.Remove(_currentKeyboard);
                    }
                    
                    _currentKeyboard.OnKeyPressed -= HandleKeyPressed;
                    _currentKeyboard.OnSpecialKeyPressed -= HandleSpecialKeyPressed;
                    _currentKeyboard.OnKeyboardClosed -= HandleKeyboardClosed;
                    _currentKeyboard = null;
                    Console.WriteLine("Previous virtual keyboard hidden");
                }

                // Set the new active input and parent window
                _activeInput = inputControl;
                _parentWindow = parentWindow;
                Console.WriteLine($"Active input set to: {_activeInput?.GetType().Name}");

                // Find the main container (usually a Grid or Panel at the root)
                if (parentWindow.Content is Panel mainPanel)
                {
                    _keyboardContainer = mainPanel;
                }
                else if (parentWindow.Content is Border border && border.Child is Panel panel)
                {
                    _keyboardContainer = panel;
                }
                else if (parentWindow.Content is Grid grid)
                {
                    _keyboardContainer = grid;
                }

                if (_keyboardContainer == null)
                {
                    Console.WriteLine("Could not find suitable container for virtual keyboard");
                    return;
                }

                // Use delayed login screen check to ensure visual tree is fully loaded
                CheckLoginScreenWithDelayAndCreate(parentWindow, keyboardMode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing virtual keyboard: {ex.Message}");
            }
                }

        /// <summary>
        /// Ensure the active input control has focus and shows the cursor
        /// </summary>
        private void EnsureInputFocus()
        {
            if (_activeInput == null) return;

            try
            {
                // Ensure the input control is focusable and focus it
                if (_activeInput.Focusable)
                {
                    _activeInput.Focus();
                    Keyboard.Focus(_activeInput);
                    Console.WriteLine($"Focus set to: {_activeInput.GetType().Name}");
                }
                
                // For TextBox, also ensure cursor is visible
                if (_activeInput is TextBox textBox && textBox.IsLoaded)
                {
                    // This ensures the caret is visible and blinking
                    textBox.Select(textBox.Text?.Length ?? 0, 0);
                }
                // For PasswordBox, ensure cursor is positioned correctly at the end
                else if (_activeInput is PasswordBox passwordBox && passwordBox.IsLoaded)
                {
                    // Use Dispatcher to ensure the caret positioning happens after the UI updates
                    passwordBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Move cursor to the end of the password
                            passwordBox.Focus();
                            
                            // Use reflection to set the caret position to the end
                            var passwordLength = passwordBox.Password?.Length ?? 0;
                            var selectMethod = passwordBox.GetType().GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            selectMethod?.Invoke(passwordBox, new object[] { passwordLength, 0 });
                        }
                        catch (Exception reflectionEx)
                        {
                            Console.WriteLine($"Reflection approach failed, using alternative: {reflectionEx.Message}");
                            // Fallback - just ensure focus
                            passwordBox.Focus();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring input focus: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if this is a login screen with a delay to ensure visual tree is loaded, then create keyboard
        /// </summary>
        private async void CheckLoginScreenWithDelayAndCreate(Window parentWindow, VirtualKeyboardMode keyboardMode)
        {
            try
            {
                // First immediate check
                var isLoginScreen = IsLoginScreen(parentWindow);
                Console.WriteLine($"Immediate login screen check: {isLoginScreen}");

                // If not detected as login screen, wait a bit for visual tree to load and check again
                if (!isLoginScreen)
                {
                    Console.WriteLine("Login screen not detected immediately, checking again after delay...");
                    await System.Threading.Tasks.Task.Delay(100); // Short delay for visual tree to load
                    isLoginScreen = IsLoginScreen(parentWindow);
                    Console.WriteLine($"Delayed login screen check: {isLoginScreen}");
                }

                // Create keyboard with appropriate transparency
                CreateAndShowKeyboard(keyboardMode, isLoginScreen);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in delayed login screen check: {ex.Message}");
                // Fallback - create keyboard without transparency
                CreateAndShowKeyboard(keyboardMode, false);
            }
        }

        /// <summary>
        /// Create and show the virtual keyboard
        /// </summary>
        private void CreateAndShowKeyboard(VirtualKeyboardMode keyboardMode, bool isLoginScreen)
        {
            try
            {
                // Create and configure virtual keyboard
                _currentKeyboard = new VirtualKeyboard(keyboardMode, isLoginScreen);
                Console.WriteLine($"Virtual keyboard created with transparency: {isLoginScreen}");
                
                Console.WriteLine("Subscribing to keyboard events");
                _currentKeyboard.OnKeyPressed += HandleKeyPressed;
                _currentKeyboard.OnSpecialKeyPressed += HandleSpecialKeyPressed;
                _currentKeyboard.OnKeyboardClosed += HandleKeyboardClosed;
                Console.WriteLine("Event subscription complete");

                // Position keyboard at bottom of screen initially
                _currentKeyboard.VerticalAlignment = VerticalAlignment.Bottom;
                _currentKeyboard.HorizontalAlignment = HorizontalAlignment.Stretch;
                _currentKeyboard.Margin = new Thickness(0, 0, 0, 0);

                // Add keyboard to container
                if (_keyboardContainer != null)
                {
                    // Create a Canvas wrapper for proper drag functionality
                    var canvasWrapper = new Canvas
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        ClipToBounds = false // Allow keyboard to be dragged anywhere
                    };
                    
                    Panel.SetZIndex(canvasWrapper, 9999); // Ensure wrapper is on top
                    _keyboardContainer.Children.Add(canvasWrapper);
                    
                    // Add keyboard to canvas wrapper
                    canvasWrapper.Children.Add(_currentKeyboard);
                    
                    // Now Canvas positioning will work properly
                    // Center horizontally if possible, otherwise position at left
                    if (_keyboardContainer.ActualWidth > 0 && _currentKeyboard.Width > 0)
                    {
                        var centerX = (_keyboardContainer.ActualWidth - _currentKeyboard.Width) / 2;
                        Canvas.SetLeft(_currentKeyboard, Math.Max(0, centerX));
                    }
                    else
                    {
                        // Fallback position for initial load
                        Canvas.SetLeft(_currentKeyboard, 100);
                    }
                    
                    Canvas.SetBottom(_currentKeyboard, 0);
                    
                    // Initialize drag and resize with the canvas wrapper as parent
                    _currentKeyboard.InitializeDragResize(canvasWrapper);
                    
                    // Ensure the input field has focus when keyboard is shown
                    EnsureInputFocus();
                    
                    Console.WriteLine($"Virtual keyboard shown with drag/resize functionality and transparency: {isLoginScreen}");
                }
                else
                {
                    Console.WriteLine("Error: Keyboard container is null, cannot add keyboard");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating and showing virtual keyboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Hide the virtual keyboard
        /// </summary>
        public void HideKeyboard()
        {
            try
            {
                if (_currentKeyboard != null && _keyboardContainer != null)
                {
                    // Find and remove the canvas wrapper that contains the keyboard
                    Canvas? canvasWrapper = null;
                    foreach (var child in _keyboardContainer.Children.OfType<Canvas>())
                    {
                        if (child.Children.Contains(_currentKeyboard))
                        {
                            canvasWrapper = child;
                            break;
                        }
                    }

                    if (canvasWrapper != null)
                    {
                        _keyboardContainer.Children.Remove(canvasWrapper);
                    }
                    else
                    {
                        // Fallback - try to remove directly (for older keyboards)
                        _keyboardContainer.Children.Remove(_currentKeyboard);
                    }
                    
                    _currentKeyboard.OnKeyPressed -= HandleKeyPressed;
                    _currentKeyboard.OnSpecialKeyPressed -= HandleSpecialKeyPressed;
                    _currentKeyboard.OnKeyboardClosed -= HandleKeyboardClosed;
                    _currentKeyboard = null;
                    Console.WriteLine("Virtual keyboard hidden");
                }

                _activeInput = null;
                _keyboardContainer = null;
                _parentWindow = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error hiding virtual keyboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a keyboard mode is text-based (Text or Password)
        /// </summary>
        private bool IsTextBasedMode(VirtualKeyboardMode mode)
        {
            return mode == VirtualKeyboardMode.Text || mode == VirtualKeyboardMode.Password;
        }

        /// <summary>
        /// Determine the appropriate keyboard mode based on input control
        /// </summary>
        private VirtualKeyboardMode DetermineKeyboardMode(Control inputControl)
        {
            // Check control name or properties to determine mode
            var controlName = inputControl.Name?.ToLower() ?? "";
            
            if (controlName.Contains("price") || controlName.Contains("number") || 
                controlName.Contains("priority") || controlName.Contains("sort"))
            {
                return VirtualKeyboardMode.Numeric;
            }

            if (inputControl is PasswordBox)
            {
                return VirtualKeyboardMode.Password;
            }

            // Default to text mode
            return VirtualKeyboardMode.Text;
        }

        /// <summary>
        /// Check if the current window is the login screen
        /// </summary>
        private bool IsLoginScreen(Window window)
        {
            try
            {
                // Check window type or title to determine if it's the login screen
                var windowType = window.GetType().Name;
                var windowTitle = window.Title ?? "";
                
                Console.WriteLine($"Window type: {windowType}, Title: {windowTitle}");
                
                // VERY RESTRICTIVE: Only apply transparency if we can confirm this is definitely the login screen
                // Must have BOTH AdminLoginScreen UserControl AND specific login controls
                if (window.Content != null)
                {
                    var hasAdminLoginScreen = HasAdminLoginScreenUserControl(window.Content);
                    var hasSpecificLoginControls = HasSpecificLoginControls(window.Content);
                    
                    Console.WriteLine($"Has AdminLoginScreen UserControl: {hasAdminLoginScreen}");
                    Console.WriteLine($"Has specific login controls: {hasSpecificLoginControls}");
                    
                    // BOTH conditions must be true
                    if (hasAdminLoginScreen && hasSpecificLoginControls)
                    {
                        Console.WriteLine("CONFIRMED: This is the login screen (both UserControl and controls found)");
                        return true;
                    }
                }
                
                // Don't rely on window type or title alone - too unreliable
                Console.WriteLine("NOT CONFIRMED as login screen - applying normal keyboard styling");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting login screen: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the content contains the AdminLoginScreen UserControl specifically
        /// </summary>
        private bool HasAdminLoginScreenUserControl(object content)
        {
            try
            {
                if (content is DependencyObject dependencyObject)
                {
                    // Check if this is the AdminLoginScreen UserControl specifically
                    if (content.GetType().Name == "AdminLoginScreen")
                    {
                        Console.WriteLine("Found AdminLoginScreen UserControl (exact match)");
                        return true;
                    }
                    
                    // Check children recursively
                    var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(dependencyObject);
                    for (int i = 0; i < childrenCount; i++)
                    {
                        var child = System.Windows.Media.VisualTreeHelper.GetChild(dependencyObject, i);
                        if (HasAdminLoginScreenUserControl(child))
                        {
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for AdminLoginScreen UserControl: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the content contains the specific login controls (UsernameInput, PasswordInput, LoginButton)
        /// </summary>
        private bool HasSpecificLoginControls(object content)
        {
            try
            {
                var foundControls = new HashSet<string>();
                CheckForSpecificLoginControls(content, foundControls);
                
                // Must have ALL three specific controls
                var hasUsername = foundControls.Contains("usernameinput");
                var hasPassword = foundControls.Contains("passwordinput");
                var hasLoginButton = foundControls.Contains("loginbutton");
                
                Console.WriteLine($"Found controls: {string.Join(", ", foundControls)}");
                Console.WriteLine($"Has Username: {hasUsername}, Has Password: {hasPassword}, Has LoginButton: {hasLoginButton}");
                
                return hasUsername && hasPassword && hasLoginButton;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for specific login controls: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Recursively check for specific login controls by name
        /// </summary>
        private void CheckForSpecificLoginControls(object content, HashSet<string> foundControls)
        {
            try
            {
                if (content is FrameworkElement element)
                {
                    var name = element.Name?.ToLower() ?? "";
                    
                    // Only look for EXACT matches of our login controls
                    if (name == "usernameinput" || name == "passwordinput" || name == "loginbutton")
                    {
                        foundControls.Add(name);
                        Console.WriteLine($"Found specific login control: {name}");
                    }
                }
                
                if (content is DependencyObject dependencyObject)
                {
                    // Check children recursively
                    var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(dependencyObject);
                    for (int i = 0; i < childrenCount; i++)
                    {
                        var child = System.Windows.Media.VisualTreeHelper.GetChild(dependencyObject, i);
                        CheckForSpecificLoginControls(child, foundControls);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckForSpecificLoginControls: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle regular key presses
        /// </summary>
        private void HandleKeyPressed(string key)
        {
            Console.WriteLine($"HandleKeyPressed called with key: '{key}'");
            
            if (_activeInput == null) 
            {
                Console.WriteLine("No active input control");
                return;
            }

            try
            {
                Console.WriteLine($"Active input type: {_activeInput.GetType().Name}");
                
                // Ensure the input control has focus before processing key
                EnsureInputFocus();
                
                if (_activeInput is TextBox textBox)
                {
                    var caretIndex = textBox.CaretIndex;
                    var currentText = textBox.Text ?? "";
                    
                    Console.WriteLine($"Current text: '{currentText}', Caret: {caretIndex}");
                    
                    // Insert character at caret position
                    var newText = currentText.Insert(caretIndex, key);
                    textBox.Text = newText;
                    textBox.CaretIndex = caretIndex + 1;
                    
                    // Ensure focus is maintained after text change
                    textBox.Focus();
                    
                    Console.WriteLine($"New text: '{newText}'");
                }
                else if (_activeInput is PasswordBox passwordBox)
                {
                    // For password boxes, just append (can't get caret position easily)
                    passwordBox.Password += key;
                    
                    // Use Dispatcher to ensure proper focus and cursor positioning after password change
                    passwordBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            passwordBox.Focus();
                            
                            // Use reflection to position cursor at end
                            var passwordLength = passwordBox.Password?.Length ?? 0;
                            var selectMethod = passwordBox.GetType().GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            selectMethod?.Invoke(passwordBox, new object[] { passwordLength, 0 });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error positioning password cursor: {ex.Message}");
                            passwordBox.Focus(); // Fallback
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                    
                    Console.WriteLine($"Added to password box");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling key press: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle special key presses (backspace, enter, etc.)
        /// </summary>
        private void HandleSpecialKeyPressed(VirtualKeyboardSpecialKey specialKey)
        {
            Console.WriteLine($"HandleSpecialKeyPressed called with: {specialKey}");
            
            if (_activeInput == null) 
            {
                Console.WriteLine("No active input control for special key");
                return;
            }

            try
            {
                switch (specialKey)
                {
                    case VirtualKeyboardSpecialKey.Backspace:
                        Console.WriteLine("Handling backspace");
                        HandleBackspace();
                        break;
                    case VirtualKeyboardSpecialKey.Enter:
                        Console.WriteLine("Handling enter");
                        HandleEnter();
                        break;
                    case VirtualKeyboardSpecialKey.Space:
                        Console.WriteLine("Handling space");
                        HandleKeyPressed(" ");
                        break;
                    case VirtualKeyboardSpecialKey.Tab:
                        Console.WriteLine("Handling tab");
                        HandleTab();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling special key: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle backspace key
        /// </summary>
        private void HandleBackspace()
        {
            // Ensure the input control has focus before processing backspace
            EnsureInputFocus();
            
            if (_activeInput is TextBox textBox)
            {
                var caretIndex = textBox.CaretIndex;
                var currentText = textBox.Text ?? "";
                
                if (caretIndex > 0 && currentText.Length > 0)
                {
                    var newText = currentText.Remove(caretIndex - 1, 1);
                    textBox.Text = newText;
                    textBox.CaretIndex = caretIndex - 1;
                    
                    // Ensure focus is maintained after text change
                    textBox.Focus();
                }
            }
            else if (_activeInput is PasswordBox passwordBox)
            {
                var currentPassword = passwordBox.Password;
                if (currentPassword.Length > 0)
                {
                    passwordBox.Password = currentPassword.Substring(0, currentPassword.Length - 1);
                    
                    // Use Dispatcher to ensure proper focus and cursor positioning after password change
                    passwordBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            passwordBox.Focus();
                            
                            // Use reflection to position cursor at end
                            var passwordLength = passwordBox.Password?.Length ?? 0;
                            var selectMethod = passwordBox.GetType().GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            selectMethod?.Invoke(passwordBox, new object[] { passwordLength, 0 });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error positioning password cursor after backspace: {ex.Message}");
                            passwordBox.Focus(); // Fallback
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        /// <summary>
        /// Handle enter key
        /// </summary>
        private void HandleEnter()
        {
            // Simulate Enter key press
            var enterKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_activeInput), 0, Key.Enter)
            {
                RoutedEvent = UIElement.KeyDownEvent
            };
            
            _activeInput?.RaiseEvent(enterKey);
            
            // Also try to move focus to next control or close keyboard
            _activeInput?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// Handle tab key
        /// </summary>
        private void HandleTab()
        {
            // Move focus to next control
            _activeInput?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// Handle keyboard closed event
        /// </summary>
        private void HandleKeyboardClosed()
        {
            HideKeyboard();
        }
    }

    /// <summary>
    /// Virtual keyboard display modes
    /// </summary>
    public enum VirtualKeyboardMode
    {
        Text,
        Numeric,
        Password
    }

    /// <summary>
    /// Special keys for virtual keyboard
    /// </summary>
    public enum VirtualKeyboardSpecialKey
    {
        Backspace,
        Enter,
        Space,
        Tab,
        Shift,
        CapsLock
    }
} 