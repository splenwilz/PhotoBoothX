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

        // Event to notify when keyboard visibility changes
        public event EventHandler<bool>? KeyboardVisibilityChanged;

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

                        }
                        else
                        {

                        }
                        return;
                    }
                    else
                    {

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

                }

                // Set the new active input and determine the correct parent window
                _activeInput = inputControl;


                // Always use the main application window for keyboard positioning, even for modal dialogs
                var mainWindow = GetMainApplicationWindow(parentWindow);
                _parentWindow = mainWindow;


                // Check if input control's window is different from main window
                var inputWindow = Window.GetWindow(_activeInput);


                // Find the main container (usually a Grid or Panel at the root) in the main window
                if (_parentWindow?.Content is Panel mainPanel)
                {
                    _keyboardContainer = mainPanel;
                }
                else if (_parentWindow?.Content is Border border && border.Child is Panel panel)
                {
                    _keyboardContainer = panel;
                }
                else if (_parentWindow?.Content is Grid grid)
                {
                    _keyboardContainer = grid;
                }

                if (_keyboardContainer == null)
                {

                    return;
                }

                // Use delayed login screen check to ensure visual tree is fully loaded
                // Pass the original parentWindow for login detection, but use mainWindow for positioning
                CheckLoginScreenWithDelayAndCreate(parentWindow, keyboardMode);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to show keyboard", ex);
            }
        }

        /// <summary>
        /// Ensure the active input control has focus and shows the cursor
        /// </summary>
        private void EnsureInputFocus()
        {
            if (_activeInput == null) 
            {

                return;
            }

            try
            {


                // Find the window that contains the active input control
                var inputWindow = Window.GetWindow(_activeInput);


                // If the input is in a different window (modal dialog), handle cross-window focus
                if (inputWindow != null && inputWindow != _parentWindow)
                {


                    // DON'T make the modal topmost - this blocks keyboard button clicks
                    // Instead, just ensure the modal is active but allow main window to receive input
                    if (!inputWindow.IsActive)
                    {
                        inputWindow.Activate();

                    }
                    
                    // Keep both windows accessible - don't use topmost
                    // This allows the modal to receive text input while keyboard buttons remain clickable


                }
                else
                {

                }
                
                // Use Dispatcher to ensure focus happens after window activation
                _activeInput.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // For cross-window scenarios, ensure the input window is brought to foreground
                        var inputWindow = Window.GetWindow(_activeInput);
                        if (inputWindow != null && inputWindow != _parentWindow)
                        {
                            // Bring the modal window to foreground without making it topmost
                            inputWindow.Activate();
                            inputWindow.Focus();

                        }
                        
                        // Ensure the input control is focusable and focus it


                        if (_activeInput.Focusable)
                        {
                            var focusResult1 = _activeInput.Focus();
                            var focusResult2 = Keyboard.Focus(_activeInput);


                            // Check if focus was actually set
                            var currentFocus = Keyboard.FocusedElement;


                        }
                        else
                        {

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
                            try
                            {
                                // Move cursor to the end of the password
                                passwordBox.Focus();
                                
                                // Use reflection to set the caret position to the end
                                var passwordLength = passwordBox.Password?.Length ?? 0;
                                var selectMethod = passwordBox.GetType().GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                selectMethod?.Invoke(passwordBox, new object[] { passwordLength, 0 });
                            }
                            catch
                            {

                                // Fallback - just ensure focus
                                passwordBox.Focus();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Application.Error("Failed to set input focus in dispatcher", ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);

            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to ensure input focus", ex);
            }
        }

        /// <summary>
        /// Get the main application window, avoiding modal dialogs
        /// </summary>
        private Window GetMainApplicationWindow(Window currentWindow)
        {
            return GetMainApplicationWindow(currentWindow, 0);
        }

        /// <summary>
        /// Get the main application window with recursion depth limiting to prevent stack overflow
        /// </summary>
        private Window GetMainApplicationWindow(Window currentWindow, int depth)
        {
            const int MaxDepth = 10;
            if (depth > MaxDepth)
            {
                LoggingService.Application.Warning("Maximum recursion depth reached in GetMainApplicationWindow",
                    ("Depth", depth), ("CurrentWindowType", currentWindow?.GetType().Name ?? "null"));
                return currentWindow ?? Application.Current.MainWindow ?? throw new InvalidOperationException("No main window available");
            }

            try
            {
                // If this is the main window, use it
                if (currentWindow == Application.Current.MainWindow)
                {
                    return currentWindow;
                }
                
                // If this is a modal dialog with an owner, check if the owner is the main window
                if (currentWindow.Owner != null)
                {
                    if (currentWindow.Owner == Application.Current.MainWindow)
                    {
                        return currentWindow.Owner;
                    }
                    // Recursively check if the owner's owner is the main window (nested modals)
                    return GetMainApplicationWindow(currentWindow.Owner, depth + 1);
                }
                
                // Fallback to Application.Current.MainWindow if available
                if (Application.Current.MainWindow != null)
                {
                    return Application.Current.MainWindow;
                }
                
                // Last resort - use the current window
                return currentWindow;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to get main application window", ex);
                return currentWindow; // Fallback
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

                // If not detected as login screen, wait a bit for visual tree to load and check again
                if (!isLoginScreen)
                {

                    await System.Threading.Tasks.Task.Delay(200); // Increased delay for visual tree to load
                    isLoginScreen = IsLoginScreen(parentWindow);

                }

                // Create keyboard with appropriate transparency
                CreateAndShowKeyboard(keyboardMode, isLoginScreen);
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to check login screen with delay", ex);
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


                _currentKeyboard.OnKeyPressed += HandleKeyPressed;
                _currentKeyboard.OnSpecialKeyPressed += HandleSpecialKeyPressed;
                _currentKeyboard.OnKeyboardClosed += HandleKeyboardClosed;

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

                    // Notify that keyboard is now visible
                    KeyboardVisibilityChanged?.Invoke(this, true);
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to create and show keyboard", ex);
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

                    // Notify that keyboard is now hidden
                    KeyboardVisibilityChanged?.Invoke(this, false);
                }

                _activeInput = null;
                _keyboardContainer = null;
                _parentWindow = null;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to hide keyboard", ex);
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

                // VERY RESTRICTIVE: Only apply transparency if we can confirm this is definitely the login screen
                // Must have BOTH AdminLoginScreen UserControl AND specific login controls
                if (window.Content != null)
                {
                    var hasAdminLoginScreen = HasAdminLoginScreenUserControl(window.Content);
                    var hasSpecificLoginControls = HasSpecificLoginControls(window.Content);


                    // BOTH conditions must be true
                    if (hasAdminLoginScreen && hasSpecificLoginControls)
                    {

                        return true;
                    }
                }
                
                // Don't rely on window type or title alone - too unreliable

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to check if window is login screen", ex);
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
                LoggingService.Application.Error("Failed to check for AdminLoginScreen UserControl", ex);
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


                return hasUsername && hasPassword && hasLoginButton;
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to check for specific login controls", ex);
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
                LoggingService.Application.Error("Failed to check for specific login controls", ex);
            }
        }

        /// <summary>
        /// Handle regular key presses
        /// </summary>
        private void HandleKeyPressed(string key)
        {


            if (_activeInput == null) 
            {

                return;
            }

            try
            {


                // Check current focus before we ensure focus
                var currentFocusBefore = Keyboard.FocusedElement;


                // Ensure the input control has focus before processing key
                EnsureInputFocus();
                
                // Check focus after ensuring focus
                var currentFocusAfter = Keyboard.FocusedElement;


                if (_activeInput is TextBox textBox)
                {
                    // Use Dispatcher to ensure proper threading for cross-window operations
                    textBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var caretIndex = textBox.CaretIndex;
                            var currentText = textBox.Text ?? "";


                            // Insert character at caret position
                            var newText = currentText.Insert(caretIndex, key);
                            textBox.Text = newText;
                            textBox.CaretIndex = caretIndex + 1;
                            
                            // Ensure focus is maintained after text change
                            var focusResult = textBox.Focus();


                        }
                        catch (Exception ex)
                        {
                            LoggingService.Application.Error("Failed to handle key press for TextBox", ex);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
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
                            LoggingService.Application.Error("Failed to set password box cursor position in HandleKeyPressed", ex);
                            passwordBox.Focus(); // Fallback
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);

                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to handle key press", ex);
            }

        }

        /// <summary>
        /// Handle special key presses (backspace, enter, etc.)
        /// </summary>
        private void HandleSpecialKeyPressed(VirtualKeyboardSpecialKey specialKey)
        {

            if (_activeInput == null) 
            {

                return;
            }

            try
            {
                switch (specialKey)
                {
                    case VirtualKeyboardSpecialKey.Backspace:

                        HandleBackspace();
                        break;
                    case VirtualKeyboardSpecialKey.Enter:

                        HandleEnter();
                        break;
                    case VirtualKeyboardSpecialKey.Space:

                        HandleKeyPressed(" ");
                        break;
                    case VirtualKeyboardSpecialKey.Tab:

                        HandleTab();
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to handle special key press", ex);
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
                // Use Dispatcher to ensure proper threading for cross-window operations
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
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
                    catch (Exception ex)
                    {
                        LoggingService.Application.Error("Failed to handle backspace for TextBox", ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
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
                            LoggingService.Application.Error("Failed to set password box cursor position in HandleBackspace", ex);
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
