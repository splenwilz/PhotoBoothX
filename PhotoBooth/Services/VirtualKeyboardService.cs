using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using Photobooth.Controls;
using System.Collections.Generic;

namespace Photobooth.Services
{
    public class VirtualKeyboardService
    {
        // Use lazy initialization pattern for thread-safe singleton
        private static readonly Lazy<VirtualKeyboardService> _instance = 
            new Lazy<VirtualKeyboardService>(() => new VirtualKeyboardService());

        private VirtualKeyboard? _currentKeyboard;
        private Panel? _keyboardContainer;
        private Control? _activeInput;
        private Window? _parentWindow;

        public static VirtualKeyboardService Instance => _instance.Value;

        // Events
        public event EventHandler<bool>? KeyboardVisibilityChanged;

        private VirtualKeyboardService() { }

        // Add flag to prevent recursive operations during text manipulation
        private bool _isManipulatingText = false;
        private bool _bindingReenabled = false;
        private Dictionary<TextBox, BindingExpression> _textBoxBindingExpressions = new Dictionary<TextBox, BindingExpression>();

        /// <summary>
        /// Show virtual keyboard for the specified input control
        /// </summary>
        public async Task ShowKeyboardAsync(Control inputControl, Window parentWindow)
        {
            try
            {
                Console.WriteLine($"=== VirtualKeyboardService.ShowKeyboard CALLED ===");
                Console.WriteLine($"InputControl: {inputControl?.GetType().Name ?? "null"} '{inputControl?.Name ?? "null"}'");
                Console.WriteLine($"ParentWindow: {parentWindow?.GetType().Name ?? "null"}");
                Console.WriteLine($"Current keyboard exists: {_currentKeyboard != null}");
                
                // Prevent recursive calls during text manipulation
                if (_isManipulatingText)
                {
                    Console.WriteLine("SKIPPING ShowKeyboard - currently manipulating text");
                    return;
                }
                
                // Validate input parameters
                if (inputControl == null)
                {
                    LoggingService.Application.Warning("ShowKeyboard called with null inputControl");
                    Console.WriteLine("WARNING: inputControl is null, returning");
                    return;
                }
                
                if (parentWindow == null)
                {
                    LoggingService.Application.Warning("ShowKeyboard called with null parentWindow");
                    Console.WriteLine("WARNING: parentWindow is null, returning");
                    return;
                }

                // CRITICAL FIX: If switching to a different input, restore binding for previous input
                if (_activeInput != null && _activeInput != inputControl)
                {
                    Console.WriteLine($"Switching from previous input, restoring binding for previous input");
                    ReenableBinding(_activeInput);
                }

                // Store the active input and disable binding if it's a bound TextBox
                _activeInput = inputControl;
                
                // Only disable binding if this is a new input or the input has changed
                if (inputControl is TextBox textBox && !_textBoxBindingExpressions.ContainsKey(textBox))
                {
                    DisableBindingDuringEdit(_activeInput);
                }
                else if (inputControl is TextBox)
                {
                    Console.WriteLine($"Binding already disabled for TextBox: {inputControl.Name}");
                }
                else
                {
                    // Not a TextBox, no binding to disable
                    Console.WriteLine($"No binding to disable for {inputControl.GetType().Name}: {inputControl.Name}");
                }

                // Determine keyboard mode based on input type
                var keyboardMode = DetermineKeyboardMode(inputControl);
                Console.WriteLine($"Keyboard mode: {keyboardMode}");

                // Check if we can reuse the existing keyboard
                if (_currentKeyboard != null && _parentWindow == parentWindow)
                {
                    Console.WriteLine("Checking if existing keyboard can be reused...");
                    
                    // Check if the keyboard mode matches and we're on the same window
                    var currentMode = _currentKeyboard.GetCurrentMode();
                    
                    // Allow reusing keyboard for all text-based modes (Text and Password) on same page
                    // Only recreate for truly different input types (like Numeric)
                    bool canReuse = (currentMode == keyboardMode) || 
                                   (IsTextBasedMode(currentMode) && IsTextBasedMode(keyboardMode));
                    
                    Console.WriteLine($"Can reuse: {canReuse} (current: {currentMode}, new: {keyboardMode})");
                    
                    if (canReuse)
                    {
                        Console.WriteLine("Reusing existing keyboard");
                        
                        // Ensure the new input has focus
                        EnsureInputFocus();
                        
                        // If the mode is different but compatible, update the keyboard mode
                        if (currentMode != keyboardMode)
                        {
                            _currentKeyboard.ChangeMode(keyboardMode);
                        }
                        else
                        {
                            LoggingService.Application.Debug("Reusing existing keyboard for login screen");
                        }
                        
                        Console.WriteLine("=== VirtualKeyboardService.ShowKeyboard COMPLETED (reused) ===");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Cannot reuse existing keyboard, creating new one");
                        // Hide the existing keyboard first
                        HideKeyboard();
                    }
                    }
                    else
                    {
                    Console.WriteLine("No existing keyboard to reuse");
                }

                // Store parent window for future reference
                _parentWindow = parentWindow;

                // Always use the main application window for keyboard positioning, even for modal dialogs
                var mainWindow = GetMainApplicationWindow(parentWindow);
                _parentWindow = mainWindow;

                // Find the main container (usually a Grid or Panel at the root) in the main window
                // Try multiple attempts with small delays to handle timing issues
                var attempts = 0;
                const int maxAttempts = 3;
                
                while (_keyboardContainer == null && attempts < maxAttempts)
                {
                    attempts++;
                    Console.WriteLine($"Finding keyboard container (attempt {attempts}/{maxAttempts})");
                    
                if (_parentWindow?.Content is Panel mainPanel)
                {
                    _keyboardContainer = mainPanel;
                        Console.WriteLine("Found keyboard container as Panel");
                }
                else if (_parentWindow?.Content is Border border && border.Child is Panel panel)
                {
                    _keyboardContainer = panel;
                        Console.WriteLine("Found keyboard container as Border > Panel");
                }
                else if (_parentWindow?.Content is Grid grid)
                {
                    _keyboardContainer = grid;
                        Console.WriteLine("Found keyboard container as Grid");
                }

                    // If still not found and we have more attempts, wait a bit
                    if (_keyboardContainer == null && attempts < maxAttempts)
                {
                        Console.WriteLine("Container not found, waiting before retry...");
                        await Task.Delay(100);
                    }
                }

                if (_keyboardContainer == null)
                {
                    LoggingService.Application.Error("Failed to find keyboard container after multiple attempts", null,
                        ("ParentWindowContent", _parentWindow?.Content?.GetType().Name ?? "null"),
                        ("InputControl", inputControl.GetType().Name),
                        ("Attempts", attempts));
                    Console.WriteLine($"ERROR: Failed to find keyboard container after {attempts} attempts");
                    return;
                }

                // Try to find keyboard container with retries
                Console.WriteLine("Successfully found keyboard container, creating keyboard");
                CreateAndShowKeyboard(keyboardMode, IsLoginScreen(parentWindow));
                
                Console.WriteLine("=== VirtualKeyboardService.ShowKeyboard COMPLETED (new keyboard) ===");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to show virtual keyboard", ex);
                Console.WriteLine($"ShowKeyboard failed: {ex.Message}");
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
                Console.WriteLine("=== VirtualKeyboardService.CreateAndShowKeyboard CALLED ===");
                Console.WriteLine($"Keyboard mode: {keyboardMode}, Is login screen: {isLoginScreen}");
                Console.WriteLine($"Current keyboard exists before creation: {_currentKeyboard != null}");
                
                // CRITICAL FIX: If a keyboard already exists, clean it up first to prevent duplicates
                if (_currentKeyboard != null)
                {
                    Console.WriteLine("WARNING: A keyboard already exists! Cleaning it up before creating new one...");
                    
                    // Remove existing keyboard from container
                    if (_keyboardContainer != null)
                    {
                        Canvas? existingCanvasWrapper = null;
                        foreach (var child in _keyboardContainer.Children.OfType<Canvas>())
                        {
                            if (child.Children.Contains(_currentKeyboard))
                            {
                                existingCanvasWrapper = child;
                                break;
                            }
                        }

                        if (existingCanvasWrapper != null)
                        {
                            Console.WriteLine("Removing existing keyboard canvas wrapper");
                            _keyboardContainer.Children.Remove(existingCanvasWrapper);
                        }
                        else
                        {
                            Console.WriteLine("Removing existing keyboard directly");
                            _keyboardContainer.Children.Remove(_currentKeyboard);
                        }
                    }
                    
                    // Detach existing event handlers
                    Console.WriteLine("Detaching existing keyboard event handlers");
                    _currentKeyboard.OnKeyPressed -= HandleKeyPressed;
                    _currentKeyboard.OnSpecialKeyPressed -= HandleSpecialKeyPressed;
                    _currentKeyboard.OnKeyboardClosed -= HandleKeyboardClosed;
                    _currentKeyboard = null;
                    Console.WriteLine("Existing keyboard cleaned up successfully");
                }
                
                // Create and configure virtual keyboard
                _currentKeyboard = new VirtualKeyboard(keyboardMode, isLoginScreen);
                Console.WriteLine($"Virtual keyboard created - Instance ID: {_currentKeyboard.GetHashCode()}");

                Console.WriteLine("Attaching event handlers...");
                _currentKeyboard.OnKeyPressed += HandleKeyPressed;
                _currentKeyboard.OnSpecialKeyPressed += HandleSpecialKeyPressed;
                _currentKeyboard.OnKeyboardClosed += HandleKeyboardClosed;
                Console.WriteLine($"Event handlers attached successfully to keyboard {_currentKeyboard.GetHashCode()}");

                // Position keyboard at bottom of screen initially
                _currentKeyboard.VerticalAlignment = VerticalAlignment.Bottom;
                _currentKeyboard.HorizontalAlignment = HorizontalAlignment.Stretch;
                _currentKeyboard.Margin = new Thickness(0, 0, 0, 0);
                Console.WriteLine("Keyboard positioning set");

                // Add keyboard to container
                if (_keyboardContainer != null)
                {
                    Console.WriteLine("Adding keyboard to container...");
                    
                    // Create a Canvas wrapper for proper drag functionality
                    var canvasWrapper = new Canvas
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        ClipToBounds = false // Allow keyboard to be dragged anywhere
                    };
                    
                    Panel.SetZIndex(canvasWrapper, 9999); // Ensure wrapper is on top
                    _keyboardContainer.Children.Add(canvasWrapper);
                    Console.WriteLine("Canvas wrapper added to container");
                    
                    // Add keyboard to canvas wrapper
                    canvasWrapper.Children.Add(_currentKeyboard);
                    Console.WriteLine($"Keyboard {_currentKeyboard.GetHashCode()} added to canvas wrapper");
                    
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
                    Console.WriteLine("Keyboard positioned in canvas");
                    
                    // Initialize drag and resize with the canvas wrapper as parent
                    _currentKeyboard.InitializeDragResize(canvasWrapper);
                    Console.WriteLine("Drag and resize initialized");
                    
                    // Ensure the input field has focus when keyboard is shown
                    EnsureInputFocus();
                    Console.WriteLine("Input focus ensured");

                    // Notify that keyboard is now visible
                    KeyboardVisibilityChanged?.Invoke(this, true);
                    Console.WriteLine("KeyboardVisibilityChanged event invoked");
                }
                else
                {
                    Console.WriteLine("ERROR: No keyboard container available");
                }

                Console.WriteLine($"=== VirtualKeyboardService.CreateAndShowKeyboard COMPLETED - Final keyboard ID: {_currentKeyboard?.GetHashCode()} ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: CreateAndShowKeyboard failed: {ex.Message}");
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
                Console.WriteLine("=== VirtualKeyboardService.HideKeyboard CALLED ===");
                Console.WriteLine($"Current keyboard exists: {_currentKeyboard != null}");
                if (_currentKeyboard != null)
                {
                    Console.WriteLine($"Current keyboard ID to hide: {_currentKeyboard.GetHashCode()}");
                }
                Console.WriteLine($"Current container exists: {_keyboardContainer != null}");
                
                LoggingService.Application.Debug("Hiding virtual keyboard");
                
                if (_currentKeyboard != null && _keyboardContainer != null)
                {
                    Console.WriteLine($"Looking for keyboard {_currentKeyboard.GetHashCode()} canvas wrapper...");
                    
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
                        Console.WriteLine($"Found canvas wrapper for keyboard {_currentKeyboard.GetHashCode()}, removing from container");
                        _keyboardContainer.Children.Remove(canvasWrapper);
                        LoggingService.Application.Debug("Removed keyboard canvas wrapper from container");
                    }
                    else
                    {
                        Console.WriteLine($"No canvas wrapper found for keyboard {_currentKeyboard.GetHashCode()}, trying direct removal");
                        // Fallback - try to remove directly (for older keyboards)
                        _keyboardContainer.Children.Remove(_currentKeyboard);
                        LoggingService.Application.Debug("Removed keyboard directly from container");
                    }
                    
                    Console.WriteLine($"Detaching event handlers from keyboard {_currentKeyboard.GetHashCode()}...");
                    _currentKeyboard.OnKeyPressed -= HandleKeyPressed;
                    _currentKeyboard.OnSpecialKeyPressed -= HandleSpecialKeyPressed;
                    _currentKeyboard.OnKeyboardClosed -= HandleKeyboardClosed;
                    var hiddenKeyboardId = _currentKeyboard.GetHashCode();
                    _currentKeyboard = null;
                    Console.WriteLine($"Event handlers detached, keyboard {hiddenKeyboardId} set to null");

                    // Notify that keyboard is now hidden
                    KeyboardVisibilityChanged?.Invoke(this, false);
                    Console.WriteLine("KeyboardVisibilityChanged event invoked");
                }
                else
                {
                    Console.WriteLine("No keyboard or container to hide");
                }

                // Re-enable binding for the active input before clearing it (unless already done)
                if (_activeInput != null && !_bindingReenabled)
                {
                    ReenableBinding(_activeInput);
                }
                _bindingReenabled = false; // Reset flag

                // Reset manipulation flag
                _isManipulatingText = false;

                // Clear the active input reference
                _activeInput = null;
                Console.WriteLine("Active input cleared");
                
                // Don't clear _keyboardContainer and _parentWindow - preserve them for next show
                // This prevents timing issues when quickly hiding and showing keyboard
                LoggingService.Application.Debug("Virtual keyboard hidden, preserving container for next show");
                Console.WriteLine("=== VirtualKeyboardService.HideKeyboard COMPLETED ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: HideKeyboard failed: {ex.Message}");
                LoggingService.Application.Error("Failed to hide keyboard", ex);
            }
        }

        /// <summary>
        /// Reset the keyboard service state completely
        /// Call this when navigating between different screens/windows
        /// </summary>
        public void ResetState()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== VirtualKeyboardService.ResetState CALLED ===");
                System.Diagnostics.Debug.WriteLine($"Current keyboard exists: {_currentKeyboard != null}");
                System.Diagnostics.Debug.WriteLine($"Current container exists: {_keyboardContainer != null}");
                System.Diagnostics.Debug.WriteLine($"Current active input exists: {_activeInput != null}");
                System.Diagnostics.Debug.WriteLine($"Current parent window exists: {_parentWindow != null}");
                
                LoggingService.Application.Debug("Resetting virtual keyboard service state");
                
                // Hide any existing keyboard first
                System.Diagnostics.Debug.WriteLine("Calling HideKeyboard()...");
                HideKeyboard();
                System.Diagnostics.Debug.WriteLine("HideKeyboard() completed");
                
                // Clear all state
                _currentKeyboard = null;
                _keyboardContainer = null;
                _activeInput = null;
                _parentWindow = null;
                _isManipulatingText = false;
                
                System.Diagnostics.Debug.WriteLine("All state cleared");
                LoggingService.Application.Debug("Virtual keyboard service state reset complete");
                System.Diagnostics.Debug.WriteLine("=== VirtualKeyboardService.ResetState COMPLETED ===");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to reset keyboard service state", ex);
                System.Diagnostics.Debug.WriteLine($"ResetState failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
            var controlTag = inputControl.Tag?.ToString()?.ToLower() ?? "";
            
            // Check both Name and Tag for price/number indicators (case-insensitive)
            if (controlName.Contains("price") || controlName.Contains("number") || 
                controlName.Contains("priority") || controlName.Contains("sort") ||
                controlName.Contains("discount") || controlName.Contains("amount") ||
                controlTag.Contains("price") || controlTag.Contains("number") ||
                controlTag.Contains("discount") || controlTag.Contains("amount") ||
                // Specific product keys that should use numeric keyboard for pricing
                controlTag.Contains("photostrips") || controlTag.Contains("photo4x6") || 
                controlTag.Contains("smartphoneprint") ||
                // Specific TextBox names for the new simplified pricing system
                controlName.Contains("extracopypriceinput") || controlName.Contains("multiplecopydiscountinput"))
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
        /// Handle regular key press
        /// </summary>
        private void HandleKeyPressed(string key)
        {
            Console.WriteLine($"=== VirtualKeyboardService.HandleKeyPressed CALLED ===");
            Console.WriteLine($"Key pressed: '{key}'");
            Console.WriteLine($"Active input: {_activeInput?.GetType().Name ?? "null"} '{_activeInput?.Name ?? "null"}'");
            
            if (_activeInput != null)
                        {
                Console.WriteLine($"Active input current text: '{GetInputText(_activeInput)}'");
                Console.WriteLine("Sending key to active input...");
                    
                // Send the key to the active input control
                SendKeyToInput(key);
                
                Console.WriteLine($"Active input text after key: '{GetInputText(_activeInput)}'");
                        }
            else
            {
                Console.WriteLine("WARNING: No active input to send key to");
            }
            Console.WriteLine($"=== VirtualKeyboardService.HandleKeyPressed COMPLETED ===");
        }

        /// <summary>
        /// Handle special key press (backspace, enter, etc.)
        /// </summary>
        private void HandleSpecialKeyPressed(VirtualKeyboardSpecialKey specialKey)
        {
            Console.WriteLine($"=== VirtualKeyboardService.HandleSpecialKeyPressed CALLED ===");
            Console.WriteLine($"Special key pressed: {specialKey}");
            Console.WriteLine($"Active input: {_activeInput?.GetType().Name ?? "null"} '{_activeInput?.Name ?? "null"}'");

            if (_activeInput != null)
            {
                Console.WriteLine($"Active input current text: '{GetInputText(_activeInput)}'");
            }

                switch (specialKey)
                {
                    case VirtualKeyboardSpecialKey.Backspace:
                    Console.WriteLine("Processing backspace...");
                        HandleBackspace();
                        break;
                    case VirtualKeyboardSpecialKey.Enter:
                    Console.WriteLine("Processing enter...");
                        HandleEnter();
                        break;
                    case VirtualKeyboardSpecialKey.Space:
                    Console.WriteLine("Processing space...");
                    SendKeyToInput(" ");
                        break;
                    case VirtualKeyboardSpecialKey.Tab:
                    Console.WriteLine("Processing tab...");
                        HandleTab();
                        break;
                case VirtualKeyboardSpecialKey.Shift:
                    Console.WriteLine("Processing shift...");
                    // Note: Shift handling is done internally by the keyboard control
                    break;
                case VirtualKeyboardSpecialKey.CapsLock:
                    Console.WriteLine("Processing caps lock...");
                    // Note: CapsLock handling is done internally by the keyboard control
                    break;
            }
            
            if (_activeInput != null)
            {
                Console.WriteLine($"Active input text after special key: '{GetInputText(_activeInput)}'");
            }
            Console.WriteLine($"=== VirtualKeyboardService.HandleSpecialKeyPressed COMPLETED ===");
        }

        /// <summary>
        /// Handle backspace key
        /// </summary>
        private void HandleBackspace()
        {
            Console.WriteLine($"=== VirtualKeyboardService.HandleBackspace CALLED ===");
            Console.WriteLine($"Active input: {_activeInput?.GetType().Name ?? "null"} '{_activeInput?.Name ?? "null"}'");
            
            if (_activeInput == null)
            {
                Console.WriteLine("WARNING: No active input for backspace");
                return;
            }

            Console.WriteLine($"Active input current text before backspace: '{GetInputText(_activeInput)}'");

            try
            {
                // Set manipulation flag to prevent interference
                _isManipulatingText = true;
            
            if (_activeInput is TextBox textBox)
            {
                    Console.WriteLine($"Processing backspace for TextBox, CaretIndex: {textBox.CaretIndex}");
                    
                        var caretIndex = textBox.CaretIndex;
                        var currentText = textBox.Text ?? "";
                        
                        if (caretIndex > 0 && currentText.Length > 0)
                        {
                        Console.WriteLine($"Removing character at position {caretIndex - 1}");
                        // Remove character before caret
                            var newText = currentText.Remove(caretIndex - 1, 1);
                            textBox.Text = newText;
                            textBox.CaretIndex = caretIndex - 1;
                        Console.WriteLine($"New text after backspace: '{newText}', New caret: {caretIndex - 1}");
                        }
                    else
                    {
                        Console.WriteLine("Nothing to delete - caret at beginning or empty text");
                    }
            }
            else if (_activeInput is PasswordBox passwordBox)
            {
                    Console.WriteLine($"Processing backspace for PasswordBox, current length: {passwordBox.Password?.Length ?? 0}");
                    
                    var currentPassword = passwordBox.Password ?? "";
                if (currentPassword.Length > 0)
                {
                        Console.WriteLine($"Removing last character from password");
                        // Remove last character
                    passwordBox.Password = currentPassword.Substring(0, currentPassword.Length - 1);
                        Console.WriteLine($"New password length: {passwordBox.Password.Length}");
                    }
                    else
                    {
                        Console.WriteLine("Nothing to delete - password is empty");
                    }
                }
                        }
                        catch (Exception ex)
                        {
                Console.WriteLine($"ERROR in HandleBackspace: {ex.Message}");
                LoggingService.Application.Error("Failed to handle backspace", ex);
                        }
            finally
            {
                // Reset manipulation flag
                _isManipulatingText = false;
                }
            
            Console.WriteLine($"=== VirtualKeyboardService.HandleBackspace COMPLETED ===");
        }

        /// <summary>
        /// Handle enter key
        /// </summary>
        private void HandleEnter()
        {
            // Check if we're in a price input field in the Admin Dashboard
            if (_activeInput is TextBox textBox)
            {
                var textBoxName = textBox.Name?.ToLower() ?? "";
                var textBoxTag = textBox.Tag?.ToString() ?? "";
                
                // Check for both old tag-based and new name-based price fields
                if (textBoxTag == "PhotoStrips" || textBoxTag == "Photo4x6" || textBoxTag == "SmartphonePrint" ||
                    textBoxName.Contains("extracopypriceinput") || textBoxName.Contains("multiplecopydiscountinput"))
                {
                    // This is a product price field - update binding first, then save and close keyboard
                    _bindingReenabled = true;
                    ReenableBinding(_activeInput);
                    TriggerProductConfigurationSave();
                    HideKeyboard();
                    return;
                }
            }

            // Default Enter behavior for other inputs
            var enterKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_activeInput), 0, Key.Enter)
            {
                RoutedEvent = UIElement.KeyDownEvent
            };
            
            _activeInput?.RaiseEvent(enterKey);
            
            // Also try to move focus to next control or close keyboard
            _activeInput?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// Trigger the save functionality for product configuration
        /// </summary>
        private void TriggerProductConfigurationSave()
        {
            try
            {
                // Find the AdminDashboardScreen in the current window
                var adminDashboard = FindAdminDashboardScreen(_parentWindow);
                if (adminDashboard != null)
                {
                    // Use reflection to call the SaveProductConfig_Click method
                    var saveMethod = adminDashboard.GetType().GetMethod("SaveProductConfig_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (saveMethod != null)
                    {
                        // Create dummy event args for the method call
                        saveMethod.Invoke(adminDashboard, new object[] { adminDashboard, new RoutedEventArgs() });
                        Console.WriteLine("Product configuration save triggered by Enter key");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error triggering product configuration save: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the AdminDashboardScreen instance in the window
        /// </summary>
        private object? FindAdminDashboardScreen(Window? window)
        {
            if (window == null) return null;

            // Search through the visual tree for AdminDashboardScreen
            return FindAdminDashboardScreenInElement(window);
        }

        /// <summary>
        /// Recursively search for AdminDashboardScreen in visual tree
        /// </summary>
        private object? FindAdminDashboardScreenInElement(DependencyObject element)
        {
            if (element == null) return null;

            // Check if this element is AdminDashboardScreen
            if (element.GetType().Name == "AdminDashboardScreen")
                return element;

            // Search children
            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = FindAdminDashboardScreenInElement(child);
                if (result != null) return result;
            }

            return null;
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
            Console.WriteLine("=== VirtualKeyboardService.HandleKeyboardClosed CALLED ===");
            Console.WriteLine($"Current tracked keyboard ID: {_currentKeyboard?.GetHashCode() ?? -1}");
            Console.WriteLine("Close button clicked, calling HideKeyboard()");
            HideKeyboard();
            Console.WriteLine("=== VirtualKeyboardService.HandleKeyboardClosed COMPLETED ===");
        }

        /// <summary>
        /// Send a key to the active input control
        /// </summary>
        private void SendKeyToInput(string key)
        {
            if (_activeInput == null)
            {
                LoggingService.Application.Warning("Attempted to send key to null active input");
                return;
            }

            try
            {
                // Set manipulation flag to prevent interference
                _isManipulatingText = true;

                if (_activeInput is TextBox textBox)
                {
                    // Directly manipulate text without focus changes
                    var caretIndex = textBox.CaretIndex;
                    var currentText = textBox.Text ?? "";

                    // Insert character at caret position
                    var newText = currentText.Insert(caretIndex, key);
                    textBox.Text = newText;
                    textBox.CaretIndex = caretIndex + 1;
                    
                    Console.WriteLine($"SendKeyToInput - Inserted '{key}' at position {caretIndex}, new text: '{newText}'");
                }
                else if (_activeInput is PasswordBox passwordBox)
                {
                    // For password boxes, just append (can't get caret position easily)
                    passwordBox.Password += key;
                    
                    Console.WriteLine($"SendKeyToInput - Added '{key}' to password, new length: {passwordBox.Password?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to send key to input", ex);
            }
            finally
            {
                // Reset manipulation flag
                _isManipulatingText = false;
            }
        }

        /// <summary>
        /// Get the current text content of the active input control
        /// </summary>
        private string GetInputText(Control inputControl)
        {
            if (inputControl == null)
            {
                return "null";
            }

            if (inputControl is TextBox textBox)
            {
                return textBox.Text;
            }
            else if (inputControl is PasswordBox passwordBox)
            {
                return passwordBox.Password;
            }
            return "N/A";
        }

        /// <summary>
        /// Temporarily disable binding on TextBox controls to prevent auto-formatting
        /// </summary>
        private void DisableBindingDuringEdit(Control inputControl)
        {
            if (inputControl is TextBox textBox)
            {
                // Store the original binding expression and current text
                var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                if (bindingExpression != null)
                {
                    _textBoxBindingExpressions[textBox] = bindingExpression;
                    var currentText = textBox.Text; // Store current text value
                    
                    var textBoxId = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
                    Console.WriteLine($"About to disable binding for TextBox: {textBoxId}, current text: '{currentText}'");
                    
                    // Actually remove the binding by setting to null
                    BindingOperations.ClearBinding(textBox, TextBox.TextProperty);
                    
                    // Restore the current text value after clearing binding
                    textBox.Text = currentText;
                    
                    Console.WriteLine($"Temporarily disabled binding for TextBox: {textBoxId}, preserved text: '{currentText}'");
                }
                else
                {
                    var textBoxId = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
                    Console.WriteLine($"No binding found for TextBox: {textBoxId}");
                }
            }
        }

        /// <summary>
        /// Re-enable binding on TextBox controls if they were previously disabled
        /// </summary>
        private void ReenableBinding(Control inputControl)
        {
            if (inputControl is TextBox textBox && _textBoxBindingExpressions.ContainsKey(textBox))
            {
                var bindingExpression = _textBoxBindingExpressions[textBox];
                if (bindingExpression != null)
                {
                    // Preserve the current user input before restoring binding
                    var currentUserInput = textBox.Text;
                    Console.WriteLine($"Preserving user input before binding restore: '{currentUserInput}'");
                    
                    // Restore the original binding
                    var binding = bindingExpression.ParentBinding;
                    textBox.SetBinding(TextBox.TextProperty, binding);
                    
                    // Set the user's input back to prevent ViewModel overwrite
                    var currentTextAfterBinding = textBox.Text;
                    Console.WriteLine($"Text after binding restore: '{currentTextAfterBinding}', setting back to user input: '{currentUserInput}'");
                    textBox.Text = currentUserInput;
                    
                    // CRITICAL FIX: Force binding to update the source (ViewModel) IMMEDIATELY
                    // This ensures the ViewModel gets the new value even with UpdateSourceTrigger=LostFocus
                    var newBindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                    if (newBindingExpression != null)
                    {
                        var textBoxId = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
                        Console.WriteLine($"Manually updating binding source for TextBox: {textBoxId} with value: '{currentUserInput}'");
                        newBindingExpression.UpdateSource();
                    }
                    
                    var textBoxId2 = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
                    Console.WriteLine($"Re-enabled binding for TextBox: {textBoxId2}, restored user input: '{currentUserInput}'");
                }
                _textBoxBindingExpressions.Remove(textBox);
            }
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
