using System;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Callback to be invoked when price input is completed (Enter key pressed on price fields)
        /// </summary>
        public Action<string>? OnPriceInputComplete { get; set; }

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
                LoggingService.Application.Debug("VirtualKeyboardService.ShowKeyboard called", 
                    ("InputControlType", inputControl?.GetType().Name ?? "null"),
                    ("InputControlName", inputControl?.Name ?? "null"),
                    ("ParentWindowType", parentWindow?.GetType().Name ?? "null"),
                    ("CurrentKeyboardExists", _currentKeyboard != null));
                
                // Prevent recursive calls during text manipulation
                if (_isManipulatingText)
                {
                    LoggingService.Application.Debug("Skipping ShowKeyboard - currently manipulating text");
                    return;
                }
                
                // Validate input parameters
                if (inputControl == null)
                {
                    LoggingService.Application.Warning("ShowKeyboard called with null inputControl");
                    return;
                }
                
                if (parentWindow == null)
                {
                    LoggingService.Application.Warning("ShowKeyboard called with null parentWindow");
                    return;
                }

                // CRITICAL FIX: If switching to a different input, restore binding for previous input
                if (_activeInput != null && _activeInput != inputControl)
                {
                    LoggingService.Application.Debug("Switching from previous input, restoring binding for previous input");
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
                    LoggingService.Application.Debug("Binding already disabled for TextBox", ("TextBoxName", inputControl.Name));
                }
                else
                {
                    // Not a TextBox, no binding to disable
                    LoggingService.Application.Debug("No binding to disable for non-TextBox control", 
                        ("ControlType", inputControl.GetType().Name),
                        ("ControlName", inputControl.Name));
                }

                // Determine keyboard mode based on input type
                var keyboardMode = DetermineKeyboardMode(inputControl);
                LoggingService.Application.Debug("Keyboard mode determined", ("Mode", keyboardMode.ToString()));

                // Check if we can reuse the existing keyboard
                if (_currentKeyboard != null && _parentWindow == parentWindow)
                {
                    LoggingService.Application.Debug("Checking if existing keyboard can be reused");
                    
                    // Check if the keyboard mode matches and we're on the same window
                    var currentMode = _currentKeyboard.GetCurrentMode();
                    
                    // Allow reusing keyboard for all text-based modes (Text and Password) on same page
                    // Only recreate for truly different input types (like Numeric)
                    bool canReuse = (currentMode == keyboardMode) || 
                                   (IsTextBasedMode(currentMode) && IsTextBasedMode(keyboardMode));
                    
                    LoggingService.Application.Debug("Keyboard reuse decision", 
                        ("CanReuse", canReuse),
                        ("CurrentMode", currentMode.ToString()),
                        ("NewMode", keyboardMode.ToString()));
                    
                    if (canReuse)
                    {
                        LoggingService.Application.Debug("Reusing existing keyboard");
                        
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
                        
                        LoggingService.Application.Debug("VirtualKeyboardService.ShowKeyboard completed (reused)");
                        return;
                    }
                    else
                    {
                        LoggingService.Application.Debug("Cannot reuse existing keyboard, creating new one");
                        // Hide the existing keyboard first
                        HideKeyboard();
                    }
                    }
                    else
                    {
                        LoggingService.Application.Debug("No existing keyboard to reuse");
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
                    LoggingService.Application.Debug("Finding keyboard container", ("Attempt", attempts), ("MaxAttempts", maxAttempts));
                    
                    if (_parentWindow?.Content is Panel mainPanel)
                    {
                        _keyboardContainer = mainPanel;
                        LoggingService.Application.Debug("Found keyboard container as Panel");
                    }
                    else if (_parentWindow?.Content is Border border && border.Child is Panel panel)
                    {
                        _keyboardContainer = panel;
                        LoggingService.Application.Debug("Found keyboard container as Border > Panel");
                    }
                    else if (_parentWindow?.Content is Grid grid)
                    {
                        _keyboardContainer = grid;
                        LoggingService.Application.Debug("Found keyboard container as Grid");
                    }

                    // If still not found and we have more attempts, wait a bit
                    if (_keyboardContainer == null && attempts < maxAttempts)
                    {
                        LoggingService.Application.Debug("Container not found, waiting before retry");
                        await Task.Delay(100);
                    }
                }

                if (_keyboardContainer == null)
                {
                    LoggingService.Application.Error("Failed to find keyboard container after multiple attempts", null,
                        ("ParentWindowContent", _parentWindow?.Content?.GetType().Name ?? "null"),
                        ("InputControl", inputControl.GetType().Name),
                        ("Attempts", attempts));
                    return;
                }

                // Try to find keyboard container with retries
                LoggingService.Application.Debug("Successfully found keyboard container, creating keyboard");
                CreateAndShowKeyboard(keyboardMode, IsLoginScreen(parentWindow));
                
                LoggingService.Application.Debug("VirtualKeyboardService.ShowKeyboard completed (new keyboard)");
            }
            catch (Exception ex)
            {
                LoggingService.Application.Error("Failed to show virtual keyboard", ex);
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
                LoggingService.Application.Debug("VirtualKeyboardService.CreateAndShowKeyboard called",
                    ("KeyboardMode", keyboardMode.ToString()),
                    ("IsLoginScreen", isLoginScreen),
                    ("CurrentKeyboardExists", _currentKeyboard != null));
                
                // CRITICAL FIX: If a keyboard already exists, clean it up first to prevent duplicates
                if (_currentKeyboard != null)
                {
                    LoggingService.Application.Debug("A keyboard already exists, cleaning it up before creating new one");
                    
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
                            LoggingService.Application.Debug("Removing existing keyboard canvas wrapper");
                            _keyboardContainer.Children.Remove(existingCanvasWrapper);
                        }
                        else
                        {
                            LoggingService.Application.Debug("Removing existing keyboard directly");
                            _keyboardContainer.Children.Remove(_currentKeyboard);
                        }
                    }
                    
                    // Detach existing event handlers
                    LoggingService.Application.Debug("Detaching existing keyboard event handlers");
                    _currentKeyboard.OnKeyPressed -= HandleKeyPressed;
                    _currentKeyboard.OnSpecialKeyPressed -= HandleSpecialKeyPressed;
                    _currentKeyboard.OnKeyboardClosed -= HandleKeyboardClosed;
                    _currentKeyboard = null;
                    LoggingService.Application.Debug("Existing keyboard cleaned up successfully");
                }
                
                // Create and configure virtual keyboard
                _currentKeyboard = new VirtualKeyboard(keyboardMode, isLoginScreen);
                LoggingService.Application.Debug("Virtual keyboard created", ("InstanceId", _currentKeyboard.GetHashCode()));

                LoggingService.Application.Debug("Attaching event handlers");
                _currentKeyboard.OnKeyPressed += HandleKeyPressed;
                _currentKeyboard.OnSpecialKeyPressed += HandleSpecialKeyPressed;
                _currentKeyboard.OnKeyboardClosed += HandleKeyboardClosed;
                LoggingService.Application.Debug("Event handlers attached successfully", ("KeyboardId", _currentKeyboard.GetHashCode()));

                // Position keyboard at bottom of screen initially
                _currentKeyboard.VerticalAlignment = VerticalAlignment.Bottom;
                _currentKeyboard.HorizontalAlignment = HorizontalAlignment.Stretch;
                _currentKeyboard.Margin = new Thickness(0, 0, 0, 0);
#if DEBUG
                Console.WriteLine("Keyboard positioning set");
#endif

                // Add keyboard to container
                if (_keyboardContainer != null)
                {
#if DEBUG
                    Console.WriteLine("Adding keyboard to container...");
#endif
                    
                    // Create a Canvas wrapper for proper drag functionality
                    var canvasWrapper = new Canvas
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        ClipToBounds = false // Allow keyboard to be dragged anywhere
                    };
                    
                    Panel.SetZIndex(canvasWrapper, 9999); // Ensure wrapper is on top
                    _keyboardContainer.Children.Add(canvasWrapper);
                    LoggingService.Application.Debug("Canvas wrapper added to container");
                    
                    // Add keyboard to canvas wrapper
                    canvasWrapper.Children.Add(_currentKeyboard);
                    LoggingService.Application.Debug("Keyboard added to canvas wrapper", ("KeyboardId", _currentKeyboard.GetHashCode()));
                    
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
                    LoggingService.Application.Debug("Keyboard positioned in canvas");
                    
                    // Initialize drag and resize with the canvas wrapper as parent
                    _currentKeyboard.InitializeDragResize(canvasWrapper);
                    LoggingService.Application.Debug("Drag and resize initialized");
                    
                    // Ensure the input field has focus when keyboard is shown
                    EnsureInputFocus();
                    LoggingService.Application.Debug("Input focus ensured");

                    // Notify that keyboard is now visible
                    KeyboardVisibilityChanged?.Invoke(this, true);
                    LoggingService.Application.Debug("KeyboardVisibilityChanged event invoked");
                }
                else
                {
                    LoggingService.Application.Error("No keyboard container available");
                }

                LoggingService.Application.Debug("VirtualKeyboardService.CreateAndShowKeyboard completed", ("FinalKeyboardId", _currentKeyboard?.GetHashCode() ?? -1));
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
                LoggingService.Application.Debug("VirtualKeyboardService.HideKeyboard called",
                    ("CurrentKeyboardExists", _currentKeyboard != null),
                    ("CurrentKeyboardId", _currentKeyboard?.GetHashCode() ?? -1),
                    ("CurrentContainerExists", _keyboardContainer != null));
                
                LoggingService.Application.Debug("Hiding virtual keyboard");
                
                if (_currentKeyboard != null && _keyboardContainer != null)
                {
                    LoggingService.Application.Debug("Looking for keyboard canvas wrapper", ("KeyboardId", _currentKeyboard.GetHashCode()));
                    
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
                        LoggingService.Application.Debug("Found canvas wrapper for keyboard, removing from container", ("KeyboardId", _currentKeyboard.GetHashCode()));
                        _keyboardContainer.Children.Remove(canvasWrapper);
                        LoggingService.Application.Debug("Removed keyboard canvas wrapper from container");
                    }
                    else
                    {
                        LoggingService.Application.Debug("No canvas wrapper found for keyboard, trying direct removal", ("KeyboardId", _currentKeyboard.GetHashCode()));
                        // Fallback - try to remove directly (for older keyboards)
                        _keyboardContainer.Children.Remove(_currentKeyboard);
                        LoggingService.Application.Debug("Removed keyboard directly from container");
                    }
                    
                    LoggingService.Application.Debug("Detaching event handlers from keyboard", ("KeyboardId", _currentKeyboard.GetHashCode()));
                    _currentKeyboard.OnKeyPressed -= HandleKeyPressed;
                    _currentKeyboard.OnSpecialKeyPressed -= HandleSpecialKeyPressed;
                    _currentKeyboard.OnKeyboardClosed -= HandleKeyboardClosed;
                    var hiddenKeyboardId = _currentKeyboard.GetHashCode();
                    _currentKeyboard = null;
                    LoggingService.Application.Debug("Event handlers detached, keyboard set to null", ("HiddenKeyboardId", hiddenKeyboardId));

                    // Notify that keyboard is now hidden
                    KeyboardVisibilityChanged?.Invoke(this, false);
                    LoggingService.Application.Debug("KeyboardVisibilityChanged event invoked");
                }
                else
                {
                    LoggingService.Application.Debug("No keyboard or container to hide");
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
                LoggingService.Application.Debug("Active input cleared");
                
                // Don't clear _keyboardContainer and _parentWindow - preserve them for next show
                // This prevents timing issues when quickly hiding and showing keyboard
                LoggingService.Application.Debug("Virtual keyboard hidden, preserving container for next show");
                LoggingService.Application.Debug("VirtualKeyboardService.HideKeyboard completed");
            }
            catch (Exception ex)
            {
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
                _textBoxBindingExpressions.Clear();
                _bindingReenabled = false;
                
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
#if DEBUG
            Console.WriteLine($"=== VirtualKeyboardService.HandleKeyPressed CALLED ===");
            Console.WriteLine($"Key pressed: {(_activeInput is PasswordBox ? "<hidden>" : $"'{key}'")}");
            Console.WriteLine($"Active input: {_activeInput?.GetType().Name ?? "null"} '{_activeInput?.Name ?? "null"}'");
#endif
            
            if (_activeInput != null)
                        {
#if DEBUG
                Console.WriteLine($"Active input current text: '{(_activeInput is PasswordBox ? "<hidden>" : GetInputText(_activeInput))}'");
                Console.WriteLine("Sending key to active input...");
#endif
                    
                // Send the key to the active input control
                SendKeyToInput(key);
                
#if DEBUG
                Console.WriteLine($"Active input text after key: '{(_activeInput is PasswordBox ? "<hidden>" : GetInputText(_activeInput))}'");
#endif
                        }
            else
            {
#if DEBUG
                Console.WriteLine("WARNING: No active input to send key to");
#endif
            }
#if DEBUG
            Console.WriteLine($"=== VirtualKeyboardService.HandleKeyPressed COMPLETED ===");
#endif
        }

        /// <summary>
        /// Handle special key press (backspace, enter, etc.)
        /// </summary>
        private void HandleSpecialKeyPressed(VirtualKeyboardSpecialKey specialKey)
        {
#if DEBUG
            Console.WriteLine($"=== VirtualKeyboardService.HandleSpecialKeyPressed CALLED ===");
            Console.WriteLine($"Special key pressed: {specialKey}");
            Console.WriteLine($"Active input: {_activeInput?.GetType().Name ?? "null"} '{_activeInput?.Name ?? "null"}'");
#endif

            if (_activeInput != null)
            {
#if DEBUG
                Console.WriteLine($"Active input current text: '{GetInputText(_activeInput)}'");
#endif
            }

                switch (specialKey)
                {
                    case VirtualKeyboardSpecialKey.Backspace:
#if DEBUG
                    Console.WriteLine("Processing backspace...");
#endif
                        HandleBackspace();
                        break;
                    case VirtualKeyboardSpecialKey.Enter:
#if DEBUG
                    Console.WriteLine("Processing enter...");
#endif
                        HandleEnter();
                        break;
                    case VirtualKeyboardSpecialKey.Space:
#if DEBUG
                    Console.WriteLine("Processing space...");
#endif
                    SendKeyToInput(" ");
                        break;
                    case VirtualKeyboardSpecialKey.Tab:
#if DEBUG
                    Console.WriteLine("Processing tab...");
#endif
                        HandleTab();
                        break;
                case VirtualKeyboardSpecialKey.Shift:
#if DEBUG
                    Console.WriteLine("Processing shift...");
#endif
                    // Note: Shift handling is done internally by the keyboard control
                    break;
                case VirtualKeyboardSpecialKey.CapsLock:
#if DEBUG
                    Console.WriteLine("Processing caps lock...");
#endif
                    // Note: CapsLock handling is done internally by the keyboard control
                    break;
            }
            
            if (_activeInput != null)
            {
#if DEBUG
                Console.WriteLine($"Active input text after special key: '{GetInputText(_activeInput)}'");
#endif
            }
#if DEBUG
            Console.WriteLine($"=== VirtualKeyboardService.HandleSpecialKeyPressed COMPLETED ===");
#endif
        }

        /// <summary>
        /// Handle backspace key
        /// </summary>
        private void HandleBackspace()
        {
#if DEBUG
            Console.WriteLine($"=== VirtualKeyboardService.HandleBackspace CALLED ===");
            Console.WriteLine($"Active input: {_activeInput?.GetType().Name ?? "null"} '{_activeInput?.Name ?? "null"}'");
#endif
            
            if (_activeInput == null)
            {
#if DEBUG
                Console.WriteLine("WARNING: No active input for backspace");
#endif
                return;
            }

#if DEBUG
            Console.WriteLine($"Active input current text before backspace: '{GetInputText(_activeInput)}'");
#endif

            try
            {
                // Set manipulation flag to prevent interference
                _isManipulatingText = true;
            
            if (_activeInput is TextBox textBox)
            {
#if DEBUG
                    Console.WriteLine($"Processing backspace for TextBox, CaretIndex: {textBox.CaretIndex}");
#endif
                    
                        var caretIndex = textBox.CaretIndex;
                        var currentText = textBox.Text ?? "";
                        
                        if (caretIndex > 0 && currentText.Length > 0)
                        {
#if DEBUG
                        Console.WriteLine($"Removing character at position {caretIndex - 1}");
#endif
                        // Remove character before caret
                            var newText = currentText.Remove(caretIndex - 1, 1);
                            textBox.Text = newText;
                            textBox.CaretIndex = caretIndex - 1;
#if DEBUG
                        Console.WriteLine($"New text after backspace: '{newText}', New caret: {caretIndex - 1}");
#endif
                        }
                    else
                    {
#if DEBUG
                        Console.WriteLine("Nothing to delete - caret at beginning or empty text");
#endif
                    }
            }
            else if (_activeInput is PasswordBox passwordBox)
            {
#if DEBUG
                    Console.WriteLine($"Processing backspace for PasswordBox, current length: {passwordBox.Password?.Length ?? 0}");
#endif
                    
                    var currentPassword = passwordBox.Password ?? "";
                if (currentPassword.Length > 0)
                {
#if DEBUG
                        Console.WriteLine($"Removing last character from password");
#endif
                        // Remove last character
                    passwordBox.Password = currentPassword.Substring(0, currentPassword.Length - 1);
#if DEBUG
                        Console.WriteLine($"New password length: {passwordBox.Password.Length}");
#endif
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine("Nothing to delete - password is empty");
#endif
                    }
                }
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                Console.WriteLine($"ERROR in HandleBackspace: {ex.Message}");
#endif
                LoggingService.Application.Error("Failed to handle backspace", ex);
                        }
            finally
            {
                // Reset manipulation flag
                _isManipulatingText = false;
                }
            
#if DEBUG
            Console.WriteLine($"=== VirtualKeyboardService.HandleBackspace COMPLETED ===");
#endif
        }

        /// <summary>
        /// Find a visual child by name
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && (child as FrameworkElement)?.Name == name)
                {
                    return t;
                }
                
                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
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
                var textBoxTagLower = textBoxTag.ToLowerInvariant();
                
                // Check for both old tag-based and new name-based price fields
                if (textBoxTagLower == "photostrips" || textBoxTagLower == "photo4x6" || textBoxTagLower == "smartphoneprint" ||
                    textBoxName.Contains("extracopypriceinput") || textBoxName.Contains("multiplecopydiscountinput"))
                {
                    // This is a product price field - update binding first, then invoke callback and close keyboard
                    _bindingReenabled = true;
                    ReenableBinding(_activeInput);
                    
                    // Invoke callback with tag (if present) else name for context
                    var contextId = !string.IsNullOrWhiteSpace(textBoxTag) ? textBoxTag : textBoxName;
                    OnPriceInputComplete?.Invoke(contextId);
                    
                    HideKeyboard();
                    return;
                }
                
                // Check if this is a PIN box (PIN1, PIN2, PIN3, PIN4, etc.)
                if (textBoxName.StartsWith("pin") || textBoxName.StartsWith("confirm"))
                {
                    Console.WriteLine($"=== VirtualKeyboardService: Detected PIN box {textBoxName}, looking for action button ===");
                    
                    // Find the action button in the PIN screen (ContinueButton or VerifyButton)
                    var parentWindow = Window.GetWindow(_activeInput);
                    if (parentWindow != null)
                    {
                        // Try to find ContinueButton first (PIN Setup screen)
                        var continueButton = FindVisualChild<Button>(parentWindow, "ContinueButton");
                        if (continueButton != null && continueButton.IsEnabled)
                        {
                            Console.WriteLine($"=== VirtualKeyboardService: Found Continue button, clicking it ===");
                            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            
                            // Hide keyboard after successful PIN submission
                            HideKeyboard();
                            return;
                        }
                        
                        // Try to find VerifyButton (PIN Recovery screen)
                        var verifyButton = FindVisualChild<Button>(parentWindow, "VerifyButton");
                        if (verifyButton != null && verifyButton.IsEnabled)
                        {
                            Console.WriteLine($"=== VirtualKeyboardService: Found Verify button, clicking it ===");
                            verifyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            
                            // Hide keyboard after PIN verification attempt
                            HideKeyboard();
                            return;
                        }
                        
                        Console.WriteLine($"=== VirtualKeyboardService: No action button found or enabled ===");
                    }
                    
                    // Fallback: try to raise keyboard events
                    var pinEnterKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_activeInput), 0, Key.Enter)
                    {
                        RoutedEvent = UIElement.PreviewKeyDownEvent
                    };
                    
                    _activeInput?.RaiseEvent(pinEnterKey);
                    return;
                }
                
                // Check if this is a password input field (NewPasswordInput, NewPasswordTextInput, ConfirmPasswordInput, etc.)
                if (textBoxName.Contains("password") || textBoxName.Contains("Password"))
                {
                    Console.WriteLine($"=== VirtualKeyboardService: Detected password field {textBoxName}, looking for ResetPasswordButton ===");
                    
                    // Find the ResetPasswordButton in the Password Reset screen
                    var parentWindow = Window.GetWindow(_activeInput);
                    if (parentWindow != null)
                    {
                        var resetPasswordButton = FindVisualChild<Button>(parentWindow, "ResetPasswordButton");
                        if (resetPasswordButton != null && resetPasswordButton.IsEnabled)
                        {
                            Console.WriteLine($"=== VirtualKeyboardService: Found ResetPasswordButton, clicking it ===");
                            resetPasswordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            
                            // Hide keyboard after password reset attempt
                            HideKeyboard();
                            return;
                        }
                        
                        Console.WriteLine($"=== VirtualKeyboardService: ResetPasswordButton not found or not enabled ===");
                    }
                    
                    // Fallback: try to raise keyboard events
                    var passwordEnterKey = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_activeInput), 0, Key.Enter)
                    {
                        RoutedEvent = UIElement.PreviewKeyDownEvent
                    };
                    
                    _activeInput?.RaiseEvent(passwordEnterKey);
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
#if DEBUG
            Console.WriteLine("=== VirtualKeyboardService.HandleKeyboardClosed CALLED ===");
            Console.WriteLine($"Current tracked keyboard ID: {_currentKeyboard?.GetHashCode() ?? -1}");
            Console.WriteLine("Close button clicked, calling HideKeyboard()");
#endif
            HideKeyboard();
#if DEBUG
            Console.WriteLine("=== VirtualKeyboardService.HandleKeyboardClosed COMPLETED ===");
#endif
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
                    
                    Console.WriteLine($"SendKeyToInput - Inserted character at position {caretIndex}, new text length: {newText.Length}");
                }
                else if (_activeInput is PasswordBox passwordBox)
                {
                    // For password boxes, just append (can't get caret position easily)
                    passwordBox.Password += key;
                    
                    Console.WriteLine($"SendKeyToInput - Added character to password, new length: {passwordBox.Password?.Length ?? 0}");
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
                return "<hidden>";
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
#if DEBUG
                    Console.WriteLine($"About to disable binding for TextBox: {textBoxId}");
#endif
                    
                    // Actually remove the binding by setting to null
                    BindingOperations.ClearBinding(textBox, TextBox.TextProperty);
                    
                    // Restore the current text value after clearing binding
                    textBox.Text = currentText;
                    
#if DEBUG
                    Console.WriteLine($"Temporarily disabled binding for TextBox: {textBoxId}");
#endif
                }
                else
                {
                    var textBoxId = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
#if DEBUG
                    Console.WriteLine($"No binding found for TextBox: {textBoxId}");
#endif
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
#if DEBUG
                    Console.WriteLine($"Preserving user input before binding restore");
#endif
                    
                    // Restore the original binding
                    var binding = bindingExpression.ParentBinding;
                    textBox.SetBinding(TextBox.TextProperty, binding);
                    
                    // Set the user's input back to prevent ViewModel overwrite
                    var currentTextAfterBinding = textBox.Text;
#if DEBUG
                    Console.WriteLine($"Text after binding restore, setting back to user input");
#endif
                    textBox.Text = currentUserInput;
                    
                    // CRITICAL FIX: Force binding to update the source (ViewModel) IMMEDIATELY
                    // This ensures the ViewModel gets the new value even with UpdateSourceTrigger=LostFocus
                    var newBindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                    if (newBindingExpression != null)
                    {
                        var textBoxId = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
#if DEBUG
                        Console.WriteLine($"Manually updating binding source for TextBox: {textBoxId}");
#endif
                        newBindingExpression.UpdateSource();
                    }
                    
                    var textBoxId2 = string.IsNullOrEmpty(textBox.Name) ? textBox.Tag?.ToString() ?? "Unknown" : textBox.Name;
#if DEBUG
                    Console.WriteLine($"Re-enabled binding for TextBox: {textBoxId2}, restored user input");
#endif
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
